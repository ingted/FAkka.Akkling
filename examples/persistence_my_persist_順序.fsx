#r "nuget: Akka.Serialization.Hyperion"
#r "nuget: Akka.Persistence"
//#r "nuget: Akkling"
#r @"G:\git\Akkling\src\Akkling\bin\Debug\netstandard2.0\Akkling.dll"
#r "nuget: Akka.Streams"
//#r "nuget: Akkling.Persistence"
#r @"G:\git\Akkling\src\Akkling.Streams\bin\Debug\netstandard2.0\Akkling.Streams.dll"
#r @"G:\git\Akkling\src\Akkling.Persistence\bin\Debug\netstandard2.0\Akkling.Persistence.dll"

open System
open Akkling
open Akkling.Persistence
open Akka
let system = System.create "cluster-system" <| Configuration.defaultConfig()

type CounterChanged =
    { Delta : int }

type BasicCommand =
| SaveSS

type CounterCommand =
    | Inc
    | Inc2
    | FailIt of int
    | Dec
    | GetState

type CounterMessage =
    | BasicCM of BasicCommand
    | Command of CounterCommand
    | Event of CounterChanged * Akka.Actor.IActorRef

let (|CommonPass|_|) (o:obj) =
    match o with
    | :? Akka.Persistence.RecoveryCompleted
    | :? Akka.Persistence.RecoverySuccess -> Some o
    | _ -> None
    

let counter =
    let mutable ifError = false
    spawn system "counter-1" <| propsPersist(fun (mailbox:Eventsourced<obj>) ->
        let rec retryFirst state =
            actor {
                printfn "---------------i am retryFirst---------------"
                let! msg = mailbox.Receive()
                let me = mailbox.Self.Underlying :?> Akka.Actor.IActorRef
                match msg with
                | :? CounterMessage as Command(cmd) ->
                    match cmd with
                    | FailIt i ->
                        if i <= 0 then
                            printfn "passed"
                            ifError <- false
                            mailbox.UnstashAll()
                            return! loop state
                        else
                            failwithf "orz %d" i
                    | _ ->
                        printfn "non FailIt, stashed!! %A" cmd
                        mailbox.Stash()
                | SnapshotOffer sso ->
                    printfn "sso: %d" sso
                    return! loop sso
                | LifecycleEvent le ->
                    match le with
                    | PreStart -> return Unhandled
                    | PreRestart (exn, msg) ->
                        ifError <- true
                        match msg with
                        | :? CounterMessage as Command(FailIt i) ->
                            mailbox.Self <! Command(FailIt (i-1))
                        | _ -> ()
                        printfn "return! retryFirst state in retryFirst"
                        return! retryFirst state
                    | _ -> return Unhandled
                | PersistentLifecycleEvent ple ->
                    printfn "ple: %A" ple
                | CommonPass o ->
                    printfn "Commonly passed %A" o
                | unhandled ->
                    printfn "Unhandled msg: %A" unhandled

                    mailbox.Stash()
            }
        
        and loop state =
            actor {
                printfn "---------------i am loop---------------"
                let! msg = mailbox.Receive()
                let me = mailbox.Self.Underlying :?> Akka.Actor.IActorRef
                match msg with
                | :? CounterMessage as Event(changed, _) -> 
                    printfn "changed: %A" changed
                    return! loop (state + changed.Delta)
                | :? CounterMessage as Command(cmd) ->
                    match cmd with
                    | GetState ->
                        printfn "GetState: %A" state
                        mailbox.Sender() <! state
                        return! loop state
                    | Inc -> 
                        printfn "process cmd Inc1"
                        Async.Sleep 5000 |> Async.RunSynchronously
                        return Persist (box (Event ({ Delta = 1 }, me)))
                        return Persist (box (Event ({ Delta = 9 }, me)))
                    | Inc2 -> 
                        printfn "process cmd Inc2"
                        let l = seq[box (Event ({ Delta = 2 }, me)); box (Event ({ Delta = 3 }, me))]
                        //for eff in l do
                        //    return eff
                        return PersistAll l
                    | FailIt i ->
                        Async.Sleep 5000 |> Async.RunSynchronously
                        failwithf "orz %d" i
                        //mailbox.Self <! Command(FailIt (i-1))
                        //return! retryFirst state
                    | Dec -> return Persist (box (Event ({ Delta = -1 }, me)))
                | :? CounterMessage as BasicCM SaveSS ->
                    SaveSnapshot state
                    return! loop state
                | SnapshotOffer sso ->
                    printfn "sso: %d" sso
                    return! loop sso
                | LifecycleEvent le ->
                    match le with
                    | PreStart -> return Unhandled
                    | PreRestart (exn, msg) ->
                        ifError <- true
                        match msg with
                        | :? CounterMessage as Command(FailIt i) ->
                            mailbox.Self <! Command(FailIt (i-1))
                        | _ -> ()
                        printfn "return! retryFirst state"
                        return! retryFirst state

                    | _ -> return Unhandled
                | PersistentLifecycleEvent ple ->
                    printfn "ple: %A" ple
                | CommonPass o ->
                    printfn "Commonly passed %A" o
                | unhandled ->
                    printfn "Unhandled msg in Loop: %A" unhandled
            }
        if ifError then
            retryFirst 0
        else
            loop 0)

//實驗一
counter <! Command Inc
counter <! Command Inc2
counter <! Command GetState
//實驗一結束

//實驗二
counter <! Command (FailIt 3)
counter <! Command Inc2
counter <! Command GetState
//實驗二結束

async { let! reply = counter <? Command GetState
        printfn "Current state of %A: %i" counter reply } |> Async.RunSynchronously

system.Stop (counter.Underlying :?> Akka.Actor.IActorRef)

counter.Underlying.Tell(Akka.Actor.PoisonPill.Instance, Akka.Actor.ActorRefs.Nobody)