// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text.Json;

namespace NuGet.ProjectModel
{
    internal static class Utf8JsonReaderExtensions
    {
        internal static string ReadTokenAsString(this ref Utf8JsonReader reader)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.True:
                    return bool.TrueString;
                case JsonTokenType.False:
                    return bool.FalseString;
                case JsonTokenType.Number:
                    if (reader.TryGetInt16(out short shortValue))
                    {
                        return shortValue.ToString();
                    }
                    if (reader.TryGetInt32(out int intValue))
                    {
                        return intValue.ToString();
                    }
                    else if (reader.TryGetInt64(out long longValue))
                    {
                        return longValue.ToString();
                    }
                    return reader.GetDouble().ToString();
                case JsonTokenType.String:
                    return reader.GetString();
                case JsonTokenType.None:
                case JsonTokenType.Null:
                    return null;
                default:
                    throw new InvalidCastException();
            }
        }
    }
}
