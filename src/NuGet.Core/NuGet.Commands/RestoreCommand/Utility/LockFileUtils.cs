using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NuGet.Client;
using NuGet.ContentModel;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Repositories;

namespace NuGet.Commands
{
    internal static class LockFileUtils
    {
        public static LockFileTargetLibrary CreateLockFileTargetLibrary(
            LockFileLibrary library,
            LocalPackageInfo package,
            RestoreTargetGraph targetGraph,
            LibraryIncludeFlags dependencyType)
        {
            return CreateLockFileTargetLibrary(
                library,
                package,
                targetGraph,
                dependencyType: dependencyType,
                targetFrameworkOverride: null,
                dependencies: null);
        }

        public static LockFileTargetLibrary CreateLockFileTargetLibrary(
            LockFileLibrary library,
            LocalPackageInfo package,
            RestoreTargetGraph targetGraph,
            LibraryIncludeFlags dependencyType,
            NuGetFramework targetFrameworkOverride,
            IEnumerable<LibraryDependency> dependencies)
        {
            var lockFileLib = new LockFileTargetLibrary();

            var framework = targetFrameworkOverride ?? targetGraph.Framework;
            var runtimeIdentifier = targetGraph.RuntimeIdentifier;
            
            lockFileLib.Name = package.Id;
            lockFileLib.Version = package.Version;
            lockFileLib.Type = LibraryType.Package;

            IList<string> files;
            var contentItems = new ContentItemCollection();
            HashSet<string> referenceFilter = null;

            // If the previous LockFileLibrary was given, use that to find the file list. Otherwise read the nupkg.
            if (library == null)
            {
                using (var packageReader = new PackageFolderReader(package.ExpandedPath))
                {
                    if (Path.DirectorySeparatorChar != LockFile.DirectorySeparatorChar)
                    {
                        files = packageReader
                            .GetFiles()
                            .Select(p => p.Replace(Path.DirectorySeparatorChar, LockFile.DirectorySeparatorChar))
                            .ToList();
                    }
                    else
                    {
                        files = packageReader
                            .GetFiles()
                            .ToList();
                    }
                }
            }
            else
            {
                if (Path.DirectorySeparatorChar != LockFile.DirectorySeparatorChar)
                {
                    files = library.Files.Select(p => p.Replace(Path.DirectorySeparatorChar, LockFile.DirectorySeparatorChar)).ToList();
                }
                else
                {
                    files = library.Files;
                }
            }

            contentItems.Load(files);

            // This will throw an appropriate error if the nuspec is missing
            var nuspec = package.Nuspec;

            if (dependencies == null)
            {
                var dependencySet = nuspec
                    .GetDependencyGroups()
                    .GetNearest(framework);

                if (dependencySet != null)
                {
                    var set = dependencySet.Packages;

                    if (set != null)
                    {
                        lockFileLib.Dependencies = set.ToList();
                    }
                }
            }
            else
            {
                // Filter the dependency set down to packages and projects.
                // Framework references will not be displayed
                lockFileLib.Dependencies = dependencies
                    .Where(ld => ld.LibraryRange.TypeConstraintAllowsAnyOf(LibraryDependencyTarget.PackageProjectExternal))
                    .Select(ld => new PackageDependency(ld.Name, ld.LibraryRange.VersionRange))
                    .ToList();
            }

            var referenceSet = nuspec.GetReferenceGroups().GetNearest(framework);
            if (referenceSet != null)
            {
                referenceFilter = new HashSet<string>(referenceSet.Items, StringComparer.OrdinalIgnoreCase);
            }

            // Exclude framework references for package based frameworks.
            if (!framework.IsPackageBased)
            {
                var frameworkAssemblies = nuspec.GetFrameworkReferenceGroups().GetNearest(framework);
                if (frameworkAssemblies != null)
                {
                    foreach (var assemblyReference in frameworkAssemblies.Items)
                    {
                        lockFileLib.FrameworkAssemblies.Add(assemblyReference);
                    }
                }
            }

            // Create an ordered list of selection criteria. Each will be applied, if the result is empty
            // fallback frameworks from "imports" will be tried.
            // These are only used for framework/RID combinations where content model handles everything.
            var orderedCriteria = CreateCriteria(targetGraph, framework);

            // Compile
            var compileGroup = GetLockFileItems(
                orderedCriteria,
                contentItems,
                targetGraph.Conventions.Patterns.CompileAssemblies,
                targetGraph.Conventions.Patterns.RuntimeAssemblies);

            lockFileLib.CompileTimeAssemblies.AddRange(compileGroup);

            // Runtime
            var runtimeGroup = GetLockFileItems(
                orderedCriteria,
                contentItems,
                targetGraph.Conventions.Patterns.RuntimeAssemblies);

            lockFileLib.RuntimeAssemblies.AddRange(runtimeGroup);

            // Resources
            var resourceGroup = GetLockFileItems(
                orderedCriteria,
                contentItems,
                targetGraph.Conventions.Patterns.ResourceAssemblies);

            lockFileLib.ResourceAssemblies.AddRange(resourceGroup);

            // Native
            var nativeCriteria = new List<SelectionCriteria>(orderedCriteria);
            nativeCriteria.Add(targetGraph.Conventions.Criteria.ForRuntime(targetGraph.RuntimeIdentifier));

            var nativeGroup = GetLockFileItems(
                nativeCriteria,
                contentItems,
                targetGraph.Conventions.Patterns.NativeLibraries);

            lockFileLib.NativeLibraries.AddRange(nativeGroup);

            // content v2 items
            var contentFileGroups = contentItems.FindItemGroups(targetGraph.Conventions.Patterns.ContentFiles);

            // Multiple groups can match the same framework, find all of them
            var contentFileGroupsForFramework = ContentFileUtils.GetContentGroupsForFramework(
                lockFileLib,
                framework,
                contentFileGroups);

            lockFileLib.ContentFiles = ContentFileUtils.GetContentFileGroup(
                framework,
                nuspec,
                contentFileGroupsForFramework);

            // Runtime targets
            // These are applied only to non-RID target graphs.
            // They are not used for compatibility checks.
            if (string.IsNullOrEmpty(runtimeIdentifier))
            {
                // Runtime targets contain all the runtime specific assets
                // that could be contained in the runtime specific target graphs.
                // These items are contained in a flat list and have additional properties 
                // for the RID and lock file section the assembly would belong to.
                var runtimeTargetItems = new List<LockFileRuntimeTarget>();

                // Runtime
                runtimeTargetItems.AddRange(GetRuntimeTargetLockFileItems(
                    contentItems,
                    framework,
                    dependencyType,
                    LibraryIncludeFlags.Runtime,
                    targetGraph.Conventions.Patterns.RuntimeAssemblies,
                    "runtime"));

                // Resource
                runtimeTargetItems.AddRange(GetRuntimeTargetLockFileItems(
                    contentItems,
                    framework,
                    dependencyType,
                    LibraryIncludeFlags.Runtime,
                    targetGraph.Conventions.Patterns.ResourceAssemblies,
                    "resource"));

                // Native
                runtimeTargetItems.AddRange(GetRuntimeTargetLockFileItems(
                    contentItems,
                    framework,
                    dependencyType,
                    LibraryIncludeFlags.Native,
                    targetGraph.Conventions.Patterns.NativeLibraries,
                    "native"));

                lockFileLib.RuntimeTargets = runtimeTargetItems;
            }

            // COMPAT: Support lib/contract so older packages can be consumed
            var contractPath = "lib/contract/" + package.Id + ".dll";
            var hasContract = files.Any(path => path == contractPath);
            var hasLib = lockFileLib.RuntimeAssemblies.Any();

            if (hasContract
                && hasLib
                && !framework.IsDesktop())
            {
                lockFileLib.CompileTimeAssemblies.Clear();
                lockFileLib.CompileTimeAssemblies.Add(new LockFileItem(contractPath));
            }

            // Apply filters from the <references> node in the nuspec
            if (referenceFilter != null)
            {
                // Remove anything that starts with "lib/" and is NOT specified in the reference filter.
                // runtimes/* is unaffected (it doesn't start with lib/)
                lockFileLib.RuntimeAssemblies = lockFileLib.RuntimeAssemblies.Where(p => !p.Path.StartsWith("lib/") || referenceFilter.Contains(Path.GetFileName(p.Path))).ToList();
                lockFileLib.CompileTimeAssemblies = lockFileLib.CompileTimeAssemblies.Where(p => !p.Path.StartsWith("lib/") || referenceFilter.Contains(Path.GetFileName(p.Path))).ToList();
            }

            // Exclude items
            if ((dependencyType & LibraryIncludeFlags.Runtime) == LibraryIncludeFlags.None)
            {
                ClearIfExists(lockFileLib.RuntimeAssemblies);
                lockFileLib.FrameworkAssemblies.Clear();
                lockFileLib.ResourceAssemblies.Clear();
            }

            if ((dependencyType & LibraryIncludeFlags.Compile) == LibraryIncludeFlags.None)
            {
                ClearIfExists(lockFileLib.CompileTimeAssemblies);
            }

            if ((dependencyType & LibraryIncludeFlags.Native) == LibraryIncludeFlags.None)
            {
                ClearIfExists(lockFileLib.NativeLibraries);
            }

            if ((dependencyType & LibraryIncludeFlags.ContentFiles) == LibraryIncludeFlags.None
                && GroupHasNonEmptyItems(lockFileLib.ContentFiles))
            {
                // Empty lock file items still need lock file properties for language, action, and output.
                lockFileLib.ContentFiles.Clear();
                lockFileLib.ContentFiles.Add(ContentFileUtils.CreateEmptyItem());
            }

            return lockFileLib;
        }

        /// <summary>
        /// Create lock file items for the best matching group.
        /// </summary>
        /// <remarks>Enumerate this once after calling.</remarks>
        private static IEnumerable<LockFileItem> GetLockFileItems(
            IReadOnlyList<SelectionCriteria> criteria,
            ContentItemCollection items,
            params PatternSet[] patterns)
        {
            // Loop through each criteria taking the first one that matches one or more items.
            foreach (var managedCriteria in criteria)
            {
                var group = items.FindBestItemGroup(
                    managedCriteria,
                    patterns);

                if (group != null)
                {
                    foreach (var item in group.Items)
                    {
                        var newItem = new LockFileItem(item.Path);
                        object locale;
                        if (item.Properties.TryGetValue("locale", out locale))
                        {
                            newItem.Properties["locale"] = (string)locale;
                        }
                        yield return newItem;
                    }

                    // Take only the first group that has items
                    break;
                }
            }

            yield break;
        }

        /// <summary>
        /// Creates an ordered list of selection criteria to use. This supports fallback frameworks.
        /// </summary>
        private static IReadOnlyList<SelectionCriteria> CreateCriteria(
            RestoreTargetGraph targetGraph,
            NuGetFramework framework)
        {
            var managedCriteria = new List<SelectionCriteria>(1);

            var fallbackFramework = framework as FallbackFramework;

            if (fallbackFramework == null)
            {
                var standardCriteria = targetGraph.Conventions.Criteria.ForFrameworkAndRuntime(
                    framework,
                    targetGraph.RuntimeIdentifier);

                managedCriteria.Add(standardCriteria);
            }
            else
            {
                // Add the project framework
                var primaryFramework = NuGetFramework.Parse(fallbackFramework.DotNetFrameworkName);
                var primaryCriteria = targetGraph.Conventions.Criteria.ForFrameworkAndRuntime(
                    primaryFramework,
                    targetGraph.RuntimeIdentifier);

                managedCriteria.Add(primaryCriteria);

                // Add each fallback framework in order
                foreach (var fallback in fallbackFramework.Fallback)
                {
                    var fallbackCriteria = targetGraph.Conventions.Criteria.ForFrameworkAndRuntime(
                        fallback,
                        targetGraph.RuntimeIdentifier);

                    managedCriteria.Add(fallbackCriteria);
                }
            }

            return managedCriteria;
        }

        private static bool HasItems(ContentItemGroup compileGroup)
        {
            return (compileGroup != null && compileGroup.Items.Any());
        }

        private static LockFileItem ToResourceLockFileItem(ContentItem item)
        {
            return new LockFileItem(item.Path)
            {
                Properties =
                {
                    { "locale", item.Properties["locale"].ToString()}
                }
            };
        }

        /// <summary>
        /// Clears a lock file group and replaces the first item with _._ if 
        /// the group has items. Empty groups are left alone.
        /// </summary>
        private static void ClearIfExists<T>(IList<T> group) where T: LockFileItem
        {
            if (GroupHasNonEmptyItems(group))
            {
                // Take the root directory
                var firstItem = group.OrderBy(item => item.Path.LastIndexOf('/'))
                    .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
                    .First();

                var fileName = Path.GetFileName(firstItem.Path);

                Debug.Assert(!string.IsNullOrEmpty(fileName));
                Debug.Assert(firstItem.Path.IndexOf('/') > 0);

                var emptyDir = firstItem.Path.Substring(0, firstItem.Path.Length - fileName.Length)
                    + PackagingCoreConstants.EmptyFolder;

                group.Clear();

                // Create a new item with the _._ path
                var emptyItem = (T)Activator.CreateInstance(typeof(T), new [] { emptyDir });

                // Copy over the properties from the first 
                foreach (var pair in firstItem.Properties)
                {
                    emptyItem.Properties.Add(pair.Key, pair.Value);
                }

                group.Add(emptyItem);
            }
        }

        /// <summary>
        /// True if the group has items that do not end with _._
        /// </summary>
        private static bool GroupHasNonEmptyItems(IEnumerable<LockFileItem> group)
        {
            return group?.Any(item => !item.Path.EndsWith(PackagingCoreConstants.ForwardSlashEmptyFolder)) == true;
        }

        /// <summary>
        /// Group all items by the primary key, then select the nearest TxM 
        /// within each group.
        /// Items that do not contain the primaryKey will be filtered out.
        /// </summary>
        private static List<ContentItemGroup> GetContentGroupsForFramework(
            NuGetFramework framework,
            List<ContentItemGroup> contentGroups,
            string primaryKey)
        {
            var groups = new List<ContentItemGroup>();

            // Group by primary key and find the nearest TxM under each.
            var primaryGroups = new Dictionary<string, List<ContentItemGroup>>(StringComparer.Ordinal);

            foreach (var group in contentGroups)
            {
                object keyObj;
                if (group.Properties.TryGetValue(primaryKey, out keyObj))
                {
                    string key = (string)keyObj;

                    List<ContentItemGroup> index;
                    if (!primaryGroups.TryGetValue(key, out index))
                    {
                        index = new List<ContentItemGroup>(1);
                        primaryGroups.Add(key, index);
                    }

                    index.Add(group);
                }
            }

            // Find the nearest TxM within each primary key group.
            foreach (var primaryGroup in primaryGroups)
            {
                var groupedItems = primaryGroup.Value;

                var nearestGroup = NuGetFrameworkUtility.GetNearest<ContentItemGroup>(groupedItems, framework,
                    group =>
                    {
                        // In the case of /native there is no TxM, here any should be used.
                        object frameworkObj;
                        if (group.Properties.TryGetValue(
                            ManagedCodeConventions.PropertyNames.TargetFrameworkMoniker,
                            out frameworkObj))
                        {
                            return (NuGetFramework)frameworkObj;
                        }

                        return NuGetFramework.AnyFramework;
                    });

                // If a compatible group exists within the secondary key add it to the results
                if (nearestGroup != null)
                {
                    groups.Add(nearestGroup);
                }
            }

            return groups;
        }

        private static List<LockFileRuntimeTarget> GetRuntimeTargetLockFileItems(
            ContentItemCollection contentItems,
            NuGetFramework framework,
            LibraryIncludeFlags dependencyType,
            LibraryIncludeFlags groupType,
            PatternSet patternSet,
            string assetType)
        {
            var groups = contentItems.FindItemGroups(patternSet).ToList();

            var groupsForFramework = GetContentGroupsForFramework(
                framework,
                groups,
                ManagedCodeConventions.PropertyNames.RuntimeIdentifier);

            var items = GetRuntimeTargetItems(groupsForFramework, assetType);

            if ((dependencyType & groupType) == LibraryIncludeFlags.None)
            {
                ClearIfExists<LockFileRuntimeTarget>(items);
            }

            return items;
        }

        /// <summary>
        /// Create LockFileItems from groups of library items.
        /// </summary>
        /// <param name="groups">Library items grouped by RID.</param>
        /// <param name="assetType">Lock file section the items apply to.</param>
        private static List<LockFileRuntimeTarget> GetRuntimeTargetItems(List<ContentItemGroup> groups, string assetType)
        {
            var results = new List<LockFileRuntimeTarget>();

            // Loop through RID groups
            foreach (var group in groups)
            {
                var rid = (string)group.Properties[ManagedCodeConventions.PropertyNames.RuntimeIdentifier];

                // Create lock file entries for each assembly.
                foreach (var item in group.Items)
                {
                    results.Add(new LockFileRuntimeTarget(item.Path)
                    {
                        AssetType = assetType,
                        Runtime = rid
                    });
                }
            }

            return results;
        }
    }
}
