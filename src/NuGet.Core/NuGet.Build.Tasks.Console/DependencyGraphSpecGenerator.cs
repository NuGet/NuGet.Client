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
using ILogger = Microsoft.Build.Framework.ILogger;

namespace NuGet.Build.Tasks.Console
{
    internal class DependencyGraphSpecGenerator : IDisposable
    {
        private static readonly Lazy<IMachineWideSettings> MachineWideSettingsLazy = new Lazy<IMachineWideSettings>(() => new XPlatMachineWideSetting());

        /// <summary>
        /// Represents the small list of targets that must be executed in order for PackageReference, PackageDownload, and FrameworkReference items to be accurate.
        /// </summary>
        private static readonly string[] TargetsToBuild =
        {
            "CollectPackageReferences",
            "CollectPackageDownloads",
            "CollectFrameworkReferences"
        };

        private readonly Lazy<ConsoleLoggingQueue> _loggingQueueLazy;

        private readonly Lazy<MSBuildLogger> _msBuildLoggerLazy;

        private readonly SettingsLoadingContext _settingsLoadContext = new SettingsLoadingContext();

        public DependencyGraphSpecGenerator(bool debug = false)
        {
            Debug = debug;

            // TODO: Pass verbosity from main process
            _loggingQueueLazy = new Lazy<ConsoleLoggingQueue>(() => new ConsoleLoggingQueue(LoggerVerbosity.Normal));
            _msBuildLoggerLazy = new Lazy<MSBuildLogger>(() => new MSBuildLogger(LoggingQueue.TaskLoggingHelper));
        }

        /// <summary>
        /// Gets or sets a value indicating if this application is being debugged.
        /// </summary>
        public bool Debug { get; set; }

        /// <summary>
        /// Gets a <see cref="ConsoleLoggingQueue" /> object to be used for logging.
        /// </summary>
        protected ConsoleLoggingQueue LoggingQueue => _loggingQueueLazy.Value;

        /// <summary>
        /// Gets a <see cref="MSBuildLogger" /> object to be used for logging.
        /// </summary>
        protected MSBuildLogger MSBuildLogger => _msBuildLoggerLazy.Value;

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
        /// <param name="entryProjectPath">The main project to restore.  This can be a project for a Visual Studio© Solution File.</param>
        /// <param name="globalProperties">The global properties to use when evaluation MSBuild projects.</param>
        /// <param name="options">The set of options to use when restoring.  These options come from the main MSBuild process and control how restore functions.</param>
        /// <returns><code>true</code> if the restore succeeded, otherwise <code>false</code>.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task<bool> RestoreAsync(string entryProjectPath, Dictionary<string, string> globalProperties, Dictionary<string, string> options)
        {
            var dependencyGraphSpec = GetDependencyGraphSpec(entryProjectPath, globalProperties);

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
        /// Gets the package fallback folders for the specified project.
        /// </summary>
        /// <param name="project">An <see cref="IMSBuildItem" /> that represents the project.</param>
        /// <param name="projectDirectory">The full path to the directory that contains the project.</param>
        /// <param name="innerNodes">A <see cref="IReadOnlyCollection{ProjectInstance}" /> representing the inner nodes of the project if it is targeting multiple frameworks.</param>
        /// <param name="settings">The <see cref="ISettings" /> for the specified project.</param>
        /// <returns>An <see cref="IEnumerable{String}" /> containing the fallback folders for the specified project.</returns>
        private static IEnumerable<string> GetFallbackFolders(IMSBuildItem project, string projectDirectory, IReadOnlyCollection<ProjectInstance> innerNodes, ISettings settings)
        {
            var restoreFallbackFolders = project.SplitPropertyValueOrNull("RestoreFallbackFolders");

            var currentFallbackFolders = RestoreSettingsUtils.GetValue(
                () => project.SplitPropertyValueOrNull("RestoreFallbackFoldersOverride")?.Select(i => UriUtility.GetAbsolutePath(projectDirectory, i)),
                () => MSBuildRestoreUtility.ContainsClearKeyword(restoreFallbackFolders) ? Array.Empty<string>() : null,
                () => restoreFallbackFolders?.Select(e => UriUtility.GetAbsolutePath(projectDirectory, e)),
                () => SettingsUtility.GetFallbackPackageFolders(settings).ToArray());

            // Append additional fallback folders after removing excluded folders
            var additionalProjectFallbackFolders = MSBuildRestoreUtility.AggregateSources(
                    values: innerNodes.SelectMany(i => MSBuildStringUtility.Split(i.GetPropertyValue("RestoreAdditionalProjectFallbackFolders"))),
                    excludeValues: innerNodes.SelectMany(i => MSBuildStringUtility.Split(i.GetPropertyValue("RestoreAdditionalProjectFallbackFoldersExcludes"))))
                .Select(i => UriUtility.GetAbsolutePath(projectDirectory, i));

            return currentFallbackFolders
                .Concat(additionalProjectFallbackFolders)
                .Where(i => !string.IsNullOrWhiteSpace(i));
        }

        /// <summary>
        /// Gets the framework references for the specified project.
        /// </summary>
        /// <param name="projectInstance">The <see cref="ProjectInstance" /> to get framework references for.</param>
        /// <returns>A <see cref="List{FrameworkDependency}" /> containing the framework references for the specified project.</returns>
        private static List<FrameworkDependency> GetFrameworkReferences(ProjectInstance projectInstance)
        {
            // Get the unique FrameworkReference items, ignoring duplicates
            var frameworkReferenceItems = projectInstance.GetItems("FrameworkReference").Distinct(ProjectItemInstanceEvaluatedIncludeComparer.Instance).ToList();

            // For best performance, its better to create a list with the exact number of items needed rather than using a LINQ statement or AddRange.  This is because if the list
            // is not allocated with enough items, the list has to be grown which can slow things down
            var frameworkDependencies = new List<FrameworkDependency>(frameworkReferenceItems.Count);

            foreach (var frameworkReferenceItem in frameworkReferenceItems)
            {
                var privateAssets = MSBuildStringUtility.Split(frameworkReferenceItem.GetMetadataValue("PrivateAssets"));

                frameworkDependencies.Add(new FrameworkDependency(frameworkReferenceItem.EvaluatedInclude, FrameworkDependencyFlagsUtils.GetFlags(privateAssets)));
            }

            return frameworkDependencies;
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
        /// Gets the original target frameworks for the specified project.
        /// </summary>
        /// <param name="project">An <see cref="IMSBuildItem" /> representing the project.</param>
        /// <param name="frameworks">An <see cref="IReadOnlyCollection{NuGetFrameowrk}" /> object containing the frameworks that were parsed from the outer project.</param>
        /// <returns>A <see cref="List{String}" /> containing the original target frameworks of the project.</returns>
        private static List<string> GetOriginalTargetFrameworks(IMSBuildItem project, IReadOnlyCollection<NuGetFramework> frameworks)
        {
            // If the project specified the TargetFrameworks property, just return that list
            var projectTargetFrameworks = project.SplitPropertyValueOrNull("TargetFrameworks");

            if (projectTargetFrameworks != null)
            {
                return projectTargetFrameworks.ToList();
            }

            // If the project did not specify a value for TargetFrameworks, return the short folder name of the frameworks which where parsed via other properties
            var targetFrameworks = new List<string>(frameworks.Count);

            foreach (var framework in frameworks)
            {
                targetFrameworks.Add(framework.GetShortFolderName());
            }

            return targetFrameworks;
        }

        /// <summary>
        /// Gets the package downloads for the specified project.
        /// </summary>
        /// <param name="projectInstance">The <see cref="ProjectInstance" /> to get package downloads for.</param>
        /// <returns>An <see cref="IEnumerable{DownloadDependency}" /> containing the package downloads for the specified project.</returns>
        private static IEnumerable<DownloadDependency> GetPackageDownloads(ProjectInstance projectInstance)
        {
            // Get the distinct PackageDownload items, ignoring duplicates
            foreach (var projectItemInstance in projectInstance.GetItems("PackageDownload").Distinct(ProjectItemInstanceEvaluatedIncludeComparer.Instance))
            {
                string id = projectItemInstance.EvaluatedInclude;

                // PackageDownload items can contain multiple versions
                foreach (var version in MSBuildStringUtility.Split(projectItemInstance.GetMetadataValue("Version")))
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
        /// Gets the package references for the specified project.
        /// </summary>
        /// <param name="projectInstance">The <see cref="ProjectInstance" /> to get package references for.</param>
        /// <returns>A <see cref="List{LibraryDependency}" /> containing the package references for the specified project.</returns>
        private static List<LibraryDependency> GetPackageReferences(ProjectInstance projectInstance)
        {
            // Get the distinct PackageReference items, ignoring duplicates
            var packageReferenceItems = projectInstance.GetItems("PackageReference").Distinct(ProjectItemInstanceEvaluatedIncludeComparer.Instance).ToList();

            var libraryDependencies = new List<LibraryDependency>(packageReferenceItems.Count);

            foreach (var packageReferenceItem in packageReferenceItems)
            {
                string version = packageReferenceItem.GetMetadataValue("Version");

                libraryDependencies.Add(new LibraryDependency
                {
                    AutoReferenced = packageReferenceItem.IsMetadataTrue("IsImplicitlyDefined"),
                    GeneratePathProperty = packageReferenceItem.IsMetadataTrue("GeneratePathProperty"),
                    IncludeType = GetLibraryIncludeFlags(packageReferenceItem.GetMetadataValue("IncludeAssets"), LibraryIncludeFlags.All) & ~GetLibraryIncludeFlags(packageReferenceItem.GetMetadataValue("ExcludeAssets"), LibraryIncludeFlags.None),
                    LibraryRange = new LibraryRange(
                        packageReferenceItem.EvaluatedInclude,
                        !string.IsNullOrWhiteSpace(version) ? VersionRange.Parse(version) : VersionRange.All,
                        LibraryDependencyTarget.Package),
                    NoWarn = MSBuildStringUtility.GetNuGetLogCodes(packageReferenceItem.GetMetadataValue("NoWarn")).ToList(),
                    SuppressParent = GetLibraryIncludeFlags(packageReferenceItem.GetMetadataValue("PrivateAssets"), LibraryIncludeFlagUtils.DefaultSuppressParent)
                });
            }

            return libraryDependencies;
        }

        /// <summary>
        /// Gets the packages path for the specified project.
        /// </summary>
        /// <param name="project">The <see cref="IMSBuildItem" /> representing the project.</param>
        /// <param name="projectDirectory">The full path to the directory containing the project.</param>
        /// <param name="settings">The <see cref="ISettings" /> of the project.</param>
        /// <returns>The full path to the packages directory for the specified project.</returns>
        private static string GetPackagesPath(IMSBuildItem project, string projectDirectory, ISettings settings)
        {
            return RestoreSettingsUtils.GetValue(
                () => UriUtility.GetAbsolutePath(projectDirectory, project.GetProperty("RestorePackagesPathOverride")),
                () => UriUtility.GetAbsolutePath(projectDirectory, project.GetProperty("RestorePackagesPath")),
                () => SettingsUtility.GetGlobalPackagesFolder(settings));
        }

        /// <summary>
        /// Gets the list of project graph entry points.  If the entry project is a solution, this method returns all of the projects it contains.
        /// </summary>
        /// <param name="entryProjectPath">The full path to the main project or solution file.</param>
        /// <param name="globalProperties">A <see cref="Dictionary{String,String}" /> representing the global properties for the project.</param>
        /// <returns></returns>
        private static List<ProjectGraphEntryPoint> GetProjectGraphEntryPoints(string entryProjectPath, Dictionary<string, string> globalProperties)
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
        /// Gets the name of the specified project.
        /// </summary>
        /// <param name="project">The <see cref="IMSBuildItem" /> representing the project.</param>
        /// <returns>The name of the specified project.</returns>
        private static string GetProjectName(IMSBuildItem project)
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
        /// <param name="projectInstance">The <see cref="ProjectInstance" /> to get project references for.</param>
        /// <returns>A <see cref="List{ProjectRestoreReference}" /> containing the project references for the specified project.</returns>
        private static List<ProjectRestoreReference> GetProjectReferences(ProjectInstance projectInstance)
        {
            // Get the unique list of ProjectReference items that have the ReferenceOutputAssembly metadata set to "true", ignoring duplicates
            var projectReferenceItems = projectInstance.GetItems("ProjectReference")
                .Where(i => i.IsMetadataTrue("ReferenceOutputAssembly", defaultValue: true))
                .Distinct(ProjectItemInstanceEvaluatedIncludeComparer.Instance)
                .ToList();

            var projectReferences = new List<ProjectRestoreReference>(projectReferenceItems.Count);

            foreach (var projectReferenceItem in projectReferenceItems)
            {
                string fullPath = projectReferenceItem.GetMetadataValue("FullPath");

                projectReferences.Add(new ProjectRestoreReference
                {
                    ExcludeAssets = GetLibraryIncludeFlags(projectReferenceItem.GetMetadataValue("ExcludeAssets"), LibraryIncludeFlags.None),
                    IncludeAssets = GetLibraryIncludeFlags(projectReferenceItem.GetMetadataValue("IncludeAssets"), LibraryIncludeFlags.All),
                    PrivateAssets = GetLibraryIncludeFlags(projectReferenceItem.GetMetadataValue("PrivateAssets"), LibraryIncludeFlagUtils.DefaultSuppressParent),
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
        private static List<ProjectRestoreMetadataFrameworkInfo> GetProjectRestoreMetadataFrameworkInfos(IReadOnlyDictionary<NuGetFramework, ProjectInstance> projects)
        {
            var projectRestoreMetadataFrameworkInfos = new List<ProjectRestoreMetadataFrameworkInfo>(projects.Count);

            foreach (var project in projects)
            {
                projectRestoreMetadataFrameworkInfos.Add(new ProjectRestoreMetadataFrameworkInfo(project.Key)
                {
                    ProjectReferences = GetProjectReferences(project.Value)
                });
            }

            return projectRestoreMetadataFrameworkInfos;
        }

        /// <summary>
        /// Gets the target frameworks for the specified project.
        /// </summary>
        /// <param name="projectWithInnerNodes">The <see cref="ProjectWithInnerNodes" /> representing the main project and its inner nodes if it targets multiple frameworks.</param>
        /// <returns></returns>
        private static IReadOnlyDictionary<NuGetFramework, ProjectInstance> GetProjectTargetFrameworks(ProjectWithInnerNodes projectWithInnerNodes)
        {
            // Get the raw list of target frameworks that the project specifies
            var projectFrameworkStrings = MSBuildProjectFrameworkUtility.GetProjectFrameworkStrings(
                projectWithInnerNodes.OuterProject.FullPath,
                projectWithInnerNodes.OuterProject.GetPropertyValue("TargetFrameworks"),
                projectWithInnerNodes.OuterProject.GetPropertyValue("TargetFramework"),
                projectWithInnerNodes.OuterProject.GetPropertyValue("TargetFrameworkMoniker"),
                projectWithInnerNodes.OuterProject.GetPropertyValue("TargetPlatformIdentifier"),
                projectWithInnerNodes.OuterProject.GetPropertyValue("TargetPlatformVersion"),
                projectWithInnerNodes.OuterProject.GetPropertyValue("TargetPlatformMinVersion")).ToList();

            var projectTargetFrameworks = new Dictionary<NuGetFramework, ProjectInstance>();

            foreach (var projectTargetFramework in projectFrameworkStrings)
            {
                // Attempt to get the corresponding project instance for the target framework.  If one is not found, then the project must not target multiple frameworks
                // and the main project should be used
                if (!projectWithInnerNodes.TryGetValue(projectTargetFramework, out ProjectInstance innerNode))
                {
                    innerNode = projectWithInnerNodes.OuterProject;
                }

                // Add the target framework and associate it with the project instance to be used for gathering details
                projectTargetFrameworks[NuGetFramework.Parse(projectTargetFramework)] = innerNode;
            }

            return projectTargetFrameworks;
        }

        /// <summary>
        /// Gets the version of the project.
        /// </summary>
        /// <param name="project">The <see cref="IMSBuildItem" /> representing the project.</param>
        /// <returns>The <see cref="NuGetVersion" /> of the specified project if one was found, otherwise <see cref="PackageSpec.DefaultVersion" />.</returns>
        private static NuGetVersion GetProjectVersion(IMSBuildItem project)
        {
            string version = project.GetProperty("PackageVersion") ?? project.GetProperty("Version");

            if (version == null)
            {
                return PackageSpec.DefaultVersion;
            }

            return NuGetVersion.Parse(version);
        }

        /// <summary>
        /// Gets the restore output path for the specified project.
        /// </summary>
        /// <param name="project">The <see cref="IMSBuildItem" /> representing the project.</param>
        /// <param name="projectDirectory">The full path to the directory of the project.</param>
        /// <returns>The full path to the restore output directory for the specified project.</returns>
        private static string GetRestoreOutputPath(IMSBuildItem project, string projectDirectory)
        {
            string outputPath = project.GetProperty("RestoreOutputPath") ?? project.GetProperty("MSBuildProjectExtensionsPath");

            return Path.GetFullPath(Path.Combine(projectDirectory, outputPath));
        }

        /// <summary>
        /// Gets the package sources of the specified project.
        /// </summary>
        /// <param name="project">An <see cref="IMSBuildItem" /> representing the project..</param>
        /// <param name="projectDirectory">The full path to the directory that contains the project.</param>
        /// <param name="innerNodes">An <see cref="IReadOnlyCollection{ProjectInstance}" /> containing the inner nodes of the project if its targets multiple frameworks.</param>
        /// <param name="settings">The <see cref="ISettings" /> of the specified project.</param>
        /// <returns>A <see cref="List{PackageSource}" /> object containing the packages sources for the specified project.</returns>
        private static List<PackageSource> GetSources(IMSBuildItem project, string projectDirectory, IReadOnlyCollection<ProjectInstance> innerNodes, ISettings settings)
        {
            var restoreSources = project.SplitPropertyValueOrNull("RestoreSources");

            var currentSources = RestoreSettingsUtils.GetValue(
                () => project.SplitPropertyValueOrNull("RestoreSourcesOverride")?.Select(MSBuildRestoreUtility.FixSourcePath).Select(e => UriUtility.GetAbsolutePath(projectDirectory, e)),
                () => MSBuildRestoreUtility.ContainsClearKeyword(restoreSources) ? Enumerable.Empty<string>() : null,
                () => restoreSources?.Select(MSBuildRestoreUtility.FixSourcePath).Select(e => UriUtility.GetAbsolutePath(projectDirectory, e)),
                () => (PackageSourceProvider.LoadPackageSources(settings)).Where(e => e.IsEnabled).Select(e => e.Source));

            var additionalProjectSources = MSBuildRestoreUtility.AggregateSources(
                    values: innerNodes.SelectMany(i => MSBuildStringUtility.Split(i.GetPropertyValue("RestoreAdditionalProjectSources"))),
                    excludeValues: Enumerable.Empty<string>())
                .Select(MSBuildRestoreUtility.FixSourcePath)
                .Select(i => UriUtility.GetAbsolutePath(projectDirectory, i))
                .ToArray();

            return currentSources
                .Concat(additionalProjectSources)
                .Where(i => !string.IsNullOrWhiteSpace(i))
                .Select(i => new PackageSource(i)).ToList();
        }

        /// <summary>
        /// Gets the target framework information for the specified project.  This includes the package references, package downloads, and framework references.
        /// </summary>
        /// <param name="projectInnerNodes">An <see cref="IReadOnlyDictionary{NuGetFramework,ProjectInstance} "/> containing the projects by their target framework.</param>
        /// <returns>A <see cref="List{TargetFrameworkInformation}" /> containing the target framework information for the specified project.</returns>
        private static List<TargetFrameworkInformation> GetTargetFrameworkInfos(IReadOnlyDictionary<NuGetFramework, ProjectInstance> projectInnerNodes)
        {
            var targetFrameworkInfos = new List<TargetFrameworkInformation>(projectInnerNodes.Count);

            foreach (var projectInnerNode in projectInnerNodes)
            {
                var projectInstance = projectInnerNode.Value;

                var msBuildProjectInstance = new MSBuildProjectInstance(projectInstance);

                var targetFrameworkInformation = new TargetFrameworkInformation
                {
                    FrameworkName = projectInnerNode.Key,
                    RuntimeIdentifierGraphPath = msBuildProjectInstance.GetProperty(nameof(TargetFrameworkInformation.RuntimeIdentifierGraphPath))
                };

                var packageTargetFallback = MSBuildStringUtility.Split(msBuildProjectInstance.GetProperty("PackageTargetFallback")).Select(NuGetFramework.Parse).ToList();

                var assetTargetFallback = MSBuildStringUtility.Split(msBuildProjectInstance.GetProperty(nameof(TargetFrameworkInformation.AssetTargetFallback))).Select(NuGetFramework.Parse).ToList();

                AssetTargetFallbackUtility.EnsureValidFallback(packageTargetFallback, assetTargetFallback, projectInstance.FullPath);

                AssetTargetFallbackUtility.ApplyFramework(targetFrameworkInformation, packageTargetFallback, assetTargetFallback);

                targetFrameworkInformation.Dependencies.AddRange(GetPackageReferences(projectInstance));

                targetFrameworkInformation.DownloadDependencies.AddRange(GetPackageDownloads(projectInstance));

                targetFrameworkInformation.FrameworkReferences.AddRange(GetFrameworkReferences(projectInstance));

                targetFrameworkInfos.Add(targetFrameworkInformation);
            }

            return targetFrameworkInfos;
        }

        /// <summary>
        /// Gets a value indicating if the specified project is a legacy project.
        /// </summary>
        /// <param name="project">The <see cref="IMSBuildItem" /> representing the project.</param>
        /// <returns><code>true</code> if the specified project is considered legacy, otherwise <code>false</code>.</returns>
        private static bool IsLegacyProject(IMSBuildItem project)
        {
            // We consider the project to be legacy if it does not specify TargetFramework or TargetFrameworks
            return project.GetProperty("TargetFramework") == null && project.GetProperty("TargetFrameworks") == null;
        }

        /// <summary>
        /// Gets a <see cref="DependencyGraphSpec" /> for the specified project.
        /// </summary>
        /// <param name="entryProjectPath">The full path to a project or Visual Studio Solution File.</param>
        /// <param name="globalProperties">A <see cref="Dictionary{String,String}" /> containing the global properties to use when evaluation MSBuild projects.</param>
        /// <returns>A <see cref="DependencyGraphSpec" /> for the specified project if they could be loaded, otherwise <code>null</code>.</returns>
        private DependencyGraphSpec GetDependencyGraphSpec(string entryProjectPath, Dictionary<string, string> globalProperties)
        {
            try
            {
                // TODO: Use a localized resource from https://github.com/NuGet/NuGet.Client/pull/3111
                MSBuildLogger.LogMinimal("Determining projects to restore...");

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

                try
                {
                    // Get the PackageSpecs in parallel because creating each one is relatively expensive so parallelism speeds things up
                    Parallel.ForEach(projects, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, project =>
                    {
                        var packageSpec = GetPackageSpec(project);

                        if (packageSpec != null)
                        {
                            // TODO: Make the backing collection Concurrent in future PR
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

                // TODO: Localized resource
                MSBuildLogger.LogDebug(string.Format(CultureInfo.CurrentCulture, "Created DependencyGraphSpec in {0:D2}ms.", sw.ElapsedMilliseconds));

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
        /// <param name="projectWithInnerNodes">A <see cref="ProjectWithInnerNodes" /> object that represents the project and its inner nodes if it targets multiple frameworks.</param>
        /// <returns></returns>
        private PackageSpec GetPackageSpec(ProjectWithInnerNodes projectWithInnerNodes)
        {
            var msbuildProjectInstance = new MSBuildProjectInstance(projectWithInnerNodes);

            var projectDirectory = projectWithInnerNodes.OuterProject.Directory;

            var projectFileFullPath = projectWithInnerNodes.OuterProject.FullPath;

            var settings = RestoreSettingsUtils.ReadSettings(
                msbuildProjectInstance.GetProperty("RestoreSolutionDirectory"),
                msbuildProjectInstance.GetProperty("RestoreRootConfigDirectory") ?? projectDirectory,
                UriUtility.GetAbsolutePath(projectDirectory, msbuildProjectInstance.GetProperty("RestoreConfigFile")),
                MachineWideSettingsLazy,
                _settingsLoadContext);

            // Get the target frameworks for the project and the project instance for each framework
            var projectsByTargetFramework = GetProjectTargetFrameworks(projectWithInnerNodes);

            var targetFrameworkInfos = GetTargetFrameworkInfos(projectsByTargetFramework);

            var projectStyle = BuildTasksUtility.GetProjectRestoreStyle(
                    restoreProjectStyle: msbuildProjectInstance.GetProperty("RestoreProjectStyle"),
                    hasPackageReferenceItems: targetFrameworkInfos.Any(i => i.Dependencies.Any()),
                    projectJsonPath: msbuildProjectInstance.GetProperty("_CurrentProjectJsonPath"),
                    projectDirectory: projectDirectory,
                    projectName: msbuildProjectInstance.GetProperty("MSBuildProjectName"),
                    log: MSBuildLogger)
                .ProjectStyle;

            // The inner nodes represents each project instance by target framework
            var innerNodes = projectsByTargetFramework.Values.ToList();

            var projectName = GetProjectName(msbuildProjectInstance);

            var outputPath = GetRestoreOutputPath(msbuildProjectInstance, projectDirectory);

            var packageSpec = new PackageSpec(targetFrameworkInfos)
            {
                FilePath = projectFileFullPath,
                Name = projectName,
                RestoreMetadata = new ProjectRestoreMetadata
                {
                    CacheFilePath = NoOpRestoreUtilities.GetProjectCacheFilePath(outputPath, projectFileFullPath),
                    ConfigFilePaths = settings.GetConfigFilePaths(),
                    CrossTargeting = (projectStyle == ProjectStyle.PackageReference || projectStyle == ProjectStyle.DotnetToolReference) && projectsByTargetFramework.Count > 1,
                    FallbackFolders = GetFallbackFolders(msbuildProjectInstance, projectDirectory, innerNodes, settings).ToList(),
                    OriginalTargetFrameworks = GetOriginalTargetFrameworks(msbuildProjectInstance, projectsByTargetFramework.Keys.ToList()),
                    OutputPath = outputPath,
                    PackagesPath = GetPackagesPath(msbuildProjectInstance, projectDirectory, settings),
                    ProjectName = projectName,
                    ProjectPath = projectFileFullPath,
                    ProjectStyle = projectStyle,
                    ProjectUniqueName = projectFileFullPath,
                    ProjectWideWarningProperties = WarningProperties.GetWarningProperties(msbuildProjectInstance.GetProperty("TreatWarningsAsErrors"), msbuildProjectInstance.GetProperty("WarningsAsErrors"), msbuildProjectInstance.GetProperty("NoWarn")),
                    RestoreLockProperties = new RestoreLockProperties(msbuildProjectInstance.GetProperty("RestorePackagesWithLockFile"), msbuildProjectInstance.GetProperty("NuGetLockFilePath"), msbuildProjectInstance.IsPropertyTrue("RestoreLockedMode")),
                    SkipContentFileWrite = IsLegacyProject(msbuildProjectInstance),
                    Sources = GetSources(msbuildProjectInstance, projectDirectory, innerNodes, settings),
                    TargetFrameworks = GetProjectRestoreMetadataFrameworkInfos(projectsByTargetFramework),
                    ValidateRuntimeAssets = msbuildProjectInstance.IsPropertyTrue("ValidateRuntimeIdentifierCompatibility"),
                },
                RuntimeGraph = new RuntimeGraph(
                    MSBuildStringUtility.Split($"{msbuildProjectInstance.GetProperty("RuntimeIdentifiers")};{msbuildProjectInstance.GetProperty("RuntimeIdentifier")}")
                        .Concat(innerNodes.SelectMany(i => MSBuildStringUtility.Split($"{i.GetPropertyValue("RuntimeIdentifiers")};{i.GetPropertyValue("RuntimeIdentifier")}")))
                        .Distinct(StringComparer.Ordinal)
                        .Select(rid => new RuntimeDescription(rid))
                        .ToList(),
                    MSBuildStringUtility.Split(msbuildProjectInstance.GetProperty("RuntimeSupports"))
                        .Distinct(StringComparer.Ordinal)
                        .Select(s => new CompatibilityProfile(s))
                        .ToList()
                    ),
                Version = GetProjectVersion(msbuildProjectInstance)
            };

            return packageSpec;
        }

        /// <summary>
        /// Determines of the specified option is <code>true</code>.
        /// </summary>
        /// <param name="name">The name of the option.</param>
        /// <param name="options">A <see cref="Dictionary{String,String}" />containing options.</param>
        /// <returns><code>true</code> if the specified option is true, otherwise <code>false</code>.</returns>
        private bool IsOptionTrue(string name, Dictionary<string, string> options)
        {
            return options.TryGetValue(name, out string value) && StringComparer.OrdinalIgnoreCase.Equals(value, "true");
        }

        /// <summary>
        /// Recursively loads and evaluates MSBuild projects.
        /// </summary>
        /// <param name="entryProjects">An <see cref="IEnumerable{ProjectGraphEntryPoint}" /> containing the entry projects to load.</param>
        /// <returns>An <see cref="ICollection{ProjectWithInnerNodes}" /> object containing projects and their inner nodes if they are targeting multiple frameworks.</returns>
        private ICollection<ProjectWithInnerNodes> LoadProjects(IEnumerable<ProjectGraphEntryPoint> entryProjects)
        {
            var loggers = new List<ILogger>
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
                            key => new ProjectWithInnerNodes(targetFramework, projectInstance),
                            (_, item) => item.Add(targetFramework, projectInstance));

                        return projectInstance;
                    });
                }
                finally
                {
                    // EndBuild blocks until all builds are complete
                    BuildManager.DefaultBuildManager.EndBuild();
                }

                sw.Stop();

                // TODO: Localized resource
                MSBuildLogger.LogInformation(string.Format(CultureInfo.CurrentCulture, "Evaluated {0} project(s) in {1:D2}ms ({2} builds, {3} failures).", projectGraph.ProjectNodes.Count, sw.ElapsedMilliseconds, buildCount, failedBuildSubmissions.Count));

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
    }
}
