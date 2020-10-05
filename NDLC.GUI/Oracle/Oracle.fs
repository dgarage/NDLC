namespace NDLC.GUI

open FSharp.Control.Tasks

open Avalonia
open Avalonia.Controls
open Avalonia.Controls.Presenters
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Styling

open Elmish

open Avalonia.FuncUI.Components
open Avalonia.FuncUI.DSL

open NDLC.Infrastructure
open NDLC.Secp256k1

open System
open System.Diagnostics
open Avalonia.Media
open NBitcoin
open NBitcoin.DataEncoders
open NBitcoin.Secp256k1
open NDLC.GUI.GlobalMsgs
open NDLC.GUI.Oracle
open NDLC.GUI.Oracle.DomainModel
open NDLC.GUI.Utils
            
module OracleModule =
    open NDLC.GUI.Utils
    module Constants =
        [<Literal>]
        let defaultOracleName = "MyOwesomeOracleName"
    type KnownOracles = {
        Mine: OracleInfo seq
        Others: OracleInfo seq
    }
        with
        member this.Exists(o: OracleInfo) =
            this.Mine |> Seq.exists(fun m -> m = o) ||
            this.Others |> Seq.exists(fun m -> m = o)
        member this.Remove(o: OracleId) =
            {
                Mine = this.Mine |> Seq.where(fun m -> m.OracleId.ToString() <> o.ToString())
                Others = this.Others |> Seq.where(fun m -> m.OracleId.ToString() <> o.ToString())
            }
        member this.Remove(o: OracleInfo) =
            this.Remove(o.OracleId)
            
        member this.AddOrReplace(o: OracleInfo) =
            let t = this.Remove(o)
            match o.KeyPath with
            | Some _ ->
                { t with Mine = Seq.append t.Mine (Seq.singleton o) }
            | None -> { t with Others = Seq.append t.Others (Seq.singleton o) }
        
    type State =
        {
            KnownOracles: Deferred<Result<KnownOracles, string>>
            Selected: (OracleInfo * EventModule.State) option
            OracleInImport: OracleInImportModule.State option
            InvalidOracleErrorMsg: string option
        }
    type InternalMsg =
        | LoadOracles of AsyncOperationStatus<Result<KnownOracles, string>>
        | Remove of OracleInfo
        | Select of OracleInfo option
        | Generate of name: string
        | InvalidOracle of errorMsg: string
        | ToggleOracleImport
        | SetOracleNameStarted of name: string * o: OracleInfo
        | SetOracleNameFinished of OracleInfo
        | NewOracle of OracleInfo
        | EventMsg of EventModule.InternalMsg
        | OracleInImportMsg of OracleInImportModule.InternalMsg
        
    type OutMsg =
        | NewOffer of NewOfferMetadata
    type Msg =
        | ForSelf of InternalMsg
        | ForParent of OutMsg
        
    let eventMsgTranslator = EventModule.translator { OnInternalMsg = EventMsg >> ForSelf; OnNewOffer = NewOffer >> ForParent }
    let oracleInImportTranslator = OracleInImportModule.translator { OnInternalMsg = OracleInImportMsg >> ForSelf; OnNewOracle = NewOracle >> ForSelf }
    let loadOracleInfos(globalConfig) =
        task {
            let nameRepo =  (ConfigUtils.nameRepo globalConfig)
            let! nameAndId =
                nameRepo.AsOracleNameRepository().GetIds()
            let oracleInfos = ResizeArray()
            for kv in nameAndId do
                let! o = CommandBase.getOracle(globalConfig) (kv.Key)
                oracleInfos.Add({ OracleInfo.Name = kv.Key
                                  OracleId = kv.Value
                                  KeyPath = o.RootedKeyPath |> Option.ofObj
                                })
            let mine =
                oracleInfos |> Seq.where(fun o -> o.KeyPath.IsSome) |> Seq.sortBy(fun o -> o.Name)
            let others = 
                oracleInfos |> Seq.where(fun o -> o.KeyPath.IsNone) |> Seq.sortBy(fun o -> o.Name)
            return
                { Mine = mine; Others = others }
                |> Ok
                |> Finished
                |> LoadOracles
        }
        
    let removeOracle (repo: NameRepository) oracle = task {
        let! isOk = repo.RemoveMapping(Scopes.Oracles, oracle.Name)
        if (not <| isOk) then raise <| Exception("Failed to remove oracle! This should never happen") else
        return ()
    }
    
    type TranslationDictionary<'Msg> = {
        OnInternalMsg: InternalMsg -> 'Msg
        OnNewOffer: NewOfferMetadata -> 'Msg
    }
    
    type Translator<'Msg> = Msg -> 'Msg
    
    let translator ({ OnInternalMsg = onInternalMsg; OnNewOffer = onNewOffer }: TranslationDictionary<'Msg>): Translator<'Msg> =
        function
        | ForSelf i -> onInternalMsg i
        | ForParent (NewOffer x) -> onNewOffer x
        
    let init =
        { KnownOracles = Deferred.HasNotStartedYet
          Selected = None
          OracleInImport = None
          InvalidOracleErrorMsg = None
          }, Cmd.batch [Cmd.ofMsg(LoadOracles Started)]
        
    let update (globalConfig) (msg: InternalMsg) (state: State) =
        match msg with
        | LoadOracles Started ->
            { state with KnownOracles = InProgress }, Cmd.OfTask.result (loadOracleInfos globalConfig)
        | LoadOracles (Finished (Ok oracles)) ->
            { state with KnownOracles = Resolved(Ok oracles) }, Cmd.none
        | LoadOracles (Finished (Error e)) ->
            {state with KnownOracles = Resolved(Error e)} , Cmd.none
        | Remove oracle ->
            let nameRepo = ConfigUtils.nameRepo globalConfig
            (removeOracle nameRepo oracle).GetAwaiter().GetResult()
            let newOracles = state.KnownOracles |> Deferred.map(Result.map((fun o -> o.Remove(oracle))))
            { state with KnownOracles = newOracles }, Cmd.none
        | Select o ->
            match o with
            | None -> state, Cmd.none
            | Some o -> 
                let (eState, cmd) = EventModule.init (o.Name)
                {state with Selected = Some(o, eState); }, (cmd |> Cmd.map(EventMsg))
        | ToggleOracleImport ->
            match state.OracleInImport with
            | None ->
                let s = OracleInImportModule.init
                { state with OracleInImport = s |> Some }, Cmd.none
            | Some _ ->
                { state with OracleInImport = None }, Cmd.none
        | NewOracle oracle ->
            let saveOracle () =
                task {
                    let repo = ConfigUtils.repository globalConfig
                    let nameRepo = ConfigUtils.nameRepo globalConfig
                    do! nameRepo.SetMapping(Scopes.Oracles, oracle.Name, oracle.OracleId.ToString())
                    let! _ = repo.AddOracle(oracle.OracleId.PubKey, oracle.KeyPath |> Option.toObj)
                    return ()
                }
            let newOracles = state.KnownOracles |> Deferred.map(Result.map(fun x -> x.AddOrReplace(oracle)))
            { state with KnownOracles = newOracles}, Cmd.OfTask.attempt saveOracle () (fun x -> InvalidOracle(x.Message))
        | SetOracleNameStarted (name, oInfo) ->
            let oId = oInfo.OracleId
            let set() = task {
                let nameRepo = ConfigUtils.nameRepo globalConfig
                let! _ = nameRepo.RemoveMapping(Scopes.Oracles, oInfo.Name)
                do! nameRepo.SetMapping(Scopes.Oracles, name, oId.ToString())
                return { oInfo with Name = name}
            }
            let os =  state.KnownOracles |> Deferred.map(Result.map(fun x -> x.Remove(oId)))
            { state with KnownOracles = os }, Cmd.OfTask.either set () (SetOracleNameFinished)  (fun x -> (InvalidOracle(sprintf "Failed to SetOracle Name to %A \n%s" name (x.ToString()))))
        | SetOracleNameFinished o ->
            
            let os =  state.KnownOracles |> Deferred.map(Result.map(fun x -> x.AddOrReplace(o)))
            { state with KnownOracles = os }, Cmd.none
        | InvalidOracle msg ->
            { state with InvalidOracleErrorMsg = Some msg }, Cmd.none
        | Generate oracleName ->
            let generate() =
                task {
                    let! o = (CommandBase.tryGetOracle globalConfig (oracleName))
                    match o with
                    | Some _ -> return (InvalidOracle "Oracle with the same name Already exists!")
                    | None ->
                    let repo = ConfigUtils.repository globalConfig
                    let! (keyPath, key) = repo.CreatePrivateKey()
                    let pubkey,_ = key.PubKey.ToECPubKey().ToXOnlyPubKey()
                    let nameRepo = ConfigUtils.nameRepo globalConfig
                    do! nameRepo.SetMapping(Scopes.Oracles, oracleName, Encoders.Hex.EncodeData(pubkey.ToBytes()))
                    let pubkeyHex = Encoders.Hex.EncodeData(pubkey.ToBytes())
                    let! _ = repo.AddOracle(pubkey, keyPath)
                    match OracleId.TryParse pubkeyHex with
                    | true, oracleId ->
                        return
                            { OracleId = oracleId; Name = oracleName; KeyPath = Some(keyPath); }
                            |> NewOracle
                    | false, _ ->
                        return (InvalidOracle "Failed to parse Oracle id!")
                }
            state, Cmd.OfTask.either generate () id (fun ex -> InvalidOracle (ex.ToString()))
        | EventMsg m ->
            match state.Selected with
            | Some (o, eState) ->
                let newState, cmd = EventModule.update globalConfig m (eState)
                { state with Selected = Some (o, newState) }, (cmd |> Cmd.map(EventMsg))
            | None -> state, Cmd.none
        | OracleInImportMsg msg ->
            match state.OracleInImport with
            | None -> state, Cmd.none
            | Some o ->
            let newState, cmd = OracleInImportModule.update msg o
            { state with OracleInImport = newState |> Some }, (cmd |> Cmd.map(OracleInImportMsg))
        
    let private oracleListView (oracles: OracleInfo seq) dispatch =
        ListBox.create [
            ListBox.onSelectedItemChanged(fun obj ->
                match obj with
                | :? OracleInfo as o -> o |> Some
                | _ -> None
                |> Select |> ForSelf |> dispatch
            )
            ListBox.dataItems (oracles)
            ListBox.itemTemplate
                (DataTemplateView<OracleInfo>.create (fun d ->
                DockPanel.create [
                    DockPanel.lastChildFill false
                    DockPanel.children [
                        TextBox.create [
                            TextBox.text d.Name
                            TextBox.padding 5.
                            yield! TextBox.onTextInputFinished(fun x -> SetOracleNameStarted(x, d) |> ForSelf |> dispatch)
                        ]
                        TextBox.create [
                            TextBox.isEnabled false
                            TextBox.text (d.OracleId.ToString())
                            TextBox.padding 5.
                        ]
                        Button.create [
                            Button.dock Dock.Right
                            Button.content "remove"
                            Button.onClick ((fun _ -> d |> Remove |> ForSelf |> dispatch), SubPatchOptions.OnChangeOf d.OracleId)
                        ]
                    ]
                ]
            ))
        ]
        
    let renderError (errorMsg: string) =
        StackPanel.create [
            StackPanel.children [
                    StackPanel.create [
                    StackPanel.dock Dock.Top
                    StackPanel.verticalAlignment VerticalAlignment.Center
                    StackPanel.children [
                        TextBlock.create [
                            TextBlock.dock Dock.Top
                            TextBlock.classes ["h1"]
                            TextBlock.fontSize 24.
                            TextBlock.fontWeight FontWeight.Thin
                            TextBlock.text errorMsg
                        ]
                    ]
                ]
            ]
        ]
        
    let private viewOracle state dispatch =
        let o = state.KnownOracles
        match o with
        | HasNotStartedYet -> StackPanel.create []
        | InProgress -> Components.spinner
        | Resolved(Ok items) ->
            StackPanel.create [
                StackPanel.orientation Orientation.Vertical
                StackPanel.children [
                    
                    TextBlock.create [
                        TextBlock.dock Dock.Top
                        TextBlock.margin 5.
                        TextBlock.fontSize 18.
                        TextBlock.text (sprintf "List of %d oracles created by myself: (try clicking)" (items.Mine |> Seq.length))
                    ]
                    oracleListView items.Mine dispatch
                    Button.create [
                        Button.dock Dock.Left
                        Button.content "Generate New Oracle"
                        Button.margin 4.
                        Button.fontSize 15.
                        Button.classes ["round"; "add"]
                        let onClick = (fun _ -> dispatch(ForSelf(Generate("PleaseChangeMe"))))
                        Button.onClick(onClick, SubPatchOptions.Always)
                    ]
                    
                    TextBlock.create [
                        TextBlock.dock Dock.Top
                        TextBlock.margin 5.
                        TextBlock.fontSize 18.
                        TextBlock.text (sprintf "List of other %d oracles: (try clicking)" (items.Others |> Seq.length))
                    ]
                    oracleListView items.Others dispatch
                    Button.create [
                        Button.dock Dock.Left
                        Button.content "Import Oracle"
                        Button.margin 4.
                        Button.fontSize 15.
                        Button.classes ["round"; "add"]
                        let onClick = (fun _ -> ToggleOracleImport |> ForSelf |> dispatch)
                        Button.onClick(onClick, SubPatchOptions.Always)
                    ]
                    
                    StackPanel.create [
                        StackPanel.isVisible (state.OracleInImport.IsSome)
                        StackPanel.children [
                            match state.OracleInImport with
                            | Some o -> (OracleInImportModule.view o (oracleInImportTranslator >> dispatch))
                            | _ -> ()
                        ]
                    ]
                    
                    match state.InvalidOracleErrorMsg with
                    | None -> ()
                    | Some x ->
                        TextBlock.create[
                            TextBlock.text x
                            TextBlock.classes ["error"]
                        ]
                ]
            ]
        | Resolved(Error e) ->
            renderError e
    
    let private oracleDetailsAndEventView (state: State) dispatch =
        DockPanel.create [
            DockPanel.isVisible state.Selected.IsSome
            // DockPanel.width 250.
            DockPanel.children [
                match state.Selected with
                | None -> ()
                | Some (_, eState) ->
                    StackPanel.create [
                        StackPanel.dock Dock.Top
                        StackPanel.orientation Orientation.Vertical
                        StackPanel.margin 1.
                        StackPanel.children [
                            TextBlock.create [
                                TextBlock.text "Here goes oracle details"
                            ]
                            EventModule.view eState (eventMsgTranslator >> dispatch)
                        ]
                    ]
            ]
        ]
    let view (state: State) (dispatch: Msg -> unit) =
        DockPanel.create [
            DockPanel.children [
                viewOracle state dispatch
                oracleDetailsAndEventView state dispatch
            ]
        ]
        
