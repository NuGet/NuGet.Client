// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Globalization;

namespace NuGet.CommandLine.XPlat
{
    internal static class Messages
    {
        internal static string Error_NoVersionsAvailable(string packageId)
        {
            return string.Format(CultureInfo.CurrentCulture, Strings.Error_NoVersionsAvailable, packageId);
        }
    }
}
