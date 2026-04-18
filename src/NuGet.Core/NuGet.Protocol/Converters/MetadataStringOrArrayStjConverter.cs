// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NuGet.Protocol.Converters
{
    /// <summary>
    /// Reads a JSON string or array of strings into an <see cref="IReadOnlyList{T}"/> of strings.
    /// Equivalent to <see cref="MetadataStringOrArrayConverter"/> for System.Text.Json.
    /// </summary>
    /// <remarks>NSJ equivalent: <see cref="MetadataStringOrArrayConverter"/>.</remarks>
    internal sealed class MetadataStringOrArrayStjConverter : JsonConverter<IReadOnlyList<string>>
    {
        public override IReadOnlyList<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var str = reader.GetString();
                return string.IsNullOrWhiteSpace(str) ? null : new[] { str! };
            }

            var values = new List<string>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                values.Add(reader.GetString() ?? string.Empty);
            }
            return values.ToArray();
        }

        public override void Write(Utf8JsonWriter writer, IReadOnlyList<string> value, JsonSerializerOptions options)
            => throw new NotSupportedException();
    }
}
