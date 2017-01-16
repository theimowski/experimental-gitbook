(*** hide ***)
#r "/home/vagrant/github/SuaveMusicStoreTutorial/Suave.dll"
#r "/home/vagrant/github/SuaveMusicStoreTutorial/Suave.Experimental.dll"
(*** define: Path.fs ***)
module Path = begin

type IntPath = PrintfFormat<(int -> string),unit,string,string,int>

let home = "/"

module Store =
    let overview = "/store"
    let browse = "/store/browse"
    let details : IntPath = "/store/details/%d" end
(*** include: Path.fs ***)
(*** hide ***)
module View = begin

open Suave.Html

let divId id = divAttr ["id", id]
let h1 xml = tag "h1" [] xml
let aHref href = tag "a" ["href", href]

let index = 
    html [
        head [
            title "Suave Music Store"
        ]

        body [
(*** define: View.fs_16-18 ***)
            divId "header" [
                h1 (aHref Path.home (text "F# Suave Music Store"))
            ]
(*** include: View.fs_16-18 ***)
(*** hide ***)

            divId "footer" [
                text "built with "
                aHref "http://fsharp.org" (text "F#")
                text " and "
                aHref "http://suave.io" (text "Suave.IO")
            ]
        ]
    ]
    |> xmlToString end
(*** hide ***)
module App = begin

open Suave
open Suave.Filters
open Suave.Operators
open Suave.RequestErrors
open Suave.Successful

let browse =
    request (fun r ->
        match r.queryParam "genre" with
        | Choice1Of2 genre -> OK (sprintf "Genre: %s" genre)
        | Choice2Of2 msg -> BAD_REQUEST msg)

(*** define: App.fs_15-21 ***)
let webPart = 
    choose [
        path Path.home >=> (OK View.index)
        path Path.Store.overview >=> (OK "Store")
        path Path.Store.browse >=> browse
        pathScan Path.Store.details (fun id -> OK (sprintf "Details %d" id))
    ]
(*** include: App.fs_15-21 ***)
(*** hide ***)

startWebServer defaultConfig webPart end
