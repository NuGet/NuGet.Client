// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using NuGet.Common;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Repositories;
using NuGet.Versioning;

namespace NuGet.Commands
{
    public class LockFileBuilder
    {
        private readonly int _lockFileVersion;
        private readonly ILogger _logger;
        private readonly Dictionary<RestoreTargetGraph, Dictionary<string, LibraryIncludeFlags>> _includeFlagGraphs;

        public LockFileBuilder(int lockFileVersion, ILogger logger, Dictionary<RestoreTargetGraph, Dictionary<string, LibraryIncludeFlags>> includeFlagGraphs)
        {
            _lockFileVersion = lockFileVersion;
            _logger = logger;
            _includeFlagGraphs = includeFlagGraphs;
        }

        public LockFile CreateLockFile(
            LockFile previousLockFile,
            PackageSpec project,
            IEnumerable<RestoreTargetGraph> targetGraphs,
            IReadOnlyList<NuGetv3LocalRepository> localRepositories,
            RemoteWalkContext context)
        {
            var lockFile = new LockFile();
            lockFile.Version = _lockFileVersion;

            var previousLibraries = previousLockFile?.Libraries.ToDictionary(l => Tuple.Create(l.Name, l.Version));

            if (project.RestoreMetadata?.ProjectStyle == ProjectStyle.PackageReference)
            {
                AddProjectFileDependenciesForNETCore(project, lockFile, targetGraphs);
            }
            else
            {
                AddProjectFileDependenciesForSpec(project, lockFile);
            }

            // Record all libraries used
            foreach (var item in targetGraphs.SelectMany(g => g.Flattened).Distinct()
                .OrderBy(x => x.Data.Match.Library))
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
                    LocalMatch localMatch = (LocalMatch)item.Data.Match;

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

                    if (packageInfo == null)
                    {
                        continue;
                    }

                    var package = packageInfo.Package;
                    var resolver = packageInfo.Repository.PathResolver;

                    LockFileLibrary previousLibrary = null;
                    if (previousLibraries?.TryGetValue(Tuple.Create(package.Id, package.Version), out previousLibrary) == true)
                    {
                        // We mutate this previous library so we must take a clone of it. This is
                        // important because later, when deciding whether the lock file has changed,
                        // we compare the new lock file to the previous (in-memory) lock file.
                        previousLibrary = previousLibrary.Clone();
                    }

                    var sha512 = File.ReadAllText(resolver.GetHashPath(package.Id, package.Version));
                    var path = PathUtility.GetPathWithForwardSlashes(
                        resolver.GetPackageDirectory(package.Id, package.Version));

                    var lockFileLib = previousLibrary;

                    // If we have the same library in the lock file already, use that.
                    if (previousLibrary == null ||
                        previousLibrary.Sha512 != sha512 ||
                        previousLibrary.Path != path)
                    {
                        lockFileLib = CreateLockFileLibrary(
                            package,
                            sha512,
                            path);
                    }
                    else if (Path.DirectorySeparatorChar != LockFile.DirectorySeparatorChar)
                    {
                        // Fix slashes for content model patterns
                        lockFileLib.Files = lockFileLib.Files
                            .Select(p => p.Replace(Path.DirectorySeparatorChar, LockFile.DirectorySeparatorChar))
                            .ToList();
                    }

                    lockFile.Libraries.Add(lockFileLib);

                    var packageIdentity = new PackageIdentity(lockFileLib.Name, lockFileLib.Version);
                    context.PackageFileCache.TryAdd(packageIdentity, lockFileLib.Files);
                }
            }

            var libraries = lockFile.Libraries.ToDictionary(lib => Tuple.Create(lib.Name, lib.Version));

            var warnForImports = project.TargetFrameworks.Any(framework => framework.Warn);
            var librariesWithWarnings = new HashSet<LibraryIdentity>();

            var rootProjectStyle = project.RestoreMetadata?.ProjectStyle ?? ProjectStyle.Unknown;

            // Add the targets
            foreach (var targetGraph in targetGraphs
                .OrderBy(graph => graph.Framework.ToString(), StringComparer.Ordinal)
                .ThenBy(graph => graph.RuntimeIdentifier, StringComparer.Ordinal))
            {
                var target = new LockFileTarget();
                target.TargetFramework = targetGraph.Framework;
                target.RuntimeIdentifier = targetGraph.RuntimeIdentifier;

                var flattenedFlags = IncludeFlagUtils.FlattenDependencyTypes(_includeFlagGraphs, project, targetGraph);

                var fallbackFramework = target.TargetFramework as FallbackFramework;
                var warnForImportsOnGraph = warnForImports && fallbackFramework != null;

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

                        var targetLibrary = LockFileUtils.CreateLockFileTargetLibrary(
                            libraries[Tuple.Create(library.Name, library.Version)],
                            package,
                            targetGraph,
                            dependencyType: includeFlags,
                            targetFrameworkOverride: null,
                            dependencies: graphItem.Data.Dependencies);

                        target.Libraries.Add(targetLibrary);

                        // Log warnings if the target library used the fallback framework
                        if (warnForImportsOnGraph && !librariesWithWarnings.Contains(library))
                        {
                            var nonFallbackFramework = new NuGetFramework(fallbackFramework);

                            var targetLibraryWithoutFallback = LockFileUtils.CreateLockFileTargetLibrary(
                                libraries[Tuple.Create(library.Name, library.Version)],
                                package,
                                targetGraph,
                                targetFrameworkOverride: nonFallbackFramework,
                                dependencyType: includeFlags,
                                dependencies: graphItem.Data.Dependencies);

                            if (!targetLibrary.Equals(targetLibraryWithoutFallback))
                            {
                                var libraryName = $"{library.Name} {library.Version}";
                                _logger.LogWarning(string.Format(CultureInfo.CurrentCulture, Strings.Log_ImportsFallbackWarning, libraryName, String.Join(", ", fallbackFramework.Fallback), nonFallbackFramework));

                                // only log the warning once per library
                                librariesWithWarnings.Add(library);
                            }
                        }
                    }
                }

                lockFile.Targets.Add(target);
            }

            PopulatePackageFolders(localRepositories.Select(repo => repo.RepositoryRoot).Distinct(), lockFile);

            // Add the original package spec to the lock file.
            lockFile.PackageSpec = project;

            return lockFile;
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

        private static void AddProjectFileDependenciesForNETCore(PackageSpec project, LockFile lockFile, IEnumerable<RestoreTargetGraph> targetGraphs)
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

                Debug.Assert(resolvedEntry != null, "Unable to find project entry in target graph, project references will not be added");

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

        private static void PopulatePackageFolders(IEnumerable<string> packageFolders, LockFile lockFile)
        {
            lockFile.PackageFolders.AddRange(packageFolders.Select(path => new LockFileItem(path)));
        }

        private static LockFileLibrary CreateLockFileLibrary(LocalPackageInfo package, string sha512, string path)
        {
            var lockFileLib = new LockFileLibrary();
            
            lockFileLib.Name = package.Id;
            lockFileLib.Version = package.Version;
            lockFileLib.Type = LibraryType.Package;
            lockFileLib.Sha512 = sha512;

            // This is the relative path, appended to the global packages folder path. All
            // of the paths in the in the Files property should be appended to this path along
            // with the global packages folder path to get the absolute path to each file in the
            // package.
            lockFileLib.Path = path;

            using (var packageReader = new PackageFolderReader(package.ExpandedPath))
            {
                // Get package files, excluding directory entries and OPC files
                // This is sorted before it is written out
                lockFileLib.Files = packageReader
                    .GetFiles()
                    .Where(file => IsAllowedLibraryFile(file))
                    .ToList();
            }

            return lockFileLib;
        }

        /// <summary>
        /// True if the file should be added to the lock file library
        /// Fale if it is an OPC file or empty directory
        /// </summary>
        private static bool IsAllowedLibraryFile(string path)
        {
            switch (path)
            {
                case "_rels/.rels":
                case "[Content_Types].xml":
                    return false;
            }

            if (path.EndsWith("/", StringComparison.Ordinal)
                || path.EndsWith(".psmdcp", StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }
    }
}