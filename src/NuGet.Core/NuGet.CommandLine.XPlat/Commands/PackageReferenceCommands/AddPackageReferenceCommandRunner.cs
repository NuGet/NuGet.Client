// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine.XPlat
{
    public class AddPackageReferenceCommandRunner
    {
        private static string NUGET_RESTORE_MSBUILD_VERBOSITY = "NUGET_RESTORE_MSBUILD_VERBOSITY";
        private static int MSBUILD_WAIT_TIME = 2 * 60 * 1000; // 2 minutes in milliseconds

        public int ExecuteCommand(PackageReferenceArgs packageReferenceArgs, MSBuildAPIUtility msBuild)
        {
            packageReferenceArgs.Logger.LogInformation(string.Format("Adding PackageReference for package : '{0}', into project : '{1}'", packageReferenceArgs.PackageIdentity.ToString(), packageReferenceArgs.ProjectPath));
            if (packageReferenceArgs.NoRestore)
            {
                packageReferenceArgs.Logger.LogWarning("--no-restore|-n flag was used. No compatibility check will be done and the package reference will be added unconditionally.");
                msBuild.AddPackageReference(packageReferenceArgs.ProjectPath, packageReferenceArgs.PackageIdentity);
                return 0;
            }

            using (var dgFilePath = new TempFile(".dg"))
            {
                // 1. Get project dg file
                packageReferenceArgs.Logger.LogInformation("Generating project Dependency Graph");
                var dgSpec = GetProjectDependencyGraphAsync(packageReferenceArgs, dgFilePath, timeOut: MSBUILD_WAIT_TIME).Result;
                packageReferenceArgs.Logger.LogInformation("Project Dependency Graph Generated");
                var projectName = dgSpec.Restore.FirstOrDefault();
                var originalPackageSpec = dgSpec.GetProjectSpec(projectName);

                // Create a copy to avoid modifying the original spec which may be shared.
                var updatedPackageSpec = originalPackageSpec.Clone();
                PackageSpecOperations.AddOrUpdateDependency(updatedPackageSpec, packageReferenceArgs.PackageIdentity);

                var updatedDgSpec = dgSpec.WithReplacedSpec(updatedPackageSpec).WithoutRestores();
                updatedDgSpec.AddRestore(updatedPackageSpec.RestoreMetadata.ProjectUniqueName);

                // 2. Run Restore Preview
                packageReferenceArgs.Logger.LogInformation("Running Restore preview");
                var restorePreviewResult = PreviewAddPackageReference(packageReferenceArgs, updatedDgSpec, updatedPackageSpec).Result;
                packageReferenceArgs.Logger.LogInformation("Restore Review completed");

                // 3. Process Restore Result
                var compatibleFrameworks = new HashSet<NuGetFramework>(
                    restorePreviewResult
                    .Result
                    .CompatibilityCheckResults
                    .Where(t => t.Success)
                    .Select(t => t.Graph.Framework));

                if (packageReferenceArgs.HasFrameworks)
                {
                    // If the user has specified frameworks then we intersect that with the compatible frameworks.
                    var userSpecifiedFrameworks = new HashSet<NuGetFramework>(
                        packageReferenceArgs
                        .Frameworks
                        .Select(f => NuGetFramework.Parse(f)));

                    compatibleFrameworks.IntersectWith(userSpecifiedFrameworks);
                }

                // 4. Write to Project
                if (compatibleFrameworks.Count == 0)
                {
                    // Package is compatible with none of the project TFMs
                    // Do not add a package reference, throw appropriate error
                    packageReferenceArgs.Logger.LogInformation("Package is incompatible with all project TFMs");
                }
                else if (compatibleFrameworks.Count == restorePreviewResult.Result.CompatibilityCheckResults.Count())
                {
                    // Package is compatible with all the project TFMs
                    // Add an unconditional package reference to the project
                    packageReferenceArgs.Logger.LogInformation("Package is compatible with all project TFMs");
                    packageReferenceArgs.Logger.LogInformation("Adding unconditional package reference");

                    msBuild.AddPackageReference(packageReferenceArgs.ProjectPath, packageReferenceArgs.PackageIdentity);
                }
                else
                {
                    // Package is compatible with some of the project TFMs
                    // Add conditional package references to the project for the compatible TFMs
                    var compatibleOriginalFrameworks = originalPackageSpec.RestoreMetadata
                        .OriginalTargetFrameworks
                        .Where(s => compatibleFrameworks.Contains(NuGetFramework.Parse(s)));
                    packageReferenceArgs.Logger.LogInformation("Package is compatible with a subset of project TFMs");

                    msBuild.AddPackageReferencePerTFM(packageReferenceArgs.ProjectPath, packageReferenceArgs.PackageIdentity, compatibleOriginalFrameworks);
                }
            }
            return 0;
        }

        private async Task<RestoreResultPair> PreviewAddPackageReference(PackageReferenceArgs packageReferenceArgs, DependencyGraphSpec dgSpec, PackageSpec originalPackageSpec)
        {
            if (packageReferenceArgs == null)
            {
                throw new ArgumentNullException(nameof(packageReferenceArgs));
            }

            // Set user agent and connection settings.
            XPlatUtility.ConfigureProtocol();

            var providerCache = new RestoreCommandProvidersCache();

            using (var cacheContext = new SourceCacheContext())
            {
                cacheContext.NoCache = false;
                cacheContext.IgnoreFailedSources = true;

                // Pre-loaded request provider containing the graph file
                var providers = new List<IPreLoadedRestoreRequestProvider>();
                var defaultSettings = Settings.LoadDefaultSettings(root: null, configFileName: null, machineWideSettings: null);
                var sourceProvider = new CachingSourceProvider(new PackageSourceProvider(defaultSettings));

                // Create a copy to avoid modifying the original spec which may be shared.
                var updatedPackageSpec = originalPackageSpec.Clone();

                PackageSpecOperations.AddOrUpdateDependency(updatedPackageSpec, packageReferenceArgs.PackageIdentity);

                providers.Add(new DependencyGraphSpecRequestProvider(providerCache, dgSpec));

                var restoreContext = new RestoreArgs()
                {
                    CacheContext = cacheContext,
                    LockFileVersion = LockFileFormat.Version,
                    DisableParallel = true,
                    Log = packageReferenceArgs.Logger,
                    MachineWideSettings = new XPlatMachineWideSetting(),
                    PreLoadedRequestProviders = providers,
                    CachingSourceProvider = sourceProvider
                };

                if (restoreContext.DisableParallel)
                {
                    HttpSourceResourceProvider.Throttle = SemaphoreSlimThrottle.CreateBinarySemaphore();
                }

                // Generate Restore Requests. There will always be 1 request here since we are restoring for 1 project.
                var restoreRequests = await RestoreRunner.GetRequests(restoreContext);

                // Run restore without commit. This will always return 1 Result pair since we are restoring for 1 request.
                var restoreResult = await RestoreRunner.RunWithoutCommit(restoreRequests, restoreContext);

                return restoreResult.Single();
            }
        }

        private static async Task<DependencyGraphSpec> GetProjectDependencyGraphAsync(
            PackageReferenceArgs packageReferenceArgs,
            string dgFilePath,
            int timeOut)
        {
            var dotnetLocation = packageReferenceArgs.DotnetPath;

            if (!File.Exists(dotnetLocation))
            {
                throw new Exception(
                    string.Format(CultureInfo.CurrentCulture, Strings.Error_DotnetNotFound));
            }
            var argumentBuilder = new StringBuilder($@" /t:GenerateRestoreGraphFile");

            // Set the msbuild verbosity level if specified
            var msbuildVerbosity = Environment.GetEnvironmentVariable(NUGET_RESTORE_MSBUILD_VERBOSITY);

            if (string.IsNullOrEmpty(msbuildVerbosity))
            {
                argumentBuilder.Append(" /v:q ");
            }
            else
            {
                argumentBuilder.Append($" /v:{msbuildVerbosity} ");
            }

            // Pass dg file output path
            argumentBuilder.Append(" /p:RestoreGraphOutputPath=");
            AppendQuoted(argumentBuilder, dgFilePath);

            packageReferenceArgs.Logger.LogInformation($"{dotnetLocation} msbuild {argumentBuilder.ToString()}");

            var processStartInfo = new ProcessStartInfo
            {
                UseShellExecute = false,
                FileName = dotnetLocation,
                WorkingDirectory = Path.GetDirectoryName(packageReferenceArgs.ProjectPath),
                Arguments = $"msbuild {argumentBuilder.ToString()}",
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            packageReferenceArgs.Logger.LogDebug($"{processStartInfo.FileName} {processStartInfo.Arguments}");

            using (var process = Process.Start(processStartInfo))
            {
                var outputs = new ConcurrentQueue<string>();
                var outputTask = ConsumeStreamReaderAsync(process.StandardOutput, outputs);
                var errorTask = ConsumeStreamReaderAsync(process.StandardError, outputs);

                var finished = process.WaitForExit(timeOut);
                if (!finished)
                {
                    try
                    {
                        process.Kill();
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(string.Format(CultureInfo.CurrentCulture, Strings.Error_CannotKillDotnetMsBuild) + " : " +
                            ex.Message,
                            ex);
                    }

                    throw new Exception(string.Format(CultureInfo.CurrentCulture, Strings.Error_DotnetMsBuildTimedOut));
                }

                if (process.ExitCode != 0)
                {
                    await errorTask;
                    await outputTask;
                    LogQueue(outputs, packageReferenceArgs.Logger);
                    throw new Exception(string.Format(CultureInfo.CurrentCulture, Strings.Error_GenerateDGSpecTaskFailed));
                }
            }

            DependencyGraphSpec spec = null;

            if (File.Exists(dgFilePath))
            {
                spec = DependencyGraphSpec.Load(dgFilePath);
                File.Delete(dgFilePath);
            }
            else
            {
                spec = new DependencyGraphSpec();
            }

            return spec;
        }

        private static void LogQueue(ConcurrentQueue<string> outputs, ILogger logger)
        {
            foreach (var line in outputs)
            {
                logger.LogError(line);
            }
        }

        private static void AppendQuoted(StringBuilder builder, string targetPath)
        {
            builder
                .Append('"')
                .Append(targetPath)
                .Append('"');
        }

        private static async Task ConsumeStreamReaderAsync(StreamReader reader, ConcurrentQueue<string> lines)
        {
            await Task.Yield();

            string line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                lines.Enqueue(line);
            }
        }

        private class TempFile : IDisposable
        {
            private readonly string _filePath;

            /// <summary>
            /// Constructor. It creates an empty temp file under the temp directory / NuGet, with
            /// extension <paramref name="extension"/>.
            /// </summary>
            /// <param name="extension">The extension of the temp file.</param>
            public TempFile(string extension)
            {
                if (string.IsNullOrEmpty(extension))
                {
                    throw new ArgumentNullException(nameof(extension));
                }

                var tempDirectory = Path.Combine(Path.GetTempPath(), "NuGet-Scratch");

                Directory.CreateDirectory(tempDirectory);

                _filePath = Path.Combine(tempDirectory, Path.GetRandomFileName() + extension);

                if (!File.Exists(_filePath))
                {
                    try
                    {
                        File.Create(_filePath).Dispose();
                        // file is created successfully.
                        return;
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Strings.Error_FailedToCreateRandomFile) + " : " +
                                ex.Message,
                                ex);
                    }
                }
            }

            public static implicit operator string(TempFile f)
            {
                return f._filePath;
            }

            public void Dispose()
            {
                if (File.Exists(_filePath))
                {
                    try
                    {
                        File.Delete(_filePath);
                    }
                    catch
                    {
                    }
                }
            }
        }
    }
}