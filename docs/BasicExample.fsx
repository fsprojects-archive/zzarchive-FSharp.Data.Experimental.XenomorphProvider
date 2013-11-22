(** 
# FSharp.Data.Experimental.XenomorphProvider

*)
#r @"C:\Users\administrator.CORP\Documents\Xenomorph TimeScape\APIs, SDKs, Examples\APIs\v4.0 .NET API\Assemblies\Xenomorph.TimeScape.Client.dll"
#r @"C:\Users\administrator.CORP\Documents\Xenomorph TimeScape\APIs, SDKs, Examples\APIs\v4.0 .NET API\Assemblies\Xenomorph.Generic.dll"


#r "../src/FSharp.Data.Experimental.XenomorphProvider/bin/Debug/FSharp.Data.Experimental.XenomorphProvider.dll"
open FSharp.Data.Experimental
open System.Linq


type T = XenomorphProvider<Server = "XENO", UseRefinedTypes=false>

let data = T.GetDataContext()

// TODO: Reaction

for x in data.Categories.Collection do 
   printfn "%A" (x.CategoryName, try x.Collection.Count() with _ -> 0)

data.Categories
data.Categories.``Inflation Expectation Rate``.`` (GBR_122M_IE, User)``
data.Categories.``Inflation Expectation Rate``.Collection


for x in data.Categories.``GB Equity``.Collection do 
   printfn "%A" (x.Item.Code)

data.Categories.``GB Equity``.``SAINSBURY(J) (SBRY.L, Reuters)``.Close

data.Categories.``GB Equity``.``SAINSBURY(J) (SBRY.L, Reuters)``.Close

//data.Future.ItemsByName.

data.Categories.``GB Equity``.``3i GROUP (III.L, Reuters)``.``Amount Issued``
data.Categories.``GB Equity``.``3i GROUP (III.L, Reuters)``.Close

data.Categories.Equity.``Amalgamated Oil Company (RIC31239, Reuters)``.Item
data.Categories.Equity.``Amalgamated Oil Company (RIC31239, Reuters)``.Item.Category
data.Categories.Equity.``Amalgamated Oil Company (RIC31239, Reuters)``.``Amount Issued`` // TODO - convert exception to None
data.Categories.Equity.``Amalgamated Oil Company (RIC31239, Reuters)``.Close
data.Categories.Equity.``Amalgamated Oil Company (RIC31239, Reuters)``.``Close-Previous``
data.Categories.Equity.``Amalgamated Oil Company (RIC31239, Reuters)``.``Company Website``
data.Categories.Equity.``Amalgamated Oil Company (RIC31239, Reuters)``.Currency
data.Categories.Equity.``Amalgamated Oil Company (RIC31239, Reuters)``.IsExDividend
data.Categories.Equity.``Amalgamated Oil Company (RIC31239, Reuters)``.IssueDate
data.Categories.Equity.``Amalgamated Oil Company (RIC31239, Reuters)``.MktComment
data.Categories.Equity.``Amalgamated Oil Company (RIC31239, Reuters)``.NumShares
data.Categories.Equity.``Amalgamated Oil Company (RIC31239, Reuters)``.ParentCompany 
data.Categories.Equity.``Amalgamated Oil Company (RIC31239, Reuters)``.PriceOfferClose
data.Categories.Equity.``Amalgamated Oil Company (RIC31239, Reuters)``.PriceOfferHigh
data.Categories.Equity.``Amalgamated Oil Company (RIC31239, Reuters)``.PriceOfferLow
data.Categories.Equity.``Amalgamated Oil Company (RIC31239, Reuters)``.PriceOfferOpen
data.Categories.Equity.``Amalgamated Oil Company (RIC31239, Reuters)``.RiskReport
data.Categories.Equity.``Amalgamated Oil Company (RIC31239, Reuters)``.``Settlement Delay``
data.Categories.Equity.``Amalgamated Oil Company (RIC31239, Reuters)``.VolSurf
data.Categories.Equity.``Amalgamated Oil Company (RIC31239, Reuters)``.Volume
data.Categories.Equity.``Amalgamated Oil Company (RIC31239, Reuters)``.X_Dividends  
data.Categories.Equity.``Amalgamated Oil Company (RIC31239, Reuters)``.``Listed Warrants``  // TODO: no data found needs to become option

data.Categories.Equity.``Amalgamated Oil Company (RIC31239, Reuters)``
