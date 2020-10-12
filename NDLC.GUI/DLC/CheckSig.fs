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
open NDLC.Messages
open Newtonsoft.Json
open Newtonsoft.Json.Linq

let private parse<'T when 'T : null and 'T : not struct> (g, base64: string): 'T option =
    let repo = ConfigUtils.repository g
    let json = UTF8Encoding.UTF8.GetString(Encoders.Base64.DecodeData(base64));
    JsonConvert.DeserializeObject<'T>(json, repo.JsonSettings) |> Option.ofObj
    
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

let private handleAccept g (accept: Accept): Task<PSBT> = task {
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

let private handleSign g (s: Sign) = task {
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
    match parse<Accept>(globalConfig, base64),parse<Sign> (globalConfig, base64)  with
    | Some a, None ->
        return! handleAccept (globalConfig) (a)
    | _, Some s ->
        return! handleSign (globalConfig) (s)
    | None, None ->
        return failwith "Failed to parse Accept/Sign message"
}

