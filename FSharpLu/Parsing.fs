﻿/// Parsing text to F# data types
module Microsoft.FSharpLu.Parsing

open Microsoft.FSharpLu.Option

/// Try to parse an int32
let public tryParseInt (s:string) =
    System.Int32.TryParse(s) |> ofPair

/// A parser for string parameter
let public tryParseString = Some

/// Try to parse a boolean
let public tryParseBoolean = function
    | "0" -> Some false
    | "1" -> Some true
    | b -> System.Boolean.TryParse b |> ofPair

/// Parse a boolean
let public parseBoolean =
    tryParseBoolean >> orDo (fun() -> invalidOp "Invalid boolean format")

/// Parse a C#-like enumeration (i.e. of the form type MyEnum = One = 1 | Two = 2)
let public tryParseEnum<'T when 'T : struct
                            and 'T : (new : unit -> 'T)
                            and 'T :> System.ValueType> (e:string) =
    System.Enum.TryParse<'T>(e, true) |> ofPair

/// Lookup value from a dictionary and try to parse it with the provided parser
let public tryParseDictValue dict key parser =
   Collections.Dictionary.tryGetValue dict key
   |> Option.bind parser

/// Try to parse a Guid
let public tryParseGuid (value:string) =
    System.Guid.TryParse(value) |> ofPair

module Union =
    open FSharp.Reflection

    /// Parse a field-less discriminated union of type 'T from string
    /// This only works with simple *field-less* discriminated union of
    /// the form "type Bla = Foo | Bar"
    let tryParse<'T> (string:string) =
        let fields =
                typeof<'T>
                |> FSharpType.GetUnionCases
                |> Array.filter (fun case -> case.Name.Equals(string, System.StringComparison.OrdinalIgnoreCase))
        match fields with
        | [| case |] -> Some(FSharpValue.MakeUnion(case,[||]) :?> 'T)
        | _ -> None

    /// Parsing helpers for discriminated union with cases
    module WithCases =
        /// Converts a discriminated union with at most a single field per case (e.g. either `|Abc` or `|AbcDef of 'a`)
        /// to a string with the format "CaseName/FieldValue".
        /// For example case `| Abc` gets converted to "Abc" and case `AbcDef (42)` gets converted to "AbcDef/42"
        let rec fieldAsString union =
            match Microsoft.FSharp.Reflection.FSharpValue.GetUnionFields(union, union.GetType()) with
            | case, [||] -> case.Name
            | case, [| field |] when FSharpType.IsUnion (field.GetType()) ->
                sprintf "%s/%s" case.Name (fieldAsString field)
            | _ -> invalidArg "union" "Only field-less and single-field union cases can be serialized."

        /// Parse a string of the form "CaseName/FieldValue" generated by the function `fieldAsString`
        /// into a discriminated union of the corresponding type.
        let tryParseUnion<'a> (value:string): Option<'a> =
            if System.String.IsNullOrEmpty value then
                None
            else
                let values = value.Split('/') |> Array.toList
                let rec tryParse unionType =
                    function
                    | [] -> None
                    | h::t ->
                        Microsoft.FSharp.Reflection.FSharpType.GetUnionCases unionType
                        |> Array.tryFind (fun case -> case.Name = h)
                        |> Option.bind (fun case ->
                            match case.GetFields() with
                            | [||] -> FSharpValue.MakeUnion(case, [||]) |> Some
                            | [| field |] when FSharpType.IsUnion field.PropertyType ->
                                tryParse field.PropertyType t
                                |> Option.map(fun innerfield -> FSharpValue.MakeUnion (case, [| innerfield |]) )
                            | [| _ |] | _ ->
                                invalidArg "value" "Only field-less and single-field union cases can be deserialized.")

                tryParse typeof<'a> values
                |> Option.map unbox
