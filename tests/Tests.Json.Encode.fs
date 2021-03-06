module Tests.Encode

#if FABLE_COMPILER
open Thoth.Json
#else
open Thoth.Json.Net
#endif
open Util.Testing
open System

type User =
    { Id : int
      Name : string
      Email : string
      followers : int }

type SmallRecord =
    { fieldA: string }

    static member Decoder =
        Decode.object (fun get ->
            { fieldA = get.Required.Field "fieldA" Decode.string }
        )

    static member Encoder x =
        Encode.object [
            "fieldA", Encode.string x.fieldA
        ]

let tests : Test =
    testList "Thoth.Json.Encode" [

        testList "Basic" [

            testCase "a string works" <| fun _ ->
                let expected = "\"maxime\""
                let actual =
                    Encode.string "maxime"
                    |> Encode.toString 0
                equal expected actual

            testCase "an int works" <| fun _ ->
                let expected = "1"
                let actual =
                    Encode.int 1
                    |> Encode.toString 0
                equal expected actual

            testCase "a float works" <| fun _ ->
                let expected = "1.2"
                let actual =
                    Encode.float 1.2
                    |> Encode.toString 0
                equal expected actual

            testCase "an array works" <| fun _ ->
                let expected =
                    """["maxime",2]"""
                let actual =
                    Encode.array
                        [| Encode.string "maxime"
                           Encode.int 2
                        |] |> Encode.toString 0
                equal expected actual

            testCase "a list works" <| fun _ ->
                let expected =
                    """["maxime",2]"""
                let actual =
                    Encode.list
                        [ Encode.string "maxime"
                          Encode.int 2
                        ] |> Encode.toString 0
                equal expected actual

            testCase "a bool works" <| fun _ ->
                let expected = "false"
                let actual =
                    Encode.bool false
                    |> Encode.toString 0
                equal expected actual

            testCase "a null works" <| fun _ ->
                let expected = "null"
                let actual =
                    Encode.nil
                    |> Encode.toString 0
                equal expected actual

            testCase "an object works" <| fun _ ->
                let expected =
                    """{"firstname":"maxime","age":25}"""
                let actual =
                    Encode.object
                        [ ("firstname", Encode.string "maxime")
                          ("age", Encode.int 25)
                        ] |> Encode.toString 0
                equal expected actual

            testCase "a dict works" <| fun _ ->
                let expected =
                    """{"a":1,"b":2,"c":3}"""
                let actual =
                    Map.ofList
                        [ ("a", Encode.int 1)
                          ("b", Encode.int 2)
                          ("c", Encode.int 3)
                        ]
                    |> Encode.dict
                    |> Encode.toString 0
                equal expected actual

            testCase "a bigint works" <| fun _ ->
                let expected = "\"12\""
                let actual =
                    Encode.bigint 12I
                    |> Encode.toString 0

                equal expected actual

            testCase "a datetime works" <| fun _ ->
                #if FABLE_COMPILER
                let expected = "\"2018-10-01T11:12:55.000Z\""
                #else
                let expected = "\"2018-10-01T11:12:55Z\""
                #endif
                let actual =
                    DateTime(2018, 10, 1, 11, 12, 55, DateTimeKind.Utc)
                    |> Encode.datetime
                    |> Encode.toString 0

                equal expected actual

            testCase "a datetimeOffset works" <| fun _ ->
                let expected = "\"2018-07-02T12:23:45+02:00\""
                let actual =
                    DateTimeOffset(2018, 7, 2, 12, 23, 45, 0, TimeSpan.FromHours(2.))
                    |> Encode.datetimeOffset
                    |> Encode.toString 0

                equal expected actual

            testCase "a decimal works" <| fun _ ->
                let expected = "0.7833"
                let actual =
                    0.7833M
                    |> Encode.decimal
                    |> Encode.toString 0

                equal expected actual

            testCase "a guid works" <| fun _ ->
                let expected = "\"1e5dee25-8558-4392-a9fb-aae03f81068f\""
                let actual =
                    Guid.Parse("1e5dee25-8558-4392-a9fb-aae03f81068f")
                    |> Encode.guid
                    |> Encode.toString 0

                equal expected actual

            testCase "a int64 works" <| fun _ ->
                let expected = "\"7923209\""
                let actual =
                    7923209L
                    |> Encode.int64
                    |> Encode.toString 0

                equal expected actual

            testCase "a uint64 works" <| fun _ ->
                let expected = "\"7923209\""
                let actual =
                    7923209UL
                    |> Encode.uint64
                    |> Encode.toString 0

                equal expected actual

            testCase "a tuple2 works" <| fun _ ->
                let expected = """[1,"maxime"]"""
                let actual =
                    Encode.tuple2
                        Encode.int
                        Encode.string
                        (1, "maxime")
                    |> Encode.toString 0

                equal expected actual

            testCase "a tuple3 works" <| fun _ ->
                let expected = """[1,"maxime",2.5]"""
                let actual =
                    Encode.tuple3
                        Encode.int
                        Encode.string
                        Encode.float
                        (1, "maxime", 2.5)
                    |> Encode.toString 0

                equal expected actual

            testCase "a tuple4 works" <| fun _ ->
                let expected = """[1,"maxime",2.5,{"fieldA":"test"}]"""
                let actual =
                    Encode.tuple4
                        Encode.int
                        Encode.string
                        Encode.float
                        SmallRecord.Encoder
                        (1, "maxime", 2.5, { fieldA = "test" })
                    |> Encode.toString 0

                equal expected actual

            testCase "a tuple5 works" <| fun _ ->
                #if FABLE_COMPILER
                let expected = """[1,"maxime",2.5,{"fieldA":"test"},"2018-10-01T11:12:55.000Z"]"""
                #else
                let expected = """[1,"maxime",2.5,{"fieldA":"test"},"2018-10-01T11:12:55Z"]"""
                #endif
                let actual =
                    Encode.tuple5
                        Encode.int
                        Encode.string
                        Encode.float
                        SmallRecord.Encoder
                        Encode.datetime
                        (1, "maxime", 2.5, { fieldA = "test" }, DateTime(2018, 10, 1, 11, 12, 55, DateTimeKind.Utc))
                    |> Encode.toString 0

                equal expected actual

            testCase "a tuple6 works" <| fun _ ->
                let expected = """[1,"maxime",2.5,{"fieldA":"test"},false,null]"""
                let actual =
                    Encode.tuple6
                        Encode.int
                        Encode.string
                        Encode.float
                        SmallRecord.Encoder
                        Encode.bool
                        (fun _ -> Encode.nil)
                        (1, "maxime", 2.5, { fieldA = "test" }, false, null)
                    |> Encode.toString 0

                equal expected actual

            testCase "a tuple7 works" <| fun _ ->
                let expected = """[1,"maxime",2.5,{"fieldA":"test"},false,null,true]"""
                let actual =
                    Encode.tuple7
                        Encode.int
                        Encode.string
                        Encode.float
                        SmallRecord.Encoder
                        Encode.bool
                        (fun _ -> Encode.nil)
                        Encode.bool
                        (1, "maxime", 2.5, { fieldA = "test" }, false, null, true)
                    |> Encode.toString 0

                equal expected actual

            testCase "a tuple8 works" <| fun _ ->
                let expected = """[1,"maxime",2.5,{"fieldA":"test"},false,null,true,98]"""
                let actual =
                    Encode.tuple8
                        Encode.int
                        Encode.string
                        Encode.float
                        SmallRecord.Encoder
                        Encode.bool
                        (fun _ -> Encode.nil)
                        Encode.bool
                        Encode.int
                        (1, "maxime", 2.5, { fieldA = "test" }, false, null, true, 98)
                    |> Encode.toString 0

                equal expected actual

            testCase "using pretty space works" <| fun _ ->
                let expected = "{\n    \"firstname\": \"maxime\",\n    \"age\": 25\n}"

                let actual =
                    Encode.object
                        [ ("firstname", Encode.string "maxime")
                          ("age", Encode.int 25)
                        ] |> Encode.toString 4
                equal expected actual

            testCase "complexe structure works" <| fun _ ->
                let expected =
                    "{\n    \"firstname\": \"maxime\",\n    \"age\": 25,\n    \"address\": {\n        \"street\": \"main road\",\n        \"city\": \"Bordeaux\"\n    }\n}"

                let actual =
                    Encode.object
                        [ ("firstname", Encode.string "maxime")
                          ("age", Encode.int 25)
                          ("address", Encode.object
                                        [ "street", Encode.string "main road"
                                          "city", Encode.string "Bordeaux"
                                        ])
                        ] |> Encode.toString 4
                equal expected actual

            testCase "option with a value `Some ...` works" <| fun _ ->
                let expected = """{"id":1,"operator":"maxime"}"""

                let actual =
                    Encode.object
                        [ ("id", Encode.int 1)
                          ("operator", Encode.option Encode.string (Some "maxime"))
                        ] |> Encode.toString 0

                equal expected actual

            testCase "option without a value `None` works" <| fun _ ->
                let expected = """{"id":1,"operator":null}"""

                let actual =
                    Encode.object
                        [ ("id", Encode.int 1)
                          ("operator", Encode.option Encode.string None)
                        ] |> Encode.toString 0

                equal expected actual

            testCase "by default, we keep the case defined in type" <| fun _ ->
                let expected =
                    """{"Id":0,"Name":"Maxime","Email":"mail@test.com","followers":33}"""
                let value =
                    { Id = 0
                      Name = "Maxime"
                      Email = "mail@test.com"
                      followers = 33 }

                let actual = Encode.Auto.toString(0, value)
                equal expected actual

            testCase "forceCamelCase works" <| fun _ ->
                let expected =
                    """{"id":0,"name":"Maxime","email":"mail@test.com","followers":33}"""
                let value =
                    { Id = 0
                      Name = "Maxime"
                      Email = "mail@test.com"
                      followers = 33 }

                let actual = Encode.Auto.toString(0, value, true)
                equal expected actual

        ]

    ]


// Encode.bigint
// Encode.datetime


// Encode.datetimeOffset
// Encode.decimal
// Encode.guid
// Encode.int64
// Encode.keyValuePairs
// Encode.list
// Encode.nil
// Encode.object
// Encode.tuple2
// Encode.tuple3
// Encode.tuple4
// Encode.tuple5
// Encode.tuple6
// Encode.tuple7
// Encode.tuple8
// Encode.uint64
