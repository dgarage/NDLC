[<AutoOpen>]
module NDLC.GUI.ParserAndValidators

open NBitcoin
open System
open NDLC
open NDLC.Infrastructure

let tryParseContractInfo (str: string) =
    match DiscretePayoffs.TryParse(str) with
    | false, _ -> Error("Invalid payoffs!")
    | true, c ->
        if (c.Count < 2) then Error("You must specify at least 2 contract info!") else
        Ok(c)
        
let validateContractInfo(str: string) =
    match tryParseContractInfo(str) with
    | Ok _ -> None
    | Error e -> Some e
    
let tryParseLockTime (str: string) =
    match UInt32.TryParse str with
    | true, r -> Ok(LockTime(r))
    | _ -> Error("Invalid input for locktime")
    
let validateLockime(str: string) =
    match tryParseLockTime(str) with
    | Ok _ -> None
    | Error e -> Some (e)
    
let tryParseEventFullname (str: string) =
    match EventFullName.TryParse str with
    | true, r -> Ok(r)
    | _ -> Error("Invalid input for EventFullName")
    
let validateEventFullName(str: string) =
    match tryParseEventFullname(str) with
    | Ok _ -> None
    | Error e -> Some (e)
    
let tryParseFeeRate (str: string) =
    match Int64.TryParse str with
    | true, r -> Ok(FeeRate(Money (r)))
    | _ -> Error("Invalid input for FeeRate")
    
let validateFeeRate(str: string) =
    match tryParseFeeRate(str) with
    | Ok _ -> None
    | Error e -> Some (e)
    
let tryParsePSBT (str: string, n: Network)=
    match PSBT.TryParse(str, n) with
    | true, psbt -> Ok psbt
    | _ -> Error("Invalid PSBT! Maybe wrong Network?")

let validatePSBT(str: string, n: Network) =
    match tryParsePSBT (str, n) with
    | Ok _ -> None
    | Error e -> Some (e)
