module NDLC.GUI.DLC.DLCList

open Elmish
open NDLC.GUI.Utils
open NDLC.Infrastructure


type KnownDLCs = private {
    LocalName: string
    State: Repository.DLCState
}

type State = private {
    KnownDLCs: Deferred<Result<KnownDLCs, string>>
}

type InternalMsg =
    private
    | LoadDLCs of AsyncOperationStatus<Result<KnownDLCs, string>>
    
type GotoInfo = {
    NextStep: Repository.DLCState.DLCNextStep
    LocalName: string
}
type OutMsg =
    | GoToNextStep of GotoInfo
    
type Msg =
    | ForSelf of InternalMsg
    | ForParent of OutMsg
    
type TranslationDictionary<'Msg> = {
    OnInternalMsg: InternalMsg -> 'Msg
    OnGoToNextStep: GotoInfo -> 'Msg
}

type Translator<'Msg> = Msg -> 'Msg

let translator ({ OnInternalMsg = onInternalMsg; OnGoToNextStep = onInputFinished }: TranslationDictionary<'Msg>): Translator<'Msg> =
    function
    | ForSelf i -> onInternalMsg i
    | ForParent(GoToNextStep info) -> onInputFinished info
    
let init =
    { KnownDLCs = HasNotStartedYet }, Cmd.ofMsg(LoadDLCs Started)
    
let update (globalConfig) (msg: InternalMsg) (state: State) =
    match msg with
    | LoadDLCs(Started) ->
        failwith "TODO: load"
    | LoadDLCs(Finished dlcs) ->
        { state with KnownDLCs = Deferred.Resolved(dlcs) }, Cmd.none
