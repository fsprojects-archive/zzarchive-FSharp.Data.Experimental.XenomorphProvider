(** 
# FSharp.Data.Experimental.XenomorphProvider

This shows a basic example of using the type provider with the XENO database in the Azure SQLLABS cluster.

## Referencing the type provider:
*)
#I "../../bin"
#r "Xenomorph.TimeScape.Client.dll"
#r "Xenomorph.Generic.dll"
#r "FSharp.Data.Experimental.XenomorphProvider.dll"

open FSharp.Data.Experimental
open System.Linq

(** 

## Referencing the Xenomorph TimeScape database:
*)

type T = XenomorphProvider<Server = "XENO", UseRefinedTypes=false>

let data = T.GetDataContext()


(** 

Looking through the sizes of the available collections:
*)

for x in data.Categories.Collection do 
   printfn "%A" (x.CategoryName, try x.Collection.Count() with _ -> 0)

(** 

Looking through the categories:
*)

data.Categories
data.Categories.``GB Equity``


for x in data.Categories.``GB Equity``.Collection do 
   printfn "%A" (x.Item.Code)


(** 

Looking at a particular equity:
*)
data.Categories.``GB Equity``.``SAINSBURY(J) (SBRY.L, Reuters)``.Close


data.Categories.``GB Equity``.``3i GROUP (III.L, Reuters)``.``Amount Issued``
data.Categories.``GB Equity``.``3i GROUP (III.L, Reuters)``.Close


(** 

Looking at a particular properties of a particular equity:
*)
data.Categories.Equity.``Amalgamated Oil Company (RIC31239, Reuters)``.Item
data.Categories.Equity.``Amalgamated Oil Company (RIC31239, Reuters)``.Item.Category
data.Categories.Equity.``Amalgamated Oil Company (RIC31239, Reuters)``.``Amount Issued`` 
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
data.Categories.Equity.``Amalgamated Oil Company (RIC31239, Reuters)``.``Listed Warrants``  

data.Categories.Equity.``Amalgamated Oil Company (RIC31239, Reuters)``
