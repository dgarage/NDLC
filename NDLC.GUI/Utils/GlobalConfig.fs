namespace NDLC.GUI.Utils

open FSharp.Control.Tasks
open System
open System.Diagnostics
open System.IO
open NBitcoin
open NBitcoin.DataEncoders
open NBitcoin.Secp256k1
open NDLC.Infrastructure
open Newtonsoft.Json

type GlobalConfig = {
    Network: Network
    DataDir: string option
}
with
    static member  Default = {
        Network = Network.RegTest
        DataDir = None
    }


module ConfigUtils =
    let jsonSerializerSettings (c) =
        let j = JsonSerializerSettings()
        j.Formatting <- Formatting.Indented
        NDLC.Messages.Serializer.Configure(j, c.Network)
        j
    
    let mutable r: Repository = null
    let repository (c: GlobalConfig) =
        if (r |> isNull |> not) then r else
        r <- Repository(c.DataDir |> Option.toObj, c.Network)
        r
        
    let mutable nRepo: NameRepository = null
    let nameRepo (c: GlobalConfig) =
        if (nRepo |> isNull |> not) then nRepo else
        nRepo <-
            ((repository c).RepositoryDirectory, "names.json")
            |> Path.Combine
            |> NameRepository
        nRepo
