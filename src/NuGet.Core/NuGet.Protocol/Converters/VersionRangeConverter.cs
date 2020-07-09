// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using NuGet.Versioning;
using System;

namespace NuGet.Protocol
{
    /// <summary>
    /// A VersionRange JSON converter.
    /// </summary>
    public class VersionRangeConverter : JsonConverter
    {
        private readonly MetadataReferenceCache _metadataReferenceCache;

        public VersionRangeConverter() { }

        public VersionRangeConverter(MetadataReferenceCache metadataReferenceCache)
        {
            _metadataReferenceCache = metadataReferenceCache;
        }

        /// <summary>
        /// Gets a flag indicating whether or not a type is convertible.
        /// </summary>
        /// <param name="objectType">An object type to check.</param>
        /// <returns><c>true</c> if <paramref name="objectType" /> is convertible; otherwise <c>false</c>.</returns>
        public override bool CanConvert(Type objectType) => objectType == typeof(VersionRange);

        /// <summary>
        /// Reads the JSON representation of the object.
        /// </summary>
        /// <param name="reader">A JSON reader.</param>
        /// <param name="objectType">The type of the object.</param>
        /// <param name="existingValue">The existing value of the object.</param>
        /// <param name="serializer">A serializer.</param>
        /// <returns>A <see cref="VersionRange" /> object.</returns>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType != JsonToken.Null)
            {
                var versionRangeString = serializer.Deserialize<string>(reader);
                var version = _metadataReferenceCache == null ? VersionRange.Parse(versionRangeString)
                    : _metadataReferenceCache.GetVersionRange(_metadataReferenceCache.GetString(versionRangeString));
                return version;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Writes the JSON representation of the object.
        /// </summary>
        /// <param name="writer">A JSON writer.</param>
        /// <param name="value">A value to serialize.</param>
        /// <param name="serializer">A serializer.</param>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var versionRange = VersionRange.Parse(value.ToString());
            serializer.Serialize(writer, versionRange.ToString());
        }
    }
}
