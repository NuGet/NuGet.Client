// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NuGet.Protocol.Converters
{
    /// <summary>
    /// A System.Text.Json converter for metadata fields that can be either a string or an array of strings.
    /// Converts arrays to comma-separated strings.
    /// </summary>
    internal sealed class MetadataFieldStjConverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return string.Empty;
            }

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                var values = new List<string>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    if (reader.TokenType == JsonTokenType.String)
                    {
                        string? value = reader.GetString();

                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            values.Add(value!);
                        }
                    }
                }
                return string.Join(", ", values);
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                return reader.GetString() ?? string.Empty;
            }

            return string.Empty;
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
