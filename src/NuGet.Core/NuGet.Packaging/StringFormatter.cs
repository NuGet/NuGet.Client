// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Globalization;

namespace NuGet.Packaging
{
    internal static class StringFormatter
    {
        internal static string Log_InstallingPackage(
            string packageId,
            string packageVersion,
            string source)
        {
            return string.Format(CultureInfo.CurrentCulture,
                Strings.Log_InstallingPackage,
                packageId,
                packageVersion,
                source);
        }
    }
}
