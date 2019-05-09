// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NuGet.Client;
using NuGet.Common;
using NuGet.ContentModel;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Repositories;
using NuGet.Shared;
using NuGet.Versioning;

namespace NuGet.Commands
{
    public static class LockFileUtils
    {
        public static readonly string LIBANY = nameof(LIBANY);

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
                dependencies: null,
                cache: new LockFileBuilderCache());
        }

        public static LockFileTargetLibrary CreateLockFileTargetLibrary(
                LockFileLibrary library,
                LocalPackageInfo package,
                RestoreTargetGraph targetGraph,
                LibraryIncludeFlags dependencyType,
                NuGetFramework targetFrameworkOverride,
                IEnumerable<LibraryDependency> dependencies,
                LockFileBuilderCache cache)
        {
            LockFileTargetLibrary lockFileLib = null;
            var runtimeIdentifier = targetGraph.RuntimeIdentifier;
            var framework = targetFrameworkOverride ?? targetGraph.Framework;

            // This will throw an appropriate error if the nuspec is missing
            var nuspec = package.Nuspec;

            var orderedCriteriaSets = cache.GetSelectionCriteria(targetGraph, framework);
            var contentItems = cache.GetContentItems(library, package);

            var packageTypes = nuspec.GetPackageTypes().AsList();

            for (var i = 0; i < orderedCriteriaSets.Count; i++)
            {
                // Create a new library each time to avoid 
                // assets being added from other criteria.
                lockFileLib = new LockFileTargetLibrary()
                {
                    Name = package.Id,
                    Version = package.Version,
                    Type = LibraryType.Package,
                    PackageType = packageTypes
                };

                // Populate assets

                if (lockFileLib.PackageType.Contains(PackageType.DotnetTool))
                {
                    AddToolsAssets(library, package, targetGraph, dependencyType, lockFileLib, framework, runtimeIdentifier, contentItems, nuspec, orderedCriteriaSets[i]);
                    if (CompatibilityChecker.HasCompatibleToolsAssets(lockFileLib))
                    {
                        break;
                    }
                }
                else
                { 
                    AddAssets(library, package, targetGraph, dependencyType, lockFileLib, framework, runtimeIdentifier, contentItems, nuspec, orderedCriteriaSets[i]);
                    // Check if compatile assets were found.
                    // If no compatible assets were found and this is the last check
                    // continue on with what was given, this will fail in the normal
                    // compat verification.
                    if (CompatibilityChecker.HasCompatibleAssets(lockFileLib))
                    {
                        // Stop when compatible assets are found.
                        break;
                    }
                }

            }


            // Add dependencies
            AddDependencies(dependencies, lockFileLib, framework, nuspec);

            // Exclude items
            ExcludeItems(lockFileLib, dependencyType);

            return lockFileLib;
        }

        internal static List<List<SelectionCriteria>> CreateOrderedCriteriaSets(RestoreTargetGraph targetGraph, NuGetFramework framework)
        {
            // Create an ordered list of selection criteria. Each will be applied, if the result is empty
            // fallback frameworks from "imports" will be tried.
            // These are only used for framework/RID combinations where content model handles everything.
            // AssetTargetFallback frameworks will provide multiple criteria since all assets need to be
            // evaluated before selecting the TFM to use.
            var orderedCriteriaSets = new List<List<SelectionCriteria>>(1);

            var assetTargetFallback = framework as AssetTargetFallbackFramework;

            if (assetTargetFallback != null)
            {
                // Add the root project framework first.
                orderedCriteriaSets.Add(CreateCriteria(targetGraph, assetTargetFallback.RootFramework));

                // Add all fallbacks in order.
                orderedCriteriaSets.AddRange(assetTargetFallback.Fallback.Select(e => CreateCriteria(targetGraph, e)));
            }
            else
            {
                // Add the current framework.
                orderedCriteriaSets.Add(CreateCriteria(targetGraph, framework));
            }

            return orderedCriteriaSets;
        }

        /// <summary>
        /// Populate assets for a <see cref="LockFileLibrary"/>.
        /// </summary>
        private static void AddAssets(
            LockFileLibrary library,
            LocalPackageInfo package,
            RestoreTargetGraph targetGraph,
            LibraryIncludeFlags dependencyType,
            LockFileTargetLibrary lockFileLib,
            NuGetFramework framework,
            string runtimeIdentifier,
            ContentItemCollection contentItems,
            NuspecReader nuspec,
            IReadOnlyList<SelectionCriteria> orderedCriteria)
        {
            // Add framework references for desktop projects.
            AddFrameworkReferences(lockFileLib, framework, nuspec);

            // Compile
            // ref takes precedence over lib
            var compileGroup = GetLockFileItems(
                orderedCriteria,
                contentItems,
                targetGraph.Conventions.Patterns.CompileRefAssemblies,
                targetGraph.Conventions.Patterns.CompileLibAssemblies);

            lockFileLib.CompileTimeAssemblies.AddRange(compileGroup);

            // Runtime
            var runtimeGroup = GetLockFileItems(
                orderedCriteria,
                contentItems,
                targetGraph.Conventions.Patterns.RuntimeAssemblies);

            lockFileLib.RuntimeAssemblies.AddRange(runtimeGroup);

            // Embed
            var embedGroup = GetLockFileItems(
                orderedCriteria,
                contentItems,
                targetGraph.Conventions.Patterns.EmbedAssemblies);

            lockFileLib.EmbedAssemblies.AddRange(embedGroup);

            // Resources
            var resourceGroup = GetLockFileItems(
                orderedCriteria,
                contentItems,
                targetGraph.Conventions.Patterns.ResourceAssemblies);

            lockFileLib.ResourceAssemblies.AddRange(resourceGroup);

            // Native
            var nativeGroup = GetLockFileItems(
                orderedCriteria,
                contentItems,
                targetGraph.Conventions.Patterns.NativeLibraries);

            lockFileLib.NativeLibraries.AddRange(nativeGroup);

            // Add MSBuild files
            AddMSBuildAssets(library, targetGraph, lockFileLib, orderedCriteria, contentItems);

            // Add content files
            AddContentFiles(targetGraph, lockFileLib, framework, contentItems, nuspec);

            // Runtime targets
            // These are applied only to non-RID target graphs.
            // They are not used for compatibility checks.
            AddRuntimeTargets(targetGraph, dependencyType, lockFileLib, framework, runtimeIdentifier, contentItems);

            // COMPAT: Support lib/contract so older packages can be consumed
            ApplyLibContract(package, lockFileLib, framework, contentItems);

            // Apply filters from the <references> node in the nuspec
            ApplyReferenceFilter(lockFileLib, framework, nuspec);
        }

        private static void AddMSBuildAssets(
            LockFileLibrary library,
            RestoreTargetGraph targetGraph,
            LockFileTargetLibrary lockFileLib,
            IReadOnlyList<SelectionCriteria> orderedCriteria,
            ContentItemCollection contentItems)
        {
            // Build Transitive
            var btGroup = GetLockFileItems(
                orderedCriteria,
                contentItems,
                targetGraph.Conventions.Patterns.MSBuildTransitiveFiles);

            var filteredBTGroup = GetBuildItemsForPackageId(btGroup, library.Name);
            lockFileLib.Build.AddRange(filteredBTGroup);

            // Build
            var buildGroup = GetLockFileItems(
                orderedCriteria,
                contentItems,
                targetGraph.Conventions.Patterns.MSBuildFiles);

            // filter any build asset already being added as part of build transitive
            var filteredBuildGroup = GetBuildItemsForPackageId(buildGroup, library.Name).
                Where(buildItem => !filteredBTGroup.Any(
                    btItem => Path.GetFileName(btItem.Path).Equals(Path.GetFileName(buildItem.Path), StringComparison.OrdinalIgnoreCase)));

            lockFileLib.Build.AddRange(filteredBuildGroup);

            // Build multi targeting
            var buildMultiTargetingGroup = GetLockFileItems(
                orderedCriteria,
                contentItems,
                targetGraph.Conventions.Patterns.MSBuildMultiTargetingFiles);

            lockFileLib.BuildMultiTargeting.AddRange(GetBuildItemsForPackageId(buildMultiTargetingGroup, library.Name));
        }

        private static void AddToolsAssets(LockFileLibrary library,
            LocalPackageInfo package,
            RestoreTargetGraph targetGraph,
            LibraryIncludeFlags dependencyType,
            LockFileTargetLibrary lockFileLib,
            NuGetFramework framework,
            string runtimeIdentifier,
            ContentItemCollection contentItems,
            NuspecReader nuspec,
            IReadOnlyList<SelectionCriteria> orderedCriteria)
        {
            var toolsGroup = GetLockFileItems(
                orderedCriteria,
                contentItems,
                targetGraph.Conventions.Patterns.ToolsAssemblies);

            lockFileLib.ToolsAssemblies.AddRange(toolsGroup);
        }

        private static void AddContentFiles(RestoreTargetGraph targetGraph, LockFileTargetLibrary lockFileLib, NuGetFramework framework, ContentItemCollection contentItems, NuspecReader nuspec)
        {
            // content v2 items
            var contentFileGroups = contentItems.FindItemGroups(targetGraph.Conventions.Patterns.ContentFiles).ToList();

            if (contentFileGroups.Count > 0)
            {
                // Multiple groups can match the same framework, find all of them
                var contentFileGroupsForFramework = ContentFileUtils.GetContentGroupsForFramework(
                    lockFileLib,
                    framework,
                    contentFileGroups);

                lockFileLib.ContentFiles = ContentFileUtils.GetContentFileGroup(
                    framework,
                    nuspec,
                    contentFileGroupsForFramework);
            }
        }

        /// <summary>
        /// Runtime targets
        /// These are applied only to non-RID target graphs.
        /// They are not used for compatibility checks.
        /// </summary>
        private static void AddRuntimeTargets(
            RestoreTargetGraph targetGraph,
            LibraryIncludeFlags dependencyType,
            LockFileTargetLibrary lockFileLib,
            NuGetFramework framework,
            string runtimeIdentifier,
            ContentItemCollection contentItems)
        {
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
        }

        /// <summary>
        /// Add framework references.
        /// </summary>
        private static void AddFrameworkReferences(LockFileTargetLibrary lockFileLib, NuGetFramework framework, NuspecReader nuspec)
        {
            // Exclude framework references for package based frameworks.
            if (!framework.IsPackageBased)
            {
                var frameworkAssemblies = nuspec.GetFrameworkAssemblyGroups().GetNearest(framework);
                if (frameworkAssemblies != null)
                {
                    foreach (var assemblyReference in frameworkAssemblies.Items)
                    {
                        lockFileLib.FrameworkAssemblies.Add(assemblyReference);
                    }
                }
            }

            // Related to: FrameworkReference item, added first in .NET Core 3.0
            var frameworkRef = nuspec.GetFrameworkRefGroups().GetNearest(framework);

            if (frameworkRef != null)
            {
                lockFileLib.FrameworkReferences.AddRange(frameworkRef.FrameworkReferences.Select(e => e.Name));
            }
        }

        /// <summary>
        /// Apply filters from the references node in the nuspec.
        /// </summary>
        private static void ApplyReferenceFilter(LockFileTargetLibrary lockFileLib, NuGetFramework framework, NuspecReader nuspec)
        {
            if (lockFileLib.CompileTimeAssemblies.Count > 0 || lockFileLib.RuntimeAssemblies.Count > 0)
            {
                var groups = nuspec.GetReferenceGroups().ToList();

                if (groups.Count > 0)
                {
                    var referenceSet = groups.GetNearest(framework);
                    if (referenceSet != null)
                    {
                        var referenceFilter = new HashSet<string>(referenceSet.Items, StringComparer.OrdinalIgnoreCase);

                        // Remove anything that starts with "lib/" and is NOT specified in the reference filter.
                        // runtimes/* is unaffected (it doesn't start with lib/)
                        lockFileLib.RuntimeAssemblies = lockFileLib.RuntimeAssemblies.Where(p => !p.Path.StartsWith("lib/") || referenceFilter.Contains(Path.GetFileName(p.Path))).ToList();
                        lockFileLib.CompileTimeAssemblies = lockFileLib.CompileTimeAssemblies.Where(p => !p.Path.StartsWith("lib/") || referenceFilter.Contains(Path.GetFileName(p.Path))).ToList();
                    }
                }
            }
        }

        /// <summary>
        /// COMPAT: Support lib/contract so older packages can be consumed
        /// </summary>
        private static void ApplyLibContract(LocalPackageInfo package, LockFileTargetLibrary lockFileLib, NuGetFramework framework, ContentItemCollection contentItems)
        {
            if (contentItems.HasContract && lockFileLib.RuntimeAssemblies.Count > 0 && !framework.IsDesktop())
            {
                var contractPath = "lib/contract/" + package.Id + ".dll";

                if (package.Files.Any(path => path == contractPath))
                {
                    lockFileLib.CompileTimeAssemblies.Clear();
                    lockFileLib.CompileTimeAssemblies.Add(new LockFileItem(contractPath));
                }
            }
        }

        private static void AddDependencies(IEnumerable<LibraryDependency> dependencies, LockFileTargetLibrary lockFileLib, NuGetFramework framework, NuspecReader nuspec)
        {
            if (dependencies == null)
            {
                // AssetFallbackFramework does not apply to dependencies.
                // Convert it to a fallback framework if needed.
                var currentFramework = (framework as AssetTargetFallbackFramework)?.AsFallbackFramework() ?? framework;

                var dependencySet = nuspec
                    .GetDependencyGroups()
                    .GetNearest(currentFramework);

                if (dependencySet != null)
                {
                    var set = dependencySet.Packages;

                    if (set != null)
                    {
                        lockFileLib.Dependencies = set.AsList();
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
        }

        /// <summary>
        /// Create a library for a project.
        /// </summary>
        public static LockFileTargetLibrary CreateLockFileTargetProject(
            GraphItem<RemoteResolveResult> graphItem,
            LibraryIdentity library,
            LibraryIncludeFlags dependencyType,
            RestoreTargetGraph targetGraph,
            ProjectStyle rootProjectStyle)
        {
            var localMatch = (LocalMatch)graphItem.Data.Match;

            // Target framework information is optional and may not exist for csproj projects
            // that do not have a project.json file.
            string projectFramework = null;
            object frameworkInfoObject;
            if (localMatch.LocalLibrary.Items.TryGetValue(
                KnownLibraryProperties.TargetFrameworkInformation,
                out frameworkInfoObject))
            {
                // Retrieve the resolved framework name, if this is null it means that the
                // project is incompatible. This is marked as Unsupported.
                var targetFrameworkInformation = (TargetFrameworkInformation)frameworkInfoObject;
                projectFramework = targetFrameworkInformation.FrameworkName?.DotNetFrameworkName
                    ?? NuGetFramework.UnsupportedFramework.DotNetFrameworkName;
            }

            // Create the target entry
            var projectLib = new LockFileTargetLibrary()
            {
                Name = library.Name,
                Version = library.Version,
                Type = LibraryType.Project,
                Framework = projectFramework,

                // Find all dependencies which would be in the nuspec
                // Include dependencies with no constraints, or package/project/external
                // Exclude suppressed dependencies, the top level project is not written 
                // as a target so the node depth does not matter.
                Dependencies = graphItem.Data.Dependencies
                    .Where(
                        d => (d.LibraryRange.TypeConstraintAllowsAnyOf(
                            LibraryDependencyTarget.PackageProjectExternal))
                             && d.SuppressParent != LibraryIncludeFlags.All)
                    .Select(d => GetDependencyVersionRange(d))
                    .ToList()
            };

            if (rootProjectStyle == ProjectStyle.PackageReference)
            {
                // Add files under asset groups
                object filesObject;
                object msbuildPath;
                if (localMatch.LocalLibrary.Items.TryGetValue(KnownLibraryProperties.MSBuildProjectPath, out msbuildPath))
                {
                    var files = new List<ProjectRestoreMetadataFile>();
                    var fileLookup = new Dictionary<string, ProjectRestoreMetadataFile>(StringComparer.OrdinalIgnoreCase);

                    // Find the project path, this is provided by the resolver
                    var msbuildFilePathInfo = new FileInfo((string)msbuildPath);

                    // Ensure a trailing slash for the relative path helper.
                    var projectDir = PathUtility.EnsureTrailingSlash(msbuildFilePathInfo.Directory.FullName);

                    // Read files from the project if they were provided.
                    if (localMatch.LocalLibrary.Items.TryGetValue(KnownLibraryProperties.ProjectRestoreMetadataFiles, out filesObject))
                    {
                        files.AddRange((List<ProjectRestoreMetadataFile>)filesObject);
                    }

                    var targetFrameworkShortName = targetGraph.Framework.GetShortFolderName();
                    var libAnyPath = $"lib/{targetFrameworkShortName}/any.dll";

                    if (files.Count == 0)
                    {
                        // If the project did not provide a list of assets, add in default ones.
                        // These are used to detect transitive vs non-transitive project references.
                        var absolutePath = Path.Combine(projectDir, "bin", "placeholder", $"{localMatch.Library.Name}.dll");

                        files.Add(new ProjectRestoreMetadataFile(libAnyPath, absolutePath));
                    }

                    // Process and de-dupe files
                    for (var i = 0; i < files.Count; i++)
                    {
                        var path = files[i].PackagePath;

                        // LIBANY avoid compatibility checks and will always be used.
                        if (LIBANY.Equals(path, StringComparison.Ordinal))
                        {
                            path = libAnyPath;
                        }

                        if (!fileLookup.ContainsKey(path))
                        {
                            fileLookup.Add(path, files[i]);
                        }
                    }

                    var contentItems = new ContentItemCollection();
                    contentItems.Load(fileLookup.Keys);

                    // Create an ordered list of selection criteria. Each will be applied, if the result is empty
                    // fallback frameworks from "imports" will be tried.
                    // These are only used for framework/RID combinations where content model handles everything.
                    var orderedCriteria = CreateCriteria(targetGraph, targetGraph.Framework);

                    // Compile
                    // ref takes precedence over lib
                    var compileGroup = GetLockFileItems(
                        orderedCriteria,
                        contentItems,
                        targetGraph.Conventions.Patterns.CompileRefAssemblies,
                        targetGraph.Conventions.Patterns.CompileLibAssemblies);

                    projectLib.CompileTimeAssemblies.AddRange(
                        ConvertToProjectPaths(fileLookup, projectDir, compileGroup));

                    // Runtime
                    var runtimeGroup = GetLockFileItems(
                        orderedCriteria,
                        contentItems,
                        targetGraph.Conventions.Patterns.RuntimeAssemblies);

                    projectLib.RuntimeAssemblies.AddRange(
                        ConvertToProjectPaths(fileLookup, projectDir, runtimeGroup));
                }
            }

            // Add frameworkAssemblies for projects
            object frameworkAssembliesObject;
            if (localMatch.LocalLibrary.Items.TryGetValue(
                KnownLibraryProperties.FrameworkAssemblies,
                out frameworkAssembliesObject))
            {
                projectLib.FrameworkAssemblies.AddRange((List<string>)frameworkAssembliesObject);
            }

            // Add frameworkReferences for projects
            object frameworkReferencesObject;
            if (localMatch.LocalLibrary.Items.TryGetValue(
                KnownLibraryProperties.FrameworkReferences,
                out frameworkReferencesObject))
            {
                projectLib.FrameworkReferences.AddRange(
                    ((ISet<FrameworkDependency>)frameworkReferencesObject)
                        .Where(e => e.PrivateAssets != FrameworkDependencyFlags.All)
                        .Select(f => f.Name));
            }

            // Exclude items
            ExcludeItems(projectLib, dependencyType);

            return projectLib;
        }

        /// <summary>
        /// Convert from the expected nupkg path to the on disk path.
        /// </summary>
        private static IEnumerable<LockFileItem> ConvertToProjectPaths(
            Dictionary<string, ProjectRestoreMetadataFile> fileLookup,
            string projectDir,
            IEnumerable<LockFileItem> items)
        {
            foreach (var item in items)
            {
                var diskPath = fileLookup[item.Path].AbsolutePath;
                var fixedPath = PathUtility.GetPathWithForwardSlashes(
                    PathUtility.GetRelativePath(projectDir, diskPath));

                yield return new LockFileItem(fixedPath);
            }
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
        /// Get packageId.targets and packageId.props
        /// </summary>
        private static IEnumerable<LockFileItem> GetBuildItemsForPackageId(
            IEnumerable<LockFileItem> items,
            string packageId)
        {
            if (items.Any())
            {
                var skipEmptyCheck = false;

                var ordered = items.OrderBy(c => c.Path, StringComparer.OrdinalIgnoreCase)
                                   .ToArray();

                var props = ordered.FirstOrDefault(c =>
                    $"{packageId}.props".Equals(
                        Path.GetFileName(c.Path),
                        StringComparison.OrdinalIgnoreCase));

                if (props != null)
                {
                    skipEmptyCheck = true;
                    yield return props;
                }

                var targets = ordered.FirstOrDefault(c =>
                    $"{packageId}.targets".Equals(
                        Path.GetFileName(c.Path),
                        StringComparison.OrdinalIgnoreCase));

                if (targets != null)
                {
                    skipEmptyCheck = true;
                    yield return targets;
                }

                if (!skipEmptyCheck)
                {
                    // Find _._ if it exists, this file is needed
                    // but does not match the package id above.
                    var empty = ordered.FirstOrDefault(c =>
                        c.Path.EndsWith(PackagingCoreConstants.ForwardSlashEmptyFolder, StringComparison.Ordinal));

                    if (empty != null)
                    {
                        yield return empty;
                    }
                }
            }
        }

        /// <summary>
        /// Creates an ordered list of selection criteria to use. This supports fallback frameworks.
        /// </summary>
        private static List<SelectionCriteria> CreateCriteria(
            RestoreTargetGraph targetGraph,
            NuGetFramework framework)
        {
            var managedCriteria = new List<SelectionCriteria>(1);

            var fallbackFramework = framework as FallbackFramework;

            // Avoid fallback criteria if this is not a fallback framework,
            // or if AssetTargetFallback is used. For AssetTargetFallback
            // the fallback frameworks will be checked later.
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
        private static void ClearIfExists<T>(IList<T> group) where T : LockFileItem
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
                var emptyItem = (T)Activator.CreateInstance(typeof(T), new[] { emptyDir });

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
                    var key = (string)keyObj;

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

        /// <summary>
        /// Replace / with the local directory separator if needed.
        /// For OSX and Linux the same string is returned.
        /// </summary>
        public static string ToDirectorySeparator(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (Path.DirectorySeparatorChar == '/')
            {
                return path;
            }

            return path.Replace('/', Path.DirectorySeparatorChar);
        }

        private static PackageDependency GetDependencyVersionRange(LibraryDependency dependency)
        {
            var range = dependency.LibraryRange.VersionRange ?? VersionRange.All;

            if (VersionRange.All.Equals(range)
                && (dependency.LibraryRange.TypeConstraintAllows(LibraryDependencyTarget.ExternalProject)))
            {
                // For csproj -> csproj type references where there is no range, use 1.0.0
                range = VersionRange.Parse("1.0.0");
            }
            else
            {
                // For project dependencies drop the snapshot version.
                // Ex: 1.0.0-* -> 1.0.0
                range = range.ToNonSnapshotRange();
            }

            return new PackageDependency(dependency.Name, range);
        }

        /// <summary>
        /// Replace excluded asset groups with _._ if they have > 0 items.
        /// </summary>
        public static void ExcludeItems(LockFileTargetLibrary lockFileLib, LibraryIncludeFlags dependencyType)
        {
            if ((dependencyType & LibraryIncludeFlags.Runtime) == LibraryIncludeFlags.None)
            {
                ClearIfExists(lockFileLib.RuntimeAssemblies);
                lockFileLib.FrameworkAssemblies.Clear();
                lockFileLib.ResourceAssemblies.Clear();
            }

            if ((dependencyType & LibraryIncludeFlags.Compile) == LibraryIncludeFlags.None)
            {
                ClearIfExists(lockFileLib.CompileTimeAssemblies);
                ClearIfExists(lockFileLib.EmbedAssemblies);
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

            if ((dependencyType & LibraryIncludeFlags.BuildTransitive) == LibraryIncludeFlags.None &&
                (dependencyType & LibraryIncludeFlags.Build) == LibraryIncludeFlags.None)
            {
                // If BuildTransitive is excluded then all build assets are cleared.
                ClearIfExists(lockFileLib.Build);
                ClearIfExists(lockFileLib.BuildMultiTargeting);
            }
            else if ((dependencyType & LibraryIncludeFlags.Build) == LibraryIncludeFlags.None)
            {
                if (!lockFileLib.Build.Any(item => item.Path.StartsWith("buildTransitive/", StringComparison.OrdinalIgnoreCase)))
                {
                    // all build assets are from /build folder so just clear them all.
                    ClearIfExists(lockFileLib.Build);
                    ClearIfExists(lockFileLib.BuildMultiTargeting);
                }
                else
                {
                    // only clear /build assets, leaving /BuildTransitive behind
                    var newBuildAssets = new List<LockFileItem>();

                    for (var i = 0; i < lockFileLib.Build.Count; i++)
                    {
                        var currentBuildItem = lockFileLib.Build[i];

                        if (!currentBuildItem.Path.StartsWith("build/", StringComparison.OrdinalIgnoreCase))
                        {
                            newBuildAssets.Add(currentBuildItem);
                        }
                        else
                        {
                            // if current asset is from build then also clear it for BuildMultiTargeting if exists.
                            var multiBuildAsset = lockFileLib.BuildMultiTargeting.FirstOrDefault(
                                item => Path.GetFileName(item.Path).Equals(Path.GetFileName(currentBuildItem.Path), StringComparison.OrdinalIgnoreCase));

                            if (multiBuildAsset != null)
                            {
                                lockFileLib.BuildMultiTargeting.Remove(multiBuildAsset);
                            }
                        }
                    }

                    lockFileLib.Build = newBuildAssets;
                }
            }
        }
    }
}
