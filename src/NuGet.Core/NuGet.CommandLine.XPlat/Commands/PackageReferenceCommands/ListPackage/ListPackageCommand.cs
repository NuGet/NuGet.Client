// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Credentials;
using NuGet.Frameworks;

namespace NuGet.CommandLine.XPlat
{
    internal static class ListPackageCommand
    {
        public static void Register(
            CommandLineApplication app,
            Func<ILogger> getLogger,
            Action<LogLevel> setLogLevel,
            Func<IListPackageCommandRunner> getCommandRunner)
        {
            app.Command("list", listpkg =>
            {
                listpkg.Description = Strings.ListPkg_Description;
                listpkg.HelpOption(XPlatUtility.HelpOption);

                listpkg.Option(
                    CommandConstants.ForceEnglishOutputOption,
                    Strings.ForceEnglishOutput_Description,
                    CommandOptionType.NoValue);

                var path = listpkg.Argument(
                    "<PROJECT | SOLUTION>",
                    Strings.ListPkg_PathDescription,
                    multipleValues: false);

                var framework = listpkg.Option(
                    "--framework",
                    Strings.ListPkg_FrameworkDescription,
                    CommandOptionType.MultipleValue);

                var outdatedReport = listpkg.Option(
                    "--outdated",
                    Strings.ListPkg_OutdatedDescription,
                    CommandOptionType.NoValue);

                var deprecatedReport = listpkg.Option(
                    "--deprecated",
                    Strings.ListPkg_DeprecatedDescription,
                    CommandOptionType.NoValue);

                var vulnerableReport = listpkg.Option(
                    "--vulnerable",
                    Strings.ListPkg_VulnerableDescription,
                    CommandOptionType.NoValue);

                var isOffline = listpkg.Option(
                    "--offline",
                    Strings.ListPkg_OfflineDescription,
                    CommandOptionType.NoValue);

                var includeTransitive = listpkg.Option(
                    "--include-transitive",
                    Strings.ListPkg_TransitiveDescription,
                    CommandOptionType.NoValue);

                var prerelease = listpkg.Option(
                    "--include-prerelease",
                    Strings.ListPkg_PrereleaseDescription,
                    CommandOptionType.NoValue);

                var highestPatch = listpkg.Option(
                    "--highest-patch",
                    Strings.ListPkg_HighestPatchDescription,
                    CommandOptionType.NoValue);

                var highestMinor = listpkg.Option(
                    "--highest-minor",
                    Strings.ListPkg_HighestMinorDescription,
                    CommandOptionType.NoValue);

                var source = listpkg.Option(
                    "--source",
                    Strings.ListPkg_SourceDescription,
                    CommandOptionType.MultipleValue);

                var config = listpkg.Option(
                    "--config",
                    Strings.ListPkg_ConfigDescription,
                    CommandOptionType.SingleValue);

                var interactive = listpkg.Option(
                    "--interactive",
                    Strings.NuGetXplatCommand_Interactive,
                    CommandOptionType.NoValue);

                var verbosity = listpkg.Option(
                    "-v|--verbosity",
                    Strings.Verbosity_Description,
                    CommandOptionType.SingleValue);

                listpkg.OnExecute(async () =>
                {
                    var logger = getLogger();

                    setLogLevel(XPlatUtility.MSBuildVerbosityToNuGetLogLevel(verbosity.Value()));

                    var settings = ProcessConfigFile(config.Value(), path.Value);
                    var sources = source.Values;

                    var packageSources = GetPackageSources(settings, sources, config);

                    VerifyValidFrameworks(framework);

                    var packageRefArgs = new ListPackageArgs(
                        path.Value,
                        packageSources,
                        framework.Values,
                        outdatedReport.HasValue(),
                        deprecatedReport.HasValue(),
                        vulnerableReport.HasValue(),
                        isOffline.HasValue(),
                        includeTransitive.HasValue(),
                        prerelease.HasValue(),
                        highestPatch.HasValue(),
                        highestMinor.HasValue(),
                        logger,
                        CancellationToken.None);

                    ProcessOptionRules(packageRefArgs);

                    DefaultCredentialServiceUtility.SetupDefaultCredentialService(getLogger(), !interactive.HasValue());

                    var listPackageCommandRunner = getCommandRunner();
                    await listPackageCommandRunner.ExecuteCommandAsync(packageRefArgs);
                    return 0;
                });
            });
        }

        private static void ProcessOptionRules(ListPackageArgs packageRefArgs)
        {
            // Critical rule breaks
            var firstOption = string.Empty;
            var incompatibleOption = string.Empty;
            if (packageRefArgs.DeprecatedReport)
            {
                firstOption = "--deprecated";
                incompatibleOption = packageRefArgs.OutdatedReport ? "--outdated" : incompatibleOption;
                incompatibleOption = packageRefArgs.VulnerableReport ? "--vulnerable" : incompatibleOption;
                incompatibleOption = packageRefArgs.IsOffline ? "--offline" : incompatibleOption;
            }
            else if (packageRefArgs.VulnerableReport)
            {
                firstOption = "--vulnerable";
                incompatibleOption = packageRefArgs.OutdatedReport ? "--outdated" : incompatibleOption;
                incompatibleOption = packageRefArgs.IsOffline ? "--offline" : incompatibleOption;
            }
            else if (packageRefArgs.OutdatedReport)
            {
                firstOption = "--outdated";
                incompatibleOption = packageRefArgs.IsOffline ? "--offline" : incompatibleOption;
            }

            if (incompatibleOption != string.Empty)
            {
                throw new ArgumentException(string.Format(Strings.ListPkg_InvalidOptions, firstOption, incompatibleOption));
            }

            // Informationals/adjustments
            if (packageRefArgs.VulnerableReport &&
                (packageRefArgs.Prerelease || packageRefArgs.HighestMinor || packageRefArgs.HighestPatch))
            {
                Console.WriteLine(Strings.ListPkg_VulnerableIgnoredOptions);
                packageRefArgs.Prerelease = false;
                packageRefArgs.HighestPatch = false;
                packageRefArgs.HighestMinor = false;
            }
        }

        private static void VerifyValidFrameworks(CommandOption framework)
        {
            var frameworks = framework.Values.Select(f =>
                                NuGetFramework.Parse(f.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray()[0]));
            if (frameworks.Any(f => f.Framework.Equals("Unsupported", StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException(Strings.ListPkg_InvalidFramework, nameof(framework));
            }
        }

        private static ISettings ProcessConfigFile(string configFile, string projectOrSolution)
        {
            if (string.IsNullOrEmpty(configFile))
            {
                return Settings.LoadDefaultSettings(projectOrSolution);
            }

            var configFileFullPath = Path.GetFullPath(configFile);
            var directory = Path.GetDirectoryName(configFileFullPath);
            var configFileName = Path.GetFileName(configFileFullPath);
            return Settings.LoadDefaultSettings(
                directory,
                configFileName,
                machineWideSettings: new XPlatMachineWideSetting());

        }

        private static IEnumerable<PackageSource> GetPackageSources(ISettings settings, IEnumerable<string> sources, CommandOption config)
        {
            var availableSources = PackageSourceProvider.LoadPackageSources(settings).Where(source => source.IsEnabled);
            var uniqueSources = new HashSet<string>();

            var packageSources = new List<PackageSource>();
            foreach (var source in sources)
            {
                if (!uniqueSources.Contains(source))
                {
                    uniqueSources.Add(source);
                    packageSources.Add(PackageSourceProviderExtensions.ResolveSource(availableSources, source));
                }
            }

            if (packageSources.Count == 0 || config.HasValue())
            {
                packageSources.AddRange(availableSources);
            }

            return packageSources;
        }
    }
}
