#r @"packages/build/FAKE/tools/FakeLib.dll"
#load @"packages/build/FSharp.Formatting/FSharp.Formatting.fsx"

open System
open System.IO
open System.Text.RegularExpressions

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

    "[lang=fsharp]" :: "#r \"/home/tomasz/github/SuaveMusicStoreTutorial/Suave.dll\"" :: contents
    |> List.map (fun x -> "    " + x)
    |> String.concat Environment.NewLine
    |> Literate.ParseMarkdownString
    |> fun x -> Literate.WriteHtml(x, lineNumbers= false, prefix = snipId + "_")
    |> fun x -> x.Replace("<code", "<div").Replace("</code", "</div").Replace("\n","&#10;")

  else
    line    

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
