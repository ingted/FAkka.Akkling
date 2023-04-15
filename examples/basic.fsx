#r "nuget: Akka.Serialization.Hyperion"
#r "nuget: Akkling"
//alt-enter
open System
open Akkling
open Akka.Actor

type Larry = {
    gf1: int
    gf2: int
}

let allWhatLarryHas = {gf1 = 100; gf2 = 200}

printfn "allWhatLarryHas -> %O" allWhatLarryHas
printfn "allWhatLarryHas -> %A" allWhatLarryHas


type AnibaSqueezed = 
| NG of times : int
| ES of times : int

printfn "AnibaSqueezed -> %A"  (NG 88)

let n88 = NG 88

match n88 with
| NG t -> printfn "(((%d))) %s" t "OMG"
| ES _ ->
    ()


let system = System.create "basic-sys" <| Configuration.defaultConfig()

let behavior (m:Actor<_>) =
    let rec loop () = actor {
        let! msg = m.Receive ()
        match msg with
        | "stop" -> return Stop
        | "unhandle" -> return Unhandled
        | "failed" -> return (failwith "omg" : ActorEffect<_>) 
        | x ->
            printfn "%s" x
            return! loop ()
    }
    loop ()

// 1. First approach - using explicit behavior loop
let helloRef = spawnAnonymous system (props behavior)


type PingPong =
| Ping
| Pong
| Serve of IActorRef<PingPong>




let genPlayer nm = spawn system nm (props (actorOf2 (fun mbox (m:PingPong) ->
    
    printfn "I (%A) received %A" mbox.Self.Path.Name m
    match m with
    | Ping -> 
        mbox.Sender().Tell(Pong, mbox.Self.Underlying :?> IActorRef)
    | Pong -> 
        mbox.Sender().Tell(Ping, mbox.Self.Underlying :?> IActorRef)
    | Serve iactorref -> iactorref <! Ping

    
    Ignore
)))


let player1 = genPlayer "anibal"
let player2 = genPlayer "larry"

player1 <! Serve player2





helloRef <! "failed"
helloRef <! "failed_ttc"

helloRef <! "unhandle"
helloRef <! "stop"

// 2. Second approach - using implicits
let helloBehavior _ = function
    | "stop" -> stop ()
    | "unhandle" -> unhandled ()
    | x -> printfn "%s" x |> ignored

helloRef <! "ok"        // "ok"
helloRef <! "unhandle"
helloRef <! "stop"      // "ok"

// 3. Using receiver combinators
//    - <|> executes right side always when left side was unhandled
//    - <&> executes right side only when left side was handled
let allwaysHandled _ n = printfn "always handled: %s" n |> ignored
let onlyWhenHanled _ n = printfn "combined behavior: %s" n |> ignored
let orRef = spawn system "or-actor" <| props (actorOf2 (helloBehavior <|> allwaysHandled))
let andRef = spawn system "and-actor" <| props (actorOf2 (helloBehavior <&> onlyWhenHanled))

orRef <! "ok"           // "ok"
orRef <! "unhandle"     // "always handled: unhandle"

andRef <! "ok"          // "ok"
                        // "combined behavior: ok"
andRef <! "unhandle"

// 4. Stateful actors

// 4.1. using explicit loop
let stateRef = spawnAnonymous system <| props(fun ctx ->
    let rec loop state = actor {
       let! n = ctx.Receive()
       match n with
       | "print" -> printfn "Current state: %A" state
       | other -> return! loop (other::state)
    }
    loop [])

stateRef <! "a"
stateRef <! "b"
stateRef <! "print"

// 4.2. more implicit
let rec looper state = function
    | "print" -> printfn "Current state: %A" state |> ignored
    | other -> become (looper (other::state))

let stateRef2 = spawnAnonymous system <| props(actorOf (looper []))

stateRef2 <! "a"
stateRef2 <! "b"
stateRef2 <! "print"

let a =
    let mutable condition = true
    while condition do
        printfn "%s %A" "ff" 123
        condition <- false


let b = lazy(456)      
b.Value