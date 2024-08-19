// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;
using NuGet.Versioning;

namespace NuGet.Protocol.Converters
{
    internal class PackageDependencyConverter : JsonConverter<Packaging.Core.PackageDependency>
    {
        public override bool CanWrite => false;

        public override Packaging.Core.PackageDependency ReadJson(JsonReader reader, Type objectType, Packaging.Core.PackageDependency existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            string id = null;
            VersionRange version = null;

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
                        var versionString = reader.ReadAsString();
                        if (!string.IsNullOrEmpty(versionString))
                        {
                            version = serializer.Deserialize<VersionRange>(reader);
                        }
                    }
                }
            }
            if (id != null)
            {
                return new Packaging.Core.PackageDependency(id, version);
            }
            return null;
        }

        public override void WriteJson(JsonWriter writer, Packaging.Core.PackageDependency value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
