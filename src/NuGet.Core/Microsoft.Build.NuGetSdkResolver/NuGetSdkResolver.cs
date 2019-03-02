// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using NuGet.Commands;
using NuGet.Configuration;
using NuGet.Credentials;
using NuGet.LibraryModel;
using NuGet.Packaging;
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
        private static readonly Lazy<bool> DisableNuGetSdkResolver = new Lazy<bool>(() =>
            Environment.GetEnvironmentVariable("MSBUILDDISABLENUGETSDKRESOLVER") == "1");

        /// <inheritdoc />
        public override string Name => nameof(NuGetSdkResolver);

        /// <inheritdoc />
        public override int Priority => 2500;

        /// <summary>Resolves the specified SDK reference from NuGet.</summary>
        /// <param name="sdkReference">A <see cref="T:Microsoft.Build.Framework.SdkReference" /> containing the referenced SDKs be resolved.</param>
        /// <param name="resolverContext">Context for resolving the SDK.</param>
        /// <param name="factory">Factory class to create an <see cref="T:Microsoft.Build.Framework.SdkResult" /></param>
        /// <returns>
        ///     An <see cref="T:Microsoft.Build.Framework.SdkResult" /> containing the resolved SDKs or associated error / reason
        ///     the SDK could not be resolved.  Return <code>null</code> if the resolver is not
        ///     applicable for a particular <see cref="T:Microsoft.Build.Framework.SdkReference" />.
        /// </returns>
        public override SdkResult Resolve(SdkReference sdkReference, SdkResolverContext resolverContext, SdkResultFactory factory)
        {
            // Escape hatch to disable this resolver
            if (DisableNuGetSdkResolver.Value)
            {
                return null;
            }

            // This resolver only works if the user specifies a version in a project or a global.json.
            // Ignore invalid versions, there may be another resolver that can handle the version specified
            if (!TryGetNuGetVersionForSdk(sdkReference.Name, sdkReference.Version, resolverContext, out var parsedSdkVersion))
            {
                return null;
            }

            return NuGetAbstraction.GetSdkResult(sdkReference, parsedSdkVersion, resolverContext, factory);
        }

        /// <summary>
        /// Attempts to determine what version of an SDK to resolve.  A project-specific version is used first and then a version specified in a global.json.
        /// This method should not consume any NuGet classes directly to avoid loading additional assemblies when they are not needed.  This method
        /// returns an object so that NuGetVersion is not consumed directly.
        /// </summary>
        internal static bool TryGetNuGetVersionForSdk(string id, string version, SdkResolverContext context,
            out object parsedVersion)
        {
            if (!string.IsNullOrWhiteSpace(version))
            {
                // Use the version specified in the project if it is a NuGet compatible version
                return NuGetAbstraction.TryParseNuGetVersion(version, out parsedVersion);
            }

            Dictionary<string, string> msbuildSdkVersions;

            // Get the SDK versions from a previous state, otherwise find and load global.json to get them

            if (context.State is Dictionary<string, string> dictionary)
            {
                msbuildSdkVersions = dictionary;
            }
            else
            {
                msbuildSdkVersions = GlobalJsonReader.GetMSBuildSdkVersions(context);

                // Save the SDK versions in case this resolver is called again for another SDK in the same build
                context.State = msbuildSdkVersions;
            }

            // Check if global.json specified a version for this SDK and make sure its a version compatible with NuGet
            if (msbuildSdkVersions != null && msbuildSdkVersions.TryGetValue(id, out var globalJsonVersion) &&
                !string.IsNullOrWhiteSpace(globalJsonVersion))
            {
                return NuGetAbstraction.TryParseNuGetVersion(globalJsonVersion, out parsedVersion);
            }

            parsedVersion = null;

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
                // Cast the NuGet version since the caller does not want to consume NuGet classes directly
                var parsedSdkVersion = (NuGetVersion) nuGetVersion;

                // Stores errors and warnings for the result
                ICollection<string> errors = new List<string>();
                ICollection<string> warnings = new List<string>();

                // Load NuGet settings and a path resolver
                var settings = Settings.LoadDefaultSettings(context.ProjectFilePath);

                var fallbackPackagePathResolver = new FallbackPackagePathResolver(NuGetPathContext.Create(settings));

                var libraryIdentity = new LibraryIdentity(sdk.Name, parsedSdkVersion, LibraryType.Package);

                // Attempt to find a package if its already installed
                if (!TryGetMSBuildSdkPackageInfo(fallbackPackagePathResolver, libraryIdentity, out var installedPath, out var installedVersion))
                {
                    try
                    {
                        var nugetSDKLogger = new NuGetSdkLogger(context.Logger, warnings, errors);
                        DefaultCredentialServiceUtility.SetupDefaultCredentialService(nugetSDKLogger, nonInteractive: context.IsNonInteractive());

                        // Asynchronously run the restore without a commit which find the package on configured feeds, download, and unzip it without generating any other files
                        // This must be run in its own task because legacy project system evaluates projects on the UI thread which can cause RunWithoutCommit() to deadlock
                        // https://developercommunity.visualstudio.com/content/problem/311379/solution-load-never-completes-when-project-contain.html
                        var restoreTask = Task.Run(() => RestoreRunnerEx.RunWithoutCommit(
                            libraryIdentity,
                            settings,
                            nugetSDKLogger));

                        var results = restoreTask.Result;

                        fallbackPackagePathResolver = new FallbackPackagePathResolver(NuGetPathContext.Create(settings));

                        // Look for a successful result, any errors are logged by NuGet
                        foreach (var result in results.Select(i => i.Result).Where(i => i.Success))
                        {
                            // Find the information about the package that was installed.  In some cases, the version can be different than what was specified (like you specify 1.0 but get 1.0.0)
                            var installedPackage = result.GetAllInstalled().FirstOrDefault(i => i == libraryIdentity);

                            if (installedPackage != null)
                            {
                                if (TryGetMSBuildSdkPackageInfo(fallbackPackagePathResolver, installedPackage, out installedPath, out installedVersion))
                                {
                                    break;
                                }

                                // This should never happen because we were told the package was successfully installed.
                                // If we can't find it, we probably did something wrong with the NuGet API
                                errors.Add(string.Format(CultureInfo.CurrentCulture, Strings.CouldNotFindInstalledPackage, sdk));
                            }
                            else
                            {
                                // This should never happen because we were told the restore succeeded.
                                // If we can't find the package from GetAllInstalled(), we probably did something wrong with the NuGet API
                                errors.Add(string.Format(CultureInfo.CurrentCulture, Strings.PackageWasNotInstalled, sdk, sdk.Name));
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        errors.Add(e.Message);
                    }
                }

                if (errors.Count == 0)
                {
                    return factory.IndicateSuccess(path: installedPath, version: installedVersion, warnings: warnings);
                }

                return factory.IndicateFailure(errors, warnings);
            }

            /// <summary>
            /// Attempts to parse a string as a NuGetVersion and returns an object containing the instance which can be cast later.
            /// </summary>
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
    }
}
