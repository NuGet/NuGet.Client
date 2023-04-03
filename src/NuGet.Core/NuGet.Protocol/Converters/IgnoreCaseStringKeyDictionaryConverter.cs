// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NuGet.Protocol.Model;

namespace NuGet.Protocol.Converters
{
    internal class VulnerabilityFileDataConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(VulnerabilityFileData);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            Dictionary<string, IReadOnlyList<VulnerabilityInfo>> result = new(StringComparer.InvariantCultureIgnoreCase);

            if (!reader.Read())
            {
                throw new JsonSerializationException("Unexpected end of data");
            }

            bool finished = false;
            do
            {
                switch (reader.TokenType)
                {
                    case JsonToken.EndObject:
                        finished = true;
                        break;

                    case JsonToken.PropertyName:
                        {
                            string? packageId = (string?)reader.Value;
                            reader.Read();
                            IReadOnlyList<VulnerabilityInfo>? info = serializer.Deserialize<IReadOnlyList<VulnerabilityInfo>>(reader);

                            if (packageId != null && info != null)
                            {
                                result.Add(packageId, info);
                            }
                        }
                        break;

                    default:
                        throw new JsonSerializationException("Unexpected token type");
                }
            } while (!finished && reader.Read());

            return new VulnerabilityFileData(result);
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            var data = (VulnerabilityFileData?)value;

            if (data != null)
            {
                writer.WriteStartObject();

                foreach (var kvp in data.OrderBy(kvp => kvp.Key, StringComparer.InvariantCultureIgnoreCase))
                {
#pragma warning disable CA1308 // Normalize strings to uppercase - NuGet has been using lower ids for years
                    string lowerKey = kvp.Key.ToLowerInvariant();
#pragma warning restore CA1308 // Normalize strings to uppercase
                    writer.WritePropertyName(lowerKey);
                    serializer.Serialize(writer, kvp.Value);
                }

                writer.WriteEndObject();
            }
        }
    }
}
