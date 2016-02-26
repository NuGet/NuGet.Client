using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using NuGet.Commands;
using NuGet.Configuration;
using NuGet.Logging;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;

namespace NuGet.CommandLine.XPlat
{
    internal static class RestoreCommand
    {
        public static void Register(
            CommandLineApplication cmdApp,
            CommandOutputLogger log)
        {
            cmdApp.Command("restore", (Action<CommandLineApplication>)(restore =>
            {
                restore.Description = Strings.Restore_Description;
                restore.HelpOption(XPlatUtility.HelpOption);

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

                var configFile = restore.Option(
                    "--configfile <file>",
                    Strings.Restore_Switch_ConfigFile_Description,
                    CommandOptionType.SingleValue);

                var noCache = restore.Option(
                    "--no-cache",
                    Strings.Restore_Switch_NoCache_Description,
                    CommandOptionType.NoValue);

                var verbosity = restore.Option(
                    XPlatUtility.VerbosityOption,
                    Strings.Switch_Verbosity,
                    CommandOptionType.SingleValue);

                var argRoot = restore.Argument(
                    "[root]",
                    Strings.Restore_Arg_ProjectName_Description,
                    multipleValues: true);

                restore.OnExecute(async () =>
                {
                    var logLevel = XPlatUtility.GetLogLevel(verbosity);
                    log.SetLogLevel(logLevel);

                    using (var cacheContext = new SourceCacheContext())
                    {
                        cacheContext.NoCache = noCache.HasValue();
                        var providerCache = new RestoreCommandProvidersCache();

                        // Ordered request providers
                        var providers = new List<IRestoreRequestProvider>();
                        providers.Add(new MSBuildP2PRestoreRequestProvider(providerCache));
                        providers.Add(new ProjectJsonRestoreRequestProvider(providerCache));

                        var restoreContext = new RestoreArgs()
                        {
                            CacheContext = cacheContext,
                            ConfigFileName = configFile.HasValue() ? configFile.Value() : null,
                            DisableParallel = disableParallel.HasValue(),
                            GlobalPackagesFolder = packagesDirectory.HasValue() ? packagesDirectory.Value() : null,
                            Inputs = new List<string>(argRoot.Values),
                            Log = log,
                            MachineWideSettings = new CommandLineXPlatMachineWideSetting(),
                            RequestProviders = providers,
                            Sources = sources.Values,
                            FallbackSources = fallBack.Values,
                            CachingSourceProvider = _sourceProvider
                        };

                        restoreContext.Runtimes.UnionWith(runtime.Values);

                        var restoreSummaries = await RestoreRunner.Run(restoreContext);

                        // Summary
                        RestoreSummary.Log(log, restoreSummaries, logLevel < LogLevel.Minimal);

                        return restoreSummaries.All(x => x.Success) ? 0 : 1;
                    }
                });
            }));
        }

        // Create a caching source provider with the default settings, the sources will be passed in
        private static CachingSourceProvider _sourceProvider = new CachingSourceProvider(
            new PackageSourceProvider(
                Settings.LoadDefaultSettings(root: null, configFileName: null, machineWideSettings: null)));
    }
}
