// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using NuGet.Common;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.ProjectModel;
using NuGet.Repositories;

namespace NuGet.Commands
{
    public class LockFileBuilder
    {
        private readonly int _lockFileVersion;
        private readonly ILogger _logger;
        private readonly Dictionary<RestoreTargetGraph, Dictionary<string, LibraryIncludeFlags>> _includeFlagGraphs;

        public LockFileBuilder(int lockFileVersion,
            ILogger logger,
            Dictionary<RestoreTargetGraph,
            Dictionary<string, LibraryIncludeFlags>> includeFlagGraphs)
        {
            _lockFileVersion = lockFileVersion;
            _logger = logger;
            _includeFlagGraphs = includeFlagGraphs;
        }

        public LockFile CreateLockFile(LockFile previousLockFile,
            PackageSpec project,
            IEnumerable<RestoreTargetGraph> targetGraphs,
            IReadOnlyList<NuGetv3LocalRepository> localRepositories,
            RemoteWalkContext context,
            LockFileBuilderCache lockFileBuilderCache)
        {
            var lockFile = new LockFile()
            {
                Version = _lockFileVersion
            };

            var previousLibraries = previousLockFile?.Libraries.ToDictionary(l => Tuple.Create(l.Name, l.Version));

            if (project.RestoreMetadata?.ProjectStyle == ProjectStyle.PackageReference ||
                project.RestoreMetadata?.ProjectStyle == ProjectStyle.DotnetToolReference)
            {
                AddProjectFileDependenciesForPackageReference(project, lockFile, targetGraphs);
            }
            else
            {
                AddProjectFileDependenciesForSpec(project, lockFile);
            }

            // Record all libraries used
            var libraryItems = targetGraphs
                .SelectMany(g => g.Flattened) // All GraphItem<RemoteResolveResult> resolved in the graph.
                .Distinct(GraphItemKeyComparer<RemoteResolveResult>.Instance) // Distinct list of GraphItems. Two items are equal only if the itmes' Keys are equal.
                .OrderBy(x => x.Data.Match.Library);
            foreach (var item in libraryItems)
            {
                var library = item.Data.Match.Library;

                if (project.Name.Equals(library.Name, StringComparison.OrdinalIgnoreCase))
                {
                    // Do not include the project itself as a library.
                    continue;
                }

                if (library.Type == LibraryType.Project || library.Type == LibraryType.ExternalProject)
                {
                    // Project
                    var localMatch = (LocalMatch)item.Data.Match;

                    var projectLib = new LockFileLibrary()
                    {
                        Name = library.Name,
                        Version = library.Version,
                        Type = LibraryType.Project,
                    };

                    // Set the relative path if a path exists
                    // For projects without project.json this will be empty
                    if (!string.IsNullOrEmpty(localMatch.LocalLibrary.Path))
                    {
                        projectLib.Path = PathUtility.GetRelativePath(
                            project.FilePath,
                            localMatch.LocalLibrary.Path,
                            '/');
                    }

                    // The msbuild project path if it exists
                    object msbuildPath;
                    if (localMatch.LocalLibrary.Items.TryGetValue(KnownLibraryProperties.MSBuildProjectPath, out msbuildPath))
                    {
                        var msbuildRelativePath = PathUtility.GetRelativePath(
                            project.FilePath,
                            (string)msbuildPath,
                            '/');

                        projectLib.MSBuildProject = msbuildRelativePath;
                    }

                    lockFile.Libraries.Add(projectLib);
                }
                else if (library.Type == LibraryType.Package)
                {
                    // Packages
                    var packageInfo = NuGetv3LocalRepositoryUtility.GetPackage(localRepositories, library.Name, library.Version);

                    // Add the library if it was resolved, unresolved packages are not added to the assets file.
                    if (packageInfo != null)
                    {
                        var package = packageInfo.Package;
                        var resolver = packageInfo.Repository.PathResolver;

                        var sha512 = package.Sha512;
                        var path = PathUtility.GetPathWithForwardSlashes(resolver.GetPackageDirectory(package.Id, package.Version));
                        LockFileLibrary lockFileLib = null;
                        LockFileLibrary previousLibrary = null;

                        if (previousLibraries?.TryGetValue(Tuple.Create(package.Id, package.Version), out previousLibrary) == true)
                        {
                            // Check that the previous library is still valid
                            if (previousLibrary != null
                                && StringComparer.Ordinal.Equals(path, previousLibrary.Path)
                                && StringComparer.Ordinal.Equals(sha512, previousLibrary.Sha512))
                            {
                                // We mutate this previous library so we must take a clone of it. This is
                                // important because later, when deciding whether the lock file has changed,
                                // we compare the new lock file to the previous (in-memory) lock file.
                                lockFileLib = previousLibrary.Clone();
                            }
                        }

                        // Create a new lock file library if one doesn't exist already.
                        if (lockFileLib == null)
                        {
                            lockFileLib = CreateLockFileLibrary(package, sha512, path);
                        }

                        // Create a new lock file library
                        lockFile.Libraries.Add(lockFileLib);
                    }
                }
            }

            var libraries = lockFile.Libraries.ToDictionary(lib => Tuple.Create(lib.Name, lib.Version));

            var librariesWithWarnings = new HashSet<LibraryIdentity>();

            var rootProjectStyle = project.RestoreMetadata?.ProjectStyle ?? ProjectStyle.Unknown;

            // Add the targets
            foreach (var targetGraph in targetGraphs
                .OrderBy(graph => graph.Framework.ToString(), StringComparer.Ordinal)
                .ThenBy(graph => graph.RuntimeIdentifier, StringComparer.Ordinal))
            {
                var target = new LockFileTarget
                {
                    TargetFramework = targetGraph.Framework,
                    RuntimeIdentifier = targetGraph.RuntimeIdentifier
                };

                var flattenedFlags = IncludeFlagUtils.FlattenDependencyTypes(_includeFlagGraphs, project, targetGraph);

                // Check if warnings should be displayed for the current framework.
                var tfi = project.GetTargetFramework(targetGraph.Framework);

                bool warnForImportsOnGraph = tfi.Warn
                    && (target.TargetFramework is FallbackFramework
                        || target.TargetFramework is AssetTargetFallbackFramework);

                foreach (var graphItem in targetGraph.Flattened.OrderBy(x => x.Key))
                {
                    var library = graphItem.Key;

                    // include flags
                    LibraryIncludeFlags includeFlags;
                    if (!flattenedFlags.TryGetValue(library.Name, out includeFlags))
                    {
                        includeFlags = ~LibraryIncludeFlags.ContentFiles;
                    }

                    if (library.Type == LibraryType.Project || library.Type == LibraryType.ExternalProject)
                    {
                        if (project.Name.Equals(library.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            // Do not include the project itself as a library.
                            continue;
                        }

                        var projectLib = LockFileUtils.CreateLockFileTargetProject(
                            graphItem,
                            library,
                            includeFlags,
                            targetGraph,
                            rootProjectStyle);

                        target.Libraries.Add(projectLib);
                        continue;
                    }
                    else if (library.Type == LibraryType.Package)
                    {
                        var packageInfo = NuGetv3LocalRepositoryUtility.GetPackage(localRepositories, library.Name, library.Version);

                        if (packageInfo == null)
                        {
                            continue;
                        }

                        var package = packageInfo.Package;
                        var libraryDependency = tfi.Dependencies.FirstOrDefault(e => e.Name.Equals(library.Name, StringComparison.OrdinalIgnoreCase));

                        var targetLibrary = LockFileUtils.CreateLockFileTargetLibrary(
                            libraryDependency,
                            libraries[Tuple.Create(library.Name, library.Version)],
                            package,
                            targetGraph,
                            dependencyType: includeFlags,
                            targetFrameworkOverride: null,
                            dependencies: graphItem.Data.Dependencies,
                            cache: lockFileBuilderCache);

                        target.Libraries.Add(targetLibrary);

                        // Log warnings if the target library used the fallback framework
                        if (warnForImportsOnGraph && !librariesWithWarnings.Contains(library))
                        {
                            var nonFallbackFramework = new NuGetFramework(target.TargetFramework);

                            var targetLibraryWithoutFallback = LockFileUtils.CreateLockFileTargetLibrary(
                                libraryDependency,
                                libraries[Tuple.Create(library.Name, library.Version)],
                                package,
                                targetGraph,
                                targetFrameworkOverride: nonFallbackFramework,
                                dependencyType: includeFlags,
                                dependencies: graphItem.Data.Dependencies,
                                cache: lockFileBuilderCache);

                            if (!targetLibrary.Equals(targetLibraryWithoutFallback))
                            {
                                var libraryName = DiagnosticUtility.FormatIdentity(library);

                                var message = string.Format(CultureInfo.CurrentCulture,
                                    Strings.Log_ImportsFallbackWarning,
                                    libraryName,
                                    GetFallbackFrameworkString(target.TargetFramework),
                                    nonFallbackFramework);

                                var logMessage = RestoreLogMessage.CreateWarning(
                                    NuGetLogCode.NU1701,
                                    message,
                                    library.Name,
                                    targetGraph.TargetGraphName);

                                _logger.Log(logMessage);

                                // only log the warning once per library
                                librariesWithWarnings.Add(library);
                            }
                        }
                    }
                }

                lockFile.Targets.Add(target);
            }

            PopulatePackageFolders(localRepositories.Select(repo => repo.RepositoryRoot).Distinct(), lockFile);

            AddCentralTransitiveDependencyGroupsForPackageReference(project, lockFile, targetGraphs);

            // Add the original package spec to the lock file.
            lockFile.PackageSpec = project;

            return lockFile;
        }

        private static string GetFallbackFrameworkString(NuGetFramework framework)
        {
            var frameworks = (framework as AssetTargetFallbackFramework)?.Fallback
                ?? (framework as FallbackFramework)?.Fallback
                ?? new List<NuGetFramework>();

            return string.Join(", ", frameworks);
        }

        private static void AddProjectFileDependenciesForSpec(PackageSpec project, LockFile lockFile)
        {
            // Use empty string as the key of dependencies shared by all frameworks
            lockFile.ProjectFileDependencyGroups.Add(new ProjectFileDependencyGroup(
                string.Empty,
                project.Dependencies
                    .Select(group => group.LibraryRange.ToLockFileDependencyGroupString())
                    .OrderBy(group => group, StringComparer.Ordinal)));

            foreach (var frameworkInfo in project.TargetFrameworks
                .OrderBy(framework => framework.FrameworkName.ToString(),
                    StringComparer.Ordinal))
            {
                lockFile.ProjectFileDependencyGroups.Add(new ProjectFileDependencyGroup(
                    frameworkInfo.FrameworkName.ToString(),
                    frameworkInfo.Dependencies
                        .Select(x => x.LibraryRange.ToLockFileDependencyGroupString())
                        .OrderBy(dependency => dependency, StringComparer.Ordinal)));
            }
        }

        private static void AddProjectFileDependenciesForPackageReference(PackageSpec project, LockFile lockFile, IEnumerable<RestoreTargetGraph> targetGraphs)
        {
            // For NETCore put everything under a TFM section
            // Projects are included for NETCore
            foreach (var frameworkInfo in project.TargetFrameworks
                .OrderBy(framework => framework.FrameworkName.ToString(),
                    StringComparer.Ordinal))
            {
                var dependencies = new List<LibraryRange>();
                dependencies.AddRange(project.Dependencies.Select(e => e.LibraryRange));
                dependencies.AddRange(frameworkInfo.Dependencies.Select(e => e.LibraryRange));

                var targetGraph = targetGraphs.SingleOrDefault(graph =>
                    graph.Framework.Equals(frameworkInfo.FrameworkName)
                    && string.IsNullOrEmpty(graph.RuntimeIdentifier));

                var resolvedEntry = targetGraph?
                    .Flattened
                    .SingleOrDefault(library => library.Key.Name.Equals(project.Name, StringComparison.OrdinalIgnoreCase));

                // In some failure cases where there is a conflict the root level project cannot be resolved, this should be handled gracefully
                if (resolvedEntry != null)
                {
                    dependencies.AddRange(resolvedEntry.Data.Dependencies.Where(lib =>
                        lib.LibraryRange.TypeConstraint == LibraryDependencyTarget.ExternalProject)
                        .Select(lib => lib.LibraryRange));
                }

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var uniqueDependencies = new List<LibraryRange>();

                foreach (var dependency in dependencies)
                {
                    if (seen.Add(dependency.Name))
                    {
                        uniqueDependencies.Add(dependency);
                    }
                }

                // Add entry
                var dependencyGroup = new ProjectFileDependencyGroup(
                    frameworkInfo.FrameworkName.ToString(),
                    uniqueDependencies.Select(x => x.ToLockFileDependencyGroupString())
                        .OrderBy(dependency => dependency, StringComparer.Ordinal));

                lockFile.ProjectFileDependencyGroups.Add(dependencyGroup);
            }
        }

        private void AddCentralTransitiveDependencyGroupsForPackageReference(PackageSpec project, LockFile lockFile, IEnumerable<RestoreTargetGraph> targetGraphs)
        {
            if (project.RestoreMetadata?.CentralPackageVersionsEnabled == false)
            {
                return;
            }

            // Do not pack anything from the runtime graphs
            // The runtime graphs are added in addition to the graphs without a runtime 
            foreach (var targetGraph in targetGraphs.Where(targetGraph => string.IsNullOrEmpty(targetGraph.RuntimeIdentifier)))
            {
                var centralPackageVersionsForFramework = project.TargetFrameworks.Where(tfmi => tfmi.FrameworkName.Equals(targetGraph.Framework)).FirstOrDefault()?.CentralPackageVersions;

                // The transitive dependencies enforced by the central package version management file are written to the assets to be used by the pack task.
                IEnumerable<LibraryDependency> centralEnforcedTransitiveDependencies = targetGraph
                    .Flattened
                    .Where(graphItem => graphItem.IsCentralTransitive && centralPackageVersionsForFramework?.ContainsKey(graphItem.Key.Name) == true)
                    .Select((graphItem) =>
                    {
                        CentralPackageVersion matchingCentralVersion = centralPackageVersionsForFramework[graphItem.Key.Name];
                        Dictionary<string, LibraryIncludeFlags> dependenciesIncludeFlags = _includeFlagGraphs[targetGraph];

                        var libraryDependency = new LibraryDependency()
                        {
                            LibraryRange = new LibraryRange(matchingCentralVersion.Name, matchingCentralVersion.VersionRange, LibraryDependencyTarget.Package),
                            ReferenceType = LibraryDependencyReferenceType.Transitive,
                            VersionCentrallyManaged = true,
                            IncludeType = dependenciesIncludeFlags[matchingCentralVersion.Name]
                        };

                        return libraryDependency;
                    });

                if (centralEnforcedTransitiveDependencies.Any())
                {
                    var centralEnforcedTransitiveDependencyGroup = new CentralTransitiveDependencyGroup
                            (
                                targetGraph.Framework,
                                centralEnforcedTransitiveDependencies
                            );

                    lockFile.CentralTransitiveDependencyGroups.Add(centralEnforcedTransitiveDependencyGroup);
                }
            }
        }

        private static void PopulatePackageFolders(IEnumerable<string> packageFolders, LockFile lockFile)
        {
            lockFile.PackageFolders.AddRange(packageFolders.Select(path => new LockFileItem(path)));
        }

        private static LockFileLibrary CreateLockFileLibrary(LocalPackageInfo package, string sha512, string path)
        {
            var lockFileLib = new LockFileLibrary
            {
                Name = package.Id,
                Version = package.Version,
                Type = LibraryType.Package,
                Sha512 = sha512,

                // This is the relative path, appended to the global packages folder path. All
                // of the paths in the in the Files property should be appended to this path along
                // with the global packages folder path to get the absolute path to each file in the
                // package.
                Path = path
            };

            foreach (var file in package.Files)
            {
                if (!lockFileLib.HasTools && HasTools(file))
                {
                    lockFileLib.HasTools = true;
                }
                lockFileLib.Files.Add(file);
            }

            return lockFileLib;
        }

        private static bool HasTools(string file)
        {
            return file.StartsWith("tools/", StringComparison.OrdinalIgnoreCase);
        }
    }
}
