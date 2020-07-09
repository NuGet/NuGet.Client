// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using NuGet.Versioning;
using System;

namespace NuGet.Protocol
{
    public class NuGetVersionConverter : JsonConverter
    {
        private readonly MetadataReferenceCache _metadataReferenceCache;

        public NuGetVersionConverter() { }

        public NuGetVersionConverter(MetadataReferenceCache metadataReferenceCache)
        {
            _metadataReferenceCache = metadataReferenceCache;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value.ToString());
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType != JsonToken.Null)
            {
                var versionString = serializer.Deserialize<string>(reader);
                var version = _metadataReferenceCache == null ? NuGetVersion.Parse(versionString)
                    : _metadataReferenceCache.GetVersion(_metadataReferenceCache.GetString(versionString));
                return version;
            }
            else
            {
                return null;
            }
        }

        public override bool CanConvert(Type objectType) => objectType == typeof(NuGetVersion);
    }
}
