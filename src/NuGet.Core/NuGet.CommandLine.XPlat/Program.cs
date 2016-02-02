// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using Microsoft.Extensions.PlatformAbstractions;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.DependencyResolver;
using NuGet.Logging;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;
using NuGet.Repositories;

namespace NuGet.CommandLine.XPlat
{
    public class Program
    {
        private const string HelpOption = "-h|--help";
        private const string VerbosityOption = "-v|--verbosity <verbosity>";
        private static readonly int MaxDegreesOfConcurrency = Environment.ProcessorCount;

        public static ILogger Log { get; set; }

        public static int Main(string[] args)
        {
#if DEBUG
            if (args.Contains("--debug"))
            {
                args = args.Skip(1).ToArray();
                while (!Debugger.IsAttached)
                {

                }
                Debugger.Break();
            }
#endif

            var app = new CommandLineApplication();
            app.Name = "nuget3";
            app.FullName = Strings.App_FullName;
            app.HelpOption(HelpOption);
            app.VersionOption("--version", typeof(Program).GetTypeInfo().Assembly.GetName().Version.ToString());

            var verbosity = app.Option(VerbosityOption, Strings.Switch_Verbosity, CommandOptionType.SingleValue);

            SetConnectionLimit();

            SetUserAgent();

            app.Command("restore", restore =>
            {
                restore.Description = Strings.Restore_Description;
                restore.HelpOption(HelpOption);

                var sources = restore.Option(
                    "-s|--source <source>",
                    Strings.Restore_Switch_Source_Description,
                    CommandOptionType.MultipleValue);

                var packagesDirectory = restore.Option(
                    "--packages <packagesDirectory>",
                    Strings.Restore_Switch_Packages_Description,
                    CommandOptionType.SingleValue);

                var disableParallel = restore.Option(
                    "--disable-parallel",
                    Strings.Restore_Switch_DisableParallel_Description,
                    CommandOptionType.NoValue);

                var fallBack = restore.Option(
                    "-f|--fallbacksource <FEED>",
                    Strings.Restore_Switch_Fallback_Description,
                    CommandOptionType.MultipleValue);

                var runtime = restore.Option(
                    "--runtime <RID>",
                    Strings.Restore_Switch_Runtime_Description,
                    CommandOptionType.MultipleValue);

                verbosity = restore.Option(VerbosityOption,
                    Strings.Switch_Verbosity,
                    CommandOptionType.SingleValue);

                EnsureLog(verbosity);

                var argRoot = restore.Argument(
                    "[root]",
                    Strings.Restore_Arg_ProjectName_Description,
                    multipleValues: true);

                restore.OnExecute(async () =>
                {
                    // Ignore casing on windows
                    var comparer = RuntimeEnvironmentHelper.IsWindows ?
                        StringComparer.OrdinalIgnoreCase
                        : StringComparer.Ordinal;

                    var inputValues = new HashSet<string>(comparer);

                    if (argRoot.Values.Count < 1)
                    {
                        // Use the current directory if no path was given
                        var workingDir = Path.GetFullPath(".");

                        inputValues.UnionWith(GetProjectJsonFilesInDirectory(workingDir));
                    }
                    else
                    {
                        foreach (var inputPath in argRoot.Values)
                        {
                            var fullPath = Path.GetFullPath(inputPath);

                            // For directories find all children
                            if (Directory.Exists(inputPath))
                            {
                                inputValues.UnionWith(GetProjectJsonFilesInDirectory(fullPath));
                            }
                            else
                            {
                                // Add the input directly
                                inputValues.Add(fullPath);
                            }
                        }
                    }

                    // Run restores
                    var isParallel = !disableParallel.HasValue() && !RuntimeEnvironmentHelper.IsMono;
                    var maxTasks = isParallel ? MaxDegreesOfConcurrency : 1;

                    if (maxTasks < 1)
                    {
                        maxTasks = 1;
                    }

                    if (isParallel)
                    {
                        Log.LogVerbose(string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.Log_RunningParallelRestore,
                            maxTasks));
                    }
                    else
                    {
                        Log.LogVerbose(Strings.Log_RunningNonParallelRestore);
                    }

                    var localCaches = new Dictionary<string, NuGetv3LocalRepository>(StringComparer.Ordinal);
                    var remoteProviders = new Dictionary<PackageSource, IRemoteDependencyProvider>();

                    var restoreSummaries = new List<RestoreSummary>();
                    var restoreTasks = new List<Task<RestoreSummary>>(maxTasks);
                    var cacheContext = new SourceCacheContext();

                    foreach (var inputPath in inputValues)
                    {
                        // Global folder
                        // Load settings based on the current project path.
                        var projectDir = Path.GetDirectoryName(inputPath);
                        var settings = Settings.LoadDefaultSettings(projectDir,
                            configFileName: null,
                            machineWideSettings: null);

                        var globalFolderPath = string.Empty;
                        if (packagesDirectory.HasValue())
                        {
                            globalFolderPath = packagesDirectory.Value();
                        }
                        else
                        {
                            globalFolderPath = SettingsUtility.GetGlobalPackagesFolder(settings);
                        }

                        var sharedCache = new RestoreCommandSharedCache();

                        // Find the shared local cache for globalFolderPath
                        // The global folder may differ between projects
                        NuGetv3LocalRepository localCache;
                        if (!localCaches.TryGetValue(globalFolderPath, out localCache))
                        {
                            localCache = new NuGetv3LocalRepository(globalFolderPath);
                            localCaches.Add(globalFolderPath, localCache);
                        }

                        sharedCache.LocalCache = localCache;

                        var packageSources = GetSources(sources, fallBack, settings);

                        foreach (var source in packageSources)
                        {
                            IRemoteDependencyProvider provider;
                            if (!remoteProviders.TryGetValue(source.PackageSource, out provider))
                            {
                                provider = new SourceRepositoryDependencyProvider(source, Log, cacheContext);
                                remoteProviders.Add(source.PackageSource, provider);
                            }

                            sharedCache.RemoteProviders.Add(provider);
                        }

                        // Create the shared resources up front so that all projects use the same one
                        if (!localCaches.TryGetValue(globalFolderPath, out localCache))
                        {
                            localCache = new NuGetv3LocalRepository(globalFolderPath);
                            localCaches.Add(globalFolderPath, localCache);
                        }

                        // Throttle and wait for a task to finish if we have hit the limit
                        if (restoreTasks.Count == maxTasks)
                        {
                            var restoreSummary = await CompleteTaskAsync(restoreTasks);
                            restoreSummaries.Add(restoreSummary);
                        }

                        // Start a new restore
                        var task = Task.Run(async () => await ExecuteRestoreAsync(
                            packageSources,
                            packagesDirectory,
                            fallBack,
                            runtime,
                            sharedCache,
                            settings,
                            isParallel,
                            inputPath));

                        restoreTasks.Add(task);
                    }

                    // Wait for all restores to finish
                    while (restoreTasks.Count > 0)
                    {
                        var restoreSummary = await CompleteTaskAsync(restoreTasks);
                        restoreSummaries.Add(restoreSummary);
                    }

                    // Display the errors in the same order that they were produced, but grouped by project
                    if (restoreSummaries.Any())
                    {
                        foreach (var restoreSummary in restoreSummaries)
                        {
                            if (!restoreSummary.Errors.Any())
                            {
                                continue;
                            }

                            Log.LogSummary(string.Empty);
                            Log.LogSummary(string.Format(Strings.Log_ErrorSummary, restoreSummary.InputPath));
                            foreach (var error in restoreSummary.Errors)
                            {
                                Log.LogSummary($"    {error}");
                            }
                        }
                    }

                    // Return 0 if all restores were successful
                    return restoreSummaries.All(x => x.Success) ? 0 : 1;
                });
            });

            app.OnExecute(() =>
            {
                app.ShowHelp();
                return 0;
            });

            var exitCode = 0;

            try
            {
                exitCode = app.Execute(args);
            }
            catch (Exception e)
            {
                EnsureLog(verbosity);

                // Log the error
                Log.LogError(ExceptionUtilities.DisplayMessage(e));

                // Log the stack trace as verbose output.
                Log.LogVerbose(e.ToString());

                exitCode = 1;
            }

            // Limit the exit code range to 0-255 to support POSIX
            if (exitCode < 0 || exitCode > 255)
            {
                exitCode = 1;
            }

            return exitCode;
        }

        private static void SetUserAgent()
        {
            UserAgent.UserAgentString
                = UserAgent.CreateUserAgentString(
                    $"NuGet xplat");
        }

        /// <summary>
        /// Removes a task from the list and returns the restore summary.
        /// </summary>
        private static async Task<RestoreSummary> CompleteTaskAsync(List<Task<RestoreSummary>> restoreTasks)
        {
            var doneTask = await Task.WhenAny(restoreTasks);
            restoreTasks.Remove(doneTask);
            return await doneTask;
        }

        private static void EnsureLog(CommandOption verbosity)
        {
            // Set up logging.
            // For tests this will already be set.
            if (Log == null)
            {
                Log = new CommandOutputLogger(verbosity);
            }
        }

        private static void SetConnectionLimit()
        {
#if !DNXCORE50
            // Increase the maximum number of connections per server.
            if (!RuntimeEnvironmentHelper.IsMono)
            {
                ServicePointManager.DefaultConnectionLimit = 64;
            }
            else
            {
                // Keep mono limited to a single download to avoid issues.
                ServicePointManager.DefaultConnectionLimit = 1;
            }
#endif
        }

        private static IEnumerable<string> GetProjectJsonFilesInDirectory(string path)
        {
            return Directory.GetFiles(path, "project.json", SearchOption.AllDirectories);
        }

        private static async Task<RestoreSummary> ExecuteRestoreAsync
            (List<SourceRepository> sources,
            CommandOption packagesDirectory,
            CommandOption fallBack,
            CommandOption runtime,
            RestoreCommandSharedCache sharedCache,
            ISettings settings,
            bool isParallel,
            string inputPath)
        {
            // Figure out the project directory
            IEnumerable<string> externalProjects = null;

            PackageSpec project;
            var projectPath = Path.GetFullPath(inputPath);
            if (string.Equals(PackageSpec.PackageSpecFileName, Path.GetFileName(projectPath), StringComparison.OrdinalIgnoreCase))
            {
                Log.LogVerbose(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.Log_ReadingProject,
                    inputPath));

                projectPath = Path.GetDirectoryName(projectPath);
                project = JsonPackageSpecReader.GetPackageSpec(File.ReadAllText(inputPath), Path.GetFileName(projectPath), inputPath);
            }
            else if (MsBuildUtility.IsMsBuildBasedProject(projectPath))
            {
#if DNXCORE50
                                throw new NotSupportedException();
#else
                // TODO: This only finds the top level dependencies, the rest are found through folders?
                externalProjects = MsBuildUtility.GetProjectReferences(projectPath);

                var projectDirectory = Path.GetDirectoryName(Path.GetFullPath(projectPath));
                var packageSpecFile = Path.Combine(projectDirectory, PackageSpec.PackageSpecFileName);
                project = JsonPackageSpecReader.GetPackageSpec(File.ReadAllText(packageSpecFile), projectPath, inputPath);
                Log.LogVerbose(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.Log_ReadingProject, inputPath));
#endif
            }
            else
            {
                var file = Path.Combine(projectPath, PackageSpec.PackageSpecFileName);

                Log.LogVerbose(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.Log_ReadingProject,
                    file));

                project = JsonPackageSpecReader.GetPackageSpec(File.ReadAllText(file), Path.GetFileName(projectPath), file);
            }

            Log.LogVerbose(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.Log_LoadedProject,
                    project.Name, project.FilePath));

            // Resolve the root directory
            var rootDirectory = PackageSpecResolver.ResolveRootDirectory(projectPath);
            Log.LogVerbose(string.Format(
                CultureInfo.CurrentCulture,
                Strings.Log_FoundProjectRoot,
                rootDirectory));

            using (var request = new RestoreRequest(
                project,
                sources,
                sharedCache.LocalCache.RepositoryRoot))
            {
                // Resolve the packages directory
                Log.LogVerbose(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.Log_UsingPackagesDirectory,
                    request.PackagesDirectory));

                request.SharedCache = sharedCache;

                if (externalProjects != null)
                {
                    foreach (var externalReference in externalProjects)
                    {
                        var dirName = Path.GetDirectoryName(externalReference);
                        var specPath = Path.Combine(dirName, PackageSpec.PackageSpecFileName);

                        request.ExternalProjects.Add(
                            new ExternalProjectReference(
                                externalReference,
                                JsonPackageSpecReader.GetPackageSpec(externalReference, specPath),
                                msbuildProjectPath: null,
                                projectReferences: Enumerable.Empty<string>()));
                    }
                }

                // Runtime ids
                request.RequestedRuntimes.UnionWith(runtime.Values);

                var runtimeEnvironment = PlatformServices.Default.Runtime;

                var defaultRuntimes = RequestRuntimeUtility.GetDefaultRestoreRuntimes(
                    runtimeEnvironment.OperatingSystem,
                    runtimeEnvironment.GetRuntimeOsName());

                request.FallbackRuntimes.UnionWith(defaultRuntimes);

                request.MaxDegreeOfConcurrency = isParallel ? RestoreRequest.DefaultDegreeOfConcurrency : 1;

                // Run the restore
                var collectorLog = new CollectorLogger(Log);
                var command = new RestoreCommand(collectorLog, request);
                var sw = Stopwatch.StartNew();
                var result = await command.ExecuteAsync();

                // Commit the result
                Log.LogMinimal(Strings.Log_Committing);
                result.Commit(collectorLog);

                sw.Stop();

                if (result.Success)
                {
                    Log.LogMinimal(string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.Log_RestoreComplete,
                        sw.ElapsedMilliseconds));
                }
                else
                {
                    Log.LogMinimal(string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.Log_RestoreFailed,
                        sw.ElapsedMilliseconds));
                }

                return new RestoreSummary(
                    inputPath,
                    result.Success,
                    collectorLog.Errors);
            }
        }

        /// <summary>
        /// Returns a unique list of sources. New sources will be cached
        /// and shared between restores.
        /// </summary>
        private static List<SourceRepository> GetSources(
            CommandOption sources,
            CommandOption fallBack,
            ISettings settings)
        {

            // CommandLineSourceRepositoryProvider caches repositories to avoid duplicates
            var packageSourceProvider = new PackageSourceProvider(settings);

            // Take the passed in sources
            var packageSources = sources.Values.Select(s => new PackageSource(s));

            // If no sources were passed in use the NuGet.Config sources
            if (!packageSources.Any())
            {
                // Add enabled sources
                packageSources = packageSourceProvider.LoadPackageSources().Where(source => source.IsEnabled);
            }

            packageSources = packageSources.Concat(
                fallBack.Values.Select(s => new PackageSource(s)));

            return packageSources.Select(source => _sourceProvider.CreateRepository(source))
                .Distinct()
                .ToList();
        }

        // Create a caching source provider with the default settings, the sources will be passed in
        private static CachingSourceProvider _sourceProvider = new CachingSourceProvider(
            new PackageSourceProvider(
                Settings.LoadDefaultSettings(root: null, configFileName: null, machineWideSettings: null)));
    }
}
