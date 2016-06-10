using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using NuGet.Commands;
using NuGet.Configuration;
using NuGet.ProjectModel;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine.XPlat
{
    internal static class RestoreCommand
    {
        public static void Register(
            CommandLineApplication cmdApp,
            Func<CommandOutputLogger> getLogger)
        {
            cmdApp.Command("restore", (Action<CommandLineApplication>)(restore =>
            {
                restore.Description = Strings.Restore_Description;
                restore.HelpOption(XPlatUtility.HelpOption);

                restore.Option(
                    CommandConstants.ForceEnglishOutputOption,
                    Strings.ForceEnglishOutput_Description,
                    CommandOptionType.NoValue);

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

                var configFile = restore.Option(
                    "--configfile <file>",
                    Strings.Restore_Switch_ConfigFile_Description,
                    CommandOptionType.SingleValue);

                var noCache = restore.Option(
                    "--no-cache",
                    Strings.Restore_Switch_NoCache_Description,
                    CommandOptionType.NoValue);

                var inferRuntimes = restore.Option(
                    "--infer-runtimes",
                    "Temporary option to allow NuGet to infer RIDs for legacy repositories",
                    CommandOptionType.NoValue);

                var verbosity = restore.Option(
                    XPlatUtility.VerbosityOption,
                    Strings.Switch_Verbosity,
                    CommandOptionType.SingleValue);

                var argRoot = restore.Argument(
                    "[root]",
                    Strings.Restore_Arg_ProjectName_Description,
                    multipleValues: true);

                var ignoreFailedSources = restore.Option(
                    "--ignore-failed-sources",
                    Strings.Restore_Switch_IgnoreFailedSource_Description,
                    CommandOptionType.NoValue);

                restore.OnExecute(async () =>
                {
                    var log = getLogger();
                    if (verbosity.HasValue())
                    {
                        log.LogLevel = XPlatUtility.GetLogLevel(verbosity);
                    }

                    using (var cacheContext = new SourceCacheContext())
                    {
                        cacheContext.NoCache = noCache.HasValue();
                        cacheContext.IgnoreFailedSources = ignoreFailedSources.HasValue();
                        var providerCache = new RestoreCommandProvidersCache();

                        // Ordered request providers
                        var providers = new List<IRestoreRequestProvider>();
                        providers.Add(new MSBuildP2PRestoreRequestProvider(providerCache));
                        providers.Add(new ProjectJsonRestoreRequestProvider(providerCache));

                        ISettings defaultSettings = Settings.LoadDefaultSettings(root: null, configFileName: null, machineWideSettings: null);
                        CachingSourceProvider sourceProvider = new CachingSourceProvider(new PackageSourceProvider(defaultSettings));

                        var restoreContext = new RestoreArgs()
                        {
                            CacheContext = cacheContext,
                            LockFileVersion = LockFileFormat.Version,
                            ConfigFile = configFile.HasValue() ? configFile.Value() : null,
                            DisableParallel = disableParallel.HasValue(),
                            GlobalPackagesFolder = packagesDirectory.HasValue() ? packagesDirectory.Value() : null,
                            Inputs = new List<string>(argRoot.Values),
                            Log = log,
                            MachineWideSettings = new XPlatMachineWideSetting(),
                            RequestProviders = providers,
                            Sources = sources.Values,
                            FallbackSources = fallBack.Values,
                            CachingSourceProvider = sourceProvider
                        };

                        if (inferRuntimes.HasValue())
                        {
                            var runtimeOSname = PlatformApis.GetRuntimeOsName();
                            var os = PlatformApis.GetOSName();
                            var defaultRuntimes = RequestRuntimeUtility.GetDefaultRestoreRuntimes(os, runtimeOSname);
                            restoreContext.FallbackRuntimes.UnionWith(defaultRuntimes);
                        }

                        var restoreSummaries = await RestoreRunner.Run(restoreContext);

                        // Summary
                        RestoreSummary.Log(log, restoreSummaries);

                        return restoreSummaries.All(x => x.Success) ? 0 : 1;
                    }
                });
            }));
        }
    }
}
