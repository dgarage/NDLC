namespace NDLC.GUI.Utils

open NBitcoin.DataEncoders

// for stackalloc
#nowarn "9"

open System


module Option =
    let inline toObj (o: Option<'a>) =
        match o with Some oo -> oo | None -> null


type AsyncOperationStatus<'t> =
    | Started
    | Finished of 't
    
type Deferred<'t> =
    | HasNotStartedYet
    | InProgress
    | Resolved of 't
    
[<RequireQualifiedAccess>]
/// Contains utility functions to work with value of the type `Deferred<'T>`.
module Deferred =

    let hasNotStarted = function
        | HasNotStartedYet -> true
        | _ -> false
    /// Returns whether the `Deferred<'T>` value has been resolved or not.
    let resolved = function
        | HasNotStartedYet -> false
        | InProgress -> false
        | Resolved _ -> true
        

    /// Returns whether the `Deferred<'T>` value is in progress or not.
    let inProgress = function
        | HasNotStartedYet -> false
        | InProgress -> true
        | Resolved _ -> false

    /// Transforms the underlying value of the input deferred value when it exists from type to another
    let map (transform: 'T -> 'U) (deferred: Deferred<'T>) : Deferred<'U> =
        match deferred with
        | HasNotStartedYet -> HasNotStartedYet
        | InProgress -> InProgress
        | Resolved value -> Resolved (transform value)

    /// Verifies that a `Deferred<'T>` value is resolved and the resolved data satisfies a given requirement.
    let exists (predicate: 'T -> bool) = function
        | HasNotStartedYet -> false
        | InProgress -> false
        | Resolved value -> predicate value

    /// Like `map` but instead of transforming just the value into another type in the `Resolved` case, it will transform the value into potentially a different case of the the `Deferred<'T>` type.
    let bind (transform: 'T -> Deferred<'U>) (deferred: Deferred<'T>) : Deferred<'U> =
        match deferred with
        | HasNotStartedYet -> HasNotStartedYet
        | InProgress -> InProgress
        | Resolved value -> transform value
        
[<RequireQualifiedAccess>]
module String =
    open FSharp.NativeInterop
    let isBase64 (s: string) =
        let dummy' = NativePtr.stackalloc<byte>(s.Length)
        let dummy = Span<byte>(NativePtr.toVoidPtr dummy', s.Length)
        Convert.TryFromBase64String(s, dummy) |> fst
        
