// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using NuGet.Packaging.Core;

namespace NuGet.Protocol.Converters
{
    public class FingerprintsStjConverter : JsonConverter<Fingerprints>
    {
        public override Fingerprints? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            var dict = new Dictionary<string, string>();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return new Fingerprints(dict);
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException();
                }

                string? key = reader.GetString();
                reader.Read();
                string? value = reader.GetString();

                if (!string.IsNullOrEmpty(key) && !string.IsNullOrWhiteSpace(value))
                {
                    dict[key!] = value!;
                }
            }

            throw new JsonException();
        }

        public override void Write(Utf8JsonWriter writer, Fingerprints value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
