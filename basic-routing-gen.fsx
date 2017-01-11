(*** hide ***)
#r "/home/tomasz/github/SuaveMusicStoreTutorial/Suave.dll"
(*** define: App.fs_1-4 ***)
open Suave
open Suave.Filters
open Suave.Operators
open Suave.Successful
(*** include: App.fs_1-4 ***)
(*** hide ***)

(*** define: App.fs_6-14 ***)
let webPart = 
    choose [
        path "/" >=> (OK "Home")
        path "/store" >=> (OK "Store")
        path "/store/browse" >=> (OK "Store")
        path "/store/details" >=> (OK "Details")
    ]

startWebServer defaultConfig webPart
(*** include: App.fs_6-14 ***)
