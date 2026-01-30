// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NuGet.Protocol.Converters
{
    /// <summary>
    /// A System.Text.Json converter for URIs that safely handles invalid URIs.
    /// </summary>
    internal sealed class SafeUriStjConverter : JsonConverter<Uri>
    {
        public override Uri? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                string? uriString = reader.GetString();
                if (!string.IsNullOrWhiteSpace(uriString))
                {
                    if (Uri.TryCreate(uriString!.Trim(), UriKind.Absolute, out var uri))
                    {
                        return uri;
                    }
                }
                return null;
            }

            // Skip any other token type
            return null;
        }

        public override void Write(Utf8JsonWriter writer, Uri value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
