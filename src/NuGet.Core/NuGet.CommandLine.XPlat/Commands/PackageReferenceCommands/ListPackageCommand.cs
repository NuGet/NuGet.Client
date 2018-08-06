// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.CommandLine.XPlat
{
    public static class ListPackageCommand
    {
        public static void Register(CommandLineApplication app, Func<ILogger> getLogger,
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
                    CommandOptionType.SingleValue);

                var outdated = listpkg.Option(
                    "--outdated",
                    Strings.ListPkg_OutdatedDescription,
                    CommandOptionType.NoValue);

                var deprecated = listpkg.Option(
                    "--deprecated",
                    Strings.ListPkg_DeprecatedDescription,
                    CommandOptionType.NoValue);

                var transitive = listpkg.Option(
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
                    CommandOptionType.SingleValue);

                var config = listpkg.Option(
                    "--config",
                    Strings.ListPkg_ConfigDescription,
                    CommandOptionType.SingleValue);

                listpkg.OnExecute(async () =>
                {
                    var logger = getLogger();

                    var settings = ProcessConfigFile(config.Value(), path.Value);
                    var sources = ParseArgumentWithCommas(source.Value());
                    
                    var packageSources = GetPackageSources(settings, sources, config);

                    var frameworks = ParseArgumentWithCommas(framework.Value());
                    
                    var packageRefArgs = new ListPackageArgs(
                        logger,
                        path.Value,
                        packageSources,
                        framework.HasValue(),
                        frameworks,
                        outdated.HasValue(),
                        deprecated.HasValue(),
                        transitive.HasValue(),
                        prerelease.HasValue(),
                        highestPatch.HasValue(),
                        highestMinor.HasValue(),
                        CancellationToken.None
                    );

                    var msBuild = new MSBuildAPIUtility(logger);
                    var listPackageCommandRunner = getCommandRunner();
                    await listPackageCommandRunner.ExecuteCommandAsync(packageRefArgs, msBuild);
                    return 0;
                });
            });
        }

        private static ISettings ProcessConfigFile(string configFile, string currentDirectory)
        {
            if (string.IsNullOrEmpty(configFile))
            {
                return Settings.LoadDefaultSettings(currentDirectory);
            }
            else
            {
                var configFileFullPath = Path.GetFullPath(configFile);
                var directory = Path.GetDirectoryName(configFileFullPath);
                var configFileName = Path.GetFileName(configFileFullPath);
                return Settings.LoadDefaultSettings(
                    directory,
                    configFileName,
                    machineWideSettings: new XPlatMachineWideSetting());
            }

        }

        private static List<PackageSource> GetPackageSources(ISettings settings, List<string> sources, CommandOption config)
        {
            
            var sourceProvider = new PackageSourceProvider(settings);
            var availableSources = sourceProvider.LoadPackageSources().Where(source => source.IsEnabled).ToList();
            var packageSources = new List<Configuration.PackageSource>();
            foreach (var source in sources)
            {
                packageSources.Add(PackageSourceProviderExtensions.ResolveSource(availableSources, source));
            }

            if (packageSources.Count == 0 || config.HasValue())
            {
                packageSources.AddRange(availableSources);
            }

            return packageSources;
        }

        private static List<string> ParseArgumentWithCommas(string argument)
        {
            if (argument == null)
            {
                return new List<string>();
            }
            var argumentList = argument.Split(',').ToList();
            return argumentList;
        }

    }
}