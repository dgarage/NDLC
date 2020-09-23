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
        Network = Network.Main
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
        
   
    let tryGetOracle (globalConfig: GlobalConfig) (oracleName: string) =
        task {
            let! id =
                let n = nameRepo globalConfig
                n.GetId(Scopes.Oracles, oracleName)
            if isNull id then return None else
            let mutable pk: ECXOnlyPubKey = null
            match ECXOnlyPubKey.TryCreate(ReadOnlySpan(Encoders.Hex.DecodeData(id)), Context.Instance, &pk) with
            | false -> return None
            | true ->
                Debug.Assert(pk |> isNull |> not)
                let repo = repository (globalConfig)
                let! result = repo.GetOracle(pk)
                return Some (result)
        }
        
    let getOracle (globalConfig) (oracleName) = task {
        match! tryGetOracle globalConfig oracleName with
        | None -> return failwithf "Unreachable! Unknown OracleName %s" oracleName
        | Some o ->
            return o
    }
