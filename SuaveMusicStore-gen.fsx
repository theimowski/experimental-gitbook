(*** hide ***)
#r "/home/vagrant/github/SuaveMusicStoreTutorial/Suave.dll"
#r "/home/vagrant/github/SuaveMusicStoreTutorial/Suave.Experimental.dll"
(*** hide ***)
module Path = begin

type IntPath = PrintfFormat<(int -> string),unit,string,string,int>

let home = "/"

module Store =
    let overview = "/store"
    let browse = "/store/browse"
    let details : IntPath = "/store/details/%d" end
(*** hide ***)
module View = begin

open Suave.Html

let divId id = divAttr ["id", id]
let h1 xml = tag "h1" [] xml
let aHref href = tag "a" ["href", href]
let cssLink href = linkAttr [ "href", href; " rel", "stylesheet"; " type", "text/css" ]

(*** define: View.fs_10-12 ***)
let home = [
    text "Home"
]
(*** include: View.fs_10-12 ***)
(*** define: View.fs_13-24 ***)

let store = [
    text "Store"
]

let browse genre = [
    text (sprintf "Genre: %s" genre)
]

let details id = [
    text (sprintf "Details %d" id)
]
(*** include: View.fs_13-24 ***)
(*** hide ***)

(*** define: View.fs_26-31 ***)
let index container = 
    html [
        head [
            title "Suave Music Store"
            cssLink "/Site.css"
        ]
(*** include: View.fs_26-31 ***)
(*** hide ***)

(*** define: View.fs_33-38 ***)
        body [
            divId "header" [
                h1 (aHref Path.home (text "F# Suave Music Store"))
            ]

            divId "container" container
(*** include: View.fs_33-38 ***)
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

(*** define: App.fs_9-10 ***)
let html container =
    OK (View.index container)
(*** include: App.fs_9-10 ***)
(*** hide ***)

(*** define: App.fs_12-26 ***)
let browse =
    request (fun r ->
        match r.queryParam "genre" with
        | Choice1Of2 genre -> html (View.browse genre)
        | Choice2Of2 msg -> BAD_REQUEST msg)

let webPart = 
    choose [
        path Path.home >=> html View.home
        path Path.Store.overview >=> html View.store
        path Path.Store.browse >=> browse
        pathScan Path.Store.details (fun id -> html (View.details id))

        pathRegex "(.*)\.(css|png)" >=> Files.browseHome
    ]
(*** include: App.fs_12-26 ***)
(*** hide ***)

startWebServer defaultConfig webPart end
