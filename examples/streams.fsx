//https://stackoverflow.com/questions/37911174/via-viamat-to-tomat-in-akka-stream

#r "nuget: Akka.Serialization.Hyperion"
#r "nuget: Akka.Streams"
#r "nuget: Akkling"
#r "nuget: Akkling.Streams"

open System
open Akka.Streams
open Akka.Streams.Dsl
open Akkling
open Akkling.Streams

let system = System.create "streams-sys" <| Configuration.defaultConfig()
let mat = system.Materializer()

let text = """
       Lorem Ipsum is simply dummy text of the printing and typesetting industry.
       Lorem Ipsum has been the industry's standard dummy text ever since the 1500s,
       when an unknown printer took a galley of type and scrambled it to make a type
       specimen book."""

// 1. Basic stream transformation
let src = 
    Source.ofArray (text.Split())
    |> Source.map (fun x -> x.ToUpper())
    |> Source.filter (String.IsNullOrWhiteSpace >> not)
let async1 = 
    src
    |> Source.runForEach mat (printfn "1 %s")
let async2 = 
    src
    |> Source.runForEach mat (printfn "2 %s")

async {
    do! async1
    do! async2
}
|> Async.RunSynchronously

src
|> Source.runForEach mat (fun s ->
    printfn "1 %s" s
    Async.Sleep 1000 |> Async.RunSynchronously
    )

src
|> Source.runForEach mat (fun s ->
    printfn "2 %s" s
    Async.Sleep 1000 |> Async.RunSynchronously
    )


let tm =  src |> Source.toMat (Sink.forEach(fun s -> printfn "1 Received: %s" s)) Keep.left
let tm2 = src |> Source.toMat (Sink.forEach(fun s -> printfn "2 Received: %s" s)) Keep.left
tm |> Graph.run mat
tm2 |> Graph.run mat


let sink111 = Sink.forEach(fun s -> printfn "1 Received: %d" s).MapMaterializedValue(fun a -> 
                                                                                        a |> Async.RunSynchronously |> ignore
                                                                                        Akka.NotUsed.Instance
                                                                                        ) :> IGraph<SinkShape<_>, _>
let sink222 = Sink.forEach(fun s -> printfn "2 Received: %d" s).MapMaterializedValue(fun a -> 
                                                                                        a |> Async.RunSynchronously |> ignore
                                                                                        Akka.NotUsed.Instance
                                                                                        ) :> IGraph<SinkShape<_>, _>
let ftt = Flow.Create<int>().AlsoTo(sink111).To(sink222)

let g111222 = (Source.ofArray [|87;87;87|]) |> Source.toSink ftt

g111222 |> Graph.run mat



let sink111_2 = Sink.forEach(fun s -> printfn "2 Received: %d" s) |> Sink.mapMatValue (fun done_ -> Akka.NotUsed.Instance)











// 2. Actor interop

let behavior targetRef (m:Actor<_>) =
    let rec loop () = actor {
        let! msg = m.Receive ()
        targetRef <! msg
        return! loop ()
    }
    loop ()

let spawnActor targetRef =
    spawnAnonymous system <| props (behavior targetRef)

let s = 
    let arf =Source.actorRef OverflowStrategy.DropNew 1000    
    let mapv = arf |> Source.mapMaterializedValue(spawnActor)
    let sToMap = mapv |> Source.toMat(Sink.forEach(fun s -> printfn "Received: %s" s)) Keep.both
    sToMap |> Graph.run mat

fst s <! "Boo"

async {
    let! done_ = snd s
    printfn "----"
} |> Async.RunSynchronously



let s2 = 
    let arf =Source.actorRef OverflowStrategy.DropNew 1000
    let sToMap = arf |> Source.toMat(Sink.forEach(fun s -> printfn "Received: %s" s)) Keep.left
    sToMap |> Graph.run mat

s2 <! "Boo"

// 3. Dynamic streams

let sink = Sink.forEach (printfn "%s")
let consumer = 
    let srcMgHub = Source.mergeHub 10 
    srcMgHub
    |> Source.toMat sink Keep.left
    |> Graph.run mat


//Source.toMat sink Keep.left (Source.mergeHub 10)



Source.singleton "hello" |> Source.runWith mat consumer
Source.singleton "world" |> Source.runWith mat consumer

// 4. TCP stream

open Akka.Streams.Dsl

// server
let echo = Flow.id
async {
    //let! server = 
    //    system.TcpStream()
    //    |> Tcp.bindAndHandle mat "localhost" 5000 echo

    let handler = Sink.forEach (fun (conn: Tcp.IncomingConnection) ->
        printfn "New client connected (local: %A, remote: %A)" conn.LocalAddress conn.RemoteAddress
        conn.HandleWith(echo, mat))

    let! server =
        system.TcpStream()
        |> Tcp.bind "localhost" 5000
        |> Source.toMat handler Keep.left
        |> Graph.run mat

    printfn "TCP server listetning on %A" server.LocalAddress
    Console.ReadLine() |> ignore

    do! server.AsyncUnbind()
} |> Async.RunSynchronously

// client
open Akka.IO

let parser = 
    Flow.id
    |> Flow.takeWhile ((<>) "q")
    |> Flow.concat (Source.singleton "BYE")
    |> Flow.map (fun x -> ByteString.FromString(x + "\n"))


let repl = 
    Framing.delimiter true 256 (ByteString.FromString("\n"))
    |> Flow.map string
    |> Flow.iter (printfn "Server: %s")
    |> Flow.map (fun _ -> Console.ReadLine())
    |> Flow.via parser
    
async {
    let! client = 
        system.TcpStream()
        |> Tcp.outgoing "localhost" 5000 
        |> Flow.join repl
        |> Graph.run mat

    printfn "Client connected (local: %A, remote: %A)" client.LocalAddress client.RemoteAddress
} |> Async.RunSynchronously
