module internal NDLC.GUI.DLC.CheckSig

open System.Text
open System.Threading.Tasks
open FSharp.Control.Tasks
open NBitcoin
open NBitcoin.DataEncoders
open NDLC.GUI.DLC
open NDLC.GUI.Utils
open NDLC.Infrastructure
open NDLC.Messages
open Newtonsoft.Json
open Newtonsoft.Json.Linq

[<AllowNullLiteral>]
type private ContractIds() =
    member val OffererContractId: string = null with get,set
    member val AcceptorContractId: string = null with get, set

let inline private parse g (base64: string) =
    let repo = ConfigUtils.repository g
    let json = UTF8Encoding.UTF8.GetString(Encoders.Base64.DecodeData(base64));
    JsonConvert.DeserializeObject<_>(json, repo.JsonSettings) |> Option.ofObj
    
let private tryGetDLCFromID (repo: Repository) (contractId: uint256) = task {
    if (contractId |> isNull) then
        // TODO: Allow to pass a hint via command line
        return Error("This accept message does not match any of our DLC");
    else
        let! dlc = repo.GetDLC(contractId);
        if (dlc |> isNull) then
            return Error("This accept message does not match any of our DLC")
        else
            return Ok(dlc)
    }

let private handleAccept g base64: Task<PSBT> = task {
    match parse g base64 with
    | None ->
        return failwith "Failed to parse input"
    | Some (accept: Accept) ->
        let repo = ConfigUtils.repository g
        let! dlcResult = tryGetDLCFromID (repo) (accept.OffererContractId)
        match dlcResult with
        | Error e -> return failwith e
        | Ok dlc ->
        match DLCUtils.assertState (dlc, true, Repository.DLCState.DLCNextStep.CheckSigs, g.Network) with
        | Error e -> return failwith e
        | Ok _ ->
        let builder = DLCTransactionBuilder(dlc.BuilderState.ToString(), g.Network);
        builder.Sign1(accept);
        dlc.BuilderState <- builder.ExportStateJObject();
        dlc.Accept <- JObject.FromObject(accept, JsonSerializer.Create(repo.JsonSettings));
        do! repo.SaveDLC(dlc);
        return (builder.GetFundingPSBT());
}

let private handleSign g base64 = task {
    match parse g base64 with
    | None ->
        return failwith "Failed to parse input"
    | Some (s: Sign) ->
        let repo = ConfigUtils.repository g
        let! dlcResult = tryGetDLCFromID (repo) (s.AcceptorContractId)
        match dlcResult with
        | Error e -> return failwith e
        | Ok dlc ->
        match DLCUtils.assertState (dlc, false, Repository.DLCState.DLCNextStep.CheckSigs, g.Network) with
        | Error e -> return failwith e
        | Ok _ ->
        let builder = DLCTransactionBuilder(dlc.BuilderState.ToString(), g.Network)
        builder.Finalize1(s)
        dlc.Sign <- JObject.FromObject(sign, JsonSerializer.Create(repo.JsonSettings));
        dlc.BuilderState <- builder.ExportStateJObject();
        do! repo.SaveDLC(dlc);
        return (builder.GetFundingPSBT())
} 
let checksig globalConfig (base64: string) = task {
    match parse globalConfig (base64)  with
    | None -> return failwith "Failed to parse input into ContractIds"
    | Some (contractids: ContractIds) ->
    match contractids.AcceptorContractId, contractids.OffererContractId with
    | null, _ ->
        return failwith "Invalid signed message"
    | _acceptor, null ->
        return! handleSign (globalConfig) (base64)
    | _acceptor, _offerer ->
        return! handleAccept (globalConfig) (base64)
}

