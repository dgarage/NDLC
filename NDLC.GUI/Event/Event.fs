namespace NDLC.GUI

open System.Linq
open System.Threading.Tasks

open Avalonia.Controls

open FSharp.Control.Tasks
open Elmish
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Components

open NBitcoin

open NDLC.Infrastructure
open NDLC.GUI.Utils
open NDLC.Messages
open NDLC.Secp256k1

open System
open Avalonia
open NDLC
open System.Diagnostics
open NDLC.GUI.GlobalMsgs
open NDLC.GUI.Event

[<RequireQualifiedAccess>]
module EventModule =
    type EventInfo = {
        OracleName: string
        EventName: string
        Nonce: SchnorrNonce option
        Outcomes: string []
        CanReveal: bool
        NonceKeyPath: RootedKeyPath option
    }
    with
        static member Empty = {
            OracleName = ""
            EventName = ""
            Nonce = None
            Outcomes = [||]
            CanReveal = false
            NonceKeyPath = None
        }
        member this.FullName =
            sprintf "%s/%s" this.OracleName this.EventName
            
        member this.FullNameObject =
            EventFullName(this.OracleName, this.EventName)
        
        static member Create(e: Repository.Event, fullName: EventFullName) =
            {
                Nonce = e.EventId.RValue |> Some
                Outcomes = e.Outcomes
                OracleName = fullName.OracleName
                EventName = fullName.Name
                CanReveal = e.NonceKeyPath |> isNull |> not
                NonceKeyPath =  e.NonceKeyPath |> Option.ofObj
            }
         
        static member FromEventImport (e: EventInImportModule.EventImportArg) = {
            Nonce = e.Nonce
            OracleName = e._OracleName
            EventName = e.EventName
            Outcomes = e.Outcomes
            CanReveal = false
            NonceKeyPath = None
        }
        
    type InternalMsg =
        | LoadEvents of AsyncOperationStatus<Result<EventInfo list, string>>
        | EventInImportMsg of EventInImportModule.InternalMsg
        | EventInGenerationMsg of EventInGenerationModule.InternalMsg
        | Generate of EventInGenerationModule.EventGenerationArg
        | NewEvent of EventInfo
        | NewEventSaved of EventInfo
        | AttestEvent of info: EventInfo * outcomeName: string
        | SetAttestation of key: Key * outcome: string
        | CopyToClipBoard of string
        | Select of EventInfo option
        | InvalidInput of msg: string
        | Sequence of InternalMsg seq
        | NoOp
        
    type OutMsg =
        | NewOffer of NewOfferMetadata
    type Msg =
        | ForSelf of InternalMsg
        | ForParent of OutMsg
        
    type TranslationDictionary<'Msg> = {
        OnInternalMsg: InternalMsg -> 'Msg
        OnNewOffer: NewOfferMetadata -> 'Msg
    }
    
    type Translator<'Msg> = Msg -> 'Msg
    
    let translator ({ OnInternalMsg = onInternalMsg; OnNewOffer = onNewOffer }: TranslationDictionary<'Msg>): Translator<'Msg> =
        function
        | ForSelf i -> onInternalMsg i
        | ForParent (NewOffer x) -> onNewOffer x
    type State = {
        CreatorName: string
        KnownEvents: Deferred<EventInfo list>
        LoadFailed: string option
        Selected: (EventInfo) option
        Attestation: (Key * string) option
        EventInImport: EventInImportModule.State
        EventInGeneration: EventInGenerationModule.State
        ErrorMsg: string option
    }
    
    let eventInGenerationTranslator = EventInGenerationModule.translator { OnInternalMsg = EventInGenerationMsg
                                                                           OnGenerate = Generate }
    let eventInImportTranslator = EventInImportModule.translator { OnInternalMsg = EventInImportMsg
                                                                   OnNewEvent = EventInfo.FromEventImport >> NewEvent }
    let init oracleId =
        { KnownEvents = Deferred.HasNotStartedYet
          EventInGeneration = EventInGenerationModule.init
          EventInImport = EventInImportModule.init
          CreatorName = oracleId; LoadFailed = None
          Attestation = None
          ErrorMsg = None
          Selected = None;},
        Cmd.ofMsg (LoadEvents AsyncOperationStatus.Started)
        
    let private loadEventInfos (oracleName) (globalConfig) =
        task {
            let nameRepo =  (ConfigUtils.nameRepo globalConfig)
            let repo = ConfigUtils.repository globalConfig
            let eventRepo = nameRepo.AsEventRepository()
            let! eventNames = eventRepo.ListEvents(oracleName)
            let! oracleInfos = eventNames |> Seq.map eventRepo.GetEventId |> Task.WhenAll
            let! events = oracleInfos |> Seq.map repo.GetEvent |> Task.WhenAll
            let eventsWithId =
                Seq.zip3
                    (eventNames)
                    (oracleInfos |> Seq.map(fun x -> if x |> isNull then None else Some(x.RValue)))
                    (events |> Seq.map(fun x -> if x |> isNull then None else Some(x)))
            return eventsWithId
                |> Seq.map(fun (fullName, eId, e) ->
                    { EventInfo.OracleName = fullName.OracleName
                      EventName = fullName.Name
                      Nonce = eId
                      Outcomes = e |> function Some x -> x.Outcomes | None -> [||]
                      CanReveal = e |> function Some x -> x.NonceKeyPath |> isNull |> not | None -> false
                      NonceKeyPath = e |> Option.bind(fun x -> Option.ofObj x.NonceKeyPath)
                      }
                    )
                |> Seq.sortBy(fun o -> o.ToString())
                |> Seq.toList
                |> Ok
                |> Finished
                |> LoadEvents
        }
        
    let tryGetEvent (globalConfig) (fullName: EventFullName) = task {
        let nameRepo = ConfigUtils.nameRepo globalConfig
        let! e = nameRepo.AsEventRepository().GetEventId(fullName)
        if e |> isNull then return None else
        let repo = ConfigUtils.repository globalConfig
        let! e = repo.GetEvent(e)
        if (e |> isNull) then return None else
        return Some(e)
    }
    
    let rec update globalConfig (msg: InternalMsg) (state: State) =
        match msg with
        | LoadEvents Started ->
            { state with KnownEvents = InProgress }, Cmd.OfTask.result (loadEventInfos state.CreatorName globalConfig)
        | LoadEvents(Finished (Ok infos)) ->
            { state with KnownEvents = Resolved(infos) }, Cmd.none
        | LoadEvents(Finished (Error e)) ->
            { state with LoadFailed = Some(e) }, Cmd.none
        | Generate (arg) ->
            let (name, outcomes) = arg.Name, arg.Outcomes
            let fullname = EventFullName(state.CreatorName, name)
            let generate() = task {
                let! e = tryGetEvent globalConfig fullname
                match e with
                | Some _ -> return EventInGenerationModule.InvalidInput (sprintf "Event with the name %s already exists" (name)) |> EventInGenerationMsg
                | None ->
                    let! o  =
                        CommandBase.getOracle globalConfig (state.CreatorName)
                    if o.RootedKeyPath |> isNull then
                        return (sprintf "You do not own the key for an oracle (%s)" state.CreatorName) |> EventInGenerationModule.InvalidInput |> EventInGenerationMsg else
                    let repo = ConfigUtils.repository globalConfig
                    let! (path, key) = repo.CreatePrivateKey()
                    let nonce = key.ToECPrivKey().CreateSchnorrNonce();
                    let evtId = OracleInfo(o.PubKey, nonce)
                    match! repo.AddEvent(evtId, outcomes, path) with
                    | false ->
                        return (sprintf "Event with the name \"%s\" already exists" name) |> EventInGenerationModule.InvalidInput |> EventInGenerationMsg
                    | true ->
                        let nameRepo = ConfigUtils.nameRepo globalConfig
                        do! nameRepo.AsEventRepository().SetMapping(evtId, name)
                        let eventInfo =
                            { EventInfo.Nonce = nonce |> Some
                              OracleName = state.CreatorName
                              EventName = name
                              Outcomes = outcomes
                              CanReveal = o.RootedKeyPath |> isNull |> not
                              NonceKeyPath = o.RootedKeyPath |> Option.ofObj
                                 }
                        return NewEvent(eventInfo)
            }
            state, Cmd.OfTask.either generate ()
                       (fun x -> Sequence([x; EventInGenerationModule.Reset |> EventInGenerationMsg;]))
                       (fun e -> (e.Message) |> EventInGenerationModule.InvalidInput |> EventInGenerationMsg)
        | EventInGenerationMsg (msg) ->
            let neweventInGenerationState = EventInGenerationModule.update msg state.EventInGeneration
            { state with EventInGeneration = neweventInGenerationState }, Cmd.none
        | EventInImportMsg (msg) ->
            let newEventInImport = EventInImportModule.update msg state.EventInImport
            { state with EventInImport = newEventInImport }, Cmd.none
        | NewEvent e ->
            let e = { e with OracleName = state.CreatorName }
            let saveEvent () = task {
                let! existingE = CommandBase.tryGetEvent(globalConfig) e.FullNameObject
                match existingE with
                | Some _ ->
                    return ()
                | None ->
                    let! oracle = CommandBase.getOracle globalConfig (state.CreatorName)
                    let repo = ConfigUtils.repository globalConfig
                    Debug.Assert(e.Nonce.Value |> isNull |> not)
                    let evtId = OracleInfo(oracle.PubKey, e.Nonce.Value)
                    match! repo.AddEvent(evtId, e.Outcomes) with
                    | false ->
                        failwithf "An event with the same nonce already exists!"
                    | true ->
                    let nameRepo = ConfigUtils.nameRepo globalConfig
                    do! nameRepo.AsEventRepository().SetMapping(evtId, e.EventName)
            }
            state, Cmd.OfTask.either saveEvent () (fun _ -> NewEventSaved(e)) (fun e -> InvalidInput(e.Message))
        | NewEventSaved e ->
            let newEvents = state.KnownEvents |> Deferred.map(fun x -> e::x)
            { state with KnownEvents = newEvents; ErrorMsg = None}, Cmd.batch[Cmd.ofMsg(EventInImportMsg (EventInImportModule.Reset)); Cmd.ofMsg(EventInGenerationMsg (EventInGenerationModule.Reset))]
        | AttestEvent (e, outcome) ->
            let attest (g: GlobalConfig) (e: EventInfo) = task {
                let nRepo = ConfigUtils.nameRepo g
                let repo = ConfigUtils.repository g
                let! evtId = nRepo.AsEventRepository().GetEventId(e.FullNameObject)
                if (isNull evtId) then return failwithf "Event not found for event %s" e.FullName else
                let! oracle = CommandBase.getOracle g (e.OracleName)
                let mutable dOutcome = DiscreteOutcome(outcome.Trim())
                let! eventObj = CommandBase.getEvent g e.FullNameObject
                Debug.Assert(eventObj.NonceKeyPath |> isNull |> not, "Event attestation should be only possible for our own event")
                let maybeOutcome = eventObj.Outcomes.FirstOrDefault(fun o -> o.Equals(outcome, StringComparison.OrdinalIgnoreCase))
                Debug.Assert(maybeOutcome |> isNull |> not, (sprintf "should never dispatch AttestEvent for impossible outcome %s" outcome))
                Debug.Assert(oracle.RootedKeyPath |> isNull |> not, "RootedKeyPath should never be null")
                let! key = repo.GetKey(oracle.RootedKeyPath)
                if (eventObj.Attestations |> isNull |> not && eventObj.Attestations.ContainsKey(outcome)) then
                    return failwith "This outcome has already been attested" else
                if (eventObj.Attestations |> isNull |> not && eventObj.Attestations.Count > 0) then
                    return failwith "One of outcomes for this event has already been attested" else
                let! kValue = repo.GetKey(eventObj.NonceKeyPath)
                match key.ToECPrivKey().TrySignBIP140(ReadOnlySpan(dOutcome.Hash), PrecomputedNonceFunctionHardened(kValue.ToECPrivKey().ToBytes())) with
                | false, _ ->
                    return failwith "Failed to sign bip140! This should never happen"
                | true, schnorrSig ->
                    let oracleAttestation = new Key(schnorrSig.s.ToBytes());
                    match! repo.AddAttestation(evtId, oracleAttestation) with
                    | x when x <> dOutcome ->
                        return failwith "Error while validating reveal"
                    | _ ->
                        return (oracleAttestation, outcome)
            }
            let onSuccess = SetAttestation
            let onError (e: exn) = InvalidInput(e.Message)
            state, Cmd.OfTask.either (attest globalConfig) (e) onSuccess onError
        | SetAttestation (attestationKey, outcome) ->
            { state with Attestation = Some(attestationKey, outcome); ErrorMsg = None }, Cmd.none
        | Select e ->
            match e with
            | None -> { state with Selected = None }, Cmd.none
            | Some e -> 
                { state with Selected = Some (e) }, Cmd.none
        | InvalidInput msg ->
            { state with ErrorMsg = Some(msg) }, Cmd.none
        | Sequence msgs ->
           let folder (s, c) msg =
                let s', cmd = update globalConfig msg s
                s', cmd :: c
           let newState, cmdList = msgs |> Seq.fold(folder) (state, [])
           newState, (cmdList |> Cmd.batch)
        | CopyToClipBoard txt ->
            let copy (str) = task {
                do! Application.Current.Clipboard.SetTextAsync str
                return NoOp
            }
            state, Cmd.OfTask.result (copy txt)
        | NoOp -> state, Cmd.none
            
                
    let view (amIOracle: bool) (state: State) dispatch =
        match state.KnownEvents with
        | Resolved events ->
            StackPanel.create [
                StackPanel.children [
                    TextBlock.create [
                        TextBlock.top 10.
                        TextBlock.dock Dock.Top
                        TextBlock.margin 5.
                        TextBlock.fontSize 14.
                        TextBlock.text (sprintf "List of %i known events: (try right-clicking)" (events |> Seq.length))
                    ]
                    ListBox.create [
                        ListBox.onSelectedItemChanged(fun obj ->
                            match obj with
                            | :? EventInfo as o -> o |> Some
                            | _ -> None
                            |> Select |> ForSelf |> dispatch
                        )
                        ListBox.dataItems events
                        ListBox.itemTemplate 
                            (DataTemplateView<EventInfo>.create (fun d ->
                            DockPanel.create [
                                DockPanel.lastChildFill false
                                DockPanel.contextMenu (ContextMenu.create [
                                    ContextMenu.viewItems [
                                        MenuItem.create [
                                            MenuItem.header "Copy Nonce"
                                            MenuItem.onClick (fun _ -> match d.Nonce with None -> () | Some n -> CopyToClipBoard(n.ToString()) |> ForSelf |> dispatch)
                                        ]
                                        MenuItem.create [
                                            MenuItem.header "Create New Offer"
                                            MenuItem.onClick(fun _ ->
                                                { NewOfferMetadata.EventFullName = (d.FullNameObject); Outcomes = d.Outcomes }
                                                |> NewOffer
                                                |> ForParent
                                                |> dispatch
                                                )
                                        ]
                                        if (amIOracle) then
                                            MenuItem.create [
                                                MenuItem.header "Attest Event"
                                                MenuItem.viewItems
                                                    [ for outcome in d.Outcomes ->
                                                        MenuItem.create [
                                                           MenuItem.header outcome
                                                           MenuItem.onClick(fun _ -> AttestEvent(d, outcome) |> ForSelf |> dispatch)
                                                        ]
                                                    ]
                                            ]
                                        else
                                            ()
                                    ]
                                ])
                                DockPanel.children [
                                    TextBlock.create [
                                        TextBlock.text d.FullName
                                        TextBlock.margin 3.
                                    ]
                                    TextBlock.create [
                                        TextBlock.margin 3.
                                        TextBlock.text (d.Nonce.Value.ToString())
                                    ]
                                    ComboBox.create [
                                        ComboBox.selectedIndex 0
                                        ComboBox.name "Possible outcomes"
                                        ComboBox.viewItems [
                                            for i in d.Outcomes ->
                                                ComboBoxItem.create [
                                                    ComboBoxItem.margin 3.
                                                    ComboBoxItem.content i
                                                ]
                                        ]
                                    ]
                                ]
                            ]
                        ))
                    ]
                    
                    match (state.Attestation) with
                    | Some (key, outcome) ->
                        TextBlock.create [
                            TextBlock.margin 5.
                            TextBlock.fontSize 14.
                            TextBlock.text (sprintf "Result of the Attestation for outcome \"%s\"" outcome)
                        ]
                        TextBlock.create [
                            let keyHex = key.ToHex()
                            TextBlock.margin 5.
                            TextBlock.text (keyHex)
                            TextBlock.contextMenu(ContextMenu.create [
                                ContextMenu.viewItems [
                                    MenuItem.create [
                                        MenuItem.header "Copy Attestation"
                                        MenuItem.onClick(fun _ -> CopyToClipBoard (keyHex) |> ForSelf |> dispatch)
                                    ]
                                ]
                            ])
                        ]
                    | None -> ()
                    
                    if (amIOracle) then
                        TextBlock.create [
                            TextBlock.margin 5.
                            TextBlock.fontSize 18.
                            TextBlock.text "Fill in the following form to generate a new event"
                        ]
                        (EventInGenerationModule.view state.EventInGeneration (eventInGenerationTranslator >> ForSelf >> dispatch))
                    else
                        TextBlock.create [
                            TextBlock.margin 5.
                            TextBlock.fontSize 18.
                            TextBlock.text "Fill in the following form to import an event"
                        ]
                        EventInImportModule.view state.EventInImport (eventInImportTranslator >> ForSelf >> dispatch)
                    match state.ErrorMsg with
                    | Some s ->
                        StackPanel.create [
                            StackPanel.children [
                                TextBlock.create [
                                    TextBlock.classes ["error"]
                                    TextBlock.text s
                                ]
                            ]
                        ]
                    | None  -> ()
                ]
            ]

        | InProgress -> Components.spinner
        | HasNotStartedYet -> StackPanel.create [
            StackPanel.children [
                TextBlock.create [
                    TextBlock.margin 5.
                    TextBlock.fontSize 18.
                    TextBlock.text "Event not loaded"
                ]
            ]
        ]