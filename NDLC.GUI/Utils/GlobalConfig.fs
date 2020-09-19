namespace NDLC.GUI.Utils

open System.IO
open NBitcoin
open NDLC.Infrastructure
open Newtonsoft.Json

type GlobalConfig = {
    Network: Network
    DataDir: string option
}
with
    static member  Default = {
        Network = Network.Main
        DataDir = None
    }


module ConfigUtils =
    let jsonSerializerSettings (c) =
        let j = JsonSerializerSettings()
        j.Formatting <- Formatting.Indented
        NDLC.Messages.Serializer.Configure(j, c.Network)
        j
    
    let repository (c: GlobalConfig) = Repository(c.DataDir |> Option.toObj, c.Network)
    let nameRepo (c: GlobalConfig) =
        ((repository c).RepositoryDirectory, "names.json")
        |> Path.Combine
        |> NameRepository
        
    ()