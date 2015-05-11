// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

namespace Newtonsoft.Json.Linq
{
    internal static class JsonExtensions
    {
        [SuppressMessage("Microsoft.Globalization", "CA1308:NormalizeStringsToUppercase", Justification="This is intentially normalized to lower-case to match JSON boolean formatting")]
        public static string ToDisplayString(this JToken self)
        {
            Guard.NotNull(self, "self");

            switch (self.Type)
            {
                case JTokenType.Boolean:
                    return self.ToString().ToLowerInvariant();
                case JTokenType.Null:
                    return "null";
                default:
                    return self.ToString();
            }
        }
    }
}
