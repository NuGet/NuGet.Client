// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
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

                var framework = listpkg.Option(
                    "-f|--framework",
                    Strings.ListPkg_FrameworkDescription,
                    CommandOptionType.SingleValue);

                var outdated = listpkg.Option(
                    "-o|--outdated",
                    Strings.ListPkg_OutdatedDescription,
                    CommandOptionType.SingleValue);

                var deprecated = listpkg.Option(
                    "-d|--deprecated",
                    Strings.ListPkg_DeprecatedDescription,
                    CommandOptionType.SingleValue);

                var transitive = listpkg.Option(
                    "-t|--transitive",
                    Strings.ListPkg_TransitiveDescription,
                    CommandOptionType.SingleValue);

                listpkg.OnExecute(() =>
                {
                    Debugger.Launch();
                    if (framework.HasValue())
                    {
                        if (framework.Values.Count != 1)
                        {
                            //throw an err
                            return null;
                        }
                    }
                    var logger = getLogger();
                    var packageRefArgs = new ListPackageArgs(logger, framework.Values[0], outdated.HasValue(), deprecated.HasValue(), transitive.HasValue());

                    var msBuild = new MSBuildAPIUtility(logger);
                    var listPackageCommandRunner = getCommandRunner();
                    return listPackageCommandRunner.ExecuteCommand(packageRefArgs, msBuild);
                });
            });
        }

    }
}