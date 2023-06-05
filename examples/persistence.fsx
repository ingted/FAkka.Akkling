#r "nuget: Akka.Serialization.Hyperion"
#r "nuget: Akka.Persistence"
#r "nuget: Akkling"
#r "nuget: Akka.Streams"
#r "nuget: Akkling.Persistence"

open System
open Akkling
open Akkling.Persistence

let system = System.create "persisting-sys" <| Configuration.defaultConfig()

type CounterChanged =
    { Delta : int }

type CounterCommand =
    | Inc
    | Dec
    | GetState

type CounterMessage =
    | Command of CounterCommand
    | Event of CounterChanged

let counter =
    spawn system "counter-1" <| propsPersist(fun mailbox ->
        let rec loop state =
            actor {
                let! msg = mailbox.Receive()
                match msg with
                | Event(changed) -> return! loop (state + changed.Delta)
                | Command(cmd) ->
                    match cmd with
                    | GetState ->
                        mailbox.Sender() <! state
                        printfn "%A" state
                        return! loop state
                    | Inc -> return Persist (Event { Delta = 1 })
                    | Dec -> return Persist (Event { Delta = -1 })
            }
        loop 0)


   //1     C2 C1 人 
   //2     C2 E1 人 C1 
   //3        C2 人 E1 C1
//!true operator 運算子 operand 運算元
let a = Unchecked.defaultof<IActorRef<int>>
counter <! Command Inc
counter <! Command Inc
counter <! Command Dec
counter <! Command GetState

counter <! Event { Delta = 1 }

async { let! reply = counter <? Command GetState
        printfn "Current state of %A: %i" counter reply } |> Async.RunSynchronously

type AT =
| O of int


let plus1 a b = a + 1 //函數映射 mapping