// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using NuGet.Versioning;

namespace NuGet.Protocol.Plugins
{
    internal sealed class StjSemanticVersionConverter : JsonConverter<SemanticVersion>
    {
        public override SemanticVersion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var str = reader.GetString();
            if (str is null)
            {
                return null;
            }

            if (!SemanticVersion.TryParse(str, out var version))
            {
                throw new JsonException($"Invalid SemanticVersion: '{str}'");
            }

            return version;
        }

        public override void Write(Utf8JsonWriter writer, SemanticVersion value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToFullString());
    }
}
