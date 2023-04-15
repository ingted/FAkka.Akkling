#r "nuget: Akka.Serialization.Hyperion"
#r "nuget: Akka.Cluster.Sharding"
#r "nuget: Akkling"
#r "nuget: Akkling.Persistence"
#r "nuget: Akkling.Cluster.Sharding"

open System
open Akka.Actor
open Akka.Configuration
open Akka.Cluster
open Akka.Cluster.Tools.Singleton
open Akka.Cluster.Sharding
open Akka.Persistence

open Akkling
open Akkling.Persistence
open Akkling.Cluster
open Akkling.Cluster.Sharding
open Hyperion

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
            snapshot-store.plugin = "akka.persistence.snapshot-store.local"
          }
        }
        """)
    config.WithFallback(ClusterSingletonManager.DefaultConfig())

let behavior (ctx : Actor<obj>) (msg:obj) = 
    printfn "%A received %A" (ctx.Self.Path.ToStringWithAddress()) msg //|> ignored
    match msg with
    | LifecycleEvent e ->
        match e with
        | PreStart -> printfn "Actor %A has started" ctx.Self
        | PostStop -> printfn "Actor %A has stopped" ctx.Self
        | _ -> ()
        Ignore
    | x -> 
        printfn "Ignore %A" x
        Ignore



// spawn two separate systems with shard regions on each of them

let system1 = System.create "cluster-system" (configWithPort 5000)

system1.Settings.Config.GetString("akka.cluster.sharding.use-dispatcher")


let fac1 = entityFactoryFor system1 "printer" <| props (actorOf2 behavior)

// wait a while before starting a second system
System.Threading.Thread.Sleep 5000

let system2 = System.create "cluster-system" (configWithPort 5001)
let fac2 = entityFactoryFor system2 "printer" <| props (actorOf2 behavior)

System.Threading.Thread.Sleep 5000

let entity1 = fac1.RefFor "shard-1" "entity-1"
let entity1 = fac2.RefFor "shard-1" "entity-1"

let c1 = Akka.Cluster.Cluster.Get system1

//c1.JoinSeedNodes [|c1.SelfAddress|]

c1.RemotePathOf (entity1.Underlying :?> IActorRef)
system1.Terminate()


let john = fac1.RefFor "shard-2" "john"
let alice = fac1.RefFor "shard-3" "alice"
let frank = fac1.RefFor "shard-4" "frank"

entity1 <! "hello"
entity1 <! " world"
john <! "hello John"
alice <! "hello Alice"
frank <! "hello Frank"

// check which shards have been build on the second shard region

System.Threading.Thread.Sleep(5000)

open Akka.Cluster.Sharding

let printShards shardReg =
    async {
        let! (stats: ShardRegionStats) = (typed shardReg) <? GetShardRegionStats.Instance
        for kv in stats.Stats do
            printfn "\tShard '%s' has %d entities on it" kv.Key kv.Value
    } |> Async.RunSynchronously

printfn "Shards active on node 'localhost:5000':"
printShards fac1.ShardRegion
printfn "Shards active on node 'localhost:5001':"
printShards fac2.ShardRegion


system1.Stop fac1.ShardRegion




let fac1 = entityFactoryFor system1 "printer" <| props (actorOf2 behavior)

let entity1 = fac1.RefFor "shard-1" "entity-1"

entity1 <! "hello"

system1.Stop fac1.ShardRegion