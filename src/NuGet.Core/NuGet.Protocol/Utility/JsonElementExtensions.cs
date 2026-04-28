// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Text.Json;

namespace NuGet.Protocol
{
    internal static class JsonElementExtensions
    {
        /// <summary>
        /// Returns string values from a <see cref="JsonElement"/> that is either a string or an array of strings.
        /// Non-string array elements are skipped. Returns an empty enumerable for all other value kinds.
        /// </summary>
        internal static IEnumerable<string> AsStrings(this JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String && item.GetString() is string value)
                    {
                        yield return value;
                    }
                }
            }
            else if (element.ValueKind == JsonValueKind.String && element.GetString() is string str)
            {
                yield return str;
            }
        }
    }
}
