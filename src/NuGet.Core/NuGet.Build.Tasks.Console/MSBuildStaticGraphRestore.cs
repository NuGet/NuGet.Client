// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Construction;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Graph;
using Microsoft.Build.Logging;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.ProjectModel;
using NuGet.RuntimeModel;
using NuGet.Versioning;

namespace NuGet.Build.Tasks.Console
{
    internal sealed class MSBuildStaticGraphRestore : IDisposable
    {
        private static readonly Lazy<IMachineWideSettings> MachineWideSettingsLazy = new Lazy<IMachineWideSettings>(() => new XPlatMachineWideSetting());

        /// <summary>
        /// Represents the small list of targets that must be executed in order for PackageReference, PackageDownload, and FrameworkReference items to be accurate.
        /// </summary>
        private static readonly string[] TargetsToBuild =
        {
            "CollectPackageReferences",
            "CollectPackageDownloads",
            "CollectFrameworkReferences",
            "CollectCentralPackageVersions"
        };

        private readonly Lazy<ConsoleLoggingQueue> _loggingQueueLazy;

        private readonly Lazy<MSBuildLogger> _msBuildLoggerLazy;

        private readonly SettingsLoadingContext _settingsLoadContext = new SettingsLoadingContext();

        public MSBuildStaticGraphRestore(bool debug = false)
        {
            Debug = debug;

            _loggingQueueLazy = new Lazy<ConsoleLoggingQueue>(() => new ConsoleLoggingQueue(LoggerVerbosity.Normal));
            _msBuildLoggerLazy = new Lazy<MSBuildLogger>(() => new MSBuildLogger(LoggingQueue.TaskLoggingHelper));
        }

        /// <summary>
        /// Gets or sets a value indicating if this application is being debugged.
        /// </summary>
        public bool Debug { get; }

        /// <summary>
        /// Gets a <see cref="ConsoleLoggingQueue" /> object to be used for logging.
        /// </summary>
        private ConsoleLoggingQueue LoggingQueue => _loggingQueueLazy.Value;

        /// <summary>
        /// Gets a <see cref="MSBuildLogger" /> object to be used for logging.
        /// </summary>
        private MSBuildLogger MSBuildLogger => _msBuildLoggerLazy.Value;

        public void Dispose()
        {
            if (_loggingQueueLazy.IsValueCreated)
            {
                // Disposing the logging queue will wait for the queue to be drained
                _loggingQueueLazy.Value.Dispose();
            }

            _settingsLoadContext.Dispose();
        }

        /// <summary>
        /// Restores the specified projects.
        /// </summary>
        /// <param name="entryProjectFilePath">The main project to restore.  This can be a project for a Visual Studio© Solution File.</param>
        /// <param name="globalProperties">The global properties to use when evaluation MSBuild projects.</param>
        /// <param name="options">The set of options to use when restoring.  These options come from the main MSBuild process and control how restore functions.</param>
        /// <returns><code>true</code> if the restore succeeded, otherwise <code>false</code>.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task<bool> RestoreAsync(string entryProjectFilePath, IDictionary<string, string> globalProperties, IReadOnlyDictionary<string, string> options)
        {
            var dependencyGraphSpec = GetDependencyGraphSpec(entryProjectFilePath, globalProperties);

            // If the dependency graph spec is null, something went wrong evaluating the projects, so return false
            if (dependencyGraphSpec == null)
            {
                return false;
            }

            try
            {
                return await BuildTasksUtility.RestoreAsync(
                    dependencyGraphSpec: dependencyGraphSpec,
                    interactive: IsOptionTrue(nameof(RestoreTaskEx.Interactive), options),
                    recursive: IsOptionTrue(nameof(RestoreTaskEx.Recursive), options),
                    noCache: IsOptionTrue(nameof(RestoreTaskEx.NoCache), options),
                    ignoreFailedSources: IsOptionTrue(nameof(RestoreTaskEx.IgnoreFailedSources), options),
                    disableParallel: IsOptionTrue(nameof(RestoreTaskEx.DisableParallel), options),
                    force: IsOptionTrue(nameof(RestoreTaskEx.Force), options),
                    forceEvaluate: IsOptionTrue(nameof(RestoreTaskEx.ForceEvaluate), options),
                    hideWarningsAndErrors: IsOptionTrue(nameof(RestoreTaskEx.HideWarningsAndErrors), options),
                    restorePC: IsOptionTrue(nameof(RestoreTaskEx.RestorePackagesConfig), options),
                    cleanupAssetsForUnsupportedProjects: IsOptionTrue(nameof(RestoreTaskEx.CleanupAssetsForUnsupportedProjects), options),
                    log: MSBuildLogger,
                    cancellationToken: CancellationToken.None);
            }
            catch (Exception e)
            {
                LoggingQueue.TaskLoggingHelper.LogErrorFromException(e, showStackTrace: true);

                return false;
            }
        }

        /// <summary>
        /// Generates a dependency graph spec for the given properties.
        /// </summary>
        /// <param name="entryProjectFilePath">The main project to generate that graph for.  This can be a project for a Visual Studio© Solution File.</param>
        /// <param name="globalProperties">The global properties to use when evaluation MSBuild projects.</param>
        /// <param name="options">The set of options to use to generate the graph, including the restore graph output path.</param>
        /// <returns><code>true</code> if the dependency graph spec was generated and written, otherwise <code>false</code>.</returns>
        public bool WriteDependencyGraphSpec(string entryProjectFilePath, IDictionary<string, string> globalProperties, IReadOnlyDictionary<string, string> options)
        {
            var dependencyGraphSpec = GetDependencyGraphSpec(entryProjectFilePath, globalProperties);

            try
            {
                if (dependencyGraphSpec == null)
                {
                    LoggingQueue.TaskLoggingHelper.LogError(Strings.Error_DgSpecGenerationFailed);
                    return false;
                }

                if (options.TryGetValue("RestoreGraphOutputPath", out var path))
                {
                    dependencyGraphSpec.Save(path);
                    return true;
                }
                else
                {
                    LoggingQueue.TaskLoggingHelper.LogError(Strings.Error_MissingRestoreGraphOutputPath);
                }
            }
            catch (Exception e)
            {
                LoggingQueue.TaskLoggingHelper.LogErrorFromException(e, showStackTrace: true);
            }
            return false;
        }

        /// <summary>
        /// Gets the framework references per target framework for the specified project.
        /// </summary>
        /// <param name="project">The <see cref="ProjectInstance" /> to get framework references for.</param>
        /// <returns>A <see cref="List{FrameworkDependency}" /> containing the framework references for the specified project.</returns>
        internal static List<FrameworkDependency> GetFrameworkReferences(IMSBuildProject project)
        {
            // Get the unique FrameworkReference items, ignoring duplicates
            List<IMSBuildItem> frameworkReferenceItems = GetDistinctItemsOrEmpty(project, "FrameworkReference").ToList();

            // For best performance, its better to create a list with the exact number of items needed rather than using a LINQ statement or AddRange.  This is because if the list
            // is not allocated with enough items, the list has to be grown which can slow things down
            var frameworkDependencies = new List<FrameworkDependency>(frameworkReferenceItems.Count);

            foreach (var frameworkReferenceItem in frameworkReferenceItems)
            {
                var privateAssets = MSBuildStringUtility.Split(frameworkReferenceItem.GetProperty("PrivateAssets"));

                frameworkDependencies.Add(new FrameworkDependency(frameworkReferenceItem.Identity, FrameworkDependencyFlagsUtils.GetFlags(privateAssets)));
            }

            return frameworkDependencies;
        }

        /// <summary>
        /// Gets the package downloads for the specified project.
        /// </summary>
        /// <param name="project">The <see cref="ProjectInstance" /> to get package downloads for.</param>
        /// <returns>An <see cref="IEnumerable{DownloadDependency}" /> containing the package downloads for the specified project.</returns>
        internal static IEnumerable<DownloadDependency> GetPackageDownloads(IMSBuildProject project)
        {
            // Get the distinct PackageDownload items, ignoring duplicates
            foreach (IMSBuildItem projectItemInstance in GetDistinctItemsOrEmpty(project, "PackageDownload"))
            {
                string id = projectItemInstance.Identity;

                // PackageDownload items can contain multiple versions
                foreach (var version in MSBuildStringUtility.Split(projectItemInstance.GetProperty("Version")))
                {
                    // Validate the version range
                    VersionRange versionRange = !string.IsNullOrWhiteSpace(version) ? VersionRange.Parse(version) : VersionRange.All;

                    if (!(versionRange.HasLowerAndUpperBounds && versionRange.MinVersion.Equals(versionRange.MaxVersion)))
                    {
                        throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.Error_PackageDownload_OnlyExactVersionsAreAllowed, versionRange.OriginalString));
                    }

                    yield return new DownloadDependency(id, versionRange);
                }
            }
        }

        /// <summary>
        /// Gets the centrally defined package version information.
        /// </summary>
        /// <param name="project">The <see cref="ProjectInstance" /> to get PackageVersion for.</param>
        /// <returns>An <see cref="IEnumerable{CentralPackageVersion}" /> containing the package versions for the specified project.</returns>
        internal static Dictionary<string, CentralPackageVersion> GetCentralPackageVersions(IMSBuildProject project)
        {
            var result = new Dictionary<string, CentralPackageVersion>();
            IEnumerable<IMSBuildItem> packageVersionItems = GetDistinctItemsOrEmpty(project, "PackageVersion");

            foreach (var projectItemInstance in packageVersionItems)
            {
                string id = projectItemInstance.Identity;
                string version = projectItemInstance.GetProperty("Version");
                VersionRange versionRange = string.IsNullOrWhiteSpace(version) ? VersionRange.All : VersionRange.Parse(version);

                result.Add(id, new CentralPackageVersion(id, versionRange));
            }

            return result;
        }

        /// <summary>
        /// Gets the package references for the specified project.
        /// </summary>
        /// <param name="project">The <see cref="ProjectInstance" /> to get package references for.</param>
        /// <param name="isCentralPackageVersionManagementEnabled">A flag for central package version management being enabled.</param>
        /// <returns>A <see cref="List{LibraryDependency}" /> containing the package references for the specified project.</returns>
        internal static List<LibraryDependency> GetPackageReferences(IMSBuildProject project, bool isCentralPackageVersionManagementEnabled)
        {
            // Get the distinct PackageReference items, ignoring duplicates
            List<IMSBuildItem> packageReferenceItems = GetDistinctItemsOrEmpty(project, "PackageReference").ToList();

            var libraryDependencies = new List<LibraryDependency>(packageReferenceItems.Count);

            foreach (var packageReferenceItem in packageReferenceItems)
            {
                string version = packageReferenceItem.GetProperty("Version");

                libraryDependencies.Add(new LibraryDependency
                {
                    AutoReferenced = packageReferenceItem.IsPropertyTrue("IsImplicitlyDefined"),
                    GeneratePathProperty = packageReferenceItem.IsPropertyTrue("GeneratePathProperty"),
                    Aliases = packageReferenceItem.GetProperty("Aliases"),
                    IncludeType = GetLibraryIncludeFlags(packageReferenceItem.GetProperty("IncludeAssets"), LibraryIncludeFlags.All) & ~GetLibraryIncludeFlags(packageReferenceItem.GetProperty("ExcludeAssets"), LibraryIncludeFlags.None),
                    LibraryRange = new LibraryRange(
                        packageReferenceItem.Identity,
                        string.IsNullOrWhiteSpace(version) ? isCentralPackageVersionManagementEnabled ? null : VersionRange.All : VersionRange.Parse(version),
                        LibraryDependencyTarget.Package),
                    NoWarn = MSBuildStringUtility.GetNuGetLogCodes(packageReferenceItem.GetProperty("NoWarn")).ToList(),
                    SuppressParent = GetLibraryIncludeFlags(packageReferenceItem.GetProperty("PrivateAssets"), LibraryIncludeFlagUtils.DefaultSuppressParent)
                });
            }

            return libraryDependencies;
        }

        /// <summary>
        /// Gets the packages path for the specified project.
        /// </summary>
        /// <param name="project">The <see cref="IMSBuildItem" /> representing the project.</param>
        /// <param name="settings">The <see cref="ISettings" /> of the project.</param>
        /// <returns>The full path to the packages directory for the specified project.</returns>
        internal static string GetPackagesPath(IMSBuildProject project, ISettings settings)
        {
            return RestoreSettingsUtils.GetValue(
                () => UriUtility.GetAbsolutePath(project.Directory, project.GetGlobalProperty("RestorePackagesPath")),
                () => UriUtility.GetAbsolutePath(project.Directory, project.GetProperty("RestorePackagesPath")),
                () => SettingsUtility.GetGlobalPackagesFolder(settings));
        }

        /// <summary>
        /// Gets the name of the specified project.
        /// </summary>
        /// <param name="project">The <see cref="IMSBuildItem" /> representing the project.</param>
        /// <returns>The name of the specified project.</returns>
        internal static string GetProjectName(IMSBuildProject project)
        {
            string packageId = project.GetProperty("PackageId");

            if (!string.IsNullOrWhiteSpace(packageId))
            {
                // If the PackageId property was specified, return that
                return packageId;
            }

            string assemblyName = project.GetProperty("AssemblyName");

            if (!string.IsNullOrWhiteSpace(assemblyName))
            {
                // If the AssemblyName property was specified, return that
                return assemblyName;
            }

            // By default return the MSBuildProjectName which is a built-in property that represents the name of the project file without the file extension
            return project.GetProperty("MSBuildProjectName");
        }

        /// <summary>
        /// Gets the project references of the specified project.
        /// </summary>
        /// <param name="project">The <see cref="ProjectInstance" /> to get project references for.</param>
        /// <returns>A <see cref="List{ProjectRestoreReference}" /> containing the project references for the specified project.</returns>
        internal static List<ProjectRestoreReference> GetProjectReferences(IMSBuildProject project)
        {
            // Get the unique list of ProjectReference items that have the ReferenceOutputAssembly metadata set to "true", ignoring duplicates
            var projectReferenceItems = project.GetItems("ProjectReference")
                .Where(i => i.IsPropertyTrue("ReferenceOutputAssembly", defaultValue: true))
                .Distinct(ProjectItemInstanceEvaluatedIncludeComparer.Instance)
                .ToList();

            var projectReferences = new List<ProjectRestoreReference>(projectReferenceItems.Count);

            foreach (var projectReferenceItem in projectReferenceItems)
            {
                string fullPath = projectReferenceItem.GetProperty("FullPath");

                projectReferences.Add(new ProjectRestoreReference
                {
                    ExcludeAssets = GetLibraryIncludeFlags(projectReferenceItem.GetProperty("ExcludeAssets"), LibraryIncludeFlags.None),
                    IncludeAssets = GetLibraryIncludeFlags(projectReferenceItem.GetProperty("IncludeAssets"), LibraryIncludeFlags.All),
                    PrivateAssets = GetLibraryIncludeFlags(projectReferenceItem.GetProperty("PrivateAssets"), LibraryIncludeFlagUtils.DefaultSuppressParent),
                    ProjectPath = fullPath,
                    ProjectUniqueName = fullPath
                });
            }

            return projectReferences;
        }

        /// <summary>
        /// Gets the restore metadata framework information for the specified projects.
        /// </summary>
        /// <param name="projects">A <see cref="IReadOnlyDictionary{NuGetFramework,ProjectInstance}" /> representing the target frameworks and their corresponding projects.</param>
        /// <returns>A <see cref="List{ProjectRestoreMetadataFrameworkInfo}" /> containing the restore metadata framework information for the specified project.</returns>
        internal static List<ProjectRestoreMetadataFrameworkInfo> GetProjectRestoreMetadataFrameworkInfos(List<TargetFrameworkInformation> targetFrameworkInfos, IReadOnlyDictionary<string, IMSBuildProject> projects)
        {
            var projectRestoreMetadataFrameworkInfos = new List<ProjectRestoreMetadataFrameworkInfo>(projects.Count);

            foreach (var targetFrameworkInfo in targetFrameworkInfos)
            {
                var project = projects[targetFrameworkInfo.TargetAlias];
                projectRestoreMetadataFrameworkInfos.Add(new ProjectRestoreMetadataFrameworkInfo(targetFrameworkInfo.FrameworkName)
                {
                    TargetAlias = targetFrameworkInfo.TargetAlias,
                    ProjectReferences = GetProjectReferences(project)
                });
            }

            return projectRestoreMetadataFrameworkInfos;
        }

        /// <summary>
        /// Gets the target frameworks for the specified project.
        /// </summary>
        /// <param name="project">An <see cref="IMSBuildProject" /> representing the main project.</param>
        /// <param name="innerNodes">An <see cref="IReadOnlyDictionary{String,IMSBuildProject}" /> representing all inner projects by their target framework.</param>
        /// <returns></returns>
        internal static IReadOnlyDictionary<string, IMSBuildProject> GetProjectTargetFrameworks(IMSBuildProject project, IReadOnlyDictionary<string, IMSBuildProject> innerNodes)
        {
            var projectFrameworkStrings = GetTargetFrameworkStrings(project);
            var projectTargetFrameworks = new Dictionary<string, IMSBuildProject>();

            if (projectFrameworkStrings.Length > 0)
            {
                foreach (var projectTargetFramework in projectFrameworkStrings)
                {
                    // Attempt to get the corresponding project instance for the target framework.  If one is not found, then the project must not target multiple frameworks
                    // and the main project should be used
                    if (!innerNodes.TryGetValue(projectTargetFramework, out IMSBuildProject innerNode))
                    {
                        innerNode = project;
                    }
                    // Add the target framework and associate it with the project instance to be used for gathering details
                    projectTargetFrameworks[projectTargetFramework] = innerNode;
                }
            }
            else
            {
                // Attempt to get the corresponding project instance for the target framework.  If one is not found, then the project must not target multiple frameworks
                // and the main project should be used
                projectTargetFrameworks[string.Empty] = project;
            }

            return projectTargetFrameworks;
        }

        internal static string[] GetTargetFrameworkStrings(IMSBuildProject project)
        {
            var targetFrameworks = project.GetProperty("TargetFrameworks");
            if (string.IsNullOrEmpty(targetFrameworks))
            {
                targetFrameworks = project.GetProperty("TargetFramework");
            }
            var projectFrameworkStrings = MSBuildStringUtility.Split(targetFrameworks);
            return projectFrameworkStrings;
        }

        /// <summary>
        /// Gets the version of the project.
        /// </summary>
        /// <param name="project">The <see cref="IMSBuildItem" /> representing the project.</param>
        /// <returns>The <see cref="NuGetVersion" /> of the specified project if one was found, otherwise <see cref="PackageSpec.DefaultVersion" />.</returns>
        internal static NuGetVersion GetProjectVersion(IMSBuildItem project)
        {
            string version = project.GetProperty("PackageVersion") ?? project.GetProperty("Version");

            if (version == null)
            {
                return PackageSpec.DefaultVersion;
            }

            return NuGetVersion.Parse(version);
        }

        /// <summary>
        /// Gets the repository path for the specified project.
        /// </summary>
        /// <param name="project">The <see cref="IMSBuildItem" /> representing the project.</param>
        /// <param name="settings">The <see cref="ISettings" /> of the specified project.</param>
        /// <returns>The repository path of the specified project.</returns>
        internal static string GetRepositoryPath(IMSBuildProject project, ISettings settings)
        {
            return RestoreSettingsUtils.GetValue(
                () => UriUtility.GetAbsolutePath(project.Directory, project.GetGlobalProperty("RestoreRepositoryPath")),
                () => UriUtility.GetAbsolutePath(project.Directory, project.GetProperty("RestoreRepositoryPath")),
                () => SettingsUtility.GetRepositoryPath(settings),
                () =>
                {
                    string solutionDir = project.GetProperty("SolutionPath");

                    solutionDir = string.Equals(solutionDir, "*Undefined*", StringComparison.OrdinalIgnoreCase)
                        ? project.Directory
                        : Path.GetDirectoryName(solutionDir);

                    return UriUtility.GetAbsolutePath(solutionDir, PackagesConfig.PackagesNodeName);
                });
        }

        /// <summary>
        /// Gets the restore output path for the specified project.
        /// </summary>
        /// <param name="project">The <see cref="IMSBuildItem" /> representing the project.</param>
        /// <returns>The full path to the restore output directory for the specified project if a value is specified, otherwise <code>null</code>.</returns>
        internal static string GetRestoreOutputPath(IMSBuildProject project)
        {
            string outputPath = project.GetProperty("RestoreOutputPath") ?? project.GetProperty("MSBuildProjectExtensionsPath");

            return outputPath == null ? null : Path.GetFullPath(Path.Combine(project.Directory, outputPath));
        }

        /// <summary>
        /// Gets the package sources of the specified project.
        /// </summary>
        /// <param name="project">An <see cref="IMSBuildItem" /> representing the project..</param>
        /// <param name="innerNodes">An <see cref="IReadOnlyCollection{IMSBuildItem}" /> containing the inner nodes of the project if its targets multiple frameworks.</param>
        /// <param name="settings">The <see cref="ISettings" /> of the specified project.</param>
        /// <returns>A <see cref="List{PackageSource}" /> object containing the packages sources for the specified project.</returns>
        internal static List<PackageSource> GetSources(IMSBuildProject project, IReadOnlyCollection<IMSBuildProject> innerNodes, ISettings settings)
        {
            return BuildTasksUtility.GetSources(
                project.GetGlobalProperty("OriginalMSBuildStartupDirectory"),
                project.Directory,
                project.SplitPropertyValueOrNull("RestoreSources"),
                project.SplitGlobalPropertyValueOrNull("RestoreSources"),
                innerNodes.SelectMany(i => MSBuildStringUtility.Split(i.GetProperty("RestoreAdditionalProjectSources"))),
                settings)
                .Select(i => new PackageSource(i))
                .ToList();
        }

        /// <summary>
        /// Gets a value indicating if the specified project is a legacy project.
        /// </summary>
        /// <param name="project">The <see cref="IMSBuildItem" /> representing the project.</param>
        /// <returns><code>true</code> if the specified project is considered legacy, otherwise <code>false</code>.</returns>
        internal static bool IsLegacyProject(IMSBuildItem project)
        {
            // We consider the project to be legacy if it does not specify TargetFramework or TargetFrameworks
            return project.GetProperty("TargetFramework") == null && project.GetProperty("TargetFrameworks") == null;
        }

        /// <summary>
        /// Determines of the specified option is <code>true</code>.
        /// </summary>
        /// <param name="name">The name of the option.</param>
        /// <param name="options">A <see cref="Dictionary{String,String}" />containing options.</param>
        /// <returns><code>true</code> if the specified option is true, otherwise <code>false</code>.</returns>
        internal static bool IsOptionTrue(string name, IReadOnlyDictionary<string, string> options)
        {
            return options.TryGetValue(name, out string value) && StringComparer.OrdinalIgnoreCase.Equals(value, bool.TrueString);
        }

        /// <summary>
        /// Gets the <see cref="LibraryIncludeFlags" /> for the specified value.
        /// </summary>
        /// <param name="value">A semicolon delimited list of include flags.</param>
        /// <param name="defaultValue">The default value ot return if the value contains no flags.</param>
        /// <returns>The <see cref="LibraryIncludeFlags" /> for the specified value, otherwise the <paramref name="defaultValue" />.</returns>
        private static LibraryIncludeFlags GetLibraryIncludeFlags(string value, LibraryIncludeFlags defaultValue)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            string[] parts = MSBuildStringUtility.Split(value);

            return parts.Length > 0 ? LibraryIncludeFlagUtils.GetFlags(parts) : defaultValue;
        }

        /// <summary>
        /// Gets the list of project graph entry points.  If the entry project is a solution, this method returns all of the projects it contains.
        /// </summary>
        /// <param name="entryProjectPath">The full path to the main project or solution file.</param>
        /// <param name="globalProperties">An <see cref="IDictionary{String,String}" /> representing the global properties for the project.</param>
        /// <returns></returns>
        private static List<ProjectGraphEntryPoint> GetProjectGraphEntryPoints(string entryProjectPath, IDictionary<string, string> globalProperties)
        {
            // If the project's extension is .sln, parse it as a Visual Studio solution and return the projects it contains
            if (string.Equals(Path.GetExtension(entryProjectPath), ".sln", StringComparison.OrdinalIgnoreCase))
            {
                var solutionFile = SolutionFile.Parse(entryProjectPath);

                return solutionFile.ProjectsInOrder.Where(i => i.ProjectType == SolutionProjectType.KnownToBeMSBuildFormat).Select(i => new ProjectGraphEntryPoint(i.AbsolutePath, globalProperties)).ToList();
            }

            // Return just the main project in a list if its not a solution file
            return new List<ProjectGraphEntryPoint>
            {
                new ProjectGraphEntryPoint(entryProjectPath, globalProperties),
            };
        }

        /// <summary>
        /// Gets the target framework information for the specified project.  This includes the package references, package downloads, and framework references.
        /// </summary>
        /// <param name="projectInnerNodes">An <see cref="IReadOnlyDictionary{NuGetFramework,ProjectInstance} "/> containing the projects by their target framework.</param>
        /// <param name="isCpvmEnabled">A flag that is true if the Central Package Management was enabled.</param>
        /// <returns>A <see cref="List{TargetFrameworkInformation}" /> containing the target framework information for the specified project.</returns>
        internal static List<TargetFrameworkInformation> GetTargetFrameworkInfos(IReadOnlyDictionary<string, IMSBuildProject> projectInnerNodes, bool isCpvmEnabled)
        {
            var targetFrameworkInfos = new List<TargetFrameworkInformation>(projectInnerNodes.Count);

            foreach (var projectInnerNode in projectInnerNodes)
            {
                var msBuildProjectInstance = projectInnerNode.Value;
                var targetAlias = string.IsNullOrEmpty(projectInnerNode.Key) ? string.Empty : projectInnerNode.Key;

                NuGetFramework targetFramework = MSBuildProjectFrameworkUtility.GetProjectFramework(
                    projectFilePath: projectInnerNode.Value.FullPath,
                    targetFrameworkMoniker: msBuildProjectInstance.GetProperty("TargetFrameworkMoniker"),
                    targetPlatformMoniker: msBuildProjectInstance.GetProperty("TargetPlatformMoniker"),
                    targetPlatformMinVersion: msBuildProjectInstance.GetProperty("TargetPlatformMinVersion"),
                    clrSupport: msBuildProjectInstance.GetProperty("CLRSupport"));

                var targetFrameworkInformation = new TargetFrameworkInformation()
                {
                    FrameworkName = targetFramework,
                    TargetAlias = targetAlias,
                    RuntimeIdentifierGraphPath = msBuildProjectInstance.GetProperty(nameof(TargetFrameworkInformation.RuntimeIdentifierGraphPath))
                };

                var packageTargetFallback = MSBuildStringUtility.Split(msBuildProjectInstance.GetProperty("PackageTargetFallback")).Select(NuGetFramework.Parse).ToList();

                var assetTargetFallback = MSBuildStringUtility.Split(msBuildProjectInstance.GetProperty(nameof(TargetFrameworkInformation.AssetTargetFallback))).Select(NuGetFramework.Parse).ToList();

                AssetTargetFallbackUtility.EnsureValidFallback(packageTargetFallback, assetTargetFallback, msBuildProjectInstance.FullPath);

                AssetTargetFallbackUtility.ApplyFramework(targetFrameworkInformation, packageTargetFallback, assetTargetFallback);

                targetFrameworkInformation.Dependencies.AddRange(GetPackageReferences(msBuildProjectInstance, isCpvmEnabled));

                targetFrameworkInformation.DownloadDependencies.AddRange(GetPackageDownloads(msBuildProjectInstance));

                targetFrameworkInformation.FrameworkReferences.AddRange(GetFrameworkReferences(msBuildProjectInstance));

                if (isCpvmEnabled && targetFrameworkInformation.Dependencies.Any())
                {
                    targetFrameworkInformation.CentralPackageVersions.AddRange(GetCentralPackageVersions(msBuildProjectInstance));
                    LibraryDependency.ApplyCentralVersionInformation(targetFrameworkInformation.Dependencies, targetFrameworkInformation.CentralPackageVersions);
                }

                targetFrameworkInfos.Add(targetFrameworkInformation);
            }

            return targetFrameworkInfos;
        }

        /// <summary>
        /// Gets a <see cref="DependencyGraphSpec" /> for the specified project.
        /// </summary>
        /// <param name="entryProjectPath">The full path to a project or Visual Studio Solution File.</param>
        /// <param name="globalProperties">An <see cref="IDictionary{String,String}" /> containing the global properties to use when evaluation MSBuild projects.</param>
        /// <returns>A <see cref="DependencyGraphSpec" /> for the specified project if they could be loaded, otherwise <code>null</code>.</returns>
        private DependencyGraphSpec GetDependencyGraphSpec(string entryProjectPath, IDictionary<string, string> globalProperties)
        {
            try
            {
                MSBuildLogger.LogMinimal(Strings.DeterminingProjectsToRestore);

                var entryProjects = GetProjectGraphEntryPoints(entryProjectPath, globalProperties);

                // Load the projects via MSBuild and create an array of them since Parallel.ForEach is optimized for arrays
                var projects = LoadProjects(entryProjects)?.ToArray();

                // If no projects were loaded, return null indicating that the projects could not be loaded.
                if (projects == null || projects.Length == 0)
                {
                    return null;
                }

                var sw = Stopwatch.StartNew();

                var dependencyGraphSpec = new DependencyGraphSpec(isReadOnly: true);

                // Unique names created by the MSBuild restore target are project paths, these
                // can be different on case-insensitive file systems for the same project file.
                // To workaround this unique names should be compared based on the OS.
                var uniqueNameComparer = PathUtility.GetStringComparerBasedOnOS();
                var projectPathLookup = new ConcurrentDictionary<string, string>(uniqueNameComparer);

                try
                {
                    // Get the PackageSpecs in parallel because creating each one is relatively expensive so parallelism speeds things up
                    Parallel.ForEach(projects, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, project =>
                    {
                        var packageSpec = GetPackageSpec(project.OuterProject, project);

                        if (packageSpec != null)
                        {
                            // Keep track of all project path casings
                            var uniqueName = packageSpec.RestoreMetadata.ProjectUniqueName;
                            if (uniqueName != null && !projectPathLookup.ContainsKey(uniqueName))
                            {
                                projectPathLookup.TryAdd(uniqueName, uniqueName);
                            }

                            var projectPath = packageSpec.RestoreMetadata.ProjectPath;
                            if (projectPath != null && !projectPathLookup.ContainsKey(projectPath))
                            {
                                projectPathLookup.TryAdd(projectPath, projectPath);
                            }

                            lock (dependencyGraphSpec)
                            {
                                dependencyGraphSpec.AddProject(packageSpec);
                            }
                        }
                    });
                }
                catch (AggregateException e)
                {
                    // Log exceptions thrown while creating PackageSpec objects
                    foreach (var exception in e.Flatten().InnerExceptions)
                    {
                        LoggingQueue.TaskLoggingHelper.LogErrorFromException(exception);
                    }

                    return null;
                }

                // Fix project reference casings to match the original project on case insensitive file systems.
                MSBuildRestoreUtility.NormalizePathCasings(projectPathLookup, dependencyGraphSpec);

                // Add all entry projects if they support restore.  In most cases this is just a single project but if the entry
                // project is a solution, then all projects in the solution are added (if they support restore)
                foreach (var entryPoint in entryProjects)
                {
                    PackageSpec project = dependencyGraphSpec.GetProjectSpec(entryPoint.ProjectFile);

                    if (project != null && BuildTasksUtility.DoesProjectSupportRestore(project))
                    {
                        dependencyGraphSpec.AddRestore(entryPoint.ProjectFile);
                    }
                }

                sw.Stop();

                MSBuildLogger.LogDebug(string.Format(CultureInfo.CurrentCulture, Strings.CreatedDependencyGraphSpec, sw.ElapsedMilliseconds));

                return dependencyGraphSpec;
            }
            catch (Exception e)
            {
                LoggingQueue.TaskLoggingHelper.LogErrorFromException(e, showStackTrace: true);
            }

            return null;
        }

        /// <summary>
        /// Gets a <see cref="PackageSpec" /> for the specified project.
        /// </summary>
        /// <param name="project">An <see cref="IMSBuildProject" /> object that represents the project.</param>
        /// <param name="allInnerNodes">An <see cref="IReadOnlyDictionary{String,IMSBuildProject}" /> that represents all inner projects by their target framework.</param>
        /// <returns></returns>
        private PackageSpec GetPackageSpec(IMSBuildProject project, IReadOnlyDictionary<string, IMSBuildProject> allInnerNodes)
        {
            var settings = RestoreSettingsUtils.ReadSettings(
                project.GetProperty("RestoreSolutionDirectory"),
                project.GetProperty("RestoreRootConfigDirectory") ?? project.Directory,
                UriUtility.GetAbsolutePath(project.Directory, project.GetProperty("RestoreConfigFile")),
                MachineWideSettingsLazy,
                _settingsLoadContext);

            // Get the target frameworks for the project and the project instance for each framework
            var projectsByTargetFramework = GetProjectTargetFrameworks(project, allInnerNodes);

            (ProjectRestoreMetadata restoreMetadata, List<TargetFrameworkInformation> targetFrameworkInfos) = GetProjectRestoreMetadataAndTargetFrameworkInformation(project, projectsByTargetFramework, settings);

            if (restoreMetadata == null || targetFrameworkInfos == null)
            {
                return null;
            }

            var packageSpec = new PackageSpec(targetFrameworkInfos)
            {
                FilePath = project.FullPath,
                Name = restoreMetadata.ProjectName,
                RestoreMetadata = restoreMetadata,
                RuntimeGraph = new RuntimeGraph(
                    MSBuildStringUtility.Split($"{project.GetProperty("RuntimeIdentifiers")};{project.GetProperty("RuntimeIdentifier")}")
                        .Concat(projectsByTargetFramework.Values.SelectMany(i => MSBuildStringUtility.Split($"{i.GetProperty("RuntimeIdentifiers")};{i.GetProperty("RuntimeIdentifier")}")))
                        .Distinct(StringComparer.Ordinal)
                        .Select(rid => new RuntimeDescription(rid))
                        .ToList(),
                    MSBuildStringUtility.Split(project.GetProperty("RuntimeSupports"))
                        .Distinct(StringComparer.Ordinal)
                        .Select(s => new CompatibilityProfile(s))
                        .ToList()
                    ),
                Version = GetProjectVersion(project)
            };

            return packageSpec;
        }

        /// <summary>
        /// Gets the restore metadata and target framework information for the specified project.
        /// </summary>
        /// <param name="project">An <see cref="IMSBuildProject" /> representing the project.</param>
        /// <param name="projectsByTargetFramework">A <see cref="IReadOnlyDictionary{NuGetFramework,IMSBuildProject}" /> containing the inner nodes by target framework.</param>
        /// <param name="settings">The <see cref="ISettings" /> of the specified project.</param>
        /// <returns>A <see cref="Tuple" /> containing the <see cref="ProjectRestoreMetadata" /> and <see cref="List{TargetFrameworkInformation}" /> for the specified project.</returns>
        private (ProjectRestoreMetadata RestoreMetadata, List<TargetFrameworkInformation> TargetFrameworkInfos) GetProjectRestoreMetadataAndTargetFrameworkInformation(IMSBuildProject project, IReadOnlyDictionary<string, IMSBuildProject> projectsByTargetFramework, ISettings settings)
        {
            var projectName = GetProjectName(project);

            var outputPath = GetRestoreOutputPath(project);

            var projectStyleOrNull = BuildTasksUtility.GetProjectRestoreStyleFromProjectProperty(project.GetProperty("RestoreProjectStyle"));
            var isCpvmEnabled = IsCentralVersionsManagementEnabled(project, projectStyleOrNull);
            var targetFrameworkInfos = GetTargetFrameworkInfos(projectsByTargetFramework, isCpvmEnabled);

            var projectStyleResult = BuildTasksUtility.GetProjectRestoreStyle(
                restoreProjectStyle: projectStyleOrNull,
                hasPackageReferenceItems: targetFrameworkInfos.Any(i => i.Dependencies.Any()),
                projectJsonPath: project.GetProperty("_CurrentProjectJsonPath"),
                projectDirectory: project.Directory,
                projectName: project.GetProperty("MSBuildProjectName"),
                log: MSBuildLogger);

            var projectStyle = projectStyleResult.ProjectStyle;

            var innerNodes = projectsByTargetFramework.Values.ToList();

            ProjectRestoreMetadata restoreMetadata;

            if (projectStyle == ProjectStyle.PackagesConfig)
            {
                restoreMetadata = new PackagesConfigProjectRestoreMetadata
                {
                    PackagesConfigPath = projectStyleResult.PackagesConfigFilePath,
                    RepositoryPath = GetRepositoryPath(project, settings)
                };
            }
            else
            {
                restoreMetadata = new ProjectRestoreMetadata
                {
                    // CrossTargeting is on, even if the TargetFrameworks property has only 1 tfm.
                    CrossTargeting = (projectStyle == ProjectStyle.PackageReference || projectStyle == ProjectStyle.DotnetToolReference) && (
                        projectsByTargetFramework.Count > 1 || !string.IsNullOrWhiteSpace(project.GetProperty("TargetFrameworks"))),
                    FallbackFolders = BuildTasksUtility.GetFallbackFolders(
                        project.GetProperty("MSBuildStartupDirectory"),
                        project.Directory,
                        project.SplitPropertyValueOrNull("RestoreFallbackFolders"),
                        project.SplitGlobalPropertyValueOrNull("RestoreFallbackFolders"),
                        innerNodes.SelectMany(i => MSBuildStringUtility.Split(i.GetProperty("RestoreAdditionalProjectFallbackFolders"))),
                        innerNodes.SelectMany(i => MSBuildStringUtility.Split(i.GetProperty("RestoreAdditionalProjectFallbackFoldersExcludes"))),
                        settings),
                    SkipContentFileWrite = IsLegacyProject(project),
                    ValidateRuntimeAssets = project.IsPropertyTrue("ValidateRuntimeIdentifierCompatibility"),
                    CentralPackageVersionsEnabled = isCpvmEnabled && projectStyle == ProjectStyle.PackageReference
                };
            }

            restoreMetadata.CacheFilePath = NoOpRestoreUtilities.GetProjectCacheFilePath(outputPath, project.FullPath);
            restoreMetadata.ConfigFilePaths = settings.GetConfigFilePaths();
            restoreMetadata.OutputPath = outputPath;
            targetFrameworkInfos.ForEach(tfi =>
                restoreMetadata.OriginalTargetFrameworks.Add(
                        !string.IsNullOrEmpty(tfi.TargetAlias) ?
                            tfi.TargetAlias :
                            tfi.FrameworkName.GetShortFolderName()));
            restoreMetadata.PackagesPath = GetPackagesPath(project, settings);
            restoreMetadata.ProjectName = projectName;
            restoreMetadata.ProjectPath = project.FullPath;
            restoreMetadata.ProjectStyle = projectStyle;
            restoreMetadata.ProjectUniqueName = project.FullPath;
            restoreMetadata.ProjectWideWarningProperties = WarningProperties.GetWarningProperties(project.GetProperty("TreatWarningsAsErrors"), project.GetProperty("WarningsAsErrors"), project.GetProperty("NoWarn"));
            restoreMetadata.RestoreLockProperties = new RestoreLockProperties(project.GetProperty("RestorePackagesWithLockFile"), project.GetProperty("NuGetLockFilePath"), project.IsPropertyTrue("RestoreLockedMode"));
            restoreMetadata.Sources = GetSources(project, innerNodes, settings);
            restoreMetadata.TargetFrameworks = GetProjectRestoreMetadataFrameworkInfos(targetFrameworkInfos, projectsByTargetFramework);

            return (restoreMetadata, targetFrameworkInfos);
        }

        /// <summary>
        /// Recursively loads and evaluates MSBuild projects.
        /// </summary>
        /// <param name="entryProjects">An <see cref="IEnumerable{ProjectGraphEntryPoint}" /> containing the entry projects to load.</param>
        /// <returns>An <see cref="ICollection{ProjectWithInnerNodes}" /> object containing projects and their inner nodes if they are targeting multiple frameworks.</returns>
        private ICollection<ProjectWithInnerNodes> LoadProjects(IEnumerable<ProjectGraphEntryPoint> entryProjects)
        {
            var loggers = new List<Microsoft.Build.Framework.ILogger>
            {
                LoggingQueue
            };

            // Get user specified parameters for a binary logger
            string binlogParameters = Environment.GetEnvironmentVariable("RESTORE_TASK_BINLOG_PARAMETERS");

            // Attach the binary logger if Debug or binlog parameters were specified
            if (Debug || !string.IsNullOrWhiteSpace(binlogParameters))
            {
                loggers.Add(new BinaryLogger
                {
                    // Default the binlog parameters if only the debug option was specified
                    Parameters = binlogParameters ?? "LogFile=nuget.binlog"
                });
            }

            var projects = new ConcurrentDictionary<string, ProjectWithInnerNodes>(StringComparer.OrdinalIgnoreCase);

            var projectCollection = new ProjectCollection(
                globalProperties: null,
                // Attach a logger for evaluation only if the Debug option is set
                loggers: loggers,
                remoteLoggers: null,
                toolsetDefinitionLocations: ToolsetDefinitionLocations.Default,
                // Having more than 1 node spins up multiple msbuild.exe instances to run builds in parallel
                // However, these targets complete so quickly that the added overhead makes it take longer
                maxNodeCount: 1,
                onlyLogCriticalEvents: false,
                // Loading projects as readonly makes parsing a little faster since comments and whitespace can be ignored
                loadProjectsReadOnly: true);

            var failedBuildSubmissions = new ConcurrentBag<BuildSubmission>();

            try
            {
                var sw = Stopwatch.StartNew();

                var evaluationContext = EvaluationContext.Create(EvaluationContext.SharingPolicy.Shared);

                ProjectGraph projectGraph;

                int buildCount = 0;

                var buildParameters = new BuildParameters(projectCollection)
                {
                    // Use the same loggers as the project collection
                    Loggers = projectCollection.Loggers,
                    LogTaskInputs = Debug
                };

                // BeginBuild starts a queue which accepts build requests and applies the build parameters to all of them
                BuildManager.DefaultBuildManager.BeginBuild(buildParameters);

                try
                {
                    // Create a ProjectGraph object and pass a factory method which creates a ProjectInstance
                    projectGraph = new ProjectGraph(entryProjects, projectCollection, (path, properties, collection) =>
                    {
                        var projectOptions = new ProjectOptions
                        {
                            EvaluationContext = evaluationContext,
                            GlobalProperties = properties,
                            // Ignore bad imports to maximize the chances of being able to load the project and restore
                            LoadSettings = ProjectLoadSettings.IgnoreEmptyImports | ProjectLoadSettings.IgnoreInvalidImports | ProjectLoadSettings.IgnoreMissingImports | ProjectLoadSettings.DoNotEvaluateElementsWithFalseCondition,
                            ProjectCollection = collection
                        };

                        // Create a Project object which does the evaluation
                        var project = Project.FromFile(path, projectOptions);

                        // Create a ProjectInstance object which is what this factory needs to return
                        var projectInstance = project.CreateProjectInstance(ProjectInstanceSettings.None, evaluationContext);

                        if (!projectInstance.Targets.ContainsKey("_IsProjectRestoreSupported") || properties.TryGetValue("TargetFramework", out var targetFramework) && string.IsNullOrWhiteSpace(targetFramework))
                        {
                            // In rare cases, users can set an empty TargetFramework value in a project-to-project reference.  Static Graph will respect that
                            // but NuGet does not need to do anything with that instance of the project since the actual project is still loaded correctly
                            // with its actual TargetFramework.
                            return projectInstance;
                        }

                        // If the project supports restore, queue up a build of the 3 targets needed for restore
                        BuildManager.DefaultBuildManager
                            .PendBuildRequest(
                                new BuildRequestData(
                                    projectInstance,
                                    TargetsToBuild,
                                    hostServices: null,
                                    // Suppresses an error that a target does not exist because it may or may not contain the targets that we're running
                                    BuildRequestDataFlags.SkipNonexistentTargets))
                            .ExecuteAsync(
                                callback: buildSubmission =>
                                {
                                    // If the build failed, add its result to the list to be processed later
                                    if (buildSubmission.BuildResult.OverallResult == BuildResultCode.Failure)
                                    {
                                        failedBuildSubmissions.Add(buildSubmission);
                                    }
                                },
                                context: null);

                        Interlocked.Increment(ref buildCount);

                        // Add the project instance to the list, if its an inner node for a multi-targeting project it will be added to the inner collection
                        projects.AddOrUpdate(
                            path,
                            key => new ProjectWithInnerNodes(targetFramework, new MSBuildProjectInstance(projectInstance)),
                            (_, item) => item.Add(targetFramework, new MSBuildProjectInstance(projectInstance)));

                        return projectInstance;
                    });
                }
                finally
                {
                    // EndBuild blocks until all builds are complete
                    BuildManager.DefaultBuildManager.EndBuild();
                }

                sw.Stop();

                MSBuildLogger.LogInformation(string.Format(CultureInfo.CurrentCulture, Strings.ProjectEvaluationSummary, projectGraph.ProjectNodes.Count, sw.ElapsedMilliseconds, buildCount, failedBuildSubmissions.Count));

                if (failedBuildSubmissions.Any())
                {
                    // Return null if any builds failed, they will have logged errors
                    return null;
                }
            }
            catch (Exception e)
            {
                LoggingQueue.TaskLoggingHelper.LogErrorFromException(e, showStackTrace: true);

                return null;
            }
            finally
            {
                projectCollection.Dispose();
            }

            // Just return the projects not the whole dictionary as it was just used to group the projects together
            return projects.Values;
        }

        /// <summary>
        /// It evaluates the project and returns true if the project has CentralPackageVersionManagement enabled.
        /// </summary>
        /// <param name="project">The <see cref="IMSBuildProject"/> for which the CentralPackageVersionManagement will be evaluated.</param>
        /// <param name="projectStyle">The <see cref="ProjectStyle?"/>. Null is the project did not have a defined ProjectRestoreStyle property.</param>
        /// <returns>True if the project has CentralPackageVersionManagement enabled and the project is PackageReference or the projectStyle is null.</returns>
        internal static bool IsCentralVersionsManagementEnabled(IMSBuildProject project, ProjectStyle? projectStyle)
        {
            if (!projectStyle.HasValue || (projectStyle.Value == ProjectStyle.PackageReference))
            {
                return StringComparer.OrdinalIgnoreCase.Equals(project.GetProperty("_CentralPackageVersionsEnabled"), bool.TrueString);
            }
            return false;
        }

        /// <summary>
        /// Returns the list of distinct items with the <paramref name="itemName"/> name.
        /// Two items are equal if they have the same <see cref="IMSBuildItem.Identity"/>.
        /// </summary>
        /// <param name="project">The project.</param>
        /// <param name="itemName">The item name.</param>
        /// <returns>Returns the list of items with the <paramref name="itemName"/>. If the item does not exist it will return an empty list.</returns>
        private static IEnumerable<IMSBuildItem> GetDistinctItemsOrEmpty(IMSBuildProject project, string itemName)
        {
            return project.GetItems(itemName)?.Distinct(ProjectItemInstanceEvaluatedIncludeComparer.Instance) ?? Enumerable.Empty<IMSBuildItem>();
        }
    }
}
