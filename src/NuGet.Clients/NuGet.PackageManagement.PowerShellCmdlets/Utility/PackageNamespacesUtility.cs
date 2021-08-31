// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Configuration;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    internal static class PackageNamespacesUtility
    {
        public static (bool areNamespacesEnabled, int numberOfSourcesWithNamespaces, int allEntryCountInNamespaces) GetPackageNamespacesMetric(ISettings settings)
        {
            var packageNamespacesConfiguration = PackageNamespacesConfiguration.GetPackageNamespacesConfiguration(settings);
            bool areNamespacesEnabled = packageNamespacesConfiguration?.AreNamespacesEnabled ?? false;
            int numberOfSourcesWithNamespaces = 0;
            int allEntryCountInNamespaces = 0;

            if (areNamespacesEnabled)
            {
                var (numberOfSourcesWithPackageNamespaces, allEntryCountInPackageNamespaces, _) = packageNamespacesConfiguration.NamespacesMetrics;
                numberOfSourcesWithNamespaces = numberOfSourcesWithPackageNamespaces;
                allEntryCountInNamespaces = allEntryCountInPackageNamespaces;
            }

            return (areNamespacesEnabled, numberOfSourcesWithNamespaces, allEntryCountInNamespaces);
        }
    }
}
