// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Configuration;

namespace NuGet.PackageManagement.UI
{
    internal static class PackageSourceMappingUtility
    {
        internal static void ConfigureNewPackageSourceMapping(
            UserAction? userAction, List<Tuple<string, string>>? addedPackages,
            PackageSourceMappingProvider sourceMappingProvider,
            IReadOnlyList<PackageSourceMappingSourceItem> existingPackageSourceMappingSourceItems)
        {
            if (userAction?.SourceMappingSourceName == null || addedPackages == null)
            {
                return;
            }

            Dictionary<string, IReadOnlyList<string>> patternsReadOnly = existingPackageSourceMappingSourceItems
                .ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)(pair.Patterns.Select(p => p.Pattern).ToList()));

            PackageSourceMapping packageSourceMapping = new(patternsReadOnly);

            // Expand all patterns/globs so we can later check if this package ID was already mapped.
            IEnumerable<string> addedPackageIds = addedPackages.Select(action => action.Item1);
            List<string> addedPackageIdsWithoutExistingMappings = new();
            foreach (string? addedPackageId in addedPackageIds)
            {
                IReadOnlyList<string> configuredSource = packageSourceMapping.GetConfiguredPackageSources(addedPackageId);
                if (configuredSource == null || configuredSource.Count == 0)
                {
                    addedPackageIdsWithoutExistingMappings.Add(addedPackageId);
                }
            }

            // Get all newly added package IDs that were not previously Source Mapped.
            // Always include the Package ID being installed since it takes precedence over any globbing.
            string[] packageIdsNeedingNewSourceMappings = addedPackageIdsWithoutExistingMappings
                .Union(new string[] { userAction.PackageId })
                .ToArray();

            CreateAndSavePackageSourceMappings(
                sourceName: userAction.SourceMappingSourceName,
                newPackageIdsToSourceMap: packageIdsNeedingNewSourceMappings,
                sourceMappingProvider,
                existingPackageSourceMappingSourceItems);
        }

        private static void CreateAndSavePackageSourceMappings(
            string sourceName,
            string[] newPackageIdsToSourceMap,
            PackageSourceMappingProvider mappingProvider,
            IReadOnlyList<PackageSourceMappingSourceItem> existingPackageSourceMappingSourceItems)
        {
            if (string.IsNullOrWhiteSpace(sourceName) || newPackageIdsToSourceMap is null || newPackageIdsToSourceMap.Length == 0)
            {
                return;
            }

            IEnumerable<PackagePatternItem> newPackagePatternItems = newPackageIdsToSourceMap.Select(packageId => new PackagePatternItem(packageId));

            List<PackageSourceMappingSourceItem> newAndExistingPackageSourceMappingItems = new(existingPackageSourceMappingSourceItems);

            PackageSourceMappingSourceItem packageSourceMappingItemForSource =
                        existingPackageSourceMappingSourceItems
                        .Where(mappingItem => mappingItem.Key == sourceName)
                        .FirstOrDefault();

            // Source is being mapped for the first time.
            if (packageSourceMappingItemForSource is null)
            {
                packageSourceMappingItemForSource = new PackageSourceMappingSourceItem(sourceName, newPackagePatternItems);
                newAndExistingPackageSourceMappingItems.Add(packageSourceMappingItemForSource);
            }
            else // Source already had an existing mapping.
            {
                foreach (PackagePatternItem newPattern in newPackagePatternItems)
                {
                    if (!packageSourceMappingItemForSource.Patterns.Contains(newPattern))
                    {
                        packageSourceMappingItemForSource.Patterns.Add(newPattern);
                    }
                }
            }

            mappingProvider.SavePackageSourceMappings(newAndExistingPackageSourceMappingItems);
        }
    }
}
