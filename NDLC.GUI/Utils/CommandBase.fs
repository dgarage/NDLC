module NDLC.GUI.Utils.CommandBase

open System
open System.Diagnostics
open System.Threading.Tasks
open FSharp.Control.Tasks
open NBitcoin.DataEncoders
open NBitcoin.Secp256k1
open NDLC.Infrastructure


let tryGetOracle (globalConfig) (oracleName: string): Task<Repository.Oracle option>  = task {
    let nameRepo = ConfigUtils.nameRepo globalConfig
    let! id = nameRepo.GetId(Scopes.Oracles, oracleName)
    if (id |> isNull) then return None else
    let mutable pk: ECXOnlyPubKey = null
    match ECXOnlyPubKey.TryCreate(ReadOnlySpan(Encoders.Hex.DecodeData(id)), Context.Instance, &pk) with
    | false -> return None
    | true ->
        Debug.Assert(pk |> isNull |> not)
        let repo = ConfigUtils.repository (globalConfig)
        let! result = repo.GetOracle(pk)
        return result |> Option.ofObj
}

let getOracle (globalConfig) (oracleName: string): Task<Repository.Oracle>  = task {
    let! r = tryGetOracle globalConfig (oracleName)
    match r with
    | Some x -> return x
    | None -> return failwithf "Failed to get oracle with %s " oracleName
}

let getEvent (globalConfig) (optionName: string, evtName: EventFullName): Task<Repository.Event> = task {
    return failwith "TODO"
}
let tryGetDLC globalConfig (dlcName: string) = task {
    return failwith ""
}
