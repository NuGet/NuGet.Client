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
        /// Max allowed length for package Id.
        /// In case update this value please update in src\NuGet.Core\NuGet.Packaging\PackageCreation\Utility\PackageIdValidator.cs too.
        /// </summary>
        internal static int PackageIdMaxLength { get; } = 100;

        /// <summary>
        /// Source name to package namespace list.
        /// </summary>
        internal Dictionary<string, IReadOnlyList<string>> Namespaces { get; }

        private Lazy<SearchTree> SearchTree { get; }

        /// <summary>
        /// Indicate if any packageSource exist in package namespace section
        /// </summary>
        public bool AreNamespacesEnabled { get; }

        /// <summary>
        /// Tuple with number of sources and total number of entries defined with package namespaces, used for telemetry.
        /// Item1 is number of sources.
        /// Item2 is total number of of entries.
        /// </summary>
        public Tuple<int, int, List<string>> NamespacesMetrics { get; }

        /// <summary>
        /// Get package source names with matching prefix "term" from package namespaces section.
        /// </summary>
        /// <param name="term">Search term. Cannot be null, empty, or whitespace only. </param>
        /// <returns>Package source names with matching prefix "term" from package namespaces.</returns>
        /// <exception cref="ArgumentException"> if <paramref name="term"/> is null, empty, or whitespace only.</exception>
        public IReadOnlyList<string> GetConfiguredPackageSources(string term)
        {
            return SearchTree.Value?.GetConfiguredPackageSources(term);
        }

        internal PackageNamespacesConfiguration(Dictionary<string, IReadOnlyList<string>> namespaces)
        {
            Namespaces = namespaces ?? throw new ArgumentNullException(nameof(namespaces));
            AreNamespacesEnabled = Namespaces.Keys.Count > 0;
            SearchTree = new Lazy<SearchTree>(() => GetSearchTree());

            if (AreNamespacesEnabled)
            {
                NamespacesMetrics = new Tuple<int, int, List<string>>(Namespaces.Keys.Count, Namespaces.Values.Sum(v => v.Count), Namespaces.Keys.ToList());
            }
            else
            {
                NamespacesMetrics = new Tuple<int, int, List<string>>(0, 0, null);
            }
        }

        /// <summary>
        /// Generates a <see cref="PackageNamespacesConfiguration"/> based on the settings object.
        /// </summary>
        /// <param name="settings">Search term. Cannot be null, empty, or whitespace only. </param>
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

            foreach (PackageNamespacesSourceItem packageSourceNamespaceItem in packageNamespacesProvider.GetPackageSourceNamespaces())
            {
                namespaces.Add(packageSourceNamespaceItem.Key, new List<string>(packageSourceNamespaceItem.Namespaces.Select(e => e.Id)));
            }

            return new PackageNamespacesConfiguration(namespaces);
        }

        private SearchTree GetSearchTree()
        {
            SearchTree namespaceLookup = null;

            if (AreNamespacesEnabled)
            {
                namespaceLookup = new SearchTree(this);
            }

            return namespaceLookup;
        }
    }
}
