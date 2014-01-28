namespace FSharp.Data.Experimental

open System
open System.Data
open System.Data.SqlClient
open System.Reflection
open System.Collections.Generic
open System.Threading
open System.Linq

open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Reflection

open Xenomorph.TimeScape.Client
open Xenomorph.Generic

open Samples.FSharp.ProvidedTypes

[<AutoOpen>]
module internal Utilities = 
    let catchStart (s:seq<'T>) = 
        { new seq<'T> with 
                member x.GetEnumerator() = try s.GetEnumerator() with _ -> Seq.empty.GetEnumerator()
            interface System.Collections.IEnumerable with 
                member x.GetEnumerator() = (s.GetEnumerator()) :> System.Collections.IEnumerator }

    let initLazy = 
      lazy
        EventExtension.add_OnSourceStatusChange(fun ch -> () (* printfn "Status change: %A" ch.Status *))
        EventExtension.add_OnError(fun err -> () (* printfn "Error: %A" err.Error.Message *) )

type XenoConnection(server:string) = 
    let db = new Database(server)
    interface System.IDisposable with
        member x.Dispose() = db.Dispose()
    /// The name of the category 
    member x.Server = server
    /// The underlying Xenomorph database object
    member x.Connection  = db

type XenoItem(item: Item) = 
    new (database:Database, code, codeType) = XenoItem(new Item(database, code, codeType))
    // /// The underlying object for the connection
    // member x.Connection = item.Database
    /// The underlying object for the item
    member x.Item = item
    member x._GetPropertyChange(propertyName:string, dataSource:string) = 
        initLazy.Force()
#if REAL
        let itemTick = item //new Item(data, item.Code, item.CodeType)
#else
        // TODO: This only works if the DEMOTICK is on the local server for now
        let server = new Server()
        // TODO: DEMOTICK is a sample ticking data source provided by xenomorph.
        let dbTick = new Database(server, "DEMOTICK")
        // TODO: Reuters should not be hardwired here
        let itemTick = new Item(dbTick, item.Code, item.CodeType)
#endif
        { new System.IObservable<DateTime * double> with 
             member x.Subscribe(obs) = 
                //printfn "subscribing..."
                let listener = 
                  { new IPropertySubscriptionListener with 
                       member x.OnSubscriptionError(error, evArgs) = () 
                       member x.OnUnsubscriptionError(error, evArgs) = () 
                       member x.OnSubscriptionSuccess(b, evArgs) = () 
                       member x.OnUnsubscriptionSuccess(a, evArgs) = () 
                       member x.OnPropertyUpdate(update, evArgs) = obs.OnNext(update.Timestamp, (update.Value :?> double)) }

                itemTick.SubscribePropertyChange(propertyName, dataSource, listener)
                //printfn "subscribed..."

                { new System.IDisposable with 
                    member x.Dispose() = itemTick.UnsubscribePropertyChange(propertyName, dataSource) } 
         }

type XenoCategory(database: Database, category: string) = 
    /// The name of the category 
    member x.CategoryName = category
    /// The items in the category, untyped
    member x.Collection = 
        seq { for item in database.GetItems(filterCategory=category) -> XenoItem(item) } |> catchStart

type XenoCategoryImpl(database: Database, category: string) = 
    inherit XenoCategory(database, category)
    let items = seq { for item in database.GetItems(filterCategory=category) -> XenoItem(item) } |> catchStart
    interface seq<XenoItem> with 
         member x.GetEnumerator() = items.GetEnumerator()
    interface System.Collections.IEnumerable with 
         member x.GetEnumerator() = ((x :> seq<XenoItem>).GetEnumerator()) :> System.Collections.IEnumerator 
    // /// The underlying object for the connection
    member x.Connection = database

type XenoCategories(database: Database) = class end

type XenoCategoriesImpl(database: Database) = 
    inherit XenoCategories(database)
    let items = seq { for cat in database.GetCategories() -> XenoCategory(database, cat) } 
    interface seq<XenoCategory> with 
         member x.GetEnumerator() = items.GetEnumerator()
    interface System.Collections.IEnumerable with 
         member x.GetEnumerator() = (items :> System.Collections.IEnumerable).GetEnumerator()
    /// The underlying object for the connection
    member x.Connection = database

[<TypeProvider>]
type XenomorphProviderImplementation(config : TypeProviderConfig) as this = 
    inherit TypeProviderForNamespaces()
    let escape (s:string) = System.Security.SecurityElement.Escape(s)
    let mutable watcher = null : IDisposable
(*
    let toSqlParameter p = 
      match p with
      | Value vp ->
          <@@ SqlParameter(%%Expr.Value (vp.Name), %%Expr.Value(vp.SqlDbTypeId) |> enum, Direction = %%Expr.Value (vp.Direction)) @@>
      | TableValued tvp -> 
          <@@ let p = SqlParameter(%%Expr.Value (tvp.Name), %%Expr.Value (tvp.SqlDbTypeId) |> enum)
              p.TypeName <- %%Expr.Value (tvp.TableTypeName)
              p @@>
*)


    let nameSpace = this.GetType().Namespace
    let assembly = Assembly.GetExecutingAssembly()
    let providerType = ProvidedTypeDefinition(assembly, nameSpace, "XenomorphProvider", Some typeof<obj>, HideObjectMethods = true)
    let invalidateE = new Event<EventHandler,EventArgs>()    
    //let addParamDoc doc (p:ProvidedStaticParameter) = p.AddXmlDoc ("<summary>" + doc + "</summary>"); p
    do 
        providerType.AddXmlDoc """<summary>A type provider for connections to Xenomorph TimeScape servers</summary>
         <param name='Server'>The name of the Xenomorph TimeScape server.</param> 
         <param name='UseOptionTypes'>Give all properties type 'option' with value None if the data is not present. If false, raise an exception.</param> 
         <param name='UseRefinedTypes'>Use refined types for individual entities. Using missing properties on the entity will give a compilation warning (if option types are used) and an error (if option types are not used).</param> 
        """
        providerType.DefineStaticParameters(
            parameters = [ 
                ProvidedStaticParameter("Server", typeof<string>) 
                ProvidedStaticParameter("UseRefinedTypes", typeof<bool>, box true)  
                ProvidedStaticParameter("UseOptionTypes", typeof<bool>, box false) 
            ],             
            instantiationFunction = this.CreateType
        )
        this.AddNamespace(nameSpace, [ providerType ])
    
    interface ITypeProvider with
        [<CLIEvent>]
        override this.Invalidate = invalidateE.Publish

    interface IDisposable with 
        member this.Dispose() = 
           if watcher <> null then 
               try watcher.Dispose() with _ -> ()

    member internal this.CreateType typeName parameters = 

        let server : string = unbox parameters.[0] 
        let useRefinedTypesForItems : bool = unbox parameters.[1]
        let useOptionForMissingProperties : bool = unbox parameters.[2]

        let db = new Database(server)

        // T
        let providedType = ProvidedTypeDefinition(assembly, nameSpace, typeName, baseType = Some typeof<obj>, HideObjectMethods = true)

        // T.ServiceTypes
        let serviceTypesType = ProvidedTypeDefinition("ServiceTypes", baseType = Some typeof<obj>, HideObjectMethods = true)
        serviceTypesType.AddXmlDocDelayed(fun () -> "<summary>Contains the service types for the representation of the instruments in the Xenomorph server " + server + "</summary>")
        providedType.AddMembers [ serviceTypesType ]

        // T.ServiceTypes.ServiceType
        let serviceType = ProvidedTypeDefinition("ServiceType", baseType = Some typeof<XenoConnection>, HideObjectMethods = true)
        serviceType.AddXmlDocDelayed(fun () -> "<summary>Represents a strongly typed connection to the instruments in the Xenomorph server " + escape server + "</summary>")
        serviceTypesType.AddMembers [ serviceType ]

        // T.ServiceTypes.Categories
        let categoriesType = ProvidedTypeDefinition("Categories", baseType = Some typeof<XenoCategories>, HideObjectMethods = true)
        categoriesType.AddXmlDocDelayed(fun () -> "<summary>Represents the categories in the Xenomorph server " + escape server + "</summary>")
        serviceTypesType.AddMembers [ categoriesType ]

        // T.ServiceTypes.CategoriesCollection
        let categoriesCollectionType = ProvidedTypeDefinition("CategoriesCollection",baseType = Some typeof<obj>, HideObjectMethods = true)
        categoriesCollectionType.AddInterfaceImplementation(typeof<seq<XenoCategory>>)
        serviceTypesType.AddMember categoriesCollectionType

        let makeProvidedPropertyForItemProperty (catProp: PropertyDefinition) = 
            let catPropType = 
                match catProp.DataType, catProp.Historic, catProp.HasStatus with 
                | Xenomorph.DbDataType.Blob, false, _ -> typeof<Blob>
                | Xenomorph.DbDataType.Boolean, false, _ -> typeof<bool>
                | Xenomorph.DbDataType.DateTime, false, _ -> typeof<System.DateTime>
                | Xenomorph.DbDataType.Double, false, _ -> typeof<double>
                | Xenomorph.DbDataType.Excel, false, _ -> typeof<Excel>
                | Xenomorph.DbDataType.Formula, false, _ -> typeof<obj>
                | Xenomorph.DbDataType.FormulaGrid, false, _ -> typeof<obj>
                | Xenomorph.DbDataType.Hyperlink, false, _ -> typeof<Hyperlink>
                | Xenomorph.DbDataType.Int16, false, _ -> typeof<int16>
                | Xenomorph.DbDataType.Int32, false, _ -> typeof<int32>
                | Xenomorph.DbDataType.ItemRef, false, _ -> typeof<ItemRef>
                | Xenomorph.DbDataType.List, false, _ -> typeof<Xenomorph.Generic.List>
                | Xenomorph.DbDataType.Matrix, false, _ -> typeof<Matrix>
                | Xenomorph.DbDataType.String, false, _ -> typeof<string>

                | Xenomorph.DbDataType.Blob, true, _ -> typeof<TimeSeries<Blob>>
                | Xenomorph.DbDataType.Boolean, true, _ -> typeof<TimeSeries<bool>>
                | Xenomorph.DbDataType.DateTime, true, _ -> typeof<TimeSeries<System.DateTime>>
                | Xenomorph.DbDataType.Double, true, _ -> typeof<TimeSeries<double>>
                | Xenomorph.DbDataType.Excel, true, _ -> typeof<TimeSeries<Excel>>
                | Xenomorph.DbDataType.Formula, true, _ -> typeof<TimeSeries<obj>>
                | Xenomorph.DbDataType.FormulaGrid, true, _ -> typeof<TimeSeries<obj>>
                | Xenomorph.DbDataType.Hyperlink, true, _ -> typeof<TimeSeries<Hyperlink>>
                | Xenomorph.DbDataType.Int16, true, _ -> typeof<TimeSeries<int16>>
                | Xenomorph.DbDataType.Int32, true, _ -> typeof<TimeSeries<int32>>
                | Xenomorph.DbDataType.ItemRef, true, _ -> typeof<TimeSeries<Xenomorph.Generic.ItemRef>>
                | Xenomorph.DbDataType.List, true, _ -> typeof<TimeSeries<Xenomorph.Generic.List>>
                | Xenomorph.DbDataType.Matrix, true, _ -> typeof<TimeSeries<Matrix>>
                | Xenomorph.DbDataType.String, true, _ -> typeof<TimeSeries<string>>

                | _ -> failwith "unknown data type"

            let catPropType = if useOptionForMissingProperties then typedefof<int option>.MakeGenericType(catPropType) else catPropType

            // Convert null return values --> None
            // Convert all "data not available" exceptions  --> None
            // Good results --> Some
            let nullToOptionExpr (ty:Type) (e:Expr)  = 
                assert useOptionForMissingProperties
                let v = Var("tmp", e.Type)
                let mi = match <@@ System.Object.ReferenceEquals(null, null) @@> with Patterns.Call(_,mi,_) -> mi | _ -> failwith "unreachable"
                let optionTy = typeof<int option>.GetGenericTypeDefinition().MakeGenericType(ty) 
                let someUnionCase = Reflection.FSharpType.GetUnionCases(optionTy).[1]
                let noneValue = Expr.DefaultValue(optionTy)
                let body = Expr.Let(v,e,Expr.IfThenElse(Expr.Call(mi,[Expr.Coerce(e,typeof<obj>);<@@ null : obj @@>]),noneValue, Expr.NewUnionCase(someUnionCase, [ Expr.Coerce (Expr.Var v, ty) ])))                
                // Convert all "data not available" exceptions  --> None
                // TODO: should really call something to check if the property is available?
                Expr.TryWith(body, Var("exn",typeof<exn>), Expr.Value(true), Var("exn",typeof<exn>), noneValue)

            // T.ServiceTypes.{category} . Property
            let p = ProvidedProperty(catProp.Name, catPropType,GetterCode=(fun args -> 
                let catPropName = catProp.Name
                let itemExpr = <@@ ( %%args.[0] : XenoItem).Item @@>
                if catProp.Historic then 
                    let mi = match <@@  ( null : Item).LoadSeries<double>("",null,null,null,Nullable(),null) @@> with Patterns.Call(_,mi,_) -> mi | _ -> failwith "unreachable"
                    let catTimeSeriesType = catPropType.GetGenericArguments().[0]
                    let catTimeSeriesElementType = 
                        if useOptionForMissingProperties then catTimeSeriesType.GetGenericArguments().[0] 
                        else catTimeSeriesType
                    let loadSeriesMeth = mi.GetGenericMethodDefinition().MakeGenericMethod [| catTimeSeriesElementType |]
                    Expr.Call (itemExpr, loadSeriesMeth, [ Expr.Value catPropName; <@@ null : string @@>; <@@ null : Xenomorph.DateRange @@>; <@@ null : Xenomorph.Step @@>; <@@ Nullable<DateTime>() @@>; <@@ null : string @@> ])
                        |> (if useOptionForMissingProperties then nullToOptionExpr catTimeSeriesType else id)
                else
                    let catPropNonNullType = 
                        if useOptionForMissingProperties then catPropType.GetGenericArguments().[0]
                        else catPropType

                    <@@ ( %%itemExpr : Item).LoadValue(catPropName,null,Nullable(),null) @@>
                        |> (if useOptionForMissingProperties then nullToOptionExpr catPropNonNullType else id)
                    ))
            p.AddXmlDocDelayed(fun () -> "<summary>" + escape catProp.Description + "</summary>")
            p

        let makeProvidedPropertyForItemEventProperty (cat:string) (propName: string) = 
            let propType = typeof<System.IObservable<DateTime * double>>
            let dataSource = "IDN_RDF"
            let p = ProvidedProperty(propName, propType,GetterCode=(fun args -> 
                <@@ ( %%args.[0] : XenoItem)._GetPropertyChange(propName,dataSource) @@> 
                    ))
            p.AddXmlDocDelayed(fun () -> "<summary>Get an observable of the " + propName + " property</summary>")
            p


        let categoryTypes = 
            [  for cat in db.GetCategories() do

                // T.ServiceTypes.{category}
                let catEntityType = ProvidedTypeDefinition(cat,baseType = Some typeof<XenoItem>, HideObjectMethods = true)

                catEntityType.AddMembersDelayed(fun () -> 
                    let catProps = db.GetPropertyDefinitions(cat) |> Seq.toArray

                    [ for catProp in catProps do
                        let p = makeProvidedPropertyForItemProperty catProp
                        yield p 
                      yield makeProvidedPropertyForItemEventProperty cat "Ask" //"_AskSample" 
                      yield makeProvidedPropertyForItemEventProperty cat "Bid" //"_AskSample" 
                      //yield makeProvidedPropertyForItemEventProperty cat "BidSample" 
                      ])

                serviceTypesType.AddMember catEntityType

                // T.ServiceTypes.``{category} Category``
                let catType = ProvidedTypeDefinition(cat + " Category",baseType = Some typeof<XenoCategory>, HideObjectMethods = true)
                catType.AddXmlDocDelayed(fun () -> "<summary>The type representing the category '" + escape cat + "' in the server '" + server + "'. Property 'Individuals' gets individual entities, property 'Collection' gets an enumerable collection of all entities.</summary>")
                serviceTypesType.AddMember catType

                // T.ServiceTypes.``{category} Collection``
                let catCollectionType = ProvidedTypeDefinition(cat + " Collection",baseType = Some typeof<obj>, HideObjectMethods = true)
                catCollectionType.AddXmlDocDelayed(fun () -> "<summary>The type representing the collection of all items in the category '" + escape cat + "' in the server '" + server + "'</summary>")
                catCollectionType.AddInterfaceImplementation(typedefof<seq<int>>.MakeGenericType(catEntityType))
                serviceTypesType.AddMember catCollectionType

                // // T.ServiceTypes.``{category} Individuals``
                // let catItemsType = ProvidedTypeDefinition(cat + " Individuals",baseType = Some typeof<obj>, HideObjectMethods = true)
                // catType.AddXmlDocDelayed(fun () -> "<summary>The type representing the individual items of category '" + escape cat + "' in the server '" + escape server + "'</summary>")
                catType.AddMembersDelayed(fun () -> 
                    [ for item in db.GetItems(filterCategory=cat) do
                        let itemCode = item.Code
                        let itemCodeType = item.CodeType
                        let itemName = item.Description + " " + string (item.Code, item.CodeType)
                
                        let itemRefinedType = 
                            if useRefinedTypesForItems then 
                                // This is a bit cheeky as the item type is added after the "ServiceTypes" collection may have been iterated.
                                // TODO: add the type using a delayed structure and dereference/link to the type here, rather than creating it here.
                                let itemRefinedType = ProvidedTypeDefinition(itemName + " Item",baseType = Some (catEntityType :> Type), HideObjectMethods = true)
                                itemRefinedType.AddXmlDocDelayed(fun () -> "<summary>The type representing the refined view of item '" + itemName + "' in the server '" + server + "'</summary>")

                                // Add obsolete members for all the properties which are not available for this item. This hides them
                                // and gives a warning if they are used.
                                itemRefinedType.AddMembersDelayed(fun () -> 
                                    let availProps = [ for x in item.GetAvailProperties(null,null) -> x.Name ]
                                    let catProps = db.GetPropertyDefinitions(cat) |> Seq.toArray

                                    [ for catProp in catProps do
                                        if not (availProps.Contains(catProp.Name)) then
                                            let p = makeProvidedPropertyForItemProperty catProp
                                            p.AddObsoleteAttribute("The property '" + catProp.Name + "' is not available for the item '" + itemName + "'", isError=(not useOptionForMissingProperties))
                                            yield p ])

                                serviceTypesType.AddMember itemRefinedType
                                itemRefinedType
                            else
                                catEntityType

                        // This is the property to get the item
                        let p = ProvidedProperty(itemName, itemRefinedType, GetterCode=(fun args -> <@@ new XenoItem( ( (%%args.[0] : XenoCategory) :?> XenoCategoryImpl).Connection, itemCode, itemCodeType ) @@>)) 
                        p.AddXmlDocDelayed(fun () -> "<summary>" + escape item.Description + " (code " + escape item.Code + ")</summary>")
                        yield p ])

                catType.AddMembersDelayed(fun () ->
                    [   
                        //let itemsProp = ProvidedProperty("Individuals", catItemsType, GetterCode=(fun args -> args.[0]))
                        //itemsProp.AddXmlDocDelayed(fun () -> "<summary>The type representing the individual items in category '" + escape cat + "' in server " + escape server + ", organzied by name</summary>")
                        //yield itemsProp

                        let collectionProp = ProvidedProperty("Collection", catCollectionType, GetterCode=(fun args -> args.[0]))
                        collectionProp.AddXmlDocDelayed(fun () -> "<summary>The type representing the collection of items in category '" + escape cat + "' in server " + escape server + ", organzied by name</summary>")
                        yield collectionProp
                    ])

                yield (cat, catType) ]


        categoriesType.AddMembersDelayed(fun () -> 
              [  // data.Categories.{category}
                 for (c,catType) in categoryTypes do
                   let p = ProvidedProperty(c, catType, GetterCode=(fun args -> <@@ new XenoCategoryImpl( ( ( %%args.[0] : XenoCategories) :?> XenoCategoriesImpl).Connection, c ) :> XenoCategory @@>))
                   p.AddXmlDocDelayed(fun () -> "<summary>" + escape (db.GetCategoryDescription(c)) + "</summary>")
                   yield (p :> MemberInfo)
                 
                 // data.Categories.Collection
                 let collectionProp = ProvidedProperty("Collection", categoriesCollectionType, GetterCode=(fun args -> args.[0]))
                 collectionProp.AddXmlDocDelayed(fun () -> "<summary>The collection of categories in the server " + escape server + ", organzied by name</summary>")
                 yield (collectionProp :> MemberInfo)
                    ])

        serviceType.AddMembersDelayed(fun () ->
            [
                // data.Categories
                let categoriesProp = 
                    let p = ProvidedProperty("Categories", categoriesType, GetterCode=(fun args -> <@@ new XenoCategoriesImpl( ( %%args.[0] : XenoConnection).Connection ) :> XenoCategories @@>))
                    p.AddXmlDocDelayed(fun () -> "<summary>The categories in the Xenomorph server '" + server + "'</summary>")
                    p
                yield categoriesProp :> MemberInfo
            ])

        // T.GetDataContext
        let getDataContextMeth = 
            ProvidedMethod("GetDataContext",[ProvidedParameter("server", typeof<string>, optionalValue = null)],serviceType,
                            IsStaticMethod=true,
                            InvokeCode=(fun args -> <@@ new XenoConnection( match (%%args.[0] : string) with null -> server | s -> s) @@>))
        getDataContextMeth.AddXmlDocDelayed(fun () -> "<summary>Get a data context for the Xenomorph server " + escape server + "</summary>")

        providedType.AddMember getDataContextMeth

        providedType         
