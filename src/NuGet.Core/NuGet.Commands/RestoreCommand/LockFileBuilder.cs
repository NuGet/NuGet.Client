using System;
using System.Collections.Generic;
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
            RemoteWalkContext context,
            IEnumerable<ToolRestoreResult> toolRestoreResults)
        {
            var lockFile = new LockFile();
            lockFile.Version = _lockFileVersion;

            var previousLibraries = previousLockFile?.Libraries.ToDictionary(l => Tuple.Create(l.Name, l.Version));

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

                    var sha512 = File.ReadAllText(resolver.GetHashPath(package.Id, package.Version));

                    LockFileLibrary previousLibrary = null;
                    previousLibraries?.TryGetValue(Tuple.Create(library.Name, library.Version), out previousLibrary);

                    var lockFileLib = previousLibrary;

                    // If we have the same library in the lock file already, use that.
                    if (previousLibrary == null || previousLibrary.Sha512 != sha512)
                    {
                        var path = resolver.GetPackageDirectory(package.Id, package.Version);
                        path = PathUtility.GetPathWithForwardSlashes(path);

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

                    if (library.Type == LibraryType.Project || library.Type == LibraryType.ExternalProject)
                    {
                        if (project.Name.Equals(library.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            // Do not include the project itself as a library.
                            continue;
                        }

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
                        var lib = new LockFileTargetLibrary()
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

                        object compileAssetObject;
                        if (localMatch.LocalLibrary.Items.TryGetValue(
                            KnownLibraryProperties.CompileAsset,
                            out compileAssetObject))
                        {
                            var item = new LockFileItem((string)compileAssetObject);
                            lib.CompileTimeAssemblies.Add(item);
                            lib.RuntimeAssemblies.Add(item);
                        }

                        // Add frameworkAssemblies for projects
                        object frameworkAssembliesObject;
                        if (localMatch.LocalLibrary.Items.TryGetValue(
                            KnownLibraryProperties.FrameworkAssemblies,
                            out frameworkAssembliesObject))
                        {
                            lib.FrameworkAssemblies.AddRange((List<string>)frameworkAssembliesObject);
                        }

                        target.Libraries.Add(lib);
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

                        // include flags
                        LibraryIncludeFlags includeFlags;
                        if (!flattenedFlags.TryGetValue(library.Name, out includeFlags))
                        {
                            includeFlags = ~LibraryIncludeFlags.ContentFiles;
                        }

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

            PopulateProjectFileToolGroups(project, lockFile);

            PopulateTools(toolRestoreResults, lockFile);

            return lockFile;
        }

        private static void PopulateTools(IEnumerable<ToolRestoreResult> toolRestoreResults, LockFile lockFile)
        {
            // Add the tool targets (this states what tools were actually restored)
            var toolTargets = new Dictionary<string, LockFileTarget>();
            foreach (var result in toolRestoreResults)
            {
                if (result.FileTargetLibrary == null)
                {
                    continue;
                }

                var newTarget = new LockFileTarget
                {
                    TargetFramework = result.LockFileTarget.TargetFramework,
                    RuntimeIdentifier = result.LockFileTarget.RuntimeIdentifier
                };
                LockFileTarget existingTarget;
                if (!toolTargets.TryGetValue(newTarget.Name, out existingTarget))
                {
                    toolTargets[newTarget.Name] = newTarget;
                    existingTarget = newTarget;
                    lockFile.Tools.Add(newTarget);
                }

                existingTarget.Libraries.Add(result.FileTargetLibrary);
            }
        }

        private static void PopulateProjectFileToolGroups(PackageSpec project, LockFile lockFile)
        {
            // Add the tool dependencies (this states what the project requires)
            if (project.Tools.Any())
            {
                lockFile.ProjectFileToolGroups.Add(new ProjectFileDependencyGroup(
                    LockFile.ToolFramework.ToString(),
                    project.Tools
                        .Select(x => x.LibraryRange.ToLockFileDependencyGroupString())
                        .OrderBy(dependency => dependency, StringComparer.Ordinal)));
            }
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
    }
}