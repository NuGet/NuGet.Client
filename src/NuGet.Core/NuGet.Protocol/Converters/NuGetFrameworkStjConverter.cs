// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using NuGet.Frameworks;

namespace NuGet.Protocol.Converters
{
    internal class NuGetFrameworkStjConverter : JsonConverter<NuGetFramework>
    {
        public override NuGetFramework Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException("Expected string value for NuGetVersion");
            }

            var value = reader.GetString();
            var framework = NuGetFramework.AnyFramework;

            if (!string.IsNullOrEmpty(value))
            {
                framework = NuGetFramework.Parse(value);
            }

            return framework;
        }

        public override void Write(Utf8JsonWriter writer, NuGetFramework value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
