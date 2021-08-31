// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Configuration;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    internal static class PackageNamespacesUtility
    {
        public static bool AreNamespacesEnabled(ISettings settings)
        {
            var packageNamespacesConfiguration = PackageNamespacesConfiguration.GetPackageNamespacesConfiguration(settings);
            bool areNamespacesEnabled = packageNamespacesConfiguration?.AreNamespacesEnabled ?? false;
            return areNamespacesEnabled;
        }
    }
}
