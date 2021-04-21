// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Configuration
{
    public class PackageNamespacesConfiguration
    {
        /// <summary>
        /// Source name to package namespace list.
        /// </summary>
        public Dictionary<string, IReadOnlyList<string>> Namespaces { get; }

        public PackageNamespacesConfiguration(Dictionary<string, IReadOnlyList<string>> namespaces)
        {
            Namespaces = namespaces ?? throw new ArgumentNullException(nameof(namespaces));
        }

        /// <summary>
        /// Generates a <see cref="PackageNamespacesConfiguration"/> based on the settings object.
        /// </summary>
        /// <param name="settings">Settings. Never null. </param>
        /// <returns>A <see cref="PackageNamespacesConfiguration"/> based on the settings.</returns>
        /// <exception cref="ArgumentNullException"> if <paramref name="settings"/> is null.</exception>
        public static PackageNamespacesConfiguration GetPackageNamespacesConfiguration(ISettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var packageNamespacesProvider = new PackageNamespacesProvider(settings);

            var namespaces = new Dictionary<string, IReadOnlyList<string>>();

            foreach (var packageSourceNamespaceItem in packageNamespacesProvider.GetPackageSourceNamespaces())
            {
                namespaces.Add(packageSourceNamespaceItem.Key, new List<string>(packageSourceNamespaceItem.Namespaces.Select(e => e.Id)));
            }

            return new PackageNamespacesConfiguration(namespaces);
        }
    }
}
