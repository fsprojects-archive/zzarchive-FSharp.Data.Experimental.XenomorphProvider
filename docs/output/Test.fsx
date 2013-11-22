(**
    Use cases
*)
#r "../src/XenomorphProvider/bin/Debug/XenomorphProvider.dll"

open FSharp.Data.SqlClient
open System.Data
open System

[<Literal>]
let connectionString = """Data Source=(LocalDb)\v11.0;Initial Catalog=AdventureWorks2012;Integrated Security=True"""

[<Literal>]
let queryProductsSql = " 
SELECT TOP (@top) Name AS ProductName, SellStartDate, Size
FROM Production.Product 
WHERE SellStartDate > @SellStartDate
"

//Tuples
type QueryProductsAsTuples = SqlCommand<queryProductsSql, connectionString>
let cmd = QueryProductsAsTuples(top = 7L, SellStartDate = System.DateTime.Parse "2002-06-01")
let result : Async<(string * DateTime * option<string>) seq> = cmd.AsyncExecute()
result |> Async.RunSynchronously |> Seq.iter (fun(productName, sellStartDate, size) -> printfn "Product name: %s. Sells start date %A, size: %A" productName sellStartDate size)
cmd.Execute() |> Seq.iter (fun(productName, sellStartDate, size) -> printfn "Product name: %s. Sells start date %A, size: %A" productName sellStartDate size)

//Command from file
type q = SqlCommand<"sampleCommand.sql", connectionString>
let cmdFromFile = q()
cmdFromFile.Execute() |> ignore

//Custom record types and connection string override
type QueryProducts = SqlCommand<queryProductsSql, connectionString, ResultType = ResultType.Records>
let cmd1 = QueryProducts(connectionString = "Data Source=(local);Initial Catalog=AdventureWorks2012;Integrated Security=True", top = 7L, SellStartDate = System.DateTime.Parse "2002-06-01")
let result1 : Async<QueryProducts.Record seq> = cmd1.AsyncExecute()
result1 |> Async.RunSynchronously |> Seq.iter (fun x -> printfn "Product name: %s. Sells start date %A, size: %A" x.ProductName x.SellStartDate x.Size)
cmd1.Execute() |> Seq.iter (fun x -> printfn "Product name: %s. Sells start date %A, size: %A" x.ProductName x.SellStartDate x.Size)


//DataTable for data binding scenarios and update
type QueryProductDataTable = SqlCommand<queryProductsSql, connectionString, ResultType = ResultType.DataTable>
let cmd2 = QueryProductDataTable(top = 7L, SellStartDate = System.DateTime.Parse "2002-06-01")
let result2 : Async<DataTable<QueryProductDataTable.Row>> = cmd2.AsyncExecute() 
result2 |> Async.RunSynchronously  |> Seq.iter (fun row -> printfn "Product name: %s. Sells start date %O, size: %A" row.ProductName row.SellStartDate row.Size)
cmd2.Execute() |> Seq.iter (fun row -> printfn "Product name: %s. Sells start date %O, size: %A" row.ProductName row.SellStartDate row.Size)

//Single row hint and optional output columns. Records result type.
type QueryPersonInfoSingletone = SqlCommand<"SELECT * FROM dbo.ufnGetContactInformation(@PersonId)", connectionString, ResultType = ResultType.Records, SingleRow=true>
let cmd3 = new QueryPersonInfoSingletone(PersonId = 2)
let result3 : Async<QueryPersonInfoSingletone.Record> = cmd3.AsyncExecute() 
result3 |> Async.RunSynchronously |> fun x -> printfn "Person info: Id - %i, FirstName - %O, LastName - %O, JobTitle - %O, BusinessEntityType - %O" x.PersonID x.FirstName x.LastName x.JobTitle x.BusinessEntityType
cmd3.Execute() |> fun x -> printfn "Person info: Id - %i, FirstName - %O, LastName - %O, JobTitle - %O, BusinessEntityType - %O" x.PersonID x.FirstName x.LastName x.JobTitle x.BusinessEntityType

//Single row hint and optional output columns. Tuple result type.
type QueryPersonInfoSingletoneTuples = SqlCommand<"SELECT * FROM dbo.ufnGetContactInformation(@PersonId)", connectionString, SingleRow=true>
let cmd35 = new QueryPersonInfoSingletoneTuples(PersonId = 2)
let result35 : Async<_> = cmd35.AsyncExecute() 
result35 |> Async.RunSynchronously |> fun(personId, firstName, lastName, jobTitle, businessEntityType) -> printfn "Person info: Id - %i, FirstName - %O, LastName - %O, JobTitle - %O, BusinessEntityType - %O" personId firstName lastName jobTitle businessEntityType
cmd35.Execute() |> fun(personId, firstName, lastName, jobTitle, businessEntityType) -> printfn "Person info: Id - %i, FirstName - %O, LastName - %O, JobTitle - %O, BusinessEntityType - %O" personId firstName lastName jobTitle businessEntityType

//Single row hint and optional output columns. Single value.
type QueryPersonInfoSingleValue = SqlCommand<"SELECT FirstName FROM dbo.ufnGetContactInformation(@PersonId)", connectionString, SingleRow=true>
let cmd36 = new QueryPersonInfoSingleValue(PersonId = 2)
let result36 : Async<_> = cmd36.AsyncExecute() 
result36 |> Async.RunSynchronously |> (function | Some firstName -> printfn "FirstName - %s" firstName | None -> printfn "Nothing to print" )
cmd36.Execute() |> (function | Some firstName -> printfn "FirstName - %s" firstName | None -> printfn "Nothing to print" )

//Single row hint and optional output columns. Data table result type.
type QueryPersonInfoSingletoneDataTable = SqlCommand<"SELECT * FROM dbo.ufnGetContactInformation(@PersonId)", connectionString, ResultType = ResultType.DataTable>
let cmd37 = new QueryPersonInfoSingletoneDataTable(PersonId = 2)
let result37 = cmd37.AsyncExecute() |> Async.RunSynchronously 
let printPersonInfo(x : QueryPersonInfoSingletoneDataTable.Row) = printfn "Person info: Id - %i, FirstName - %O, LastName - %O, JobTitle - %O, BusinessEntityType - %O" x.PersonID x.FirstName x.LastName x.JobTitle x.BusinessEntityType
result37 |> Seq.iter printPersonInfo
result37.[0].FirstName <- result37.[0].FirstName |> Option.map (fun x -> x + "1")
result37 |> Seq.iter printPersonInfo
result37.[0].FirstName <- None
result37 |> Seq.iter printPersonInfo
cmd37.Execute() |> Seq.iter printPersonInfo

//Non-query
type UpdateEmplInfoCommand = SqlCommand<"EXEC HumanResources.uspUpdateEmployeePersonalInfo @BusinessEntityID, @NationalIDNumber,@BirthDate, @MaritalStatus, @Gender", connectionString>
let cmd4 = new UpdateEmplInfoCommand(BusinessEntityID = 2, NationalIDNumber = "245797967", BirthDate = System.DateTime(1965, 09, 01), MaritalStatus = "S", Gender = "M")
let result4 : Async<int> = cmd4.AsyncExecute() 
let rowsAffected = result4 |> Async.RunSynchronously 
let cmd45 = new UpdateEmplInfoCommand(BusinessEntityID = 2, NationalIDNumber = "245797967", BirthDate = System.DateTime(1965, 09, 01), MaritalStatus = "S", Gender = "M")
cmd45.Execute()

//Single value
type GetServerTime = SqlCommand<"IF @IsUtc = CAST(1 AS BIT) SELECT GETUTCDATE() ELSE SELECT GETDATE()", connectionString, SingleRow=true>
let getSrvTime = new GetServerTime(IsUtc = true)
let result5 : Async<DateTime> = getSrvTime.AsyncExecute()
result5 |> Async.RunSynchronously |> printfn "%A"
getSrvTime.IsUtc <- false
//Execute again
getSrvTime.AsyncExecute() |> Async.RunSynchronously |> printfn "%A"
getSrvTime.Execute() |> printfn "%A"

//Stored procedure by name only
type UpdateEmplInfoCommandSp = SqlCommand<"HumanResources.uspUpdateEmployeePersonalInfo", connectionString, CommandType = CommandType.StoredProcedure>
let cmdSp = new UpdateEmplInfoCommandSp(BusinessEntityID = 2, NationalIDNumber = "245797967", BirthDate = System.DateTime(1965, 09, 01), MaritalStatus = "S", Gender = "F")
cmdSp.AsyncExecute() |> Async.RunSynchronously
cmdSp.SpReturnValue
cmdSp.Execute()
cmdSp.SpReturnValue

