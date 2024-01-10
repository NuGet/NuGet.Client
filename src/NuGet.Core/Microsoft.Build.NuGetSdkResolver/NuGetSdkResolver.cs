// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Credentials;
using NuGet.LibraryModel;
using NuGet.Packaging;
#if !NETFRAMEWORK
using NuGet.Packaging.Signing;
#endif
using NuGet.Versioning;

namespace Microsoft.Build.NuGetSdkResolver
{
    /// <summary>
    /// Represents a NuGet-based SDK resolver.  It is very important that this class does not reference any NuGet assemblies
    /// directly as an optimization to avoid loading them unless they are needed.  The current implementation only loads
    /// Newtonsoft.Json if a global.json is found and it contains the msbuild-sdks section and a few NuGet assemblies to parse
    /// a version.  The remaining NuGet assemblies are then loaded to do a restore.
    /// </summary>
    public sealed class NuGetSdkResolver : SdkResolver
    {
        private static readonly Lazy<bool> DisableNuGetSdkResolver = new Lazy<bool>(() => Environment.GetEnvironmentVariable("MSBUILDDISABLENUGETSDKRESOLVER") == "1");

        private static readonly Lazy<object> SettingsLoadContext = new Lazy<object>(() => new SettingsLoadingContext());

        private static readonly Lazy<object> MachineWideSettings = new Lazy<object>(() => new XPlatMachineWideSetting());

        private readonly IGlobalJsonReader _globalJsonReader;

        /// <summary>
        /// Initializes a new instance of the NuGetSdkResolver class.
        /// </summary>
        public NuGetSdkResolver()
            : this(GlobalJsonReader.Instance)
        {
        }

        /// <summary>
        /// Initializes a new instance of the NuGetSdkResolver class with the specified <see cref="IGlobalJsonReader" />.
        /// </summary>
        /// <param name="globalJsonReader">An <see cref="IGlobalJsonReader" /> to use when reading a global.json file.</param>
        internal NuGetSdkResolver(IGlobalJsonReader globalJsonReader)
        {
            _globalJsonReader = globalJsonReader;
        }

        /// <inheritdoc />
        public override string Name => nameof(NuGetSdkResolver);

        /// <inheritdoc />
        public override int Priority => 6000;

        /// <summary>Resolves the specified SDK reference from NuGet.</summary>
        /// <param name="sdkReference">A <see cref="T:Microsoft.Build.Framework.SdkReference" /> containing the referenced SDKs be resolved.</param>
        /// <param name="resolverContext">Context for resolving the SDK.</param>
        /// <param name="factory">Factory class to create an <see cref="T:Microsoft.Build.Framework.SdkResult" /></param>
        /// <returns>
        ///     An <see cref="T:Microsoft.Build.Framework.SdkResult" /> containing the resolved SDKs or associated error / reason
        ///     the SDK could not be resolved.  Return <see langword="null" /> if the resolver is not
        ///     applicable for a particular <see cref="T:Microsoft.Build.Framework.SdkReference" />.
        /// </returns>
        public override SdkResult Resolve(SdkReference sdkReference, SdkResolverContext resolverContext, SdkResultFactory factory)
        {
            // Escape hatch to disable this resolver
            if (DisableNuGetSdkResolver.Value)
            {
                // Errors returned to MSBuild aren't logged if another SDK resolver succeeds.  Returning errors on non-succcess is
                // what all SDK resolvers should be doing and if no SDK resolver succeeds then MSBuild logs all of the errors as
                // one.  In this case, the SDK resolver is disabled and it might be helpful for a user to see that they've disabled
                // it in case it was a mistake.
                return factory.IndicateFailure(errors: new List<string>() { Strings.Error_DisabledSdkResolver }, warnings: null);
            }

            if (NuGetEventSource.IsEnabled) TraceEvents.ResolveStart(sdkReference);

            try
            {
                // This resolver only works if the user specifies a version in a project or a global.json.
                // Ignore invalid versions, there may be another resolver that can handle the version specified
                if (!TryGetNuGetVersionForSdk(sdkReference.Name, sdkReference.Version, resolverContext, out var parsedSdkVersion))
                {
                    // Errors returned to MSBuild aren't logged if another SDK resolver succeeds.  Returning errors on non-succcess is
                    // what all SDK resolvers should be doing and if no SDK resolver succeeds then MSBuild logs all of the errors as
                    // one.  In this case, the user might have mispelled global.json or the SDK name in global.json.
                    return factory.IndicateFailure(errors: new List<string>() { Strings.Error_NoSdkVersion }, warnings: null);
                }

                NuGet.Common.Migrations.MigrationRunner.Run();

                return NuGetAbstraction.GetSdkResult(sdkReference, parsedSdkVersion, resolverContext, factory);
            }
            finally
            {
                if (NuGetEventSource.IsEnabled) TraceEvents.ResolveStop(sdkReference);
            }
        }

        /// <summary>
        /// Attempts to determine what version of an SDK to resolve.  A project-specific version is used first and then a version specified in a global.json.
        /// This method should not consume any NuGet classes directly to avoid loading additional assemblies when they are not needed.  This method
        /// returns an object so that NuGetVersion is not consumed directly.
        /// </summary>
        internal bool TryGetNuGetVersionForSdk(string id, string version, SdkResolverContext context, out object parsedVersion)
        {
            if (!string.IsNullOrWhiteSpace(version))
            {
                // Use the version specified in the project if it is a NuGet compatible version
                return NuGetAbstraction.TryParseNuGetVersion(version, out parsedVersion);
            }

            parsedVersion = null;

            // Don't try to find versions defined in global.json if the project full path isn't set because an in-memory project is being evaluated and there's no
            // way to be sure where to look
            if (string.IsNullOrWhiteSpace(context?.ProjectFilePath))
            {
                return false;
            }

            Dictionary<string, string> msbuildSdkVersions = _globalJsonReader.GetMSBuildSdkVersions(context);

            // Check if global.json specified a version for this SDK and make sure its a version compatible with NuGet
            if (msbuildSdkVersions != null && msbuildSdkVersions.TryGetValue(id, out var globalJsonVersion) &&
                !string.IsNullOrWhiteSpace(globalJsonVersion))
            {
                return NuGetAbstraction.TryParseNuGetVersion(globalJsonVersion, out parsedVersion);
            }

            return false;
        }

        /// <summary>
        /// IMPORTANT: This class is used to ensure that <see cref="NuGetSdkResolver"/> does not consume any NuGet classes directly.  This ensures that no NuGet assemblies
        /// are loaded unless they are needed.  Do not implement anything in <see cref="NuGetSdkResolver"/> that uses a NuGet class and instead place it here.
        /// </summary>
        private static class NuGetAbstraction
        {
            public static SdkResult GetSdkResult(SdkReference sdk, object nuGetVersion, SdkResolverContext context, SdkResultFactory factory)
            {
                var logger = new NuGetSdkLogger(context.Logger);

                // Cast the NuGet version since the caller does not want to consume NuGet classes directly
                var parsedSdkVersion = (NuGetVersion)nuGetVersion;

                if (NuGetEventSource.IsEnabled) TraceEvents.GetResultStart(sdk.Name, parsedSdkVersion.OriginalVersion);

                SdkResult result = null;

                try
                {
                    if (NuGetEventSource.IsEnabled) TraceEvents.LoadSettingsStart();

                    // Load NuGet settings and a path resolver
                    ISettings settings;
                    try
                    {
                        settings = Settings.LoadDefaultSettings(context.ProjectFilePath, configFileName: null, MachineWideSettings.Value as IMachineWideSettings, SettingsLoadContext.Value as SettingsLoadingContext);
                    }
                    catch (Exception e)
                    {
                        logger.LogError(string.Format(CultureInfo.CurrentCulture, Strings.Error_FailedToReadSettings, e.Message));

                        result = factory.IndicateFailure(logger.Errors, logger.Warnings);

                        return result;
                    }
                    finally
                    {
                        if (NuGetEventSource.IsEnabled) TraceEvents.LoadSettingsStop();
                    }

                    var fallbackPackagePathResolver = new FallbackPackagePathResolver(NuGetPathContext.Create(settings));

                    var libraryIdentity = new LibraryIdentity(sdk.Name, parsedSdkVersion, LibraryType.Package);

                    // Attempt to find a package if its already installed
                    if (!TryGetMSBuildSdkPackageInfo(fallbackPackagePathResolver, libraryIdentity, out var installedPath, out var installedVersion))
                    {
                        try
                        {
                            DefaultCredentialServiceUtility.SetupDefaultCredentialService(logger, nonInteractive: !context.Interactive);

#if !NETFRAMEWORK
                            X509TrustStore.InitializeForDotNetSdk(logger);
#endif

                            if (NuGetEventSource.IsEnabled) TraceEvents.RestorePackageStart(libraryIdentity);

                            // Asynchronously run the restore without a commit which find the package on configured feeds, download, and unzip it without generating any other files
                            // This must be run in its own task because legacy project system evaluates projects on the UI thread which can cause RunWithoutCommit() to deadlock
                            // https://developercommunity.visualstudio.com/content/problem/311379/solution-load-never-completes-when-project-contain.html
                            var restoreTask = Task.Run(() => RestoreRunnerEx.RunWithoutCommit(
                                libraryIdentity,
                                settings,
                                logger));

                            var results = restoreTask.Result;

                            if (NuGetEventSource.IsEnabled) TraceEvents.RestorePackageStop(libraryIdentity);

                            fallbackPackagePathResolver = new FallbackPackagePathResolver(NuGetPathContext.Create(settings));

                            // Look for a successful result, any errors are logged by NuGet
                            foreach (RestoreResult restoreResult in results.Select(i => i.Result).Where(i => i.Success))
                            {
                                // Find the information about the package that was installed.  In some cases, the version can be different than what was specified (like you specify 1.0 but get 1.0.0)
                                var installedPackage = restoreResult.GetAllInstalled().FirstOrDefault(i => i == libraryIdentity);

                                if (installedPackage != null)
                                {
                                    if (TryGetMSBuildSdkPackageInfo(fallbackPackagePathResolver, installedPackage, out installedPath, out installedVersion))
                                    {
                                        break;
                                    }

                                    // This should never happen because we were told the package was successfully installed.
                                    // If we can't find it, we probably did something wrong with the NuGet API
                                    logger.LogError(string.Format(CultureInfo.CurrentCulture, Strings.CouldNotFindInstalledPackage, sdk));
                                }
                                else
                                {
                                    // This should never happen because we were told the restore succeeded.
                                    // If we can't find the package from GetAllInstalled(), we probably did something wrong with the NuGet API
                                    logger.LogError(string.Format(CultureInfo.CurrentCulture, Strings.PackageWasNotInstalled, sdk, sdk.Name));
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            logger.LogError(e.ToString());
                        }
                        finally
                        {
                            // The CredentialService lifetime is for the duration of the process. We should not leave a potentially unavailable logger. 
                            DefaultCredentialServiceUtility.UpdateCredentialServiceDelegatingLogger(NullLogger.Instance);
                        }
                    }

                    if (logger.Errors.Count == 0)
                    {
                        result = factory.IndicateSuccess(path: installedPath, version: installedVersion, warnings: logger.Warnings);

                        return result;
                    }

                    result = factory.IndicateFailure(logger.Errors, logger.Warnings);

                    return result;
                }
                finally
                {
                    if (NuGetEventSource.IsEnabled) TraceEvents.GetResultStop(sdk.Name, parsedSdkVersion.OriginalVersion, result);
                }
            }

            /// <summary>
            /// Attempts to parse a string as a NuGetVersion and returns an object containing the instance which can be cast later.
            /// </summary>
            [MethodImpl(MethodImplOptions.NoInlining)]
            public static bool TryParseNuGetVersion(string version, out object parsed)
            {
                if (NuGetVersion.TryParse(version, out var nuGetVersion))
                {
                    parsed = nuGetVersion;

                    return true;
                }

                parsed = null;
                return false;
            }

            /// <summary>
            /// Attempts to find a NuGet package if it is already installed.
            /// </summary>
            private static bool TryGetMSBuildSdkPackageInfo(FallbackPackagePathResolver fallbackPackagePathResolver, LibraryIdentity libraryIdentity, out string installedPath, out string installedVersion)
            {
                // Find the package
                var packageInfo = fallbackPackagePathResolver.GetPackageInfo(libraryIdentity.Name, libraryIdentity.Version);

                if (packageInfo == null)
                {
                    installedPath = null;
                    installedVersion = null;
                    return false;
                }

                // Get the installed path and add the expected "Sdk" folder.  Windows file systems are not case sensitive
                installedPath = Path.Combine(packageInfo.PathResolver.GetInstallPath(packageInfo.Id, packageInfo.Version), "Sdk");


                if (!NuGet.Common.RuntimeEnvironmentHelper.IsWindows && !Directory.Exists(installedPath))
                {
                    // Fall back to lower case "sdk" folder in case the file system is case sensitive
                    installedPath = Path.Combine(packageInfo.PathResolver.GetInstallPath(packageInfo.Id, packageInfo.Version), "sdk");
                }

                installedVersion = packageInfo.Version.ToString();

                return true;
            }
        }

        private static class TraceEvents
        {
            private const string EventNameGetResult = "SdkResolver/GetResult";
            private const string EventNameLoadSettings = "SdkResolver/LoadSettings";
            private const string EventNameResolve = "SdkResolver/Resolve";
            private const string EventNameRestorePackage = "SdkResolver/RestorePackage";

            public static void GetResultStart(string id, string version)
            {
                var eventOptions = new EventSourceOptions
                {
                    ActivityOptions = EventActivityOptions.Detachable,
                    Keywords = NuGetEventSource.Keywords.Performance | NuGetEventSource.Keywords.SdkResolver,
                    Opcode = EventOpcode.Start
                };

                NuGetEventSource.Instance.Write(EventNameGetResult, eventOptions, new GetResultStartEventData(id, version));
            }

            public static void GetResultStop(string id, string version, SdkResult result)
            {
                var eventOptions = new EventSourceOptions
                {
                    ActivityOptions = EventActivityOptions.Detachable,
                    Keywords = NuGetEventSource.Keywords.Performance | NuGetEventSource.Keywords.SdkResolver,
                    Opcode = EventOpcode.Stop
                };

                NuGetEventSource.Instance.Write(EventNameGetResult, eventOptions, new GetResultStopEventData(id, version, InstallPath: result?.Path, Success: result == null ? false : result.Success));
            }

            public static void LoadSettingsStart()
            {
                var eventOptions = new EventSourceOptions
                {
                    ActivityOptions = EventActivityOptions.Detachable,
                    Keywords = NuGetEventSource.Keywords.Performance | NuGetEventSource.Keywords.SdkResolver,
                    Opcode = EventOpcode.Start
                };

                NuGetEventSource.Instance.Write(EventNameLoadSettings, eventOptions);
            }

            public static void LoadSettingsStop()
            {
                var eventOptions = new EventSourceOptions
                {
                    ActivityOptions = EventActivityOptions.Detachable,
                    Keywords = NuGetEventSource.Keywords.Performance | NuGetEventSource.Keywords.SdkResolver,
                    Opcode = EventOpcode.Stop
                };

                NuGetEventSource.Instance.Write(EventNameLoadSettings, eventOptions);
            }

            public static void ResolveStart(SdkReference sdkReference)
            {
                var eventOptions = new EventSourceOptions
                {
                    ActivityOptions = EventActivityOptions.Detachable,
                    Keywords = NuGetEventSource.Keywords.SdkResolver | NuGetEventSource.Keywords.Performance,
                    Opcode = EventOpcode.Start
                };

                NuGetEventSource.Instance.Write(EventNameResolve, eventOptions, new ResolveEventData(sdkReference.Name, sdkReference.Version));
            }

            public static void ResolveStop(SdkReference sdkReference)
            {
                var eventData = new EventSourceOptions
                {
                    ActivityOptions = EventActivityOptions.Detachable,
                    Keywords = NuGetEventSource.Keywords.SdkResolver | NuGetEventSource.Keywords.Performance,
                    Opcode = EventOpcode.Stop
                };

                NuGetEventSource.Instance.Write(EventNameResolve, eventData, new ResolveEventData(sdkReference.Name, sdkReference.Version));
            }

            public static void RestorePackageStart(LibraryIdentity libraryIdentity)
            {
                var eventOptions = new EventSourceOptions
                {
                    ActivityOptions = EventActivityOptions.Detachable,
                    Keywords = NuGetEventSource.Keywords.Performance | NuGetEventSource.Keywords.SdkResolver,
                    Opcode = EventOpcode.Start
                };

                NuGetEventSource.Instance.Write(EventNameRestorePackage, eventOptions, new RestorePackageEventData(libraryIdentity.Name, libraryIdentity.Version.OriginalVersion));
            }

            public static void RestorePackageStop(LibraryIdentity libraryIdentity)
            {
                var eventOptions = new EventSourceOptions
                {
                    ActivityOptions = EventActivityOptions.Detachable,
                    Keywords = NuGetEventSource.Keywords.Performance | NuGetEventSource.Keywords.SdkResolver,
                    Opcode = EventOpcode.Stop
                };

                NuGetEventSource.Instance.Write(EventNameRestorePackage, eventOptions, new RestorePackageEventData(libraryIdentity.Name, libraryIdentity.Version.OriginalVersion));
            }

            [EventData]
            private record struct GetResultStartEventData(string Id, string Version);

            [EventData]
            private record struct GetResultStopEventData(string Id, string Version, string InstallPath, bool Success);

            [EventData]
            private record struct ResolveEventData(string Name, string Version);

            [EventData]
            private record struct RestorePackageEventData(string Id, string Version);
        }
    }
}
