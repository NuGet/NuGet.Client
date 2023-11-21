// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NuGet.ProjectModel
{
    internal static class Utf8JsonReaderExtensions
    {
        private static readonly char[] DelimitedStringDelimiters = new char[] { ' ', ',' };

        internal static IReadOnlyList<string> ReadDelimitedString(this ref Utf8JsonReader reader)
        {
            if (ReadNextToken(ref reader))
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.String:
                        var value = reader.GetString();

                        return value.Split(DelimitedStringDelimiters, StringSplitOptions.RemoveEmptyEntries);

                    default:
                        var invalidCastException = new InvalidCastException();
                        throw new JsonException(invalidCastException.Message, invalidCastException);
                }
            }

            return null;
        }

        internal static bool ReadNextToken(this ref Utf8JsonReader reader)
        {
            bool wasRead;

            while ((wasRead = reader.Read()) && reader.TokenType == JsonTokenType.Comment)
            {
            }

            return wasRead;
        }

        internal static bool ReadNextTokenAsBoolOrFalse(this ref Utf8JsonReader reader)
        {
            if (reader.ReadNextToken() && (reader.TokenType == JsonTokenType.False || reader.TokenType == JsonTokenType.True))
            {
                return reader.GetBoolean();
            }
            return false;
        }

        internal static string ReadNextTokenAsString(this ref Utf8JsonReader reader)
        {
            if (ReadNextToken(ref reader))
            {
                return ReadTokenAsString(ref reader);
            }

            return null;
        }

        internal static IList<T> ReadObjectAsList<T>(this ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                return new List<T>(0);

            }
            //We use JsonObjects for the arrays so we advance to the first property in the object which is the name/ver of the first library
            reader.ReadNextToken();

            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return new List<T>(0);
            }

            var objectConverter = (JsonConverter<T>)options.GetConverter(typeof(T));
            var listObjects = new List<T>();
            do
            {
                listObjects.Add(objectConverter.Read(ref reader, typeof(T), options));
                //At this point we're looking at the EndObject token for the object, need to advance.
                reader.ReadNextToken();
            }
            while (reader.TokenType != JsonTokenType.EndObject);
            return listObjects;
        }

        internal static List<string> ReadNextStringArrayAsList(this ref Utf8JsonReader reader, List<string> strings = null)
        {
            ReadNextToken(ref reader);
            return ReadStringArrayAsList(ref reader, strings);
        }

        internal static List<string> ReadStringArrayAsList(this ref Utf8JsonReader reader, List<string> strings = null)
        {
            if (reader.TokenType == JsonTokenType.StartArray)
            {
                while (ReadNextToken(ref reader) && reader.TokenType != JsonTokenType.EndArray)
                {
                    string value = ReadTokenAsString(ref reader);

                    strings = strings ?? new List<string>();

                    strings.Add(value);
                }
            }

            return strings;
        }


        internal static IList<string> ReadStringArrayAsIList(this ref Utf8JsonReader reader, IList<string> strings = null)
        {
            if (reader.TokenType == JsonTokenType.StartArray)
            {
                while (ReadNextToken(ref reader) && reader.TokenType != JsonTokenType.EndArray)
                {
                    string value = ReadTokenAsString(ref reader);

                    strings = strings ?? new List<string>();

                    strings.Add(value);
                }
            }

            return strings;
        }

        internal static void ReadArrayOfObjects<T1, T2>(this ref Utf8JsonReader reader, JsonSerializerOptions options, IList<T2> objectList) where T1 : T2
        {
            if (objectList is null)
            {
                return;
            }

            var type = typeof(T1);
            var objectConverter = (JsonConverter<T1>)options.GetConverter(type);

            if (ReadNextToken(ref reader) && reader.TokenType == JsonTokenType.StartArray)
            {
                while (ReadNextToken(ref reader) && reader.TokenType != JsonTokenType.EndArray)
                {
                    var convertedObject = objectConverter.Read(ref reader, type, options);
                    if (convertedObject != null)
                    {
                        objectList.Add(convertedObject);
                    }
                }
            }

        }

        internal static IReadOnlyList<string> ReadNextStringOrArrayOfStringsAsReadOnlyList(this ref Utf8JsonReader reader)
        {
            if (ReadNextToken(ref reader))
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.String:
                        return new[] { (string)reader.GetString() };

                    case JsonTokenType.StartArray:
                        return ReadStringArrayAsReadOnlyListFromArrayStart(ref reader);

                    case JsonTokenType.StartObject:
                        return null;
                }
            }

            return null;
        }

        internal static IReadOnlyList<string> ReadStringArrayAsReadOnlyListFromArrayStart(this ref Utf8JsonReader reader)
        {
            List<string> strings = null;

            while (ReadNextToken(ref reader) && reader.TokenType != JsonTokenType.EndArray)
            {
                string value = ReadTokenAsString(ref reader);

                strings = strings ?? new List<string>();

                strings.Add(value);
            }

            return (IReadOnlyList<string>)strings ?? Array.Empty<string>();
        }

        public static string ReadTokenAsString(this ref Utf8JsonReader reader)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.True:
                case JsonTokenType.False:
                    return reader.GetBoolean().ToString();
                case JsonTokenType.Number:
                    if (reader.TryGetInt16(out short shortValue))
                    {
                        return shortValue.ToString();
                    }
                    if (reader.TryGetInt32(out int intValue))
                    {
                        return intValue.ToString();
                    }
                    else if (reader.TryGetInt64(out long longValue))
                    {
                        return longValue.ToString();
                    }
                    return reader.GetDouble().ToString();
                case JsonTokenType.String:
                    return reader.GetString();
                case JsonTokenType.None:
                case JsonTokenType.Null:
                    return null;
                default:
                    throw new InvalidCastException();
            }
        }
    }
}
