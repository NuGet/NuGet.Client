// Copyright (c) .NET Foundation. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Common.Migrations;
using NuGet.Configuration;
using NuGet.Credentials;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.PackageExtraction;
using NuGet.Packaging.Signing;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.RuntimeModel;
using NuGet.Versioning;

using ILogger = NuGet.Common.ILogger;
using SdkResolverBase = Microsoft.Build.Framework.SdkResolver;
using SdkResultBase = Microsoft.Build.Framework.SdkResult;

namespace Microsoft.Build.NuGetSdkResolver
{
    /// <summary>
    /// Represents an experimental NuGet-based MSBuild project SDK resolver.  To enable this resolver, set the <see cref="MSBuildEnableExperimentalNuGetSdkResolver" /> environment variable to <see langword="true" />.
    /// </summary>
    public sealed class SdkResolver : SdkResolverBase
    {
        /// <summary>
        /// Represents an environment variable a user can set to disable this SDK resolver.
        /// </summary>
        internal const string MSBuildDisableNuGetSdkResolver = nameof(MSBuildDisableNuGetSdkResolver);

        /// <summary>
        /// Represents an environment variable a user can set to enable this SDK resolver.
        /// </summary>
        internal const string MSBuildEnableExperimentalNuGetSdkResolver = nameof(MSBuildEnableExperimentalNuGetSdkResolver);

        /// <summary>
        /// Stores a cache that stores results for by a <see cref="LibraryIdentity" />.
        /// </summary>
        internal static readonly ConcurrentDictionary<LibraryIdentity, Lazy<SdkResultBase>> ResultCache = new ConcurrentDictionary<LibraryIdentity, Lazy<SdkResultBase>>();

        /// <summary>
        /// Stores a <see cref="LocalPackageFileCache" /> instance for cache package file look ups.
        /// </summary>
        private static readonly LocalPackageFileCache LocalPackageFileCache = new LocalPackageFileCache();

        /// <summary>
        /// Stores an <see cref="IMachineWideSettings" /> instance used for reading machine-wide settings.
        /// </summary>
        private static readonly Lazy<IMachineWideSettings> MachineWideSettingsLazy = new Lazy<IMachineWideSettings>(() => new XPlatMachineWideSetting());

        /// <summary>
        /// Stores a <see cref="SettingsLoadingContext" /> instance used to cache the loading of settings.
        /// </summary>
        private static readonly SettingsLoadingContext SettingsLoadContext = new SettingsLoadingContext();

        /// <summary>
        /// Stores a <see cref="SemaphoreSlim" /> instance used to ensure that this SDK resolver is only ever resolving one SDK at a time.
        /// </summary>
        private static readonly SemaphoreSlim SingleResolutionSemaphore = new SemaphoreSlim(initialCount: 1, maxCount: 1);

        /// <summary>
        /// Stores a value indicating whether or not this SDK resolver has been disabled.
        /// </summary>
        private readonly bool _disableNuGetSdkResolver = false;

        /// <summary>
        /// Stores a value indicating whether or not this SDK resolver has been enabled.
        /// </summary>
        private readonly bool _enableExperimentalNuGetSdkResolver = false;

        /// <summary>
        /// Stores a <see cref="IGlobalJsonReader" /> instance used to read a global.json.
        /// </summary>
        private readonly IGlobalJsonReader _globalJsonReader;

        /// <summary>
        /// Initializes a new instance of the <see cref="NuGetSdkResolver" /> class.
        /// </summary>
        public SdkResolver()
            : this(GlobalJsonReader.Instance, EnvironmentVariableWrapper.Instance)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SdkResolver" /> class with the specified <see cref="IGlobalJsonReader" />.
        /// </summary>
        /// <param name="globalJsonReader">An <see cref="IGlobalJsonReader" /> to use when reading a global.json file.</param>
        /// <param name="environmentVariableReader">An <see cref="IEnvironmentVariableReader" /> to use when reading environment variables.</param>
        /// <param name="resetResultCache"><see langword="true" /> to reset the result cache, otherwise <see langword="false" />.</param>
        /// <exception cref="ArgumentNullException"><paramref name="globalJsonReader" /> is <see langword="null" />.</exception>
        internal SdkResolver(IGlobalJsonReader globalJsonReader, IEnvironmentVariableReader environmentVariableReader, bool resetResultCache = false)
        {
            _globalJsonReader = globalJsonReader ?? throw new ArgumentNullException(nameof(globalJsonReader));

            // Determine if the experimental NuGet-based MSBuild project SDK resolver has been enabled
            _enableExperimentalNuGetSdkResolver = IsFeatureFlagEnabled(environmentVariableReader, MSBuildEnableExperimentalNuGetSdkResolver);

            // Determine if the NuGet-based MSBuild project SDK resolver has been disabled
            _disableNuGetSdkResolver = IsFeatureFlagEnabled(environmentVariableReader, MSBuildDisableNuGetSdkResolver);

            if (resetResultCache)
            {
                ResultCache.Clear();
            }
        }

        public override string Name => "NuGet-based MSBuild project SDK resolver";

        public override int Priority => 5999;

        public override SdkResultBase Resolve(SdkReference sdkReference, SdkResolverContext resolverContext, SdkResultFactory resultFactory)
        {
            try
            {
                // If the experimental NuGet-based MSBuild project SDK resolver is not enabled, just return to MSBuild that nothing was resolved so that the other resolvers can run.
                if (!_enableExperimentalNuGetSdkResolver)
                {
                    return resultFactory.IndicateFailure(errors: null, warnings: null);
                }

                // Feature flag to disable this resolver
                if (_disableNuGetSdkResolver)
                {
                    // The NuGet-based MSBuild project SDK resolver did not resolve the SDK "{0}" because the resolver is disabled by the {1} environment variable.
                    return resultFactory.IndicateFailure(new List<string>(1) { string.Format(CultureInfo.CurrentCulture, Strings.Error_DisabledSdkResolver, sdkReference.Name, MSBuildDisableNuGetSdkResolver) });
                }

                if (NuGetEventSource.IsEnabled) NuGetSdkResolver.TraceEvents.ResolveStart(sdkReference);

                try
                {
                    // The main logger which logs messages back to MSBuild
                    var logger = new NuGetSdkLogger(resolverContext.Logger);

                    // Try to see if a version is specified in the project or in a global.json. The method will log a reason why a version wasn't found
                    if (!TryGetLibraryIdentityFromSdkReference(sdkReference, resolverContext, logger, out LibraryIdentity libraryIdentity))
                    {
                        return resultFactory.IndicateFailure(logger.Errors, logger.Warnings);
                    }

                    Lazy<SdkResultBase> resultLazy = ResultCache.GetOrAdd(
                        libraryIdentity,
                        (key) => new Lazy<SdkResultBase>(() => GetResult(key, resolverContext, resultFactory, logger)));

                    SdkResultBase result = resultLazy.Value;

                    return result;
                }
                finally
                {
                    if (NuGetEventSource.IsEnabled) NuGetSdkResolver.TraceEvents.ResolveStop(sdkReference);
                }
            }
            catch (Exception e)
            {
                return resultFactory.IndicateFailure(errors: new[] { Strings.Error_UnhandledException, e.ToString() });
            }
        }

        /// <summary>
        /// Determines if a feature flag is enabled by reading the environment variable with the specified name.
        /// </summary>
        /// <param name="environmentVariableReader">The <see cref="IEnvironmentVariableReader" /> to use when reading environment variables.</param>
        /// <param name="name">The name of the environment variable to read.</param>
        /// <returns><see langword="true" /> if the specified feature flag has a value of "1" or "true", otherwise <see langword="false" />.</returns>
        internal static bool IsFeatureFlagEnabled(IEnvironmentVariableReader environmentVariableReader, string name)
        {
            string value = environmentVariableReader.GetEnvironmentVariable(name);

            return string.Equals(value, "1", StringComparison.Ordinal) || string.Equals(value, bool.TrueString, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Resolve an MSBuild project SDK for the specified <see cref="SdkReference" />.
        /// </summary>
        /// <param name="libraryIdentity">The <see cref="LibraryIdentity" /> of the MSBuild project SDK to resolve.</param>
        /// <param name="resolverContext">An <see cref="SdkResolverContext" /> representing the context under which the MSBuild project SDK is being resolved.</param>
        /// <param name="resultFactory">An <see cref="SdkResultFactory" /> to use when creating an <see cref="SdkResultBase" /> object.</param>
        /// <param name="sdkLogger">An <see cref="NuGetSdkLogger" /> to use for logging information back .</param>
        /// <returns></returns>
        internal SdkResultBase GetResult(LibraryIdentity libraryIdentity, SdkResolverContext resolverContext, SdkResultFactory resultFactory, NuGetSdkLogger sdkLogger)
        {
            sdkLogger.LogVerbose(string.Format(CultureInfo.CurrentCulture, Strings.LocatingSdk, libraryIdentity.Name, libraryIdentity.Version.OriginalVersion));

            if (NuGetEventSource.IsEnabled) NuGetSdkResolver.TraceEvents.GetResultStart(libraryIdentity.Name, libraryIdentity.Version.OriginalVersion);

            SdkResultBase result = null;

            try
            {
                MigrationRunner.Run();

                if (NuGetEventSource.IsEnabled) NuGetSdkResolver.TraceEvents.LoadSettingsStart();

                ISettings settings;
                try
                {
                    settings = Settings.LoadDefaultSettings(resolverContext.ProjectFilePath, configFileName: null, MachineWideSettingsLazy.Value, SettingsLoadContext);
                }
                catch (Exception e)
                {
                    sdkLogger.LogError(string.Format(CultureInfo.CurrentCulture, Strings.Error_FailedToReadSettings, e.Message));

                    return resultFactory.IndicateFailure(sdkLogger.Errors, sdkLogger.Warnings);
                }
                finally
                {
                    if (NuGetEventSource.IsEnabled) NuGetSdkResolver.TraceEvents.LoadSettingsStop();
                }

                var versionFolderPathResolver = new VersionFolderPathResolver(SettingsUtility.GetGlobalPackagesFolder(settings));

                string installPath = GetSdkPackageInstallPath(libraryIdentity.Name, libraryIdentity.Version, versionFolderPathResolver);

                if (!string.IsNullOrWhiteSpace(installPath))
                {
                    // The package is already on disk so return the path to it
                    result = resultFactory.IndicateSuccess(installPath, libraryIdentity.Version.ToNormalizedString(), sdkLogger.Warnings);
                }
                else
                {
                    if (resolverContext.IsRunningInVisualStudio)
                    {
                        // TODO: Use JTF
                    }
                    else
                    {
                        // Restore the package from the configured feeds and return the path to the package on disk
                        result = RestorePackageAsync(libraryIdentity, resolverContext, resultFactory, settings, versionFolderPathResolver, sdkLogger).ConfigureAwait(continueOnCapturedContext: false).GetAwaiter().GetResult();
                    }
                }
            }
            finally
            {
                if (NuGetEventSource.IsEnabled) NuGetSdkResolver.TraceEvents.GetResultStop(libraryIdentity.Name, libraryIdentity.Version.OriginalVersion, result);
            }

            return result;
        }

        /// <summary>
        /// Attempts to determine a version to use for the specified MSBuild project SDK.
        /// </summary>
        /// <param name="sdkReference">An <see cref="SdkReference" /> containing details about the MSBuild project SDK.</param>
        /// <param name="resolverContext">An <see cref="SdkResolverContext" /> representing the context under which the MSBuild project SDK is being resolved.</param>
        /// <param name="logger">An <see cref="ILogger" /> to use to log any messages.</param>
        /// <param name="libraryIdentity">Receives a <see cref="LibraryIdentity" /> for the specified MSBuild project SDK if one was found, otherwise <see langword="null" />.</param>
        /// <returns><see langword="true" /> if a version was found for the specified MSBuild project SDK, otherwise <see langword="false" />.</returns>
        internal bool TryGetLibraryIdentityFromSdkReference(SdkReference sdkReference, SdkResolverContext resolverContext, ILogger logger, out LibraryIdentity libraryIdentity)
        {
            // This resolver only works if the user specifies a version in a project or a global.json.
            string sdkVersion = sdkReference.Version;

            libraryIdentity = null;

            if (string.IsNullOrWhiteSpace(sdkVersion))
            {
                Dictionary<string, string> msbuildSdkVersions = _globalJsonReader.GetMSBuildSdkVersions(resolverContext, out string globalJsonFullPath);

                if (msbuildSdkVersions == null)
                {
                    // The NuGet-based MSBuild project SDK resolver did not resolve the SDK "{0}" because there was no version specified in the project or global.json.
                    logger.LogError(string.Format(CultureInfo.CurrentCulture, Strings.Error_NoSdkVersionSpecified, sdkReference.Name));

                    return false;
                }

                if (!msbuildSdkVersions.TryGetValue(sdkReference.Name, out sdkVersion))
                {
                    // The NuGet-based MSBuild project SDK resolver did not resolve the SDK "{0}" because there was no version specified the file "{1}".
                    logger.LogError(string.Format(CultureInfo.CurrentCulture, Strings.Error_NoSdkVersionSpecifiedInGlobalJson, sdkReference.Name, globalJsonFullPath));

                    return false;
                }
            }

            // Ignore invalid versions, there may be another resolver that can handle the version specified
            if (!NuGetVersion.TryParse(sdkVersion, out NuGetVersion nuGetVersion))
            {
                // The NuGet-based MSBuild project SDK resolver did not resolve SDK "{0}" because the version specified "{1}" is not a valid NuGet version.
                logger.LogWarning(string.Format(CultureInfo.CurrentCulture, Strings.Warning_SdkVersionIsNotValidNuGetVersion, sdkReference.Name, sdkVersion));

                return false;
            }

            libraryIdentity = new LibraryIdentity(sdkReference.Name, nuGetVersion, LibraryType.Package);

            return true;
        }

        /// <summary>
        /// Gets the full path to the MSBuild project SDK for the specified package if found, otherwise <see langword="null" />.
        /// </summary>
        /// <param name="id">The ID of the package.</param>
        /// <param name="version">The <see cref="NuGetVersion" /> of the package.</param>
        /// <param name="versionFolderPathResolver">A <see cref="VersionFolderPathResolver" /> to use when resolving the path to the package.</param>
        /// <returns>The full path to the MSBuild project SDK in the package cache, otherwise <see langword="null" />.</returns>
        private static string GetSdkPackageInstallPath(string id, NuGetVersion version, VersionFolderPathResolver versionFolderPathResolver)
        {
            string installPath = versionFolderPathResolver.GetInstallPath(id, version);

            if (string.IsNullOrWhiteSpace(installPath))
            {
                return null;
            }

            string sdkPath = Path.Combine(installPath, "Sdk");

            if (Directory.Exists(sdkPath))
            {
                return sdkPath;
            }

            sdkPath = Path.Combine(installPath, "sdk");

            if (Directory.Exists(sdkPath))
            {
                return sdkPath;
            }

            return null;
        }

        /// <summary>
        /// Restores an MSBuild project SDK NuGet package.
        /// </summary>
        /// <param name="libraryIdentity">The <see cref="LibraryIdentity" /> of the NuGet package.</param>
        /// <param name="context">The <see cref="SdkResolverContext" /> under which the MSBuild project SDK is being resolved.</param>
        /// <param name="factory">An <see cref="SdkResultFactory" /> to use when creating a result</param>
        /// <param name="settings">The <see cref="ISettings" /> to use when locating the package.</param>
        /// <param name="versionFolderPathResolver">A <see cref="VersionFolderPathResolver" /> to use when locating the package.</param>
        /// <param name="sdkLogger">A <see cref="NuGetSdkLogger" /> to use for logging..</param>
        /// <returns>An <see cref="Task{SdkResultBase}" /> representing the details of the package if it was found or errors if any occurred.</returns>
        private async Task<SdkResultBase> RestorePackageAsync(LibraryIdentity libraryIdentity, SdkResolverContext context, SdkResultFactory factory, ISettings settings, VersionFolderPathResolver versionFolderPathResolver, NuGetSdkLogger sdkLogger)
        {
            if (NuGetEventSource.IsEnabled) TraceEvents.WaitForRestoreSemaphoreStart(libraryIdentity);

            // Only ever resolve one package at a time to reduce the possibility of thread starvation
            await SingleResolutionSemaphore.WaitAsync().ConfigureAwait(continueOnCapturedContext: false);

            try
            {
                if (NuGetEventSource.IsEnabled) TraceEvents.WaitForRestoreSemaphoreStop(libraryIdentity);

                if (NuGetEventSource.IsEnabled) NuGetSdkResolver.TraceEvents.RestorePackageStart(libraryIdentity);

                // Downloading SDK package "{0}" version "{1}"...
                sdkLogger.LogVerbose(string.Format(CultureInfo.CurrentCulture, Strings.DownloadingPackage, libraryIdentity.Name, libraryIdentity.Version.OriginalVersion));

                // TODO: Setting this up for each restore seems to cause the credential provider to prompt multiple times, investigate if this is because the service is being configured over and over
                DefaultCredentialServiceUtility.SetupDefaultCredentialService(sdkLogger, nonInteractive: !(context.IsRunningInVisualStudio || context.Interactive));

#if !NETFRAMEWORK
                X509TrustStore.InitializeForDotNetSdk(sdkLogger);
#endif

                using (var sourceCacheContext = new SourceCacheContext
                {
                    IgnoreFailedSources = true,
                })
                {
                    var packageSourceProvider = new PackageSourceProvider(settings);

                    var cachingSourceProvider = new CachingSourceProvider(packageSourceProvider);

                    var remoteWalkContext = new RemoteWalkContext(cacheContext: sourceCacheContext, packageSourceMapping: PackageSourceMapping.GetPackageSourceMapping(settings), sdkLogger);

                    foreach (SourceRepository source in SettingsUtility.GetEnabledSources(settings).Select(i => cachingSourceProvider.CreateRepository(i)))
                    {
                        SourceRepositoryDependencyProvider remoteProvider = new SourceRepositoryDependencyProvider(
                            source,
                            sdkLogger,
                            sourceCacheContext,
                            sourceCacheContext.IgnoreFailedSources,
                            ignoreWarning: false,
                            fileCache: LocalPackageFileCache,
                            isFallbackFolderSource: false);

                        remoteWalkContext.RemoteLibraryProviders.Add(remoteProvider);
                    }

                    var walker = new RemoteDependencyWalker(remoteWalkContext);

                    var libraryRange = new LibraryRange(libraryIdentity.Name, new VersionRange(minVersion: libraryIdentity.Version, includeMinVersion: true, libraryIdentity.Version, includeMaxVersion: true), LibraryDependencyTarget.Package);

                    GraphNode<RemoteResolveResult> result = await walker.WalkAsync(libraryRange, FrameworkConstants.CommonFrameworks.Net45, null, RuntimeGraph.Empty, recursive: false).ConfigureAwait(continueOnCapturedContext: false);

                    RemoteMatch match = result.Item.Data.Match;

                    if (match == null || match.Library.Type == LibraryType.Unresolved)
                    {
                        RestoreLogMessage message = await UnresolvedMessages.GetMessageAsync(
                            "any/any",
                            libraryRange,
                            remoteWalkContext.FilterDependencyProvidersForLibrary(libraryRange),
                            remoteWalkContext.PackageSourceMapping.IsEnabled,
                            remoteWalkContext.RemoteLibraryProviders,
                            remoteWalkContext.CacheContext,
                            remoteWalkContext.Logger,
                            CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);

                        sdkLogger.Log(message);

                        return factory.IndicateFailure(sdkLogger.Errors, sdkLogger.Warnings);
                    }

                    var packageIdentity = new PackageIdentity(match.Library.Name, match.Library.Version);

                    ClientPolicyContext clientPolicyContext = ClientPolicyContext.GetClientPolicy(settings, sdkLogger);

                    var packageExtractionContext = new PackageExtractionContext(PackageSaveMode.Defaultv3, PackageExtractionBehavior.XmlDocFileSaveMode, clientPolicyContext, sdkLogger);

                    using (IPackageDownloader downloader = await match.Provider.GetPackageDownloaderAsync(packageIdentity, sourceCacheContext, sdkLogger, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false))
                    {
                        bool installed = await PackageExtractor.InstallFromSourceAsync(
                            packageIdentity,
                            downloader,
                            versionFolderPathResolver,
                            packageExtractionContext,
                            CancellationToken.None,
                            parentId: default).ConfigureAwait(continueOnCapturedContext: false);

                        if (installed)
                        {
                            string installPath = GetSdkPackageInstallPath(packageIdentity.Id, packageIdentity.Version, versionFolderPathResolver);

                            if (!string.IsNullOrWhiteSpace(installPath))
                            {
                                // Successfully downloaded SDK package "{0}" version "{1}" to "{2}".
                                sdkLogger.LogVerbose(string.Format(CultureInfo.CurrentCulture, Strings.SuccessfullyDownloadedPackage, libraryIdentity.Name, libraryIdentity.Version.OriginalVersion, installPath));

                                return factory.IndicateSuccess(installPath, packageIdentity.Version.ToNormalizedString(), sdkLogger.Warnings);
                            }
                        }
                    }
                }

                return factory.IndicateFailure(sdkLogger.Errors, sdkLogger.Warnings);
            }
            finally
            {
                DefaultCredentialServiceUtility.UpdateCredentialServiceDelegatingLogger(NullLogger.Instance);

                if (NuGetEventSource.IsEnabled) NuGetSdkResolver.TraceEvents.RestorePackageStop(libraryIdentity);

                SingleResolutionSemaphore.Release();
            }
        }

        internal static class TraceEvents
        {
            private const string EventNameWaitForRestoreSemaphore = "SdkResolver/WaitForRestoreSemaphore";

            public static void WaitForRestoreSemaphoreStart(LibraryIdentity libraryIdentity)
            {
                var eventOptions = new EventSourceOptions
                {
                    ActivityOptions = EventActivityOptions.Detachable,
                    Keywords = NuGetEventSource.Keywords.Performance | NuGetEventSource.Keywords.SdkResolver,
                    Opcode = EventOpcode.Start
                };

                NuGetEventSource.Instance.Write(EventNameWaitForRestoreSemaphore, eventOptions, new WaitForRestoreSemaphoreEventData(libraryIdentity.Name, libraryIdentity.Version.OriginalVersion));
            }

            public static void WaitForRestoreSemaphoreStop(LibraryIdentity libraryIdentity)
            {
                var eventOptions = new EventSourceOptions
                {
                    ActivityOptions = EventActivityOptions.Detachable,
                    Keywords = NuGetEventSource.Keywords.Performance | NuGetEventSource.Keywords.SdkResolver,
                    Opcode = EventOpcode.Stop
                };

                NuGetEventSource.Instance.Write(EventNameWaitForRestoreSemaphore, eventOptions, new WaitForRestoreSemaphoreEventData(libraryIdentity.Name, libraryIdentity.Version.OriginalVersion));
            }

            [EventData]
            private record struct WaitForRestoreSemaphoreEventData(string Id, string Version);
        }
    }
}
