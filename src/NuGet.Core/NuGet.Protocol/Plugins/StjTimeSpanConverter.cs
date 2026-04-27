// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NuGet.Protocol.Plugins
{
    internal sealed class StjTimeSpanConverter : JsonConverter<TimeSpan>
    {
        // NSJ serializes TimeSpan using TimeSpan.ToString("c") which gives "[-][d.]hh:mm:ss[.fffffff]" format.
        // We use the same format for wire compatibility.
        public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var str = reader.GetString();
            return TimeSpan.Parse(str, CultureInfo.InvariantCulture);
        }

        public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToString("c"));
    }
}
