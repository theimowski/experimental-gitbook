(*** hide ***)
#r "/home/vagrant/github/SuaveMusicStoreTutorial/packages/FSharp.Core.3.1.2.5/lib/net40/FSharp.Core.dll"
#r "/home/vagrant/github/SuaveMusicStoreTutorial/packages/Suave.1.0.0/lib/net40/Suave.dll"
#r "/home/vagrant/github/SuaveMusicStoreTutorial/packages/Suave.Experimental.1.0.0/lib/net40/Suave.Experimental.dll"
(*** hide ***)
module Path = begin

type IntPath = PrintfFormat<(int -> string),unit,string,string,int>

(*** define: Path.fs_5-5 ***)
let withParam (key,value) path = sprintf "%s?%s=%s" path key value
(*** include: Path.fs_5-5 ***)
(*** hide ***)

let home = "/"

(*** define: Path.fs_9-11 ***)
module Store =
    let overview = "/store"
    let browse = "/store/browse"
(*** include: Path.fs_9-11 ***)
(*** hide ***)
    let details : IntPath = "/store/details/%d"

    let browseKey = "genre" end
(*** hide ***)
module View = begin

open Suave.Html

let divId id = divAttr ["id", id]
let h1 xml = tag "h1" [] xml
(*** define: View.fs_7-7 ***)
let h2 s = tag "h2" [] (text s)
(*** include: View.fs_7-7 ***)
(*** hide ***)
let aHref href = tag "a" ["href", href]
let cssLink href = linkAttr [ "href", href; " rel", "stylesheet"; " type", "text/css" ]
(*** define: View.fs_10-11 ***)
let ul xml = tag "ul" [] (flatten xml)
let li = tag "li" []
(*** include: View.fs_10-11 ***)
(*** hide ***)

let home = [
    h2 "Home"
]

(*** define: View.fs_17-26 ***)
let store genres = [
    h2 "Browse Genres"
    p [
        text (sprintf "Select from %d genres:" (List.length genres))
    ]
    ul [
        for g in genres -> 
            li (aHref (Path.Store.browse |> Path.withParam (Path.Store.browseKey, g)) (text g))
    ]
]
(*** include: View.fs_17-26 ***)
(*** hide ***)

let browse genre = [
    h2 (sprintf "Genre: %s" genre)
]

let details id = [
    h2 (sprintf "Details %d" id)
]

let index container = 
    html [
        head [
            title "Suave Music Store"
            cssLink "/Site.css"
        ]

        body [
            divId "header" [
                h1 (aHref Path.home (text "F# Suave Music Store"))
            ]

            divId "container" container

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

let html container =
    OK (View.index container)

(*** define: App.fs_12-14 ***)
let browse =
    request (fun r -> 
        match r.queryParam Path.Store.browseKey with
(*** include: App.fs_12-14 ***)
(*** hide ***)
        | Choice1Of2 genre -> html (View.browse genre)
        | Choice2Of2 msg -> BAD_REQUEST msg)

let webPart = 
    choose [
        path Path.home >=> html View.home
(*** define: App.fs_21-21 ***)
        path Path.Store.overview >=> html (View.store ["Rock"; "Disco"; "Pop"])
(*** include: App.fs_21-21 ***)
(*** hide ***)
        path Path.Store.browse >=> browse
        pathScan Path.Store.details (fun id -> html (View.details id))

        pathRegex "(.*)\.(css|png)" >=> Files.browseHome
    ]

startWebServer defaultConfig webPart end
