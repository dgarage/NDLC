module NDLC.GUI.Oracle.DomainModel

open NBitcoin
open NBitcoin.Secp256k1
open NDLC.Infrastructure

type OracleInfo = {
    Name: string
    OracleId: OracleId
    KeyPath: RootedKeyPath option
}
    

