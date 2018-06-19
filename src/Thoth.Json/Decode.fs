module Thoth.Json.Decode

open Fable.Core
open Fable.Core.JsInterop
open Fable.Import

module Helpers =

    [<Emit("typeof $0 === 'string'")>]
    let isString (_ : obj) : bool = jsNative

    [<Emit("typeof $0 === 'boolean'")>]
    let isBoolean (_ : obj) : bool = jsNative

    [<Emit("typeof $0 === 'number'")>]
    let isNumber (_ : obj) : bool = jsNative

    [<Emit("$0 instanceof Array")>]
    let isArray (_ : obj) : bool = jsNative

    [<Emit("Object.getPrototypeOf($0 || false) === Object.prototype")>]
    let isObject (_ : obj) : bool = jsNative

    [<Emit("Number.isNaN($0)")>]
    let isNaN (_: obj) : bool = jsNative

    [<Emit("-2147483648 < $0 && $0 < 2147483647 && ($0 | 0) === $0")>]
    let isValidIntRange (_: obj) : bool = jsNative

    [<Emit("isFinite($0) && !($0 % 1)")>]
    let isIntFinite (_: obj) : bool = jsNative

    [<Emit("($0 !== undefined)")>]
    let isDefined (_: obj) : bool = jsNative

    [<Emit("JSON.stringify($0, null, 4) + ''")>]
    let anyToString (_: obj) : string= jsNative

    [<Emit("typeof $0 === 'function'")>]
    let isFunction (_: obj) : bool = jsNative

    [<Emit("Object.keys($0)")>]
    let objectKeys (_: obj) : string list = jsNative

type DecoderError =
    | BadPrimitive of string * obj
    | BadType of string * obj
    | BadPrimitiveExtra of string * obj * string
    | BadField of string * obj
    | BadPath of string * obj * string
    | TooSmallArray of string * obj
    | FailMessage of string
    | BadOneOf of string list
    | Direct of string

type Decoder<'T> = obj -> Result<'T, DecoderError>

let private genericMsg msg value newLine =
    try
        "Expecting "
            + msg
            + " but instead got:"
            + (if newLine then "\n" else " ")
            + (Helpers.anyToString value)
    with
        | _ ->
            "Expecting "
            + msg
            + " but decoder failed. Couldn't report given value due to circular structure."
            + (if newLine then "\n" else " ")

let private errorToString =
    function
    | BadPrimitive (msg, value) ->
        genericMsg msg value false
    | BadType (msg, value) ->
        genericMsg msg value true
    | BadPrimitiveExtra (msg, value, reason) ->
        genericMsg msg value false + "\nReason: " + reason
    | BadField (msg, value) ->
        genericMsg msg value true
    | BadPath (msg, value, fieldName) ->
        genericMsg msg value true + ("\nNode `" + fieldName + "` is unkown.")
    | TooSmallArray (msg, value) ->
        "Expecting " + msg + ".\n" + (Helpers.anyToString value)
    | BadOneOf messages ->
        "I run into the following problems:\n\n" + String.concat "\n" messages
    | FailMessage msg ->
        "I run into a `fail` decoder.\n" + msg
    | Direct msg ->
        msg

let unwrap (decoder : Decoder<'T>) (value : obj) : 'T =
    match decoder value with
    | Ok success ->
        success
    | Error error ->
        failwith (errorToString error)

///////////////
// Runners ///
/////////////

let private decodeValueError (decoder : Decoder<'T>) =
    fun value ->
        try
            match decoder value with
            | Ok success ->
                Ok success
            | Error error ->
                Error error
        with
            | ex ->
                Error (Direct ex.Message)

let decodeValue (decoder : Decoder<'T>) =
    fun value ->
        match decodeValueError decoder value with
        | Ok success ->
            Ok success
        | Error error ->
            Error (errorToString error)

let decodeString (decoder : Decoder<'T>) =
    fun value ->
        try
            let json = JS.JSON.parse value
            decodeValue decoder json
        with
            | ex ->
                Error("Given an invalid JSON: " + ex.Message)

//////////////////
// Primitives ///
////////////////

let string : Decoder<string> =
    fun value ->
        if Helpers.isString value then
            Ok(unbox<string> value)
        else
            BadPrimitive("a string", value) |> Error

let int : Decoder<int> =
    fun value ->
        if not (Helpers.isNumber value)  then
            BadPrimitive("an int", value) |> Error
        else
            if not (Helpers.isValidIntRange value) then
                BadPrimitiveExtra("an int", value, "Value was either too large or too small for an int") |> Error
            else
                Ok(unbox<int> value)

let bool : Decoder<bool> =
    fun value ->
        if Helpers.isBoolean value then
            Ok(unbox<bool> value)
        else
            BadPrimitive("a boolean", value) |> Error

let float : Decoder<float> =
    fun value ->
        if Helpers.isNumber value then
            Ok(unbox<float> value)
        else
            BadPrimitive("a float", value) |> Error


/////////////////////////
// Object primitives ///
///////////////////////

exception UndefinedValueException of string
exception NonObjectTypeException

let field (fieldName: string) (decoder : Decoder<'value>) : Decoder<'value> =
    fun value ->
        if Helpers.isObject value then
            let fieldValue = value?(fieldName)
            if Helpers.isDefined fieldValue then
                decoder fieldValue
            else
                BadField ("an object with a field named `" + fieldName + "`", value)
                |> Error
        else
            BadType("an object", value)
            |> Error

let at (fieldNames: string list) (decoder : Decoder<'value>) : Decoder<'value> =
    fun value ->
        let mutable cValue = value
        let mutable index = 0
        try
            for fieldName in fieldNames do
                if Helpers.isObject cValue then
                    let currentNode = cValue?(fieldName)
                    if Helpers.isDefined currentNode then
                        cValue <- currentNode
                    else
                        raise (UndefinedValueException fieldName)
                else
                    raise NonObjectTypeException
                index <- index + 1

            unwrap decoder cValue |> Ok
        with
            | NonObjectTypeException ->
                let path = String.concat "." fieldNames.[..index-1]
                BadType ("an object at `" + path + "`", cValue)
                |> Error
            | UndefinedValueException fieldName ->
                let msg = "an object with path `" + (String.concat "." fieldNames) + "`"
                BadPath (msg, value, fieldName)
                |> Error

let index (requestedIndex: int) (decoder : Decoder<'value>) : Decoder<'value> =
    fun value ->
        if Helpers.isArray value then
            let vArray = unbox<obj array> value
            if requestedIndex < vArray.Length then
                unwrap decoder (vArray.[requestedIndex]) |> Ok
            else
                let msg =
                    "a longer array. Need index `"
                        + (requestedIndex.ToString())
                        + "` but there are only `"
                        + (vArray.Length.ToString())
                        + "` entries"

                TooSmallArray(msg, value)
                |> Error
        else
            BadPrimitive("an array", value)
            |> Error

// let nullable (d1: Decoder<'value>) : Resul<'value option, DecoderError> =

//////////////////////
// Data structure ///
////////////////////

let list (decoder : Decoder<'value>) : Decoder<'value list> =
    fun value ->
        if Helpers.isArray value then
            unbox<obj array> value
            |> Array.map (unwrap decoder)
            |> Array.toList
            |> Ok
        else
            BadPrimitive ("a list", value)
            |> Error

let array (decoder : Decoder<'value>) : Decoder<'value array> =
    fun value ->
        if Helpers.isArray value then
            unbox<obj array> value
            |> Array.map (unwrap decoder)
            |> Ok
        else
            BadPrimitive ("an array", value)
            |> Error

let keyValuePairs (decoder : Decoder<'value>) : Decoder<(string * 'value) list> =
    fun value ->
        if not (Helpers.isObject value) || Helpers.isArray value then
            BadPrimitive ("an object", value)
            |> Error
        else
            value
            |> Helpers.objectKeys
            |> List.map (fun key -> (key, value?(key) |> unwrap decoder))
            |> Ok

//////////////////////////////
// Inconsistent Structure ///
////////////////////////////

let option (d1 : Decoder<'value>) : Decoder<'value option> =
    fun value ->
        match decodeValueError d1 value with
        | Ok v -> Ok (Some v)
        | Error ((BadPrimitive _ ) as errorInfo)
        | Error ((BadPrimitiveExtra _ ) as errorInfo)
        | Error ((BadType _ ) as errorInfo) ->
            // This mean the value was found but with a bad type/primitive
            Error errorInfo
        | Error _ ->
            // Value was not present && type was valid
            Ok None

let oneOf (decoders : Decoder<'value> list) : Decoder<'value> =
    fun value ->
        let rec runner (decoders : Decoder<'value> list) (errors : string list) =
            match decoders with
            | head::tail ->
                match decodeValue head value with
                | Ok v ->
                    Ok v
                | Error error -> runner tail (errors @ [error])
            | [] -> BadOneOf errors |> Error

        runner decoders []

//////////////////////
// Fancy decoding ///
////////////////////

let nil (output : 'a) : Decoder<'a> =
    fun value ->
        if isNull value then
            Ok output
        else
            BadPrimitive("null", value) |> Error

let value v = Ok v

let succeed (output : 'a) : Decoder<'a> =
    fun _ ->
        Ok output

let fail (msg: string) : Decoder<'a> =
    fun _ ->
        FailMessage msg |> Error

let andThen (cb: 'a -> Decoder<'b>) (decoder : Decoder<'a>) : Decoder<'b> =
    fun value ->
        match decodeValue decoder value with
        | Error error -> failwith error
        | Ok result ->
            cb result value

/////////////////////
// Map functions ///
///////////////////

let map
    (ctor : 'a -> 'value)
    (d1 : Decoder<'a>) : Decoder<'value> =
    (fun value ->
        let t = unwrap d1 value
        Ok (ctor t)
    )

let map2
    (ctor : 'a -> 'b -> 'value)
    (d1 : Decoder<'a>)
    (d2 : Decoder<'b>) : Decoder<'value> =
    (fun value ->
        let t = unwrap d1 value
        let t2 = unwrap d2 value

        Ok (ctor t t2)
    )

let map3
    (ctor : 'a -> 'b -> 'c -> 'value)
    (d1 : Decoder<'a>)
    (d2 : Decoder<'b>)
    (d3 : Decoder<'c>) : Decoder<'value> =
    (fun value ->
        let v1 = unwrap d1 value
        let v2 = unwrap d2 value
        let v3 = unwrap d3 value

        Ok (ctor v1 v2 v3)
    )

let map4
    (ctor : 'a -> 'b -> 'c -> 'd -> 'value)
    (d1 : Decoder<'a>)
    (d2 : Decoder<'b>)
    (d3 : Decoder<'c>)
    (d4 : Decoder<'d>) : Decoder<'value> =
    (fun value ->
        let v1 = unwrap d1 value
        let v2 = unwrap d2 value
        let v3 = unwrap d3 value
        let v4 = unwrap d4 value

        Ok (ctor v1 v2 v3 v4)
    )

let map5
    (ctor : 'a -> 'b -> 'c -> 'd -> 'e -> 'value)
    (d1 : Decoder<'a>)
    (d2 : Decoder<'b>)
    (d3 : Decoder<'c>)
    (d4 : Decoder<'d>)
    (d5 : Decoder<'e>) : Decoder<'value> =
    (fun value ->
        let v1 = unwrap d1 value
        let v2 = unwrap d2 value
        let v3 = unwrap d3 value
        let v4 = unwrap d4 value
        let v5 = unwrap d5 value

        Ok (ctor v1 v2 v3 v4 v5)
    )

let map6
    (ctor : 'a -> 'b -> 'c -> 'd -> 'e -> 'f -> 'value)
    (d1 : Decoder<'a>)
    (d2 : Decoder<'b>)
    (d3 : Decoder<'c>)
    (d4 : Decoder<'d>)
    (d5 : Decoder<'e>)
    (d6 : Decoder<'f>) : Decoder<'value> =
    (fun value ->
        let v1 = unwrap d1 value
        let v2 = unwrap d2 value
        let v3 = unwrap d3 value
        let v4 = unwrap d4 value
        let v5 = unwrap d5 value
        let v6 = unwrap d6 value

        Ok (ctor v1 v2 v3 v4 v5 v6)
    )

let map7
    (ctor : 'a -> 'b -> 'c -> 'd -> 'e -> 'f -> 'g -> 'value)
    (d1 : Decoder<'a>)
    (d2 : Decoder<'b>)
    (d3 : Decoder<'c>)
    (d4 : Decoder<'d>)
    (d5 : Decoder<'e>)
    (d6 : Decoder<'f>)
    (d7 : Decoder<'g>) : Decoder<'value> =
    (fun value ->
        let v1 = unwrap d1 value
        let v2 = unwrap d2 value
        let v3 = unwrap d3 value
        let v4 = unwrap d4 value
        let v5 = unwrap d5 value
        let v6 = unwrap d6 value
        let v7 = unwrap d7 value

        Ok (ctor v1 v2 v3 v4 v5 v6 v7)
    )

let map8
    (ctor : 'a -> 'b -> 'c -> 'd -> 'e -> 'f -> 'g -> 'h -> 'value)
    (d1 : Decoder<'a>)
    (d2 : Decoder<'b>)
    (d3 : Decoder<'c>)
    (d4 : Decoder<'d>)
    (d5 : Decoder<'e>)
    (d6 : Decoder<'f>)
    (d7 : Decoder<'g>)
    (d8 : Decoder<'h>) : Decoder<'value> =
        (fun value ->
            let v1 = unwrap d1 value
            let v2 = unwrap d2 value
            let v3 = unwrap d3 value
            let v4 = unwrap d4 value
            let v5 = unwrap d5 value
            let v6 = unwrap d6 value
            let v7 = unwrap d7 value
            let v8 = unwrap d8 value

            Ok (ctor v1 v2 v3 v4 v5 v6 v7 v8)
        )

let dict (decoder : Decoder<'value>) : Decoder<Map<string, 'value>> =
    map Map.ofList (keyValuePairs decoder)

////////////////
// Pipeline ///
//////////////

let custom d1 d2 = map2 (|>) d1 d2

let hardcoded<'a, 'b, 'c> : 'a -> Decoder<('a -> 'b)> -> 'c -> Result<'b,DecoderError> = succeed >> custom

let required (key : string) (valDecoder : Decoder<'a>) (decoder : Decoder<'a -> 'b>) : Decoder<'b> =
    custom (field key valDecoder) decoder

let requiredAt (path : string list) (valDecoder : Decoder<'a>) (decoder : Decoder<'a -> 'b>) : Decoder<'b> =
    custom (at path valDecoder) decoder

let decode output value = succeed output value

/// Convert a `Decoder<Result<x, 'a>>` into a `Decoder<'a>`
let resolve d1 : Decoder<'a> =
    fun value ->
        andThen id d1 value

let optionalDecoder pathDecoder valDecoder fallback =
    let nullOr decoder =
        oneOf [ decoder; nil fallback ]

    let handleResult input =
        match decodeValueError pathDecoder input with
        | Ok rawValue ->
            // Field was present, so we try to decode the value
            match decodeValue (nullOr valDecoder) rawValue with
            | Ok finalResult ->
                succeed finalResult

            | Error finalErr ->
                printfn "Error: %A" finalErr
                fail finalErr

        | Error ((BadType _ ) as errorInfo) ->
            // If the error is of type `BadType` coming from `at` decoder then return the error
            // This mean the json was expecting an object but got an array instead
            fun _ -> Error errorInfo
        | Error error ->
            printfn "Error: %A" error
            // Field was not present && type was valid
            succeed fallback

    value
    |> andThen handleResult

let optional (key : string) (valDecoder : Decoder<'a>) (fallback : 'a) (decoder : Decoder<'a -> 'b>) : Decoder<'b> =
    fun v ->
        if Helpers.isObject v then
            custom (optionalDecoder (field key value) valDecoder fallback) decoder v
        else
            BadType("an object", v)
            |> Error

let optionalAt (path : string list) (valDecoder : Decoder<'a>) (fallback : 'a) (decoder : Decoder<'a -> 'b>) : Decoder<'b> =
    fun v ->
        if Helpers.isObject v then
            custom (optionalDecoder (at path value) valDecoder fallback) decoder v
        else
            BadType("an object", v)
            |> Error

//////////////////////
// Object builder ///
////////////////////

type IRequiredGetter =
    abstract Field : string -> Decoder<'a> -> 'a
    abstract At : List<string> -> Decoder<'a> -> 'a
    abstract Index : int -> Decoder<'a> -> 'a

type IOptionalGetter =
    abstract Field : string -> Decoder<'a> -> 'a -> 'a
    abstract At : List<string> -> Decoder<'a> -> 'a -> 'a
    abstract Index : int -> Decoder<'a> -> 'a -> 'a

type IGetters =
    abstract Required: IRequiredGetter
    abstract Optional: IOptionalGetter

let object (builder: IGetters -> 'value) : Decoder<'value> =
    fun v ->
        builder { new IGetters with
            member __.Required =
                { new IRequiredGetter with
                    member __.Field (fieldName : string) (decoder : Decoder<_>) =
                        match decodeValue (field fieldName decoder) v with
                        | Ok v -> v
                        | Error msg -> failwith msg
                    member __.At (fieldNames : string list) (decoder : Decoder<_>) =
                        match decodeValue (at fieldNames decoder) v with
                        | Ok v -> v
                        | Error msg -> failwith msg
                    member __.Index (requestedIndex: int) (decoder : Decoder<_>) =
                        match decodeValue (index requestedIndex decoder) v with
                        | Ok v -> v
                        | Error msg -> failwith msg }
            member __.Optional =
                { new IOptionalGetter with
                    member __.Field (fieldName : string) (decoder : Decoder<_>) fallback =
                        match optionalDecoder (field fieldName value) decoder fallback v with
                        | Ok v -> v
                        | Error msg ->
                            failwith (errorToString msg)
                    member __.At (fieldNames : string list) (decoder : Decoder<_>) fallback =
                        match optionalDecoder (at fieldNames value) decoder fallback v with
                        | Ok v -> v
                        | Error msg ->
                            failwith (errorToString msg)
                    member __.Index (requestedIndex: int) (decoder : Decoder<_>) fallback =
                        match optionalDecoder (index requestedIndex value) decoder fallback v with
                        | Ok v -> v
                        | Error msg ->
                            failwith (errorToString msg) }
        } |> Ok
