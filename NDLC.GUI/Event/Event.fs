namespace NDLC.GUI

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
            OracleName = e.OracleName
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
        | Select of EventInfo option
        
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
        EventInImport: EventInImportModule.State
        EventInGeneration: EventInGenerationModule.State
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
    
    let update globalConfig (msg: InternalMsg) (state: State) =
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
                        ConfigUtils.getOracle globalConfig (state.CreatorName)
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
            state, Cmd.OfTask.either generate () (fun x -> x) (fun e -> (e.ToString()) |> EventInGenerationModule.InvalidInput |> EventInGenerationMsg)
        | EventInGenerationMsg (msg) ->
            let neweventInGenerationState = EventInGenerationModule.update msg state.EventInGeneration
            { state with EventInGeneration = neweventInGenerationState }, Cmd.none
        | EventInImportMsg (msg) ->
            let newEventInImport = EventInImportModule.update msg state.EventInImport
            { state with EventInImport = newEventInImport }, Cmd.none
        | NewEvent e ->
            let newEvents = state.KnownEvents |> Deferred.map(fun x -> e::x)
            { state with KnownEvents = newEvents}, Cmd.none
        | Select e ->
            match e with
            | None -> { state with Selected = None }, Cmd.none
            | Some e -> 
                { state with Selected = Some (e) }, Cmd.none
                
    let view (state: State) dispatch =
        match state.KnownEvents with
        | Resolved events ->
            StackPanel.create [
                StackPanel.children [
                    TextBlock.create [
                        TextBlock.top 10.
                        TextBlock.dock Dock.Top
                        TextBlock.margin 5.
                        TextBlock.fontSize 14.
                        TextBlock.text (sprintf "List of known events: (count: %i)" (events |> Seq.length))
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
                                            MenuItem.header "Create New Offer"
                                            MenuItem.onClick(fun _ ->
                                                { NewOfferMetadata.EventFullName = (state.Selected.Value |> fun x -> x.FullNameObject) }
                                                |> NewOffer
                                                |> ForParent
                                                |> dispatch
                                                )
                                        ]
                                        MenuItem.create [
                                            MenuItem.header "Attest Event"
                                            MenuItem.onClick(fun _ -> failwith "TODO: dispatch")
                                        ]
                                        MenuItem.create [
                                            MenuItem.header "Remove this Event"
                                            MenuItem.onClick(fun _ -> failwith "TODO: dispatch")
                                        ]
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
                    
                    TabControl.create [
                        TabControl.tabStripPlacement Dock.Top
                        TabControl.viewItems [
                            TabItem.create [
                                TabItem.classes ["sub-tubitem"; "import"]
                                TabItem.header "Import"
                                TabItem.content (EventInImportModule.view state.EventInImport (eventInImportTranslator >> ForSelf >> dispatch))
                            ]
                            TabItem.create [
                                TabItem.classes ["sub-tubitem"; "generate"]
                                TabItem.header "Generate"
                                TabItem.content (EventInGenerationModule.view state.EventInGeneration (eventInGenerationTranslator >> ForSelf >> dispatch))
                            ]
                        ]
                    ]
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