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
    
    let repository (c: GlobalConfig) = Repository(c.DataDir |> Option.toObj, c.Network)
    let nameRepo (c: GlobalConfig) =
        ((repository c).RepositoryDirectory, "names.json")
        |> Path.Combine
        |> NameRepository
        
