#r "nuget: Akka.Serialization.Hyperion"
#r "nuget: Akka.Persistence"
#r "nuget: Akkling"
#r "nuget: Akkling.Persistence"

open System
open Akka.Persistence
open Akkling
open Akkling.Persistence

(* Delivery mechanism looks like this - if sender wants to reliably deliver payload to recipient 
   using at-least-once delivery semantics, it sends that payload wrapped to Messenger actor, which 
   is responsible for the persistence and redelivery:
   
    +--------+                      +-----------+                    +-----------+
    |        |--(DeliverOrder<T>)-->|           |--(Delivery<T>:1)-->|           |
    |        |                      |           |  /* 2nd attempt */ |           |
    | Sender |                      | Messenger |--(Delivery<T>:2)-->| Recipient |
    |        |                      |           |                    |           |
    |        |                      |           |<----(Confirm:2)----|           |
    +--------+                      +-----------+                    +-----------+
*)

type Delivery = { Payload: string; DeliveryId: int64 }

type Cmd =
    | DeliverOrder of string * IActorRef<Delivery>
    | Confirm of int64
    | Save

type Evt =
    | MessageSent of string * IActorRef<Delivery> 
    | Confirmed of int64

let system = System.create "system" <| Configuration.defaultConfig()



let messenger : IActorRef<Cmd> = 
    spawn system "counter-1" (propsPersist(fun mailbox ->
        let deliverer = AtLeastOnceDelivery.createDefault mailbox
        
        printfn "MaxUnconfirmedMessages: %d" deliverer.MaxUnconfirmedMessages
        let rec loop () = 
            actor {
                let! msg = mailbox.Receive()
                let uo : Actor<obj> = upcast mailbox
                let effect = deliverer.Receive uo msg
                if effect.WasHandled()
                then return effect
                else 
                    match msg with
                    | SnapshotOffer (snap: AtLeastOnceDeliverySnapshot) ->
                        if snap <> null then
                            printfn "sso: %d" snap.UnconfirmedDeliveries.Length
                        deliverer.DeliverySnapshot <- snap
                    | :? Cmd as cmd ->
                        match cmd with
                        | DeliverOrder(payload, recipient) ->
                            return Persist(MessageSent(payload, recipient))
                        | Confirm(deliveryId) ->
                            return Persist(Confirmed(deliveryId))
                        | Save ->
                            return SaveSnapshot deliverer.DeliverySnapshot
                    | :? Evt as evt ->
                        printfn "Get Evt: %A" evt
                        match evt with
                        | MessageSent(payload, recipient) ->
                            let fac d = { Payload = payload; DeliveryId = d }
                            return deliverer.Deliver(recipient.Path, fac, mailbox.IsRecovering()) |> ignored
                        | Confirmed(deliveryId) ->
                            printfn "Message %d confirmed" deliveryId
                            return deliverer.Confirm deliveryId |> ignored
                    | _ -> return Unhandled
            }
        loop ())) 
    |> retype

let echo = spawn system "echo" <| props(actorOf2(fun mailbox msg ->
    printfn "Received: %s. Confirming" msg.Payload
    mailbox.Sender() <! Confirm msg.DeliveryId
    Ignore))

echo.Underlying.Tell(Akka.Actor.PoisonPill.Instance, Akka.Actor.ActorRefs.Nobody)
messenger <! DeliverOrder("hello world5", echo)
messenger <! DeliverOrder("hello world6", echo)
messenger.Underlying.Tell(Akka.Actor.PoisonPill.Instance, Akka.Actor.ActorRefs.Nobody)


messenger
messenger <! Save



Unchecked.defaultof<Akka.Actor.IActorRef>

