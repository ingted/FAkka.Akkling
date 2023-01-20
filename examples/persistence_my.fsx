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

let system = System.create "cluster-system" <| Configuration.defaultConfig()

type CounterChanged =
    { Delta : int }

type BasicCommand =
| SaveSS

type CounterCommand =
    | Inc
    | Inc2
    | Dec
    | GetState

type CounterMessage =
    | BasicCM of BasicCommand
    | Command of CounterCommand
    | Event of CounterChanged

let counter =
    spawn system "counter-1" <| propsPersist(fun mailbox ->
        let rec loop state =
            actor {
                let! msg = mailbox.Receive()
                match msg with
                | Event(changed) -> 
                    printfn "changed: %A" changed
                    return! loop (state + changed.Delta)
                | Command(cmd) ->
                    match cmd with
                    | GetState ->
                        printfn "GetState: %A" state
                        mailbox.Sender() <! state
                        return! loop state
                    | Inc -> 
                        return Persist (Event { Delta = 1 })
                    | Inc2 -> 
                        let l = seq[(Event { Delta = 1 }); (Event { Delta = 1 })]
                        //for eff in l do
                        //    return eff
                        return PersistAll l
                    | Dec -> return Persist (Event { Delta = -1 })
                | BasicCM SaveSS ->
                    SaveSnapshot state
                    return! loop state
                | SnapshotOffer sso ->
                    printfn "sso: %d" sso
                    return! loop sso
                | LifecycleEvent le ->
                    match le with
                    | PreStart -> return Unhandled
                        
                    | _ -> return Unhandled
                        
            }
        loop 0)

counter <! Command Inc2
counter <! Command Inc
counter <! Command Dec
counter <! Command GetState
async { let! reply = counter <? Command GetState
        printfn "Current state of %A: %i" counter reply } |> Async.RunSynchronously

counter.Underlying.Tell(Akka.Actor.PoisonPill.Instance, Akka.Actor.ActorRefs.Nobody)