(*** hide ***)
#r "/home/tomasz/github/SuaveMusicStoreTutorial/Suave.dll"
(*** define: App.fs ***)
namespace SuaveMusicStore
module App =

 open Suave
 open Suave.Filters
 open Suave.Operators
 open Suave.Successful
 let webPart = 
     choose [
         path "/" >=> (OK "Home")
         path "/store" >=> (OK "Store")
         path "/store/browse" >=> (OK "Store")
         path "/store/details" >=> (OK "Details")
     ]
 
 startWebServer defaultConfig webPart
(*** define: Db.fs ***)
module Db =

 open Suave

 let x = App.webPart
(*** include: App.fs ***)
(*** include: Db.fs ***)