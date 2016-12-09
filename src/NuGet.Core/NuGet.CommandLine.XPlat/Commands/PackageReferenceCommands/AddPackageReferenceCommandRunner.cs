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
using NuGet.ProjectModel;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine.XPlat
{
    public class AddPackageReferenceCommandRunner
    {
        private static string NUGET_RESTORE_MSBUILD_VERBOSITY = "NUGET_RESTORE_MSBUILD_VERBOSITY";

        public void ExecuteCommand(PackageReferenceArgs packageReferenceArgs)
        {
            using (var dgFilePath = new TempFile(".dg"))
            {
                // 1. Get project dg file
                var dgSpec = GetProjectDependencyGraphAsync(packageReferenceArgs, dgFilePath, timeOut: 5000, recursive: true).Result;
                var projectName = dgSpec.Restore.FirstOrDefault();
                var originalPackageSpec = dgSpec.GetProjectSpec(projectName);

                // 2. Run Restore Preview
                var restorePreviewResult = PreviewAddPackageReference(packageReferenceArgs, dgSpec, originalPackageSpec).Result;

                // 3. Process Restore Result

                var projectFrameworks = originalPackageSpec
                    .TargetFrameworks
                    .Select(t => t.FrameworkName)
                    .Distinct()
                    .ToList();

                var unsuccessfulFrameworks = restorePreviewResult
                    .Result
                    .CompatibilityCheckResults
                    .Where(t => !t.Success)
                    .Select(t => t.Graph.Framework)
                    .Distinct()
                    .ToList();

                if (unsuccessfulFrameworks.Count == projectFrameworks.Count)
                {
                    // Package is compatible with none of the project TFMs
                    // Do not add a package reference, throw appropriate error
                }
                else if (unsuccessfulFrameworks.Count == 0)
                {
                    // Package is compatible with all the project TFMs
                    // Add an unconditional package reference to the project
                    var project = MSBuildUtility.GetProject(packageReferenceArgs.ProjectPath);
                    MSBuildUtility.AddPackageReferenceAllTFMs(project, packageReferenceArgs.PackageIdentity);
                }
                else
                {
                    // Package is compatible with some of the project TFMs
                    // Add conditional package references to the project for the compatible TFMs
                    var successfulFrameworks = projectFrameworks
                        .Except(unsuccessfulFrameworks)
                        .Select(fx => fx.Framework)
                        .ToList();
                    var project = MSBuildUtility.GetProject(packageReferenceArgs.ProjectPath);
                }

                // 4. Write to Project
            }
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
            int timeOut,
            bool recursive)
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

            // pass dg file output path
            argumentBuilder.Append(" /p:RestoreGraphOutputPath=");
            AppendQuoted(argumentBuilder, dgFilePath);

            // Add all depenencies as top level restore projects if recursive is set
            if (recursive)
            {
                argumentBuilder.Append($" /p:RestoreRecursive=true ");
            }

            packageReferenceArgs.Logger.LogInformation($"{dotnetLocation} msbuild {argumentBuilder.ToString()}");

            var processStartInfo = new ProcessStartInfo
            {
                UseShellExecute = false,
                FileName = dotnetLocation,
                // WorkingDirectory = Path.GetDirectoryName(packageReferenceArgs.ProjectPath),
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