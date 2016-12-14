// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.CommandLine.XPlat
{
    public static class AddPackageReferenceCommand
    {
        private const string MSBuildExeName = "MSBuild.dll";

        public static void Register(CommandLineApplication app, Func<ILogger> getLogger, Func<IAddPackageReferenceCommandRunner> getCommandRunner)
        {
            app.Command("addpkg", addPkgRef =>
            {
                addPkgRef.Description = "dotnet add pkg <package id> <package version>";
                addPkgRef.HelpOption(XPlatUtility.HelpOption);

                addPkgRef.Option(
                    CommandConstants.ForceEnglishOutputOption,
                    Strings.ForceEnglishOutput_Description,
                    CommandOptionType.NoValue);

                var id = addPkgRef.Option(
                    "--package",
                    "ID of the package",
                    CommandOptionType.SingleValue);

                var version = addPkgRef.Option(
                    "--version",
                    "Version of the package",
                    CommandOptionType.SingleValue);

                var dotnetPath = addPkgRef.Option(
                    "--dotnet|-d",
                    "No Restore flag",
                    CommandOptionType.SingleValue);

                var projectPath = addPkgRef.Option(
                    "--project|-p",
                    "No Restore flag",
                    CommandOptionType.SingleValue);

                var frameworks = addPkgRef.Option(
                    "--frameworks|-f",
                    "Frameworks",
                    CommandOptionType.SingleValue);

                var noRestore = addPkgRef.Option(
                    "--no-restore|-n",
                    "No Restore flag",
                    CommandOptionType.NoValue);

                addPkgRef.OnExecute(() =>
                {
                    var logger = getLogger();
                    var settings = Settings.LoadDefaultSettings(root: null, configFileName: null, machineWideSettings: null);
                    ValidateArgument(id, "ID not given");
                    ValidateArgument(version, "Version not given");
                    ValidateArgument(dotnetPath, "Dotnet Path not given");
                    ValidateArgument(projectPath, "Project Path not given");
                    var packageIdentity = new PackageIdentity(id.Values[0], NuGetVersion.Parse(version.Values[0]));
                    PackageReferenceArgs packageRefArgs;
                    if (frameworks.HasValue())
                    {
                        packageRefArgs = new PackageReferenceArgs(dotnetPath.Value(), projectPath.Value(), packageIdentity, settings, logger, noRestore.HasValue(), frameworks.Value());
                    }
                    else
                    {
                        packageRefArgs = new PackageReferenceArgs(dotnetPath.Value(), projectPath.Value(), packageIdentity, settings, logger, noRestore.HasValue());
                    }
                    var msBuild = new MSBuildAPIUtility();
                    var addPackageRefCommandRunner = getCommandRunner();
                    return addPackageRefCommandRunner.ExecuteCommand(packageRefArgs, msBuild);
                });
            });
        }

        private static void ValidateArgument(CommandOption arg, string exceptionMessage)
        {
            if ((arg.Values.Count < 1) || string.IsNullOrWhiteSpace(arg.Values[0]))
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, exceptionMessage));
            }
        }
    }
}