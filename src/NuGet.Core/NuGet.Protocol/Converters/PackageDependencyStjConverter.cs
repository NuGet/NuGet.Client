// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.Protocol.Converters
{
    /// <remarks>
    /// No explicit NSJ equivalent — NSJ relies on <c>[JsonConstructor]</c> and <c>[JsonProperty]</c> attributes
    /// on <see cref="PackageDependency"/> in NuGet.Packaging. STJ ignores those attributes, requiring this converter.
    /// </remarks>
    internal sealed class PackageDependencyStjConverter : JsonConverter<PackageDependency>
    {
        private static readonly VersionRangeStjConverter _versionRangeConverter = new();

        public override PackageDependency Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            string? id = null;
            VersionRange? range = null;

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    continue;
                }

                var propName = reader.GetString();
                reader.Read();

                if (string.Equals(propName, JsonProperties.PackageId, StringComparison.OrdinalIgnoreCase))
                {
                    id = reader.GetString();
                }
                else if (string.Equals(propName, JsonProperties.Range, StringComparison.OrdinalIgnoreCase))
                {
                    if (reader.TokenType != JsonTokenType.Null)
                    {
                        range = _versionRangeConverter.Read(ref reader, typeof(VersionRange), options);
                    }
                }
                else
                {
                    reader.Skip();
                }
            }

            return new PackageDependency(id!, range);
        }

        public override void Write(Utf8JsonWriter writer, PackageDependency value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString(JsonProperties.PackageId, value.Id);
            writer.WritePropertyName(JsonProperties.Range);
            _versionRangeConverter.Write(writer, value.VersionRange, options);
            writer.WriteEndObject();
        }
    }
}
