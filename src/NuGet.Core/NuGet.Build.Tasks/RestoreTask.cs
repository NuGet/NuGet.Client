// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
#if IS_DESKTOP
using System.Collections.Concurrent;
#endif
using System.Collections.Generic;
using System.Diagnostics;
#if IS_DESKTOP
using System.IO;
#endif
using System.Linq;
#if IS_CORECLR
using System.Runtime.InteropServices;
#endif
using System.Threading;
using System.Threading.Tasks;
#if IS_DESKTOP
using System.Xml;
using System.Xml.Linq;
#endif
using Microsoft.Build.Framework;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Credentials;
#if IS_DESKTOP
using NuGet.Packaging;
using NuGet.Packaging.PackageExtraction;
using NuGet.Packaging.Signing;
using NuGet.PackageManagement;
using NuGet.ProjectManagement;
#endif
using NuGet.ProjectModel;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
#if IS_DESKTOP
using NuGet.Shared;
#endif

namespace NuGet.Build.Tasks
{
    /// <summary>
    /// .NET Core compatible restore task for PackageReference and UWP project.json projects.
    /// </summary>
    public class RestoreTask : Microsoft.Build.Utilities.Task, ICancelableTask, IDisposable
    {
#if IS_DESKTOP
        private const string HttpUserAgent = "NuGet Desktop MSBuild Task";
#else
        private const string HttpUserAgent = "NuGet .NET Core MSBuild Task";
#endif

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        /// <summary>
        /// DG file entries
        /// </summary>
        [Required]
        public ITaskItem[] RestoreGraphItems { get; set; }

        /// <summary>
        /// Disable parallel project restores and downloads
        /// </summary>
        public bool RestoreDisableParallel { get; set; }

        /// <summary>
        /// Disable the web cache
        /// </summary>
        public bool RestoreNoCache { get; set; }

        /// <summary>
        /// Ignore errors from package sources
        /// </summary>
        public bool RestoreIgnoreFailedSources { get; set; }

        /// <summary>
        /// Restore all projects.
        /// </summary>
        public bool RestoreRecursive { get; set; }

        /// <summary>
        /// Force restore, skip no op
        /// </summary>
        public bool RestoreForce { get; set; }

        /// <summary>
        /// Do not display Errors and Warnings to the user. 
        /// The Warnings and Errors are written into the assets file and will be read by an sdk target.
        /// </summary>
        public bool HideWarningsAndErrors { get; set; }

        /// <summary>
        /// Set this property if you want to get an interactive restore
        /// </summary>
        public bool Interactive { get; set; }

        /// <summary>
        /// Reevaluate resotre graph even with a lock file, skip no op as well.
        /// </summary>
        public bool RestoreForceEvaluate { get; set; }

        public override bool Execute()
        {
#if DEBUG
            var debugPackTask = Environment.GetEnvironmentVariable("DEBUG_RESTORE_TASK");
            if (!string.IsNullOrEmpty(debugPackTask) && debugPackTask.Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase))
            {
#if IS_CORECLR
                Console.WriteLine("Waiting for debugger to attach.");
                Console.WriteLine($"Process ID: {Process.GetCurrentProcess().Id}");

                while (!Debugger.IsAttached)
                {
                    System.Threading.Thread.Sleep(100);
                }
                Debugger.Break();
#else
            Debugger.Launch();
#endif
            }
#endif
            var log = new MSBuildLogger(Log);

            // Log inputs
            log.LogDebug($"(in) RestoreGraphItems Count '{RestoreGraphItems?.Count() ?? 0}'");
            log.LogDebug($"(in) RestoreDisableParallel '{RestoreDisableParallel}'");
            log.LogDebug($"(in) RestoreNoCache '{RestoreNoCache}'");
            log.LogDebug($"(in) RestoreIgnoreFailedSources '{RestoreIgnoreFailedSources}'");
            log.LogDebug($"(in) RestoreRecursive '{RestoreRecursive}'");
            log.LogDebug($"(in) RestoreForce '{RestoreForce}'");
            log.LogDebug($"(in) HideWarningsAndErrors '{HideWarningsAndErrors}'");
            log.LogDebug($"(in) RestoreForceEvaluate '{RestoreForceEvaluate}'");

            try
            {
                return ExecuteAsync(log).Result;
            }
            catch (AggregateException ex) when (_cts.Token.IsCancellationRequested && ex.InnerException is TaskCanceledException)
            {
                // Canceled by user
                log.LogError(Strings.RestoreCanceled);
                return false;
            }
            catch (Exception e)
            {
                ExceptionUtilities.LogException(e, log);
                return false;
            }
        }

        private async Task<bool> ExecuteAsync(Common.ILogger log)
        {
            if (RestoreGraphItems.Length < 1 && !HideWarningsAndErrors)
            {
                log.LogWarning(Strings.NoProjectsProvidedToTask);
                return true;
            }
            var restoreSummaries = new List<RestoreSummary>();

            // Set user agent and connection settings.
            ConfigureProtocol();

            var dgFile = MSBuildRestoreUtility.GetDependencySpec(RestoreGraphItems.Select(MSBuildUtility.WrapMSBuildItem));

            try
            {
#if IS_DESKTOP
                if (dgFile.Projects.Any(i => i.RestoreMetadata.ProjectStyle == ProjectStyle.PackagesConfig))
                {
                    var v2RestoreResult = await PerformNuGetV2RestoreAsync(log, dgFile);
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
                                    Log.LogWarning(
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
                                    Log.LogWarning(message.Message);
                                }
                            });
                    }
                }
#endif
                var providerCache = new RestoreCommandProvidersCache();

                using (var cacheContext = new SourceCacheContext())
                {
                    cacheContext.NoCache = RestoreNoCache;
                    cacheContext.IgnoreFailedSources = RestoreIgnoreFailedSources;

                    // Pre-loaded request provider containing the graph file
                    var providers = new List<IPreLoadedRestoreRequestProvider>();

                    if (dgFile.Restore.Count < 1)
                    {
                        if (restoreSummaries.Count < 1)
                        {
                            // Restore will fail if given no inputs, but here we should skip it and provide a friendly message.
                            log.LogMinimal(Strings.NoProjectsToRestore);
                        }
                        return true;
                    }

                    // Add all child projects
                    if (RestoreRecursive)
                    {
                        BuildTasksUtility.AddAllProjectsForRestore(dgFile);
                    }

                    providers.Add(new DependencyGraphSpecRequestProvider(providerCache, dgFile));

                    var restoreContext = new RestoreArgs()
                    {
                        CacheContext = cacheContext,
                        LockFileVersion = LockFileFormat.Version,
                        DisableParallel = RestoreDisableParallel,
                        Log = log,
                        MachineWideSettings = new XPlatMachineWideSetting(),
 s                      PreLoadedRequestProviders = providers,
                        AllowNoOp = !RestoreForce,
                        HideWarningsAndErrors = HideWarningsAndErrors,
                        RestoreForceEvaluate = RestoreForceEvaluate
                    };

                    // 'dotnet restore' fails on slow machines (https://github.com/NuGet/Home/issues/6742)
                    // The workaround is to pass the '--disable-parallel' option.
                    // We apply the workaround by default when the system has 1 cpu.
                    // This will fix restore failures on VMs with 1 CPU and containers with less or equal to 1 CPU assigned.
                    if (Environment.ProcessorCount == 1)
                    {
                        restoreContext.DisableParallel = true;
                    }

                    if (restoreContext.DisableParallel)
                    {
                        HttpSourceResourceProvider.Throttle = SemaphoreSlimThrottle.CreateBinarySemaphore();
                    }

                    DefaultCredentialServiceUtility.SetupDefaultCredentialService(log, !Interactive);

                    _cts.Token.ThrowIfCancellationRequested();

                    restoreSummaries.AddRange(await RestoreRunner.RunAsync(restoreContext, _cts.Token));
                }
            }
            finally
            {
                if (restoreSummaries.Any(x => x.InstallCount > 0))
                {
                    // Summary
                    RestoreSummary.Log(log, restoreSummaries);
                }
            }

            return restoreSummaries.All(x => x.Success);
        }

        private static void ConfigureProtocol()
        {
            // Set connection limit
            NetworkProtocolUtility.SetConnectionLimit();

            // Set user agent string used for network calls
            SetUserAgent();

            // This method has no effect on .NET Core.
            NetworkProtocolUtility.ConfigureSupportedSslProtocols();
        }

        private static void SetUserAgent()
        {
#if IS_CORECLR
            UserAgent.SetUserAgentString(new UserAgentStringBuilder(HttpUserAgent)
                .WithOSDescription(RuntimeInformation.OSDescription));
#else
            // OS description is set by default on Desktop
            UserAgent.SetUserAgentString(new UserAgentStringBuilder(HttpUserAgent));
#endif
        }

        public void Cancel()
        {
            _cts.Cancel();
        }

        public void Dispose()
        {
            _cts.Dispose();
        }

#if IS_DESKTOP
        internal PackageSaveMode EffectivePackageSaveMode { get; set; }

        internal string PackageSaveMode { get; set; }

        internal void CalculateEffectivePackageSaveMode(ISettings settings)
        {
            string packageSaveModeValue = PackageSaveMode;
            if (string.IsNullOrEmpty(packageSaveModeValue))
            {
                packageSaveModeValue = SettingsUtility.GetConfigValue(settings, "PackageSaveMode");
            }

            if (!string.IsNullOrEmpty(packageSaveModeValue))
            {
                // The PackageSaveMode flag only determines if nuspec and nupkg are saved at the target location.
                // For install \ restore, we always extract files.
                EffectivePackageSaveMode = Packaging.PackageSaveMode.Files;
                foreach (var v in packageSaveModeValue.Split(';'))
                {
                    if (v.Equals(Packaging.PackageSaveMode.Nupkg.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        EffectivePackageSaveMode |= Packaging.PackageSaveMode.Nupkg;
                    }
                    else if (v.Equals(Packaging.PackageSaveMode.Nuspec.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        EffectivePackageSaveMode |= Packaging.PackageSaveMode.Nuspec;
                    }
                    else
                    {
                        // TODO - where are these?
                        // string message = String.Format(
                        //     CultureInfo.CurrentCulture,
                        //     LocalizedResourceManager.GetString("Warning_InvalidPackageSaveMode"),
                        //     v);

                        // throw new InvalidOperationException(message);
                    }
                }
            }
            else
            {
                EffectivePackageSaveMode = Packaging.PackageSaveMode.None;
            }
        }

        private async Task<RestoreSummary> PerformNuGetV2RestoreAsync(Common.ILogger log, DependencyGraphSpec dgFile)
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
                    if (!RestoreNoCache)
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

            CalculateEffectivePackageSaveMode(settings);

            var packageSaveMode = EffectivePackageSaveMode == Packaging.PackageSaveMode.None ?
                Packaging.PackageSaveMode.Defaultv2 :
                EffectivePackageSaveMode;

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
            var collectorLogger = new RestoreCollectorLogger(new MSBuildLogger(Log));

            var packageRestoreContext = new PackageRestoreContext(
                nuGetPackageManager,
                packageRestoreData,
                CancellationToken.None,
                packageRestoredEvent: (sender, args) => { Interlocked.Add(ref installCount, args.Restored ? 1 : 0); },
                packageRestoreFailedEvent: (sender, args) => { failedEvents.Enqueue(args); },
                sourceRepositories: repositories,
                maxNumberOfParallelTasks: RestoreDisableParallel
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
                cacheContext.NoCache = RestoreNoCache;

                // TODO: Direct download?
                // //cacheContext.DirectDownload = DirectDownload;

                var downloadContext = new PackageDownloadContext(cacheContext, repositoryPath, directDownload: false)
                {
                    ClientPolicyContext = clientPolicyContext
                };

                DefaultCredentialServiceUtility.SetupDefaultCredentialService(log, !Interactive);

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
                    (IReadOnlyList<string>)settings.GetConfigFilePaths(),
                    (IReadOnlyList<string>)packageSources.Select(x => x.Source),
                    installCount,
                    (IReadOnlyList<IRestoreLogMessage>)collectorLogger.Errors.Concat(ProcessFailedEventsIntoRestoreLogs(failedEvents)));
            }
        }

        private IEnumerable<Packaging.PackageReference> GetInstalledPackageReferences(string projectConfigFilePath, bool allowDuplicatePackageIds)
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
#endif
    }

#if IS_DESKTOP
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