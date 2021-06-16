// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.PackageManagement
{
    internal static class StringFormatter
    {
        internal static string Log_PackageNamespaceMatchFound(
            string packageId,
            string packageSourcesAtPrefix)
        {
            return string.Format(Strings.PackageNamespaceMatchFound,
                packageId,
                packageSourcesAtPrefix
                );
        }

        internal static string Log_PackageNamespaceNoMatchFound(
            string packageId)
        {
            return string.Format(Strings.PackageNamespaceNoMatchFound,
                packageId
                );
        }
    }
}
