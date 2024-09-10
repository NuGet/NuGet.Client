// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;
using NuGet.Frameworks;

namespace NuGet.Protocol.Converters
{
    internal class NuGetFrameworkConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value.ToString());
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var value = reader.Value.ToString();
            var framework = NuGetFramework.AnyFramework;

            if (!string.IsNullOrEmpty(value))
            {
                framework = NuGetFramework.Parse(value);
            }

            return framework;
        }

        public override bool CanConvert(Type objectType) => objectType == typeof(NuGetFramework);
    }
}
