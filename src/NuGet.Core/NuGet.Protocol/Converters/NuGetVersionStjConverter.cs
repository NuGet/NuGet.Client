// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using NuGet.Versioning;

namespace NuGet.Protocol.Converters
{
    /// <remarks>NSJ equivalent: <see cref="NuGetVersionConverter"/> (registered globally in <see cref="JsonExtensions.ObjectSerializationSettings"/>).</remarks>
    internal sealed class NuGetVersionStjConverter : JsonConverter<NuGetVersion>
    {
        public override NuGetVersion? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var str = reader.GetString();
            return str is null ? null : NuGetVersion.Parse(str);
        }

        public override void Write(Utf8JsonWriter writer, NuGetVersion value, JsonSerializerOptions options)
        {
            if (value is null)
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
