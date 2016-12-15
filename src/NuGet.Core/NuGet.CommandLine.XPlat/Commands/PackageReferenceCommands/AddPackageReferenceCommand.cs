// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
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

                var sources = addPkgRef.Option(
                    "--sources|-s",
                    "Specifies a NuGet package source to use during the restore.",
                    CommandOptionType.SingleValue);

                var packageDirectory = addPkgRef.Option(
                    "--package-directory",
                    "Directory to install packages in.",
                    CommandOptionType.SingleValue);

                addPkgRef.OnExecute(() =>
                {
                    ValidateArgument(id, "ID not given");
                    ValidateArgument(version, "Version not given");
                    ValidateArgument(dotnetPath, "Dotnet Path not given");
                    ValidateArgument(projectPath, "Project Path not given");

                    var logger = getLogger();
                    var packageDependency = new PackageDependency(id.Values[0], VersionRange.Parse(version.Value()));
                    var packageRefArgs = new PackageReferenceArgs(dotnetPath.Value(), projectPath.Value(), packageDependency, logger)
                    {
                        Frameworks = StringUtility.Split(frameworks.Value()),
                        Sources = StringUtility.Split(sources.Value()),
                        PackageDirectory = packageDirectory.Value(),
                        NoRestore = noRestore.HasValue()
                    };
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