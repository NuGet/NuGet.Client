using System;
using System.Collections.Generic;
using System.Globalization;
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

namespace NuGet.Commands
{
    internal class CompatibilityChecker
    {
        private readonly IReadOnlyList<NuGetv3LocalRepository> _localRepositories;
        private readonly LockFile _lockFile;
        private readonly ILogger _log;

        public CompatibilityChecker(IReadOnlyList<NuGetv3LocalRepository> localRepositories, LockFile lockFile, ILogger log)
        {
            _localRepositories = localRepositories;
            _lockFile = lockFile;
            _log = log;
        }

        internal CompatibilityCheckResult Check(
            RestoreTargetGraph graph,
            Dictionary<string, LibraryIncludeFlags> includeFlags)
        {
            // Verify that each package contains assets for the TFM,
            // packages pulled into TFMs that do not contain compatible
            // assets will be marked as incompatible.
            var issues = new List<CompatibilityIssue>();
            foreach (var node in graph.Flattened)
            {
                _log.LogDebug(string.Format(CultureInfo.CurrentCulture, Strings.Log_CheckingPackageCompatibility, node.Key.Name, node.Key.Version, graph.Name));

                // Check project compatibility
                if (node.Key.Type == LibraryType.Project)
                {
                    // Get the full library
                    var localMatch = node.Data?.Match as LocalMatch;
                    if (localMatch == null || !IsProjectCompatible(localMatch.LocalLibrary))
                    {
                        var available = new List<NuGetFramework>();

                        // If the project info is available find all available frameworks
                        if (localMatch?.LocalLibrary != null)
                        {
                            available = GetProjectFrameworks(localMatch.LocalLibrary);
                        }

                        // Create issue
                        var issue = CompatibilityIssue.IncompatibleProject(
                            new PackageIdentity(node.Key.Name, node.Key.Version),
                            graph.Framework,
                            graph.RuntimeIdentifier,
                            available);

                        issues.Add(issue);
                        _log.LogError(issue.Format());
                    }

                    // Skip further checks on projects
                    continue;
                }

                // Find the include/exclude flags for this package
                LibraryIncludeFlags packageIncludeFlags;
                if (!includeFlags.TryGetValue(node.Key.Name, out packageIncludeFlags))
                {
                    packageIncludeFlags = LibraryIncludeFlags.All;
                }

                // If the package has compile and runtime assets excluded the compatibility check
                // is not needed. Packages with no ref or lib entries are considered
                // compatible in IsCompatible.
                if ((packageIncludeFlags &
                        (LibraryIncludeFlags.Compile
                        | LibraryIncludeFlags.Runtime)) == LibraryIncludeFlags.None)
                {
                    continue;
                }

                var compatibilityData = GetCompatibilityData(graph, node.Key);
                if (compatibilityData == null)
                {
                    continue;
                }

                if (!IsCompatible(compatibilityData))
                {
                    var available = GetPackageFrameworks(compatibilityData, graph);

                    var issue = CompatibilityIssue.IncompatiblePackage(
                        new PackageIdentity(node.Key.Name, node.Key.Version),
                        graph.Framework,
                        graph.RuntimeIdentifier,
                        available);

                    issues.Add(issue);
                    _log.LogError(issue.Format());
                }
            }

            return new CompatibilityCheckResult(graph, issues);
        }

        private static List<NuGetFramework> GetPackageFrameworks(
            CompatibilityData compatibilityData,
            RestoreTargetGraph graph)
        {
            var available = new HashSet<NuGetFramework>();

            var contentItems = new ContentItemCollection();
            contentItems.Load(compatibilityData.Files);

            var patterns = new[]
            {
                graph.Conventions.Patterns.ResourceAssemblies,
                graph.Conventions.Patterns.CompileAssemblies,
                graph.Conventions.Patterns.RuntimeAssemblies,
                graph.Conventions.Patterns.ContentFiles
            };

            foreach (var pattern in patterns)
            {
                foreach (var group in contentItems.FindItemGroups(pattern))
                {
                    object tfmObj = null;
                    object ridObj = null;
                    group.Properties.TryGetValue(ManagedCodeConventions.PropertyNames.RuntimeIdentifier, out ridObj);
                    group.Properties.TryGetValue(ManagedCodeConventions.PropertyNames.TargetFrameworkMoniker, out tfmObj);

                    NuGetFramework tfm = tfmObj as NuGetFramework;

                    // RID specific items should be ignored here since they are only used in the runtime assem check
                    if (ridObj == null && tfm?.IsSpecificFramework == true)
                    {
                        available.Add(tfm);
                    }
                }
            }

            return available.ToList();
        }

        private static List<NuGetFramework> GetProjectFrameworks(Library localLibrary)
        {
            var available = new List<NuGetFramework>();

            object frameworksObject;
            if (localLibrary.Items.TryGetValue(
                KnownLibraryProperties.ProjectFrameworks,
                out frameworksObject))
            {
                available = (List<NuGetFramework>)frameworksObject;
            }

            return available;
        }

        private static bool IsProjectCompatible(Library library)
        {
            object frameworkInfoObject;
            if (library.Items.TryGetValue(
                KnownLibraryProperties.TargetFrameworkInformation,
                out frameworkInfoObject))
            {
                var targetFrameworkInformation = (TargetFrameworkInformation)frameworkInfoObject;

                // Verify that a valid framework was selected
                return (targetFrameworkInformation.FrameworkName != null
                    && targetFrameworkInformation.FrameworkName != NuGetFramework.UnsupportedFramework);
            }
            else
            {
                // For external projects that do not have any target framework info, assume
                // compatibility was checked before hand
                return true;
            }
        }

        private bool IsCompatible(CompatibilityData compatibilityData)
        {
            // A package is compatible if it has...
            return
                compatibilityData.TargetLibrary.RuntimeAssemblies.Count > 0 ||                          // Runtime Assemblies, or
                compatibilityData.TargetLibrary.CompileTimeAssemblies.Count > 0 ||                      // Compile-time Assemblies, or
                compatibilityData.TargetLibrary.FrameworkAssemblies.Count > 0 ||                        // Framework Assemblies, or
                compatibilityData.TargetLibrary.ContentFiles.Count > 0 ||                               // Shared content
                compatibilityData.TargetLibrary.ResourceAssemblies.Count > 0 ||                         // Resources (satellite package)
                compatibilityData.TargetLibrary.Build.Count > 0 ||                                      // Build
                compatibilityData.TargetLibrary.BuildMultiTargeting.Count > 0 ||                        // Cross targeting build
                !compatibilityData.Files.Any(p =>
                    p.StartsWith("ref/", StringComparison.OrdinalIgnoreCase)
                    || p.StartsWith("lib/", StringComparison.OrdinalIgnoreCase));                       // No assemblies at all (for any TxM)
        }

        private CompatibilityData GetCompatibilityData(RestoreTargetGraph graph, LibraryIdentity libraryId)
        {
            LockFileTargetLibrary targetLibrary = null;
            var target = _lockFile.Targets.FirstOrDefault(t => Equals(t.TargetFramework, graph.Framework) && string.Equals(t.RuntimeIdentifier, graph.RuntimeIdentifier, StringComparison.Ordinal));
            if (target != null)
            {
                targetLibrary = target.Libraries.FirstOrDefault(t => t.Name.Equals(libraryId.Name) && t.Version.Equals(libraryId.Version));
            }

            IEnumerable<string> files = null;
            var lockFileLibrary = _lockFile.Libraries.FirstOrDefault(l => l.Name.Equals(libraryId.Name) && l.Version.Equals(libraryId.Version));
            if (lockFileLibrary != null)
            {
                files = lockFileLibrary.Files;
            }

            if (files != null && targetLibrary != null)
            {
                // Everything we need is in the lock file!
                return new CompatibilityData(lockFileLibrary.Files, targetLibrary);
            }
            else
            {
                // We need to generate some of the data. We'll need the local packge info to do that
                var packageInfo = NuGetv3LocalRepositoryUtility.GetPackage(
                    _localRepositories,
                    libraryId.Name,
                    libraryId.Version);

                if (packageInfo == null)
                {
                    return null;
                }

                // Collect the file list if necessary
                if (files == null)
                {
                    using (var packageReader = new PackageFolderReader(packageInfo.Package.ExpandedPath))
                    {
                        if (Path.DirectorySeparatorChar != '/')
                        {
                            files = packageReader
                                    .GetFiles()
                                    .Select(p => p.Replace(Path.DirectorySeparatorChar, '/'))
                                    .ToList();
                        }
                        else
                        {
                            files = packageReader.GetFiles().ToList();
                        }
                    }
                }

                // Generate the target library if necessary
                if (targetLibrary == null)
                {
                    targetLibrary = LockFileUtils.CreateLockFileTargetLibrary(
                        library: null,
                        package: packageInfo.Package,
                        targetGraph: graph,
                        dependencyType: LibraryIncludeFlags.All);
                }

                return new CompatibilityData(files, targetLibrary);
            }
        }

        private class CompatibilityData
        {
            public IEnumerable<string> Files { get; }
            public LockFileTargetLibrary TargetLibrary { get; }

            public CompatibilityData(IEnumerable<string> files, LockFileTargetLibrary targetLibrary)
            {
                Files = files;
                TargetLibrary = targetLibrary;
            }
        }
    }
}
