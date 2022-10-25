// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Linq;

namespace NuGet.CommandLine.XPlat
{
    internal static class EnumExtensions
    {
        public static string GetValueList<T>() where T : Enum
        {
            var enumValues = ((T[])Enum.GetValues(typeof(T)))
               .Select(x => x.ToString());

            return string.Join(", ", enumValues).ToLower(CultureInfo.CurrentCulture);
        }
    }
}
