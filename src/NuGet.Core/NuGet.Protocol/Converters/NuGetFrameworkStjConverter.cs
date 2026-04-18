// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using NuGet.Frameworks;

namespace NuGet.Protocol.Converters
{
    /// <remarks>NSJ equivalent: <see cref="NuGetFrameworkConverter"/> (registered globally in <see cref="JsonExtensions.ObjectSerializationSettings"/>).</remarks>
    internal sealed class NuGetFrameworkStjConverter : JsonConverter<NuGetFramework>
    {
        public override bool HandleNull => true;
        public override NuGetFramework Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return NuGetFramework.AnyFramework;
            }

            var value = reader.GetString();
            return string.IsNullOrEmpty(value) ? NuGetFramework.AnyFramework : NuGetFramework.Parse(value!);
        }

        public override void Write(Utf8JsonWriter writer, NuGetFramework value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.GetShortFolderName());
    }
}
