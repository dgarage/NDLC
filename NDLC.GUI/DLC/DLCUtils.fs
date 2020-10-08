module NDLC.GUI.DLC.DLCUtils

open NBitcoin
open NDLC.Infrastructure

let assertState (currentState: Repository.DLCState, expectedOfferer: bool, expectedState: Repository.DLCState.DLCNextStep, network: Network) = 
    if (currentState.BuilderState |> isNull) then
        Error ("The DLC is in an invalid state for this action")
    else
    let isOfferer = currentState.GetBuilder(network).State.IsInitiator;
    if (isOfferer && not <| expectedOfferer) then
        Error("This action must be run by the acceptor, but you are the offerer of the DLC");
    else if (not <| isOfferer && expectedOfferer) then
        Error("This action must be run by the offerer, but you are the acceptor of the DLC")
    else
    let actualStep = currentState.GetNextStep(network);
    if (actualStep <> expectedState) then
        Error (sprintf "The DLC is in an invalid state for this action. The expected state is '{%A}' but your state is '{%A}'." expectedState actualStep)
    else
        Ok()

