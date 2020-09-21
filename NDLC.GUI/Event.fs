namespace NDLC.GUI

open FSharp.Control.Tasks
open Elmish
open Avalonia.Controls

open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Components

open NBitcoin
open System.Threading.Tasks
open NDLC.Infrastructure
open NDLC.GUI.Utils
open NDLC.Messages
open NDLC.Secp256k1

[<RequireQualifiedAccess>]
module EventModule =
    type EventInfo = {
        OracleName: string
        EventName: string
        Nonce: SchnorrNonce option
        Outcomes: string seq
        CanReveal: bool
        NonceKeyPath: RootedKeyPath option
    }
    with
        static member Empty = {
            OracleName = ""
            EventName = ""
            Nonce = None
            Outcomes = []
            CanReveal = false
            NonceKeyPath = None
        }
        member this.FullName =
            sprintf "%s/%s" this.OracleName this.EventName
        
    type Msg =
        | LoadEvents of AsyncOperationStatus<Result<EventInfo list, string>>
        | ToggleEventImport
        | Generate of string
        | NewEvent of EventInfo
        | Select of EventInfo option
        | DLCMsg of DLCModule.Msg
        
    type State = {
        CreatorName: string
        EventInCreation: EventInfo option
        KnownEvents: Deferred<EventInfo list>
        LoadFailed: string option
        Selected: (EventInfo  * DLCModule.State) option
    }
    let init oracleId =
        { KnownEvents = Deferred.HasNotStartedYet; EventInCreation = None; CreatorName = oracleId; LoadFailed = None
          Selected = None; },
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
        
    let update globalConfig (msg: Msg) (state: State) =
        match msg with
        | LoadEvents Started ->
            { state with KnownEvents = InProgress }, Cmd.OfTask.result (loadEventInfos state.CreatorName globalConfig)
        | LoadEvents(Finished (Ok infos)) ->
            { state with KnownEvents = Resolved(infos) }, Cmd.none
        | LoadEvents(Finished (Error e)) ->
            { state with LoadFailed = Some(e) }, Cmd.none
        | ToggleEventImport ->
            { state with EventInCreation = Some(EventInfo.Empty)}, Cmd.none
        | Generate name ->
            let generate() = task {
                return failwith "TODO"
            }
            state, Cmd.OfTask.perform generate () (fun x -> Msg.NewEvent(x))
        | NewEvent e ->
            let newEvents = state.KnownEvents |> Deferred.map(fun x -> e::x)
            { state with KnownEvents = newEvents}, Cmd.none
        | Select e ->
            match e with
            | None -> { state with Selected = None }, Cmd.none
            | Some e -> 
                let (eState, cmd) = DLCModule.init (e.FullName)
                { state with Selected = Some (e, eState) }, (cmd |> Cmd.map(DLCMsg))
        | DLCMsg msg ->
            match state.Selected with
            | Some (o, eState) ->
                let newState, cmd = DLCModule.update msg (eState)
                { state with Selected = Some (o, newState) }, (cmd |> Cmd.map(DLCMsg))
            | None -> state, Cmd.none
        
    let eventListView state dispatch =
        failwith ""
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
                            |> Select |> dispatch
                        )
                        ListBox.dataItems events
                        ListBox.itemTemplate 
                            (DataTemplateView<EventInfo>.create (fun d ->
                            DockPanel.create [
                                DockPanel.lastChildFill false
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
                    Components.importAndGenerateButton
                        (fun _ -> dispatch ToggleEventImport)
                        (fun _ -> dispatch (Generate "MyNewEvent"))
                    match state.Selected with
                    | Some (e, dlcState) ->
                        DLCModule.view dlcState (DLCMsg >> dispatch)
                    | None -> ()
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