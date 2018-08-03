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
                    "-f|--framework",
                    Strings.ListPkg_FrameworkDescription,
                    CommandOptionType.SingleValue);

                var outdated = listpkg.Option(
                    "-o|--outdated",
                    Strings.ListPkg_OutdatedDescription,
                    CommandOptionType.NoValue);

                var deprecated = listpkg.Option(
                    "-d|--deprecated",
                    Strings.ListPkg_DeprecatedDescription,
                    CommandOptionType.NoValue);

                var transitive = listpkg.Option(
                    "-t|--include-transitive",
                    Strings.ListPkg_TransitiveDescription,
                    CommandOptionType.NoValue);

                listpkg.OnExecute(async () =>
                {
                    var logger = getLogger();
                    var frameworks = ParseFrameworks(framework.Value());

                    var settings = XPlatUtility.CreateDefaultSettings();

                    var packageSources = GetPackageSources(settings, new List<string>());
                    var sourceProvider = new PackageSourceProvider(settings);


                    var packageRefArgs = new ListPackageArgs(
                        logger,
                        path.Value,
                        sourceProvider,
                        framework.HasValue(),
                        frameworks,
                        outdated.HasValue(),
                        deprecated.HasValue(),
                        transitive.HasValue(),
                        CancellationToken.None
                    );

                    var msBuild = new MSBuildAPIUtility(logger);
                    var listPackageCommandRunner = getCommandRunner();
                    await listPackageCommandRunner.ExecuteCommandAsync(packageRefArgs, msBuild);
                    return 0;
                });
            });
        }

        private static List<PackageSource> GetPackageSources(ISettings settings, List<string> sources)
        {
            var sourceProvider = new PackageSourceProvider(settings);
            var availableSources = sourceProvider.LoadPackageSources().Where(source => source.IsEnabled).ToList();
            var packageSources = new List<Configuration.PackageSource>();
            foreach (var source in sources)
            {
                packageSources.Add(PackageSourceProviderExtensions.ResolveSource(availableSources, source));
            }

            if (packageSources.Count == 0)
            {
                packageSources.AddRange(availableSources);
            }

            return packageSources;
        }

        private static List<string> ParseFrameworks(string frameworks)
        {
            if (frameworks == null)
            {
                return new List<string>();
            }
            var frameworksList = frameworks.Split(',').ToList();
            return frameworksList;
        }

    }
}