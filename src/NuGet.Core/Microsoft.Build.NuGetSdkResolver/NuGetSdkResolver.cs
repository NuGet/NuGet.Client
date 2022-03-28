// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Build.Framework;
using NuGet.Commands;
using NuGet.Common;
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

namespace Microsoft.Build.NuGetSdkResolver
{
    /// <summary>
    /// Represents a NuGet-based MSBuild project SDK resolver.
    /// </summary>
    public sealed class NuGetSdkResolver : SdkResolver
    {
        private static readonly Lazy<bool> DisableNuGetSdkResolver = new Lazy<bool>(() => Environment.GetEnvironmentVariable("MSBUILDDISABLENUGETSDKRESOLVER") == "1");

        private static readonly SemaphoreSlim Semaphore = new SemaphoreSlim(initialCount: 1, maxCount: 1);

        private static readonly Lazy<IMachineWideSettings> MachineWideSettingsLazy = new Lazy<IMachineWideSettings>(() => new XPlatMachineWideSetting());

        private static readonly Lazy<SettingsLoadingContext> SettingsLoadContextLazy = new Lazy<SettingsLoadingContext>(() => new SettingsLoadingContext());

        private static readonly LocalPackageFileCache LocalPackageFileCache = new LocalPackageFileCache();

        internal static readonly ConcurrentDictionary<LibraryIdentity, Lazy<SdkResult>> ResultCache = new ConcurrentDictionary<LibraryIdentity, Lazy<SdkResult>>();

        private readonly IGlobalJsonReader _globalJsonReader;

        /// <summary>
        /// Initializes a new instance of the <see cref="NuGetSdkResolver" /> class.
        /// </summary>
        public NuGetSdkResolver()
            : this(new GlobalJsonReader())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NuGetSdkResolver" /> class with the specified <see cref="IGlobalJsonReader" />.
        /// </summary>
        /// <param name="globalJsonReader">An <see cref="IGlobalJsonReader" /> to use when reading a global.json file.</param>
        /// <exception cref="ArgumentNullException"><paramref name="globalJsonReader" /> is <c>null</c>.</exception>
        internal NuGetSdkResolver(IGlobalJsonReader globalJsonReader)
        {
            _globalJsonReader = globalJsonReader ?? throw new ArgumentNullException(nameof(globalJsonReader));
        }

        /// <inheritdoc />
        public override string Name => nameof(NuGetSdkResolver);

        /// <inheritdoc />
        public override int Priority => 6000;

        /// <inheritdoc />
        public override SdkResult Resolve(SdkReference sdkReference, SdkResolverContext resolverContext, SdkResultFactory factory)
        {
            NuGetSdkResolverEventSource.Instance.ResolveStart(sdkReference.Name, sdkReference.Version);

            try
            {
                // The main logger which logs messages back to MSBuild
                NuGetSdkLogger sdkLogger = new NuGetSdkLogger(resolverContext.Logger);

                // A forwarding logger that logs messages to the main logger and the event source logger
                ILogger logger = new ForwardingLogger(sdkLogger, NuGetSdkResolverEventSource.Logger);

                // Escape hatch to disable this resolver
                if (DisableNuGetSdkResolver.Value)
                {
                    logger.LogVerbose("The NuGet-based MSBuild project SDK resolver is currently disabled.");

                    return null;
                }

                // Try to see if a version is specified in the project or in a global.json.  The TryGetNuGetVersionForSdk method will log a reason why a version wasn't found
                if (!TryGetNuGetVersionForSdk(sdkReference, resolverContext, logger, out NuGetVersion nuGetVersion))
                {
                    return null;
                }

                LibraryIdentity libraryIdentity = new LibraryIdentity(sdkReference.Name, nuGetVersion, LibraryType.Package);

                Lazy<SdkResult> resultLazy = ResultCache.GetOrAdd(
                    libraryIdentity,
                    (key) => new Lazy<SdkResult>(() =>
                    {
                        logger.LogVerbose("Locating MSBuild project SDK \"" + libraryIdentity.Name + "\" version \"" + libraryIdentity.Version.OriginalVersion + "\"...");

                        NuGetSdkResolverEventSource.Instance.GetResultStart(libraryIdentity.Name, libraryIdentity.Version.OriginalVersion);

                        ISettings settings = Settings.LoadDefaultSettings(Path.GetDirectoryName(resolverContext.ProjectFilePath), configFileName: null, MachineWideSettingsLazy.Value, SettingsLoadContextLazy.Value);

                        VersionFolderPathResolver versionFolderPathResolver = new VersionFolderPathResolver(SettingsUtility.GetGlobalPackagesFolder(settings));

                        string installPath = GetSdkPackageInstallPath(sdkReference.Name, nuGetVersion, versionFolderPathResolver);

                        SdkResult result = !string.IsNullOrWhiteSpace(installPath)
                            ? factory.IndicateSuccess(installPath, nuGetVersion.ToNormalizedString(), sdkLogger.Warnings)
                            : ResolveSdk(sdkReference.Name, nuGetVersion, resolverContext, factory, settings, versionFolderPathResolver, logger, sdkLogger);

                        NuGetSdkResolverEventSource.Instance.GetResultStop(key.Name, key.Version.OriginalVersion, result.Path, result.Success);

                        return result;
                    }));

                SdkResult result = resultLazy.Value;

                return result;
            }
            catch (Exception e)
            {
                return factory.IndicateFailure(new[] { "Unhandled exception in NuGet-based MSBuild project SDK resolver", e.ToString() });
            }
            finally
            {
                NuGetSdkResolverEventSource.Instance.ResolveStop(sdkReference.Name, sdkReference.Version);
            }
        }

        /// <summary>
        /// Attempts to determine a version to use for the specified MSBuild project SDK.
        /// </summary>
        /// <param name="sdkReference">An <see cref="SdkReference" /> containing details about the MSBuild project SDK.</param>
        /// <param name="resolverContext">An <see cref="SdkResolverContext" /> representing the context under which the MSBuild project SDK is being resolved.</param>
        /// <param name="logger">An <see cref="ILogger" /> to use to log any messages.</param>
        /// <param name="nuGetVersion">Receives a <see cref="NuGetVersion" /> for the specified MSBuild project SDK if one was found, otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if a version was found for the specified MSBuild project SDK, otherwise <c>false</c>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryGetNuGetVersionForSdk(SdkReference sdkReference, SdkResolverContext resolverContext, ILogger logger, out NuGetVersion nuGetVersion)
        {
            // This resolver only works if the user specifies a version in a project or a global.json.
            string sdkVersion = sdkReference.Version;

            nuGetVersion = null;

            if (string.IsNullOrWhiteSpace(sdkVersion))
            {
                Dictionary<string, string> msbuildSdkVersions = _globalJsonReader.GetMSBuildSdkVersions(resolverContext);

                if (msbuildSdkVersions == null)
                {
                    logger.LogVerbose($"No global.json was found containing MSBuild project SDK versions.");

                    return false;
                }

                if (!msbuildSdkVersions.TryGetValue(sdkReference.Name, out sdkVersion))
                {
                    logger.LogVerbose($"No MSBuild project SDK version was found for \"{sdkReference.Name}\".");

                    return false;
                }
            }

            // Ignore invalid versions, there may be another resolver that can handle the version specified
            if (!NuGetVersion.TryParse(sdkVersion, out nuGetVersion))
            {
                logger.LogVerbose($"The SDK version \"{sdkVersion}\" for MSBuild project SDK \"{sdkReference.Name}\" is not a valid NuGet version so the NuGet-based MSBuild project SDK resolver will not resolve it.");

                return false;
            }

            return true;
        }

        /// <summary>
        /// Resolves an MSBuild project SDK as a NuGet package.
        /// </summary>
        /// <param name="id">The name of the NuGet package.</param>
        /// <param name="nuGetVersion">The <see cref="NuGetVersion" /> of the package.</param>
        /// <param name="context">The <see cref="SdkResolverContext" /> under which the MSBuild project SDK is being resolved.</param>
        /// <param name="factory">An <see cref="SdkResultFactory" /> to use when creating a result</param>
        /// <param name="settings">The <see cref="ISettings" /> to use when locating the package.</param>
        /// <param name="versionFolderPathResolver">A <see cref="VersionFolderPathResolver" /> to use when locating the package.</param>
        /// <param name="logger">A <see cref="ILogger" /> to use when logging messages.</param>
        /// <param name="sdkLogger">A <see cref="NuGetSdkLogger" /> to use when logging errors or warnings.</param>
        /// <returns>An <see cref="SdkResult" /> representing the details of the package if it was found or errors if any occured.</returns>
        private SdkResult ResolveSdk(string id, NuGetVersion nuGetVersion, SdkResolverContext context, SdkResultFactory factory, ISettings settings, VersionFolderPathResolver versionFolderPathResolver, ILogger logger, NuGetSdkLogger sdkLogger)
        {
            NuGetSdkResolverEventSource.Instance.WaitForRestoreSemaphoreStart(id, nuGetVersion.OriginalVersion);

            // Only ever resolve one package at a time to reduce the possibilty of thread starvation
            Semaphore.Wait();

            NuGetSdkResolverEventSource.Instance.WaitForRestoreSemaphoreStop(id, nuGetVersion.OriginalVersion);

            NuGetSdkResolverEventSource.Instance.RestorePackageStart(id, nuGetVersion.OriginalVersion);

            try
            {
                logger.LogVerbose("Downloading SDK package \"" + id + "\" version \"" + nuGetVersion.OriginalVersion + "\"...");

                DefaultCredentialServiceUtility.SetupDefaultCredentialService(logger, nonInteractive: !context.Interactive);

                using (var sourceCacheContext = new SourceCacheContext
                {
                    IgnoreFailedSources = true,
                })
                {
                    var packageSourceProvider = new PackageSourceProvider(settings);

                    var cachingSourceProvider = new CachingSourceProvider(packageSourceProvider);

                    var remoteWalkContext = new RemoteWalkContext(cacheContext: sourceCacheContext, packageSourceMapping: PackageSourceMapping.GetPackageSourceMapping(settings), logger);

                    foreach (SourceRepository source in SettingsUtility.GetEnabledSources(settings).Select(i => cachingSourceProvider.CreateRepository(i)))
                    {
                        SourceRepositoryDependencyProvider remoteProvider = new SourceRepositoryDependencyProvider(
                            source,
                            logger,
                            sourceCacheContext,
                            sourceCacheContext.IgnoreFailedSources,
                            ignoreWarning: false,
                            fileCache: LocalPackageFileCache,
                            isFallbackFolderSource: false);

                        remoteWalkContext.RemoteLibraryProviders.Add(remoteProvider);
                    }

                    var walker = new RemoteDependencyWalker(remoteWalkContext);

                    var library = new LibraryRange(id, new VersionRange(nuGetVersion, includeMinVersion: true, nuGetVersion, includeMaxVersion: true), LibraryDependencyTarget.Package)
                    {
                        TypeConstraint = LibraryDependencyTarget.Package
                    };

                    GraphNode<RemoteResolveResult> result = walker.WalkAsync(library, FrameworkConstants.CommonFrameworks.Net45, null, RuntimeGraph.Empty, recursive: false).Result;

                    RemoteMatch match = result.Item.Data.Match;

                    if (match == null || match.Library.Type == LibraryType.Unresolved)
                    {
                        var message = UnresolvedMessages.GetMessageAsync(
                            "any/any",
                            library,
                            remoteWalkContext.FilterDependencyProvidersForLibrary(match.Library),
                            remoteWalkContext.PackageSourceMapping.IsEnabled,
                            remoteWalkContext.RemoteLibraryProviders,
                            remoteWalkContext.CacheContext,
                            remoteWalkContext.Logger,
                            CancellationToken.None).Result;

                        logger.Log(message);

                        return factory.IndicateFailure(sdkLogger.Errors, sdkLogger.Warnings);
                    }

                    PackageIdentity packageIdentity = new PackageIdentity(match.Library.Name, match.Library.Version);

                    ClientPolicyContext clientPolicyContext = ClientPolicyContext.GetClientPolicy(settings, logger);

                    PackageExtractionContext packageExtractionContext = new PackageExtractionContext(PackageSaveMode.Defaultv3, PackageExtractionBehavior.XmlDocFileSaveMode, clientPolicyContext, logger);

                    using (IPackageDownloader downloader = match.Provider.GetPackageDownloaderAsync(packageIdentity, sourceCacheContext, logger, CancellationToken.None).Result)
                    {
                        bool installed = PackageExtractor.InstallFromSourceAsync(
                            packageIdentity,
                            downloader,
                            versionFolderPathResolver,
                            packageExtractionContext,
                            CancellationToken.None,
                            parentId: default).Result;

                        if (installed)
                        {
                            string installPath = GetSdkPackageInstallPath(packageIdentity.Id, packageIdentity.Version, versionFolderPathResolver);

                            if (!string.IsNullOrWhiteSpace(installPath))
                            {
                                logger.LogVerbose("Successfully downloaded SDK package \"" + id + "\" version \"" + nuGetVersion.OriginalVersion + "\" to \"" + installPath + "\".");

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

                NuGetSdkResolverEventSource.Instance.RestorePackageStop(id, nuGetVersion.OriginalVersion);

                Semaphore.Release();
            }
        }

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
    }
}
