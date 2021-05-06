// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Configuration;

namespace NuGet.Protocol
{
    public static class NameSpaceLookupUtility
    {
        public static INameSpaceLookup ConstructNameSpaceLookup(ISettings settings)
        {
            INameSpaceLookup nameSpaceLookup = null;

            if (settings == null)
                return nameSpaceLookup;

            PackageNamespacesConfiguration configuration = PackageNamespacesConfiguration.GetPackageNamespacesConfiguration(settings);
            var packageSourceSections = new List<PackageSourceSection>();

            foreach (var packageSourceKey in configuration.Namespaces.Keys)
            {
                string[] nugetNamespaces = configuration.Namespaces[packageSourceKey].ToArray();
                packageSourceSections.Add(new PackageSourceSection(nugetNamespaces, packageSourceKey));
            }

            if (packageSourceSections.Any())
            {
                nameSpaceLookup = new SearchTree(packageSourceSections);
            }

            return nameSpaceLookup;
        }
    }
}
