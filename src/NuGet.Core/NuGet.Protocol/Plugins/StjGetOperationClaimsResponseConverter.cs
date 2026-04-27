// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NuGet.Protocol.Plugins
{
    internal sealed class StjGetOperationClaimsResponseConverter : JsonConverter<GetOperationClaimsResponse>
    {
        public override GetOperationClaimsResponse Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            OperationClaim[]? claims = null;

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType == JsonTokenType.PropertyName &&
                    reader.ValueTextEquals(nameof(GetOperationClaimsResponse.Claims)))
                {
                    reader.Read();
                    claims = JsonSerializer.Deserialize(ref reader, PluginJsonContext.Default.OperationClaimArray);
                }
                else
                {
                    reader.Skip();
                }
            }

            return new GetOperationClaimsResponse(claims!);
        }

        public override void Write(Utf8JsonWriter writer, GetOperationClaimsResponse value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WritePropertyName(nameof(GetOperationClaimsResponse.Claims));
            JsonSerializer.Serialize(writer, value.Claims, PluginJsonContext.Default.IReadOnlyListOperationClaim);
            writer.WriteEndObject();
        }
    }
}
