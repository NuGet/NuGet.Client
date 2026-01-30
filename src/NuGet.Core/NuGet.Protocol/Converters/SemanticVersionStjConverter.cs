// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using NuGet.Versioning;

namespace NuGet.Protocol.Converters
{
    /// <summary>
    /// A System.Text.Json converter for SemanticVersion.
    /// </summary>
    internal sealed class SemanticVersionStjConverter : JsonConverter<SemanticVersion>
    {
        public override SemanticVersion? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException("Expected string value for SemanticVersion");
            }

            string? versionString = reader.GetString();
            return string.IsNullOrWhiteSpace(versionString) ? null : SemanticVersion.Parse(versionString!);
        }

        public override void Write(Utf8JsonWriter writer, SemanticVersion value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteStringValue(value.ToString());
            }
        }
    }
}
