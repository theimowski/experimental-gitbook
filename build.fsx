#I @"packages/build/FAKE/tools"
#load @"packages/build/FSharp.Formatting/FSharp.Formatting.fsx"
#r @"Microsoft.Web.XmlTransform.dll"
#r @"FakeLib.dll"
#r @"System.Xml.Linq"

open System
open System.IO
open System.Text.RegularExpressions
open System.Xml.Linq
open System.Xml.XPath

open Fake
open Fake.Git

open FSharp.Literate
let outDir = "out"

let repo = getBuildParam "repo"
if String.IsNullOrEmpty repo then failwith "select repo"
let githubAccount = getBuildParam "githubAccount"
let githubRepo = getBuildParam "githubRepo"
if String.IsNullOrEmpty githubRepo ||
     String.IsNullOrEmpty githubAccount then failwith "provide github details"
let branch = getBuildParamOrDefault "branch" "master"

let write (path, lines: list<String>) =
  if not (File.Exists path && File.ReadAllLines path |> Array.toList = lines) then
    tracefn "Writing file '%s'" path
    File.WriteAllLines (path, lines)

let (|Regex|_|) pattern input =
  let m = Regex.Match(input, pattern)
  if m.Success then Some(List.tail [ for g in m.Groups -> g.Value ])
  else None
  
let (|Int32|_|) input =
  match Int32.TryParse input with
  | true, x -> Some x
  | _ -> None
  
let fileContentsAt commit file = 
  Git.CommandHelper.getGitResult repo (sprintf "show %s:%s" commit file) 
   
module List =
  let prepend xs ys = List.append ys xs

type Snippet =
| SnippetWholeFile
| SnippetLinesBounded of startLine : int * endLine : int

let snipId = function
| file, SnippetWholeFile -> file
| file, SnippetLinesBounded (s, e) -> sprintf "%s_%d-%d" file s e

let tryParseSnippet = function
| Regex "^==> ([\w\.]+):(\d+)-(\d+)$" [file; Int32 sl; Int32 el] -> 
  Some (file, SnippetLinesBounded(sl,el))
| Regex "^==> ([\w\.]+)$" [file] -> 
  Some (file, SnippetWholeFile)
| _ -> 
  None

let (|Snip|_|) = tryParseSnippet

let inline normalizePath(path:string) = 
  let dirSeparator = Path.DirectorySeparatorChar.ToString()
  path
    .Replace("\\",dirSeparator)
    .Replace("/",dirSeparator)
    .TrimEnd(Path.DirectorySeparatorChar)
    .Replace(dirSeparator + "." + dirSeparator, dirSeparator)

let regexReplace (pattern: string) (replacement: string) (input: string) =
  Regex(pattern).Replace(input, replacement)

let parseFirstMsgLine (firstLine: string) =
  let level = max 0 (firstLine.LastIndexOf("#"))
  let title = firstLine.Substring(level + 1).Trim()
  let fileName =
    if title = "Introduction" then
      "README.md"
    else
      let title =
        Path.GetInvalidFileNameChars()
        |> Array.fold (fun (title: string) c -> title.Replace(c.ToString(), "")) title
      title.ToLowerInvariant().Replace(" ", "_") + ".md"
  level,title,fileName

let fillSnippets commit msg =
  let fsproj = 
    fileContentsAt commit "SuaveMusicStore.fsproj"
    |> String.concat "\n"
    |> fun x -> if String.IsNullOrWhiteSpace x then None else Some x
    |> Option.map XDocument.Parse

      (*
  [ "/home/vagrant/github/SuaveMusicStoreTutorial/Suave.dll"
    "/home/vagrant/github/SuaveMusicStoreTutorial/Suave.Experimental.dll" ]
*)
  let srcFiles,refDlls =
    match fsproj with
    | Some fsproj ->
      let ns = System.Xml.XmlNamespaceManager(System.Xml.NameTable())
      ns.AddNamespace("msbuild", "http://schemas.microsoft.com/developer/msbuild/2003")
      let files = 
        fsproj.Root.XPathSelectElements ("//msbuild:Compile", ns)
        |> Seq.map (fun e -> e.Attribute(XName.op_Implicit "Include").Value)
        |> Seq.toList
        |> List.filter ((<>) "AssemblyInfo.fs")
      let dlls = 
        fsproj.Root.XPathSelectElements ("//msbuild:Reference/msbuild:HintPath", ns)
        |> Seq.map (fun e -> sprintf "%s/%s" repo (normalizePath e.Value))
        |> Seq.toList
      files, dlls
    | None ->
      tracef "No fsproj for commit %s\n" commit
      [], []

  refDlls
  |> List.iter (fun path -> if not <| File.Exists path then failwithf "Cannot find '%s'" path)

  let snippets = List.choose tryParseSnippet msg
    
  let snippetMap =
    snippets
    |> List.groupBy fst
    |> List.map (fun (k, vs) -> (k,List.map snd vs))
    |> Map.ofList

  let snippetOrder =
    let f (_,(srcA,snipA)) (_,(srcB,snipB)) = 
      let indexA = List.findIndex ((=)srcA) srcFiles
      let indexB = List.findIndex ((=)srcB) srcFiles
      let srcOrder = compare indexA indexB
      if srcOrder <> 0 then
        srcOrder
      else
        compare snipA snipB

    snippets
    |> List.indexed
    |> List.sortWith f
    |> List.map fst

  let verboseTopLvlModule lines =
    match lines with
    | Regex "module .+\.(.+)" [name] :: t ->
      let mid, last = List.take (List.length t - 1) t, List.last t
      sprintf "module %s = begin" name :: mid @ [last + " end"]
    | _ ->
      lines

  let srcFileContent src =
    let snippets = 
      match Map.tryFind src snippetMap with
      | Some s -> s
      | _ -> []
      |> List.sort

    let contents = 
      fileContentsAt commit src 
      |> Seq.cast<string> 
      |> Seq.toList
      |> verboseTopLvlModule
    
    let rec chunk line chunkAcc (contents, snippets) =
      match snippets,contents with
      | [SnippetWholeFile], contents ->
        [Some SnippetWholeFile, contents]
      | [], [] ->
        List.rev chunkAcc
      | [], _ -> 
        List.rev ((None, contents) :: chunkAcc)
      | SnippetLinesBounded (sL, eL) :: snippets, _ when sL = line + 1 ->
        let lines = contents |> List.take (eL - line)
        let rest  = contents |> List.skip (eL - line)
        let s = Some (SnippetLinesBounded (sL, eL)), lines
        chunk eL (s :: chunkAcc) (rest,snippets)
      | SnippetLinesBounded (sL, _) :: _, _ ->
        let lines = contents |> List.take (sL - line - 1)
        let rest  = contents |> List.skip (sL - line - 1)
        let n = None, lines
        chunk (sL - 1) (n :: chunkAcc) (rest,snippets)
      | s,c -> 
        failwithf "unexpected case, line: %d; snippets: %A; contents: %A" line s c

    let formatChunk = function
      | None, lines -> 
        [ [ "(*** hide ***)" ]
          lines ] |> List.concat
      | (Some snippet), lines ->
        [ [ sprintf "(*** define: %s ***)" (snipId (src,snippet)) ]
          lines 
          [ sprintf "(*** include: %s ***)" (snipId (src,snippet)) ] ] |> List.concat
    
    chunk 0 [] (contents, snippets)
    |> List.collect formatChunk

  
  let lines = "(*** hide ***)" :: (List.map (sprintf "#r \"%s\"") refDlls)

  let lines = 
    srcFiles 
    |> List.collect srcFileContent 
    |> List.append lines

  let scriptOutName = "SuaveMusicStore"
  let _,_,outName = parseFirstMsgLine (Seq.head msg)
  write(scriptOutName + ".fsx", lines)
  Literate.ProcessScriptFile(scriptOutName + ".fsx",lineNumbers = false)
  let rawHtml = File.ReadAllText (scriptOutName + ".html")

  let html = XDocument.Parse ("<root>" + rawHtml + "</root>", LoadOptions.PreserveWhitespace)
  let snippets =
    html.Root.XPathSelectElements "pre"
    |> Seq.map (fun x -> x.ToString(SaveOptions.DisableFormatting)
                          .Replace("<code", "<div")
                          .Replace("</code", "</div")
                          .Replace("\n","&#10;")
                          .Replace(""" <span class="k">end</span>""","")
                          |> regexReplace 
                              """class="t">(\w+)</span> <span class="o">=</span> <span class="k">begin</span>""" 
                              ("""class="t">""" + scriptOutName + """.$1</span>"""))
    |> Seq.toList
    |> List.zip snippetOrder
    |> List.sortBy fst
    |> List.map snd

  let tips =
    html.Root.XPathSelectElements "div[@class='tip']"
    |> Seq.map (fun x -> x.ToString())
    |> Seq.toList
      
  let rec insertSnippets acc snippets content =
    match content,snippets with
      | [],[] -> List.rev acc
      | Snip _ :: t, s :: ss ->
        insertSnippets (s :: acc) ss t
      | h :: t, s -> 
        insertSnippets (h :: acc) s t
      | _ ->
        failwith "different amount of snippets found"
  let insertTips content = 
    tips
    |> List.append content
  
  msg
  |> insertSnippets [] snippets
  |> insertTips
  
let insertGithubCommit commit code = 
  sprintf "GitHub commit: [%s](https://github.com/%s/%s/commit/%s)"
          commit
          githubAccount
          githubRepo
          commit
  |> List.singleton
  |> List.append ["";"---";""]
  |> List.append code

let numStat line =
  let split = function
  | Regex "^(\w)\s+(.*)$" [status; name] -> status,name
  | _ -> failwithf "split cannot parse: %s" line

  let human = function
  | "A" -> "added"
  | "D" -> "deleted"
  | "M" -> "modified"
  | x -> failwithf "human: %s" x

  let (stat,name) = split line
  sprintf "* %s (%s)" name (human stat)

let insertGitDiff commit code =
  let filesChanged =
    Git.CommandHelper.getGitResult repo (sprintf "diff %s^..%s --name-status" commit commit)
    |> Seq.toList
  
  if filesChanged = List.empty then 
    code
  else
    filesChanged
    |> List.map numStat
    |> List.append ["";"Files changed:";""]
    |> List.append code

let generate () =
  CreateDir outDir
  let commits = Git.CommandHelper.getGitResult repo ("log --reverse --pretty=%H " + branch)
  let summary =
    commits
    |> Seq.map (fun commit ->
        let msg = Git.CommandHelper.getGitResult repo ("log --format=%B -n 1 " + commit) |> Seq.toList
        let firstLine = msg |> Seq.item 0
        let level,title,fileName = parseFirstMsgLine firstLine
        let contents = 
          msg
          |> fillSnippets commit
          |> insertGithubCommit commit
          |> insertGitDiff commit
        let outFile = outDir </> fileName
        write (outFile, contents)
        sprintf "%s* [%s](%s)" (String.replicate level "\t") title fileName)
    |> Seq.toList

  [ "book.json"
    "custom.css"
    "tips.js" ]
  |> Copy outDir
  write (outDir </> "SUMMARY.md", summary)

let handleWatcherEvents (events:FileChange seq) =
  for e in events do
    let fi = fileInfo e.FullPath
    traceImportant <| sprintf "%s was changed." fi.Name
    match fi.Attributes.HasFlag FileAttributes.Hidden || fi.Attributes.HasFlag FileAttributes.Directory with
    | true -> ()
    | _ -> generate ()

Target "Generate" (fun _ ->
  CleanDir outDir

  generate()
)

Target "Preview" (fun _ ->
  
  ExecProcess (fun si ->
          si.FileName <- "gitbook"
          si.Arguments <- sprintf "%s %s" "install" outDir
      ) (TimeSpan.FromSeconds 10.)
  |> ignore
  
  use watcher = 
    !! (repo </> ".git" </> "refs" </> "heads" </> "*.*") 
    |> WatchChanges handleWatcherEvents

  StartProcess (fun si ->
          si.FileName <- "gitbook"
          si.Arguments <- sprintf "%s %s" "serve" outDir
      )
    
  traceImportant "Waiting for git edits. Press any key to stop."
  System.Console.ReadKey() |> ignore
  watcher.Dispose()
)

Target "Publish" (fun _ ->
  let publishRepo = sprintf "https://github.com/%s/%s.git" githubAccount githubRepo

  let publishDir = "publish"
  let publishBranch = 
    match branch with
    | Regex "^(.*)_src$" [name] -> name
    | name -> name + "_gb"

  CleanDir publishDir
  cloneSingleBranch "" publishRepo publishBranch publishDir

  fullclean publishDir
  CopyRecursive outDir publishDir true |> printfn "%A"
  StageAll publishDir
  Commit publishDir (sprintf "Update generated documentation %s" <| DateTime.UtcNow.ToShortDateString())
  Branches.push publishDir
)

Target "All" DoNothing

"Generate"
  ==> "Preview"
  ==> "All"

"Generate"
  ==> "Publish"

RunTargetOrDefault "All"
