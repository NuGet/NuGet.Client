// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Configuration;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging;
using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement.UI
{
    internal static class PackageSourceMappingUtility
    {
        /// <summary>
        /// Determines if new package source mappings should be written to the <paramref name="sourceMappingProvider"/> which is returned as a count.
        /// </summary>
        /// <param name="userAction"></param>
        /// <param name="addedPackageIds"></param>
        /// <param name="sourceMappingProvider"></param>
        /// <param name="existingPackageSourceMappingSourceItems"></param>
        /// <param name="globalPackageFolderPaths">Global Package Folder paths</param>
        /// <param name="countCreatedTopLevelSourceMappings">For Top-Level packages: <see langword="null" /> if not applicable; 0 if none needed to be added; > 0 is the count of new package source mappings added.</param>
        /// <param name="countCreatedTransitiveSourceMappings">For Transitive packages: <see langword="null" /> if not applicable; 0 if none needed to be added; > 0 is the count of new package source mappings added.</param>
        internal static void ConfigureNewPackageSourceMapping(
            UserAction? userAction,
            IReadOnlyList<Tuple<string, string>>? addedPackages,
            PackageSourceMappingProvider sourceMappingProvider,
            IReadOnlyList<PackageSourceMappingSourceItem> existingPackageSourceMappingSourceItems,
            IReadOnlyList<SourceRepository>? globalPackageFolders,
            IReadOnlyList<string> enabledPackageSourceNames,
            out int? countCreatedTopLevelSourceMappings,
            out int? countCreatedTransitiveSourceMappings)
        {
            countCreatedTopLevelSourceMappings = null;
            countCreatedTransitiveSourceMappings = null;

            if (userAction?.SelectedSourceName is null || addedPackages is null)
            {
                return;
            }

            countCreatedTopLevelSourceMappings = 0;
            countCreatedTransitiveSourceMappings = 0;

            //IEnumerable<string>? globalPackageFolderNames = null;
            string? globalPackageFolderName = null;
            string topLevelPackageId = userAction.PackageId;
            Dictionary<string, IReadOnlyList<string>> patternsReadOnly = existingPackageSourceMappingSourceItems
                .ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)(pair.Patterns.Select(p => p.Pattern).ToList()));

            PackageSourceMapping packageSourceMapping = new(patternsReadOnly);

            // Expand all patterns/globs so we can later check if this package ID was already mapped.
            List<string> addedPackageIdsWithoutExistingMappings = new(capacity: addedPackages.Count + 1);

            foreach (Tuple<string, string> addedPackage in addedPackages)
            {
                string addedPackageId = addedPackage.Item1;
                string addedPackageVersion = addedPackage.Item2;
                IReadOnlyList<string> configuredSource = packageSourceMapping.GetConfiguredPackageSources(addedPackageId);

                // Top-level package was looked up.
                if (addedPackageId == topLevelPackageId)
                {
                    // The top-level package is not already mapped to the selected source.
                    if (configuredSource.Count == 0 || !configuredSource.Contains(userAction.SelectedSourceName))
                    {
                        countCreatedTopLevelSourceMappings = 1;
                        addedPackageIdsWithoutExistingMappings.Add(topLevelPackageId);
                    }
                }
                // Transitive package was looked up.
                else if (configuredSource.Count == 0)
                {
                    // Check whether the package exists in the GPF.
                    if (globalPackageFolderName == null && globalPackageFolders != null)
                    {
                        globalPackageFolderName = globalPackageFolders.Where(folder => folder.PackageSource.IsLocal).FirstOrDefault()?.PackageSource.Name;
                    }

                    if (globalPackageFolderName != null)
                    {
                        string? sourceFoundInGlobalPackageFolder = AddNewSourceMappingsFromGlobalPackagesFolder(addedPackageId, addedPackageVersion, globalPackageFolderName, enabledPackageSourceNames);

                        if (string.IsNullOrEmpty(sourceFoundInGlobalPackageFolder))
                        {
                            // Wasn't able to check GPF.
                        }
                        else if (enabledPackageSourceNames.Contains(sourceFoundInGlobalPackageFolder, StringComparer.Ordinal))
                        {
                            // Map to the GPF source.
                            // ...
                        }
                        else // Map to the selected source.
                        {
                            addedPackageIdsWithoutExistingMappings.Add(addedPackageId);
                        }
                    }
                }
            }

            countCreatedTransitiveSourceMappings = addedPackageIdsWithoutExistingMappings.Count - countCreatedTopLevelSourceMappings;

            CreateAndSavePackageSourceMappings(
                userAction.SelectedSourceName,
                addedPackageIdsWithoutExistingMappings,
                sourceMappingProvider,
                existingPackageSourceMappingSourceItems);
        }

        private static string? AddNewSourceMappingsFromGlobalPackagesFolder(string packageId, string packageVersion, string globalPackageFolderName, IReadOnlyList<string> enabledPackageSourceNames) //IEnumerable<string>? globalPackageFolderNames)
        {
            if (string.IsNullOrEmpty(packageId))
            {
                throw new ArgumentNullException(nameof(packageId));
            }

            if (string.IsNullOrEmpty(packageVersion))
            {
                throw new ArgumentNullException(nameof(packageVersion));
            }

            if (string.IsNullOrEmpty(globalPackageFolderName) || enabledPackageSourceNames is null || enabledPackageSourceNames.Count == 0)
            {
                return null;
            }

            try
            {
                var resolver = new VersionFolderPathResolver(globalPackageFolderName);
                //var hashPath = resolver.GetHashPath("nuget.versioning", NuGetVersion.Parse("1.0.7"));
                string nupkgMetadataPath = resolver.GetNupkgMetadataPath(packageId, Versioning.NuGetVersion.Parse(packageVersion));
                NupkgMetadataFile nupkgMetadata = NupkgMetadataFileFormat.Read(nupkgMetadataPath);
                if (string.IsNullOrEmpty(nupkgMetadata.Source))
                {
                    return null;
                }

                return nupkgMetadata.Source;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void CreateAndSavePackageSourceMappings(
            string sourceName,
            List<string> newPackageIdsToSourceMap,
            PackageSourceMappingProvider mappingProvider,
            IReadOnlyList<PackageSourceMappingSourceItem> existingPackageSourceMappingSourceItems)
        {
            if (string.IsNullOrWhiteSpace(sourceName) || newPackageIdsToSourceMap is null || newPackageIdsToSourceMap.Count == 0)
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

        internal static string? GetNewSourceMappingSourceName(PackageSourceMapping packageSourceMapping, PackageSourceMoniker activePackageSourceMoniker)
        {
            string? sourceMappingSourceName = packageSourceMapping.IsEnabled
                && !activePackageSourceMoniker.IsAggregateSource
                ? activePackageSourceMoniker.PackageSourceNames.First() : null;

            return sourceMappingSourceName;
        }

        internal static void AddNewSourceMappingsFromAddedPackages(ref Dictionary<string, SortedSet<string>>? newSourceMappings, string newMappingSourceName, List<AccessiblePackageIdentity> added, PackageSourceMapping packageSourceMapping)
        {
            if (newMappingSourceName is null || added.Count == 0 || packageSourceMapping is null)
            {
                return;
            }

            foreach (AccessiblePackageIdentity addedPackage in added)
            {
                IReadOnlyList<string> configuredSources = packageSourceMapping.GetConfiguredPackageSources(packageId: addedPackage.Id);

                if (configuredSources.Count > 0)
                {
                    continue;
                }

                if (newSourceMappings is null)
                {
                    newSourceMappings = new Dictionary<string, SortedSet<string>>(capacity: 1)
                    {
                        { newMappingSourceName, new SortedSet<string>(new List<string>(capacity: added.Count) { addedPackage.Id }) }
                    };
                }
                else if (newSourceMappings.TryGetValue(newMappingSourceName, out SortedSet<string>? newMappingPackageIds))
                {
                    newMappingPackageIds.Add(addedPackage.Id);
                }
                else
                {
                    newSourceMappings.Add(newMappingSourceName, new SortedSet<string>(new List<string>(capacity: added.Count) { addedPackage.Id }));
                }
            }
        }
    }
}
