// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;
using NuGet.Versioning;

namespace NuGet.Protocol
{
    public class PackageDependencyConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(Packaging.Core.PackageDependency);

        public override bool CanWrite => false;

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            string id = null;
            string version = null;

            while (reader.Read() && reader.TokenType != JsonToken.EndObject)
            {
                if (reader.TokenType.Equals(JsonToken.PropertyName))
                {
                    if (reader.Value.Equals(JsonProperties.PackageId))
                    {
                        id = reader.ReadAsString();
                    }
                    else if (reader.Value.Equals(JsonProperties.Range))
                    {
                        version = id = reader.ReadAsString();
                    }
                }
            }
            if (id != null)
            {
                return new Packaging.Core.PackageDependency(id, string.IsNullOrEmpty(version) ? null : VersionRange.Parse(version));
            }
            return null;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
