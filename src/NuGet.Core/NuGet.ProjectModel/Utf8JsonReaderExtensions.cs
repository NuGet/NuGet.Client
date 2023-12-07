// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Buffers;
using System.Text;
using System.Text.Json;

namespace NuGet.ProjectModel
{
    internal static class Utf8JsonReaderExtensions
    {
        private static readonly UTF8Encoding Utf8Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        internal static string ReadTokenAsString(this ref Utf8JsonReader reader)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.True:
                    return bool.TrueString;
                case JsonTokenType.False:
                    return bool.FalseString;
                case JsonTokenType.Number:
                    var span = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan;
#if NETCOREAPP
                    return Utf8Encoding.GetString(span);
#else
                    return Utf8Encoding.GetString(span.ToArray());
#endif
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
