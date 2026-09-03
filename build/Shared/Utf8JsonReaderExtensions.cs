// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace NuGet.Shared
{
    internal static class Utf8JsonReaderExtensions
    {
        internal static string? ReadTokenAsString(this ref Utf8JsonReader reader)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.True:
                    return bool.TrueString;
                case JsonTokenType.False:
                    return bool.FalseString;
                case JsonTokenType.Number:
                    return reader.ReadNumberAsString();
                case JsonTokenType.String:
                    return reader.GetString();
                case JsonTokenType.None:
                case JsonTokenType.Null:
                    return null;
                default:
                    throw new InvalidCastException();
            }
        }

        private static string ReadNumberAsString(this ref Utf8JsonReader reader)
        {
#if NET5_0_OR_GREATER
            return Encoding.UTF8.GetString(reader.ValueSpan);
#else
            return Encoding.UTF8.GetString(reader.ValueSpan.ToArray());
#endif
        }
    }

    internal static class JsonElementExtensions
    {
        internal static string GetRequiredString(this JsonElement json)
        {
            if (json.ValueKind != JsonValueKind.String)
            {
                throw new JsonException();
            }

            return json.GetString() ?? throw new JsonException();
        }

        internal static List<JsonProperty> GetUniqueProperties(this JsonElement json)
        {
            if (json.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException();
            }

            var properties = new List<JsonProperty>();
            var indexes = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (JsonProperty property in json.EnumerateObject())
            {
                if (indexes.TryGetValue(property.Name, out int index))
                {
                    properties[index] = property;
                }
                else
                {
                    indexes.Add(property.Name, properties.Count);
                    properties.Add(property);
                }
            }

            return properties;
        }
    }
}
