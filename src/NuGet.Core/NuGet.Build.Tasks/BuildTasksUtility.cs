// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
#if IS_CORECLR
using System.Runtime.InteropServices;
#endif
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Credentials;
using NuGet.ProjectModel;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

#if IS_DESKTOP
using System.Collections.Concurrent;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using NuGet.Packaging;
using NuGet.Packaging.PackageExtraction;
using NuGet.Packaging.Signing;
using NuGet.PackageManagement;
using NuGet.ProjectManagement;
using NuGet.Shared;
#endif

namespace NuGet.Build.Tasks
{
    public static class BuildTasksUtility
    {
        public static void LogInputParam(Common.ILogger log, string name, params string[] values)
        {
            LogTaskParam(log, "in", name, values);
        }

        public static void LogOutputParam(Common.ILogger log, string name, params string[] values)
        {
            LogTaskParam(log, "out", name, values);
        }

        private static void LogTaskParam(Common.ILogger log, string direction, string name, params string[] values)
        {
            var stringValues = values?.Select(s => s) ?? Enumerable.Empty<string>();

            log.Log(Common.LogLevel.Debug, $"({direction}) {name} '{string.Join(";", stringValues)}'");
        }

        /// <summary>
        /// Add all restorable projects to the restore list.
        /// This is the behavior for --recursive
        /// </summary>
        public static void AddAllProjectsForRestore(DependencyGraphSpec spec)
        {
            // Add everything from projects except for packages.config and unknown project types
            foreach (var project in spec.Projects.Where(DoesProjectSupportRestore))
            {
                spec.AddRestore(project.RestoreMetadata.ProjectUniqueName);
            }
        }

        public static void CopyPropertyIfExists(ITaskItem item, IDictionary<string, string> properties, string key)
        {
            CopyPropertyIfExists(item, properties, key, key);
        }

        public static void CopyPropertyIfExists(ITaskItem item, IDictionary<string, string> properties, string key, string toKey)
        {
            var wrapper = new MSBuildTaskItem(item);

            var propertyValue = wrapper.GetProperty(key);

            if (!string.IsNullOrEmpty(propertyValue)
                && !properties.ContainsKey(key))
            {
                properties.Add(toKey, propertyValue);
            }
        }

        /// <summary>
        /// Determines if the specified <see cref="PackageSpec" /> supports restore.
        /// </summary>
        /// <param name="packageSpec">A <see cref="PackageSpec" /> for a project.</param>
        /// <returns><code>true</code> if the project supports restore, otherwise <code>false</code>.</returns>
        internal static bool DoesProjectSupportRestore(PackageSpec packageSpec)
        {
            return RestorableTypes.Contains(packageSpec.RestoreMetadata.ProjectStyle);
        }

        public static string GetPropertyIfExists(ITaskItem item, string key)
        {
            var wrapper = new MSBuildTaskItem(item);

            var propertyValue = wrapper.GetProperty(key);

            if (!string.IsNullOrEmpty(propertyValue))
            {
                return propertyValue;
            }

            return null;
        }

        public static void AddPropertyIfExists(IDictionary<string, string> properties, string key, string value)
        {
            if (!string.IsNullOrEmpty(value)
                && !properties.ContainsKey(key))
            {
                properties.Add(key, value);
            }
        }

        public static void AddPropertyIfExists(IDictionary<string, string> properties, string key, string[] value)
        {
            if (value != null && !properties.ContainsKey(key))
            {
                properties.Add(key, string.Concat(value.Select(e => e + ";")));
            }
        }

        private static HashSet<ProjectStyle> RestorableTypes = new HashSet<ProjectStyle>()
        {
            ProjectStyle.DotnetCliTool,
            ProjectStyle.PackageReference,
            ProjectStyle.Standalone,
            ProjectStyle.ProjectJson
        };

        internal static async Task<bool> RestoreAsync(
            DependencyGraphSpec dependencyGraphSpec,
            bool interactive,
            bool recursive,
            bool noCache,
            bool ignoreFailedSources,
            bool disableParallel,
            bool force,
            bool forceEvaluate,
            bool hideWarningsAndErrors,
            Common.ILogger log,
            CancellationToken cancellationToken)
        {
            if (dependencyGraphSpec == null)
            {
                throw new ArgumentNullException(nameof(dependencyGraphSpec));
            }

            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            try
            {
                DefaultCredentialServiceUtility.SetupDefaultCredentialService(log, !interactive);

                // Set connection limit
                NetworkProtocolUtility.SetConnectionLimit();

                // Set user agent string used for network calls
#if IS_CORECLR
                UserAgent.SetUserAgentString(new UserAgentStringBuilder("NuGet .NET Core MSBuild Task")
                    .WithOSDescription(RuntimeInformation.OSDescription));
#else
                // OS description is set by default on Desktop
                UserAgent.SetUserAgentString(new UserAgentStringBuilder("NuGet Desktop MSBuild Task"));
#endif

                // This method has no effect on .NET Core.
                NetworkProtocolUtility.ConfigureSupportedSslProtocols();

                var providerCache = new RestoreCommandProvidersCache();

                using (var cacheContext = new SourceCacheContext())
                {
                    cacheContext.NoCache = noCache;
                    cacheContext.IgnoreFailedSources = ignoreFailedSources;

                    // Pre-loaded request provider containing the graph file
                    var providers = new List<IPreLoadedRestoreRequestProvider>();

                    if (dependencyGraphSpec.Restore.Count < 1)
                    {
                        // Restore will fail if given no inputs, but here we should skip it and provide a friendly message.
                        log.LogMinimal(Strings.NoProjectsToRestore);
                        return true;
                    }

                    // Add all child projects
                    if (recursive)
                    {
                        AddAllProjectsForRestore(dependencyGraphSpec);
                    }

                    providers.Add(new DependencyGraphSpecRequestProvider(providerCache, dependencyGraphSpec));

                    var restoreContext = new RestoreArgs()
                    {
                        CacheContext = cacheContext,
                        LockFileVersion = LockFileFormat.Version,
                        // 'dotnet restore' fails on slow machines (https://github.com/NuGet/Home/issues/6742)
                        // The workaround is to pass the '--disable-parallel' option.
                        // We apply the workaround by default when the system has 1 cpu.
                        // This will fix restore failures on VMs with 1 CPU and containers with less or equal to 1 CPU assigned.
                        DisableParallel = Environment.ProcessorCount == 1 ? true : disableParallel,
                        Log = log,
                        MachineWideSettings = new XPlatMachineWideSetting(),
                        PreLoadedRequestProviders = providers,
                        AllowNoOp = !force,
                        HideWarningsAndErrors = hideWarningsAndErrors,
                        RestoreForceEvaluate = forceEvaluate
                    };

                    if (restoreContext.DisableParallel)
                    {
                        HttpSourceResourceProvider.Throttle = SemaphoreSlimThrottle.CreateBinarySemaphore();
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    var restoreSummaries = await RestoreRunner.RunAsync(restoreContext, cancellationToken);

#if IS_DESKTOP
                    if (dependencyGraphSpec.Projects.Any(i => i.RestoreMetadata.ProjectStyle == ProjectStyle.PackagesConfig))
                    {
                        var v2RestoreResult = await PerformNuGetV2RestoreAsync(log, dependencyGraphSpec, noCache, disableParallel, interactive);
                        restoreSummaries.Add(v2RestoreResult);

                        // TODO: Message if no packages needed to be restored?
                        //var message = string.Format(
                        //    CultureInfo.CurrentCulture,
                        //    LocalizedResourceManager.GetString("InstallCommandNothingToInstall"),
                        //    "packages.config");

                        //Console.LogMinimal(message);

                        if (!v2RestoreResult.Success)
                        {
                            v2RestoreResult
                                .Errors
                                .Where(l => l.Level == LogLevel.Warning)
                                .ForEach(message =>
                                {
                                    if (message.Code > NuGetLogCode.Undefined && message.Code.TryGetName(out var codeString))
                                    {
                                        log.LogWarning(
                                            null,
                                            codeString,
                                            null,
                                            message.FilePath,
                                            message.StartLineNumber,
                                            message.StartColumnNumber,
                                            message.EndLineNumber,
                                            message.EndColumnNumber,
                                            message.Message);
                                    }
                                    else
                                    {
                                        log.LogWarning(message.Message);
                                    }
                                });
                        }
                    }
#endif
                    // Summary
                    RestoreSummary.Log(log, restoreSummaries);

                    return restoreSummaries.All(x => x.Success);
                }
            }
            finally
            {
                // The CredentialService lifetime is for the duration of the process. We should not leave a potentially unavailable logger. 
                // We need to update the delegating logger with a null instance
                // because the tear downs of the plugins and similar rely on idleness and process exit.
                DefaultCredentialServiceUtility.UpdateCredentialServiceDelegatingLogger(NullLogger.Instance);
            }
        }

        /// <summary>
        /// Determines the restore style of a project.
        /// </summary>
        /// <param name="restoreProjectStyle">An optional user supplied restore style.</param>
        /// <param name="hasPackageReferenceItems">A <see cref="bool"/> indicating whether or not the project has any PackageReference items.</param>
        /// <param name="projectJsonPath">An optional path to the project's project.json file.</param>
        /// <param name="projectDirectory">The full path to the project directory.</param>
        /// <param name="projectName">The name of the project file.</param>
        /// <param name="log">An <see cref="NuGet.Common.ILogger"/> object used to log messages.</param>
        /// <returns>A <see cref="Tuple{ProjectStyle, Boolean}"/> containing the project style and a value indicating if the project is using a style that is compatible with PackageReference.
        /// If the value of <paramref name="restoreProjectStyle"/> is not empty and could not be parsed, <code>null</code> is returned.</returns>
        internal static (ProjectStyle ProjectStyle, bool IsPackageReferenceCompatibleProjectStyle) GetProjectRestoreStyle(string restoreProjectStyle, bool hasPackageReferenceItems, string projectJsonPath, string projectDirectory, string projectName, Common.ILogger log)
        {
            ProjectStyle projectStyle;

            // Allow a user to override by setting RestoreProjectStyle in the project.
            if (!string.IsNullOrWhiteSpace(restoreProjectStyle))
            {
                if (!Enum.TryParse(restoreProjectStyle, ignoreCase: true, out projectStyle))
                {
                    // Any value that is not recognized is treated as Unknown
                    projectStyle = ProjectStyle.Unknown;
                }
            }
            else if (hasPackageReferenceItems)
            {
                // If any PackageReferences exist treat it as PackageReference. This has priority over project.json.
                projectStyle = ProjectStyle.PackageReference;
            }
            else if (!string.IsNullOrWhiteSpace(projectJsonPath))
            {
                // If this is not a PackageReference project check if project.json or projectName.project.json exists.
                projectStyle = ProjectStyle.ProjectJson;
            }
            else if (ProjectHasPackagesConfigFile(projectDirectory, projectName))
            {
                // If this is not a PackageReference or ProjectJson project check if packages.config or packages.ProjectName.config exists
                projectStyle = ProjectStyle.PackagesConfig;
            }
            else
            {
                // This project is either a packages.config project or one that does not use NuGet at all.
                projectStyle = ProjectStyle.Unknown;
            }

            bool isPackageReferenceCompatibleProjectStyle = projectStyle == ProjectStyle.PackageReference || projectStyle == ProjectStyle.DotnetToolReference;

            return (projectStyle, isPackageReferenceCompatibleProjectStyle);
        }

        /// <summary>
        /// Determines if the project has a packages.config file.
        /// </summary>
        /// <param name="projectDirectory">The full path of the project directory.</param>
        /// <param name="projectName">The name of the project file.</param>
        /// <returns><code>true</code> if a packages.config exists next to the project, otherwise <code>false</code>.</returns>
        private static bool ProjectHasPackagesConfigFile(string projectDirectory, string projectName)
        {
            if (string.IsNullOrWhiteSpace(projectDirectory))
            {
                throw new ArgumentException(Strings.Argument_Cannot_Be_Null_Or_Empty, nameof(projectDirectory));
            }

            if (string.IsNullOrWhiteSpace(projectName))
            {
                throw new ArgumentException(Strings.Argument_Cannot_Be_Null_Or_Empty, nameof(projectName));
            }

            string packagesConfigPath = Path.Combine(projectDirectory, NuGetConstants.PackageReferenceFile);

            if (File.Exists(packagesConfigPath))
            {
                return true;
            }

            packagesConfigPath = Path.Combine(projectDirectory, $"packages.{projectName}.config");

            if (File.Exists(packagesConfigPath))
            {
                return true;
            }

            return false;
        }
#if IS_DESKTOP
        private static async Task<RestoreSummary> PerformNuGetV2RestoreAsync(Common.ILogger log, DependencyGraphSpec dgFile, bool noCache, bool disableParallel, bool interactive)
        {
            string globalPackageFolder = null;
            string repositoryPath = null;
            string firstPackagesConfigPath = null;
            IList<PackageSource> packageSources = null;

            var installedPackageReferences = new HashSet<Packaging.PackageReference>(new PackageReferenceComparer());

            ISettings settings = null;

            foreach (PackageSpec packageSpec in dgFile.Projects.Where(i => i.RestoreMetadata.ProjectStyle == ProjectStyle.PackagesConfig))
            {
                globalPackageFolder = globalPackageFolder ?? packageSpec.RestoreMetadata.PackagesPath;
                repositoryPath = repositoryPath ?? packageSpec.RestoreMetadata.RepositoryPath;

                if (packageSources == null)
                {
                    packageSources = new List<PackageSource>();
                    if (!noCache)
                    {
                        if (!string.IsNullOrEmpty(globalPackageFolder) && Directory.Exists(globalPackageFolder))
                        {
                            packageSources.Add(new FeedTypePackageSource(globalPackageFolder, FeedType.FileSystemV3));
                        }
                    }

                    packageSources.AddRange(packageSpec.RestoreMetadata.Sources);
                }

                settings = settings ?? Settings.LoadSettingsGivenConfigPaths(packageSpec.RestoreMetadata.ConfigFilePaths);

                var packagesConfigPath = Path.Combine(Path.GetDirectoryName(packageSpec.RestoreMetadata.ProjectPath), NuGetConstants.PackageReferenceFile);

                firstPackagesConfigPath = firstPackagesConfigPath ?? packagesConfigPath;

                installedPackageReferences.AddRange(GetInstalledPackageReferences(packagesConfigPath, allowDuplicatePackageIds: true));
            }

            PackageSourceProvider packageSourceProvider = new PackageSourceProvider(settings);
            var sourceRepositoryProvider = new CommandLineSourceRepositoryProvider(packageSourceProvider);
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, settings, repositoryPath);

            // TODO: different default?  Allow user to specify?
            var packageSaveMode = Packaging.PackageSaveMode.Defaultv2;

            var missingPackageReferences = installedPackageReferences.Where(reference =>
                !nuGetPackageManager.PackageExistsInPackagesFolder(reference.PackageIdentity, packageSaveMode)).ToArray();

            if (missingPackageReferences.Length == 0)
            {
                return new RestoreSummary(true);
            }
            var packageRestoreData = missingPackageReferences.Select(reference =>
                new PackageRestoreData(
                    reference,
                    new[] { firstPackagesConfigPath },
                    isMissing: true));

            var repositories = packageSources
                .Select(sourceRepositoryProvider.CreateRepository)
                .ToArray();

            var installCount = 0;
            var failedEvents = new ConcurrentQueue<PackageRestoreFailedEventArgs>();
            var collectorLogger = new RestoreCollectorLogger(new MSBuildLogger(log));

            var packageRestoreContext = new PackageRestoreContext(
                nuGetPackageManager,
                packageRestoreData,
                CancellationToken.None,
                packageRestoredEvent: (sender, args) => { Interlocked.Add(ref installCount, args.Restored ? 1 : 0); },
                packageRestoreFailedEvent: (sender, args) => { failedEvents.Enqueue(args); },
                sourceRepositories: repositories,
                maxNumberOfParallelTasks: disableParallel
                    ? 1
                    : PackageManagementConstants.DefaultMaxDegreeOfParallelism,
                logger: collectorLogger);

            // TODO: Check require consent?
            // CheckRequireConsent();

            var clientPolicyContext = ClientPolicyContext.GetClientPolicy(settings, collectorLogger);
            var projectContext = new ConsoleProjectContext(collectorLogger)
            {
                PackageExtractionContext = new PackageExtractionContext(
                    Packaging.PackageSaveMode.Defaultv2,
                    PackageExtractionBehavior.XmlDocFileSaveMode,
                    clientPolicyContext,
                    collectorLogger)
            };

            // if (EffectivePackageSaveMode != Packaging.PackageSaveMode.None)
            {
                projectContext.PackageExtractionContext.PackageSaveMode = packageSaveMode;
            }

            using (var cacheContext = new SourceCacheContext())
            {
                cacheContext.NoCache = noCache;

                // TODO: Direct download?
                // //cacheContext.DirectDownload = DirectDownload;

                var downloadContext = new PackageDownloadContext(cacheContext, repositoryPath, directDownload: false)
                {
                    ClientPolicyContext = clientPolicyContext
                };

                DefaultCredentialServiceUtility.SetupDefaultCredentialService(log, !interactive);

                var result = await PackageRestoreManager.RestoreMissingPackagesAsync(
                    packageRestoreContext,
                    projectContext,
                    downloadContext);

                if (downloadContext.DirectDownload)
                {
                    GetDownloadResultUtility.CleanUpDirectDownloads(downloadContext);
                }

                return new RestoreSummary(
                    result.Restored,
                    "packages.config projects",
                    settings.GetConfigFilePaths(),
                    packageSources.Select(x => x.Source),
                    installCount,
                    collectorLogger.Errors.Concat(ProcessFailedEventsIntoRestoreLogs(failedEvents)));
            }
        }

        private static IEnumerable<Packaging.PackageReference> GetInstalledPackageReferences(string projectConfigFilePath, bool allowDuplicatePackageIds)
        {
            if (File.Exists(projectConfigFilePath))
            {
                try
                {
                    var xDocument = XDocument.Load(projectConfigFilePath);
                    var reader = new PackagesConfigReader(xDocument);
                    return reader.GetPackages(allowDuplicatePackageIds);
                }
                catch (XmlException)
                {
                    // TODO: Log an error?
                    //var message = string.Format(
                    //    CultureInfo.CurrentCulture,
                    //    ResourceManager.GetString("Error_PackagesConfigParseError"),
                    //    projectConfigFilePath,
                    //    ex.Message);

                    //Log.LogError(message);
                }
            }

            return Enumerable.Empty<Packaging.PackageReference>();
        }

        private static IEnumerable<RestoreLogMessage> ProcessFailedEventsIntoRestoreLogs(ConcurrentQueue<PackageRestoreFailedEventArgs> failedEvents)
        {
            var result = new List<RestoreLogMessage>();

            foreach (var failedEvent in failedEvents)
            {
                if (failedEvent.Exception is SignatureException)
                {
                    var signatureException = failedEvent.Exception as SignatureException;

                    var errorsAndWarnings = signatureException
                        .Results.SelectMany(r => r.Issues)
                        .Where(i => i.Level == LogLevel.Error || i.Level == LogLevel.Warning)
                        .Select(i => i.AsRestoreLogMessage());

                    result.AddRange(errorsAndWarnings);
                }
                else
                {
                    result.Add(new RestoreLogMessage(LogLevel.Error, NuGetLogCode.Undefined, failedEvent.Exception.Message));
                }
            }

            return result;
        }
        public class CommandLineSourceRepositoryProvider : ISourceRepositoryProvider
        {
            private readonly Configuration.IPackageSourceProvider _packageSourceProvider;
            private readonly List<Lazy<INuGetResourceProvider>> _resourceProviders;
            private readonly List<SourceRepository> _repositories = new List<SourceRepository>();

            // There should only be one instance of the source repository for each package source.
            private static readonly ConcurrentDictionary<Configuration.PackageSource, SourceRepository> _cachedSources
                = new ConcurrentDictionary<Configuration.PackageSource, SourceRepository>();

            public CommandLineSourceRepositoryProvider(Configuration.IPackageSourceProvider packageSourceProvider)
            {
                _packageSourceProvider = packageSourceProvider;

                _resourceProviders = new List<Lazy<INuGetResourceProvider>>();
                _resourceProviders.AddRange(FactoryExtensionsV3.GetCoreV3(Repository.Provider));

                // Create repositories
                _repositories = _packageSourceProvider.LoadPackageSources()
                    .Where(s => s.IsEnabled)
                    .Select(CreateRepository)
                    .ToList();
            }

            /// <summary>
            /// Retrieve repositories that have been cached.
            /// </summary>
            public IEnumerable<SourceRepository> GetRepositories()
            {
                return _repositories;
            }

            /// <summary>
            /// Create a repository and add it to the cache.
            /// </summary>
            public SourceRepository CreateRepository(Configuration.PackageSource source)
            {
                return CreateRepository(source, FeedType.Undefined);
            }

            public SourceRepository CreateRepository(Configuration.PackageSource source, FeedType type)
            {
                return _cachedSources.GetOrAdd(source, new SourceRepository(source, _resourceProviders, type));
            }

            public Configuration.IPackageSourceProvider PackageSourceProvider
            {
                get { return _packageSourceProvider; }
            }
        }
#endif
    }
}
