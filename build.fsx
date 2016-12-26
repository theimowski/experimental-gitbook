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
   
let insertSnippet (commit : string) (line : string) =
  if line.StartsWith "==> " then
    let contents, snipId = 
      match line with
      | Regex "^==> ([\w\.]+):(\d+)-(\d+)$" [file;Int32 lstart;Int32 lend] ->
        fileContentsAt commit file 
        |> Seq.skip (lstart - 1)
        |> Seq.take (lend - lstart + 1)
        |> Seq.toList, line.Substring("==> ".Length)
      | Regex "^==> ([\w\.]+)$" [file] ->
        fileContentsAt commit file 
        |> Seq.toList, line.Substring("==> ".Length)
      | _ -> 
        failwithf "invalid format '%s'" line

    let tipsRegex = Text.RegularExpressions.Regex("fs\d+")

    
    "[lang=fsharp]" 
      :: "#r \"/home/tomasz/github/SuaveMusicStoreTutorial/Suave.dll\"" 
      :: contents
    |> List.map (fun x -> "    " + x)
    |> List.append [sprintf "<em style=\"padding-left: 2em\">%s</em>" snipId; ""] 
    |> String.concat Environment.NewLine
    |> Literate.ParseMarkdownString
    |> fun x -> Literate.WriteHtml(x, lineNumbers= false, prefix = snipId + "_")
    |> fun x -> x.Replace("<code", "<div").Replace("</code", "</div").Replace("\n","&#10;")

  else
    line    

module List =
  let prepend xs ys = List.append ys xs

type Snippet =
| SnippetWholeFile
| SnippetLinesBounded of startLine : int * endLine : int

let snipId = function
| file, SnippetWholeFile -> file
| file, SnippetLinesBounded (s, e) -> sprintf "%s:%d-%d" file s e

let projectToScript projectFile =
  let commit = "c376191"
  let fsproj = 
    fileContentsAt commit "SuaveMusicStore.fsproj"
    |> String.concat "\n"
    |> XDocument.Parse

  //let msg = Git.CommandHelper.getGitResult repo ("log --format=%B -n 1 " + commit) |> Seq.toList
  //let snippets =
  //  msg
  //  |> List.filter (fun x -> x.StartsWith("==> "))
  //  |> List.map (fun x -> x.Substring("==> ".Length))

  let snippets = 
    [ "App.fs", SnippetLinesBounded (1, 4)
      "App.fs", SnippetLinesBounded (6, 14) ]
    |> List.groupBy fst
    |> List.map (fun (k,vs) -> k, List.map snd vs)
    |> Map.ofList

  let ns = System.Xml.XmlNamespaceManager(System.Xml.NameTable())
  ns.AddNamespace("msbuild", "http://schemas.microsoft.com/developer/msbuild/2003");

  let rec chunkByPoints points xs =
    match points,xs with
    | [], xs -> [xs]
    | b :: _, [] -> failwith "nothing to partition"
    | b :: bs, xs -> 
      let h,t = List.take b xs, List.skip b xs
      h :: chunkByPoints (List.map (fun x -> x - b) points) t

  let srcFileContent src =
    let snippets = Map.find src snippets

    let contents = 
      fileContentsAt commit src 
      |> Seq.cast<string> 
      |> Seq.toList

    let chunks =
      chunkByPoints [4;5] contents
      |> List.zip [ Some (SnippetLinesBounded(1,4))
                    None
                    Some (SnippetLinesBounded(6, 14)) ]

    let formatChunk = function
      | None, lines -> 
        [ [ "(*** hide ***)" ]
          lines ] |> List.concat
      | (Some snippet), lines ->
        [ [ sprintf "(*** define: %s ***)" (snipId (src,snippet)) ]
          lines 
          [ sprintf "(*** include: %s ***)" (snipId (src,snippet)) ] ] |> List.concat
    //  [ (1,4),  Some (SnippetLinesBounded(1,4))
    //    (5,5),  None
    //    (6,14), Some (SnippetLinesBounded(6, 14)) ]

    chunks
    |> List.collect formatChunk

    
   // |> List.append [sprintf "(*** define: %s ***)" src]
   // |> List.prepend [sprintf "(*** include: %s ***)" src]
 
  let srcFiles = 
    fsproj.Root.XPathSelectElements ("//msbuild:Compile", ns)
    |> Seq.map (fun e -> e.Attribute(XName.op_Implicit "Include").Value)
    |> Seq.toList
    |> List.filter ((<>) "AssemblyInfo.fs")
    |> List.collect srcFileContent

  let lines =
    [ "(*** hide ***)"
      "#r \"/home/tomasz/github/SuaveMusicStoreTutorial/Suave.dll\"" ]

  let lines = 
    srcFiles |> List.append lines

  write("basic-routing-gen.fsx", lines)
  Literate.ProcessScriptFile("basic-routing.fsx",lineNumbers = false)
  Literate.ProcessScriptFile("basic-routing-gen.fsx",lineNumbers = false)


let insertSnippets commit code = List.map (insertSnippet commit) code

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
        let contents = 
          msg
          |> insertSnippets commit
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

Target "Project" (fun () -> projectToScript())


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
