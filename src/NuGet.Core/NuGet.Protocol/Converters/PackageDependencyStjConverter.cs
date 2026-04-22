// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.Protocol.Converters
{
    /// <remarks>No NSJ equivalent.</remarks>
    internal sealed class PackageDependencyStjConverter : JsonConverter<PackageDependency>
    {
        private static readonly VersionRangeStjConverter VersionRangeConverter = new();

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
                        range = VersionRangeConverter.Read(ref reader, typeof(VersionRange), options);
                    }
                }
                else
                {
                    reader.Skip();
                }
            }

            if (string.IsNullOrEmpty(id))
            {
                throw new JsonException(string.Format(CultureInfo.CurrentCulture, Strings.Error_RequiredJsonPropertyMissing, JsonProperties.PackageId));
            }

            return new PackageDependency(id!, range);
        }

        public override void Write(Utf8JsonWriter writer, PackageDependency value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString(JsonProperties.PackageId, value.Id);
            writer.WritePropertyName(JsonProperties.Range);
            VersionRangeConverter.Write(writer, value.VersionRange, options);
            writer.WriteEndObject();
        }
    }
}
