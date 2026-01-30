// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NuGet.Protocol.Converters
{
    /// <summary>
    /// A System.Text.Json converter for booleans that safely handles various input types.
    /// </summary>
    internal sealed class SafeBoolStjConverter : JsonConverter<bool>
    {
        public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Null:
                    return false;

                case JsonTokenType.True:
                    return true;

                case JsonTokenType.False:
                    return false;

                case JsonTokenType.String:
                    var stringValue = reader.GetString();
                    if (bool.TryParse(stringValue?.Trim(), out var boolResult))
                    {
                        return boolResult;
                    }
                    return false;

                case JsonTokenType.Number:
                    if (reader.TryGetInt64(out var longValue))
                    {
                        return longValue == 1;
                    }
                    return false;

                default:
                    return false;
            }
        }

        public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
