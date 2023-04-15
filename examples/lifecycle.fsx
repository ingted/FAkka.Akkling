#r "nuget: Akka.Serialization.Hyperion"
#r "nuget: Akkling"

open System
open Akkling
open Akka.Actor

let system = System.create "basic-sys" <| Configuration.defaultConfig()

//management
let aref_string = spawn system "hello-actor-string" <| props(fun (m:Actor<string>) ->
    let rec loop () = actor {
        let! msg = m.Receive ()
        
        match msg with
        
        | x -> printfn "%A" x
        return! loop ()
    }
    loop ())


aref_string <! "i am a string"


let aref = spawn system "hello-actor1" <| props(fun m ->
    let rec loop () = actor {
        let! (msg: obj) = m.Receive ()
        
        match msg with
        | LifecycleEvent e ->
            match e with
            | PreStart -> printfn "Actor %A has started" m.Self
            | PostStop -> printfn "Actor %A has stopped" m.Self
            | _ -> return Unhandled
        | x -> printfn "%A" x
        return! loop ()
    }
    loop ())


aref <! System.DateTime.Now

let sref : IActorRef<string> = retype aref

sref <! 123
sref <! "ok"

(retype aref) <! PoisonPill.Instance
let pref : IActorRef<PoisonPill> = retype aref


pref <! PoisonPill.Instance



GracefulStopSupport.GracefulStop(pref.Underlying :?> IActorRef, TimeSpan.FromSeconds 30.0)




let spawnChild factory name = spawn factory name <| props(fun m ->
    let rec loop () = actor {
        let! (msg: obj) = m.Receive ()
        
        match msg with
        | LifecycleEvent e ->
            match e with
            | PreStart -> printfn "Actor %A has started" m.Self
            | PostStop -> printfn "Actor %A has stopped" m.Self
            | PreRestart (exn, msgObj) ->
                match msgObj with
                | :? int as i when i = 0 ->
                    printfn "I am restarting"
                    m.Self <! i //ox (i + 1)
                    Ignore
                | _ -> 
                    Ignore
            | _ -> 
                return Unhandled
        | :? int as i ->
            printfn "%A" <| 1/i


        //| :? (string * (string * obj) list) as (cmd, parmaList) ->
        //    let conn
        //    connm.query(cmd, parmaList)
            
        | x -> printfn "%A" x
        return! loop ()
    }
    loop ())

    //c <! ("select 1, @a from xxx", ["a", 123])
    //select 1,123 from xxx

    //[1;1;34;5;6]

    //1,2,3,4

    //type A = {id : int}
    //{id = 567}



    //let a =
    //    {|
    //        rrr = 123
    //    |} 
    //let b = 
    //    {|
    //        rrr = 123
    //    |} 

    
    //{|rrr = 123|} = {|rrr = 123|} 


    //let sss = seq {
    //    yield 123
    //}

    //sss |> Seq.toArray

    //list, array, seq

    //let al = new System.Collections.ArrayList()

    //al.Add("orz")

    //al[0] <- "uuu"



    //let ll = ["aa"]
    //ll[0] <- "ooo"

    //System.Collections.Generic.List<>

let bossProp = props(fun (m:Actor<obj>) ->

    let employee = spawnChild m "small3"
        
    let rec loop () = actor {
        let! msg = m.Receive ()   
        printfn "SugarDaddy Recived: %s %A" (msg.GetType().Name) msg
        employee <! msg
        return! loop ()
    }
    loop ())

let mutable childCount = 0

let notBornMoreThan3Times =
    {
        bossProp 
            with
            SupervisionStrategy = Some (
                new OneForOneStrategy(
                    3
                    , 1000
                    , (fun exn -> 
                        printfn "1-1 error handling: %A" exn.Message
                        childCount <- childCount + 1 
                        match exn with
                        | ->
                            Directive.Escalate
                        | ->
                            Directive.Resume
                        | ->
                            Directive.Restart
                        | ->
                            Directive.Stop
                    )   
                )
            )
        }


let boss = spawn system "boss3" notBornMoreThan3Times


boss <! 0



type A = {
    ID : int
    O: int
}

let b = 
    {
        ID = 123
        O = 789
    }

let c = 
    {
        b with O = 0
    }

