// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.Utility
{
    public static class TestExtensions
    {
        public static int GetSubstringCount(this string str, string substr, bool ignoreCase)
        {
            var splitChars = new char[] { '.', '?', '!', ' ', ';', ':', ',', '\r', '\n' };
            var words = str.Split(splitChars, StringSplitOptions.RemoveEmptyEntries);
            var comparisonType = ignoreCase ?
                StringComparison.OrdinalIgnoreCase :
                StringComparison.Ordinal;

            return words
                .Where(word => string.Equals(word, substr, comparisonType))
                .Count();
        }
    }
}
