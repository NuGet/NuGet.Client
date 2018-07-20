// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Common;
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

                listpkg.OnExecute(() =>
                {
                    var logger = getLogger();
                    var frameworks = ParseFrameworks(framework.Value());

                    var packageRefArgs = new ListPackageArgs(
                        logger,
                        path.Value,
                        framework.HasValue(),
                        frameworks,
                        outdated.HasValue(),
                        deprecated.HasValue(),
                        transitive.HasValue()
                    );

                    var msBuild = new MSBuildAPIUtility(logger);
                    var listPackageCommandRunner = getCommandRunner();
                    listPackageCommandRunner.ExecuteCommand(packageRefArgs, msBuild);
                    return 0;
                });
            });
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