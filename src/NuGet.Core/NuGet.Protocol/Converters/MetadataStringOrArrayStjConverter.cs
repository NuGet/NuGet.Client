// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NuGet.Protocol.Converters
{
    /// <summary>
    /// A System.Text.Json converter for string arrays that can also read single strings.
    /// </summary>
    internal sealed class MetadataStringOrArrayStjConverter : JsonConverter<IReadOnlyList<string>>
    {
        public override IReadOnlyList<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                string? str = reader.GetString();

                return string.IsNullOrWhiteSpace(str) ? null : new string[] { str! };
            }

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                var values = new List<string>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    if (reader.TokenType == JsonTokenType.String)
                    {
                        string? str = reader.GetString();
                        if (!string.IsNullOrWhiteSpace(str))
                        {
                            values.Add(str!);
                        }
                    }
                }
                return values;
            }

            return null;
        }

        public override void Write(Utf8JsonWriter writer, IReadOnlyList<string> value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
