

#r "nuget: Akka.Serialization.Hyperion"
#r "nuget: Akka.Persistence"
#r "nuget: FAkka.Akkling"
#r "nuget: Akka.Streams"
#r "nuget: FAkka.Akkling.Persistence"
//#r @"G:\git\Akkling\src\Akkling\bin\Debug\netstandard2.0\Akkling.dll"
//#r @"G:\git\Akkling\src\Akkling.Streams\bin\Debug\netstandard2.0\Akkling.Streams.dll"
//#r @"G:\git\Akkling\src\Akkling.Persistence\bin\Debug\netstandard2.0\Akkling.Persistence.dll"

open System
open Akkling
open Akkling.Persistence
open Akka
open Akka.Actor
open Akka.Streams
open Akka.Streams.Dsl

type CounterChanged =
    { Delta : int }

type BasicCommand =
| SaveSS

type CounterCommand =
    | Sleep of int
    | Inc //increase
    | Inc2
    | Inc3
    | Dec //decrease
    | GetState
    | ReturnBangBetweenReturn

type CounterMessage =
    | BasicCM of BasicCommand
    | Command of CounterCommand
    | Event of CounterChanged * Akka.Actor.IActorRef
    | BecomeIt of (obj -> Effect<obj>)

let system = System.create "cluster-system" <| Configuration.defaultConfig()
let mutable aa = 123
aa <- 456
aa

let counter =
    spawn system "counter-1" <| propsPersist(fun (mailbox:Eventsourced<obj>) ->
        let rec loop state = //recursive
            actor {
                let! msg = mailbox.Receive()
                //let s = mailbox.System.ActorSelection(ActorPath.Parse("akka://cluster-system/user/counter-1"))

                //s.ResolveOne()

                let me = mailbox.Self.Underlying :?> Akka.Actor.IActorRef
                match msg with
                | :? CounterMessage as Event(changed, _) -> 
                    printfn "changed: %A" changed
                    return! loop (state + changed.Delta)
                | :? CounterMessage as Command(cmd) ->
                    match cmd with
                    | Sleep n ->
                        Async.Sleep n |> Async.RunSynchronously
                        
                        return! loop state
                    | GetState ->
                        printfn "GetState: %A" state
                        mailbox.Sender() <! state
                        return! loop state
                    | Inc -> 
                        return Persist (box (Event ({ Delta = 1 }, me)))
                    | Inc2 -> 
                        let l = seq[box (Event ({ Delta = 1 }, me)); box (Event ({ Delta = 1 }, me))]
                        //for eff in l do
                        //    return eff
                        return PersistAll l
                    | Inc3 -> 
                        return Persist (box (Event ({ Delta = 1 }, me)))
                        return Persist (box (Event ({ Delta = 1 }, me)))
                        printfn "print in Inc3: %d" state
                        return Persist (box (Event ({ Delta = 1 }, me)))
                    | ReturnBangBetweenReturn ->
                        return Persist (box (Event ({ Delta = 10 }, me)))
                        printfn "print1 in ReturnBangBetweenReturn: %d" state
                        return! loop state
                        //經實驗，以下不被執行
                        printfn "print2 in ReturnBangBetweenReturn: %d" state
                        return Persist (box (Event ({ Delta = 10 }, me)))
                    | Dec -> return Persist (box (Event ({ Delta = -1 }, me)))
                | :? CounterMessage as BasicCM SaveSS ->
                    return SaveSnapshot state
                    return! loop state
                | :? CounterMessage as BecomeIt b ->
                    become b
                | SnapshotOffer sso ->
                    printfn "sso: %d" sso
                    return! loop sso
                | LifecycleEvent le ->
                    match le with
                    | PreStart -> return Unhandled
                        
                    | _ -> return Unhandled
                | unhandled ->
                    printfn "Unhandled msg: %A" unhandled
            }
        loop 0)

counter <! Command (Sleep 5000)
counter <! Command Inc2
counter <! Command Inc3
counter <! Command GetState
counter <! Command Inc
counter <! Command ReturnBangBetweenReturn // 不 BecomeIt 情況下會讓return!後面的 return 變成 cont 的一部分

system.Stop (counter.Underlying :?> Akka.Actor.IActorRef)

(*
    就變成好像 return! (
                        xxxxx
                        return effect
                      ) //xxxx跟 return 結合為一體
*)

counter <! Command Dec
counter <! BecomeIt (fun o ->
                        printfn "Only echo left: %O" o
                        Ignore
                    )


async { 
    //let! reply = counter <? Command GetState
    let! reply = counter.Ask(Command GetState, Some (TimeSpan.FromMilliseconds 5000))
    printfn "Current state of %A: %i" counter reply 
    } |> Async.RunSynchronously


type CounterChanged2 =
    { Delta2 : int }

type CounterCommand2 =
    | Inc20
    | ReturnBangBetweenReturn2

type CounterMessage2 =
    | Command2 of CounterCommand2
    | Event2 of CounterChanged2
    | BecomeIt2 of (obj -> Effect<obj>)

let counter2 =
    spawn system "counter-2" <| propsPersist(fun (mailbox:Eventsourced<obj>) ->
        let rec loop state = //recursive
            actor {
                let! msg = mailbox.Receive()
                let me = mailbox.Self.Underlying :?> Akka.Actor.IActorRef
                match msg with
                | :? CounterMessage2 as Event2 changed -> 
                    printfn "changed: %A" changed
                    return! loop (state + changed.Delta2)
                | :? CounterMessage2 as Command2 cmd ->
                    match cmd with
                    | Inc20 -> 
                        return Persist (box (Event2 { Delta2 = 1 }))
                    | ReturnBangBetweenReturn2 ->
                        return Persist (box (Event2 { Delta2 = 10 }))
                        printfn "print1 in ReturnBangBetweenReturn2: %d" state
                        return! loop state
                        printfn "print2 in ReturnBangBetweenReturn2: %d" state
                        return Persist (box (Event2 { Delta2 = 10 }))
                | :? CounterMessage2 as BecomeIt2 b ->
                    become b
                | ignored ->
                    printfn "Ignored msg: %A" ignored
                    return Ignore
            }
        loop 0)

//實驗一，return! 造成中斷後 become 接續
counter2 <! Command2 ReturnBangBetweenReturn2
counter2 <! BecomeIt2 (fun o ->
                        printfn "Only echo left: %O" o
                        Ignore
                    )
counter2 <! Command2 Inc20
Async.Sleep 400 |> Async.RunSynchronously
system.Stop (counter2.Underlying :?> Akka.Actor.IActorRef)



//實驗二，return! 造成中斷後 兩次 become 接續
let f o =
    printfn "Only echo left: %O" o
    Ignore :> Effect<_>
counter2 <! Command2 ReturnBangBetweenReturn2
counter2 <! BecomeIt2 f
                    
counter2 <! Command2 Inc20
Async.Sleep 400 |> Async.RunSynchronously
system.Stop (counter2.Underlying :?> Akka.Actor.IActorRef)




system.Stop (counter.Underlying :?> Akka.Actor.IActorRef)

counter.Underlying.Tell(Akka.Actor.PoisonPill.Instance, Akka.Actor.ActorRefs.Nobody)




#r "nuget: Akka.Persistence.Query"
#r "nuget: Akka.Persistence.Query.InMemory"
#r "nuget: Akka.Cluster.Tools"
open Akka.Persistence.Query.InMemory
open Akka.Persistence.Query
open Akka.Persistence.Query.Sql
open Akka.Cluster.Tools.Singleton
open Akka.Actor
open Akkling.Streams
open Akka.Persistence.Sql


let configWithPort port =
    let config = Configuration.parse ("""
        akka {
            actor {
              provider = "Akka.Cluster.ClusterActorRefProvider, Akka.Cluster"
              serializers {
                hyperion = "Akka.Serialization.HyperionSerializer, Akka.Serialization.Hyperion"
              }
              serialization-bindings {
                "System.Object" = hyperion
              }
            }
          remote {
            helios.tcp {
              public-hostname = "localhost"
              hostname = "localhost"
              port = """ + port.ToString() + """
            }
          }
          cluster {
            auto-down-unreachable-after = 5s
            seed-nodes = [ "akka.tcp://cluster-system@localhost:5000/" ]
          }
          persistence {
            journal.plugin = "akka.persistence.journal.inmem"
            snapshot-store.plugin = "akka.persistence.snapshot-store.inmem"
            query.journal.inmem.refresh-interval = 1s
          }
        }
        """)
    config
        .WithFallback(ClusterSingletonManager.DefaultConfig())
        .WithFallback(InMemoryReadJournal.DefaultConfiguration())

let system2 = System.create "cluster-system" (configWithPort 5000)


let counter3 =
    spawn system2 "counter-3" <| propsPersist(fun (mailbox:Eventsourced<obj>) ->
        let rec loop state = //recursive
            actor {
                let! msg = mailbox.Receive()
                let me = mailbox.Self.Underlying :?> Akka.Actor.IActorRef
                match msg with
                | :? CounterMessage as Event(changed, _) -> 
                    printfn "changed: %A" changed
                    return! loop (state + changed.Delta)
                | :? CounterMessage as Command(cmd) ->
                    match cmd with
                    | Sleep n ->
                        Async.Sleep n |> Async.RunSynchronously
                        
                        return! loop state
                    | GetState ->
                        printfn "GetState: %A" state
                        mailbox.Sender() <! state
                        return! loop state
                    | Inc -> 
                        return Persist (box (Event ({ Delta = 1 }, me)))
                    | Inc2 -> 
                        let l = seq [box (Event ({ Delta = 1 }, me)); box (Event ({ Delta = 1 }, me))]
                        //for eff in l do
                        //    return eff
                        return PersistAll l
                    | Inc3 -> 
                        return Persist (box (Event ({ Delta = 1 }, me)))
                        return Persist (box (Event ({ Delta = 1 }, me)))
                        printfn "print in Inc3: %d" state
                        return Persist (box (Event ({ Delta = 1 }, me)))
                    | ReturnBangBetweenReturn ->
                        return Persist (box (Event ({ Delta = 10 }, me)))
                        printfn "print1 in ReturnBangBetweenReturn: %d" state
                        return! loop state
                        //經實驗，以下不被執行
                        printfn "print2 in ReturnBangBetweenReturn: %d" state
                        return Persist (box (Event ({ Delta = 10 }, me)))
                    | Dec -> return Persist (box (Event ({ Delta = -1 }, me)))
                | :? CounterMessage as BasicCM SaveSS ->
                    return SaveSnapshot state
                    return! loop state
                | :? CounterMessage as BecomeIt b ->
                    become b
                | SnapshotOffer sso ->
                    printfn "sso: %d" sso
                    return! loop sso
                | LifecycleEvent le ->
                    match le with
                    | PreStart -> return Unhandled
                        
                    | _ -> return Unhandled
                | unhandled ->
                    printfn "Unhandled msg: %A" unhandled
            }
        loop 0)

//counter3 <! Command (Sleep 5000)
counter3 <! Command Inc2
counter3 <! Command Inc3
counter3 <! Command GetState
//counter3 <! Command Inc
//counter3 <! Command ReturnBangBetweenReturn // 不 BecomeIt 情況下會讓return!後面的 return 變成 cont 的一部分

let echo : IActorRef<obj> = 
    spawn system2 "echo" <| props (actorOf (fun m -> 
        match m with
        | :? EventEnvelope as ee ->
            printfn "%s %A" (ee.Event.GetType().Name) ee.Event
        | _ ->
            printfn "%A" m 
        Ignore
        ))

echo <! Command Inc2

system2.Stop (echo.Underlying :?> IActorRef)

let mat = system2.Materializer()
//let readJournal = system2.ReadJournalFor<InMemoryReadJournal>(InMemoryReadJournal.Identifier)
let readJournal = system2.ReadJournalFor<SqlReadJournal>(SqlReadJournal.Identifier)
let allEvents = readJournal.AllEvents()
let graph = Sink.ActorRef<EventEnvelope>(echo.Underlying :?> IActorRef, "done") :> IGraph<SinkShape<EventEnvelope>, Akka.NotUsed>
allEvents.RunWith<Akka.NotUsed>(graph, mat)
//mat.Dispose()
//let graph2 = Sink.toActorRef "done" echo
let eventNth = readJournal.AllEvents(Offset.Sequence 4L)

eventNth.RunWith<Akka.NotUsed>(graph, mat)


//modern
system2.Terminate()

#r @"C:\Users\anibal\.nuget\packages\microsoft.extensions.logging.abstractions\1.1.1\lib\netstandard1.1\Microsoft.Extensions.Logging.Abstractions.dll"
#r @"C:\Users\anibal\.nuget\packages\microsoft.extensions.logging\1.1.1\lib\netstandard1.1\Microsoft.Extensions.Logging.dll"
#r @"nuget: FAkka.Shared"
open FAkka.Shared
open Type.FActorSystemType
open FActorSystem


let system2 = (getDefaultActorSystemLocal None [] 1 true None).asys









//以下無意義 async demo


async {
    printfn "2 want to sleep"
    do! Async.Sleep 5000
    printfn "3 wake up"
} |> Async.Start

printfn "1 finish async call"



async {
    printfn "1 want to sleep"
    do! Async.Sleep 5000
    printfn "2 wake up"
} |> Async.RunSynchronously

printfn "3 finish async call"



printfn "1 want to sleep"
Async.Sleep 5000 |> Async.RunSynchronously
printfn "2 wake up"
printfn "3 finish async call"