// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Protocol.Converters
{
    /// <remarks>
    /// NSJ equivalent: <see cref="VersionInfoConverter"/> (registered globally in <see cref="JsonExtensions.ObjectSerializationSettings"/>).
    /// Used by: <see cref="VersionInfo"/> entries.
    /// </remarks>
    internal sealed class VersionInfoStjConverter : JsonConverter<VersionInfo>
    {
        public override bool HandleNull => true;
        public override VersionInfo Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException(string.Format(CultureInfo.CurrentCulture, Strings.Error_UnexpectedJsonToken, reader.TokenType));
            }

            string? version = null;
            long? downloads = null;

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    continue;
                }

                var propName = reader.GetString();
                reader.Read();

                if (string.Equals(propName, JsonProperties.Version, StringComparison.Ordinal))
                {
                    version = reader.GetString();
                }
                else if (string.Equals(propName, "downloads", StringComparison.Ordinal))
                {
                    downloads = reader.TokenType == JsonTokenType.Null ? null : reader.GetInt64();
                }
                else
                {
                    reader.Skip();
                }
            }

            if (string.IsNullOrEmpty(version))
            {
                throw new JsonException(string.Format(CultureInfo.CurrentCulture, Strings.Error_RequiredJsonPropertyMissing, JsonProperties.Version));
            }

            return new VersionInfo(NuGetVersion.Parse(version!), downloads);
        }

        public override void Write(Utf8JsonWriter writer, VersionInfo value, JsonSerializerOptions options)
        {
            throw new NotSupportedException();
        }
    }
}
