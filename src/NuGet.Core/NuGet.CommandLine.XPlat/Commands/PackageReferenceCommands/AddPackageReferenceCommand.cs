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
            app.Command("add", addpkg =>
            {
                addpkg.Description = Strings.AddPkg_Description;
                addpkg.HelpOption(XPlatUtility.HelpOption);

                addpkg.Option(
                    CommandConstants.ForceEnglishOutputOption,
                    Strings.ForceEnglishOutput_Description,
                    CommandOptionType.NoValue);

                var id = addpkg.Option(
                    "--package",
                    Strings.AddPkg_PackageIdDescription,
                    CommandOptionType.SingleValue);

                var version = addpkg.Option(
                    "--version",
                    Strings.AddPkg_PackageVersionDescription,
                    CommandOptionType.SingleValue);

                var dotnetPath = addpkg.Option(
                    "-d|--dotnet",
                    Strings.AddPkg_DotnetDescription,
                    CommandOptionType.SingleValue);

                var projectPath = addpkg.Option(
                    "-p|--project",
                    Strings.AddPkg_ProjectPathDescription,
                    CommandOptionType.SingleValue);

                var frameworks = addpkg.Option(
                    "-f|--frameworks",
                    Strings.AddPkg_FrameworksDescription,
                    CommandOptionType.SingleValue);

                var noRestore = addpkg.Option(
                    "-n|--no-restore",
                    Strings.AddPkg_NoRestoreDescription,
                    CommandOptionType.NoValue);

                var sources = addpkg.Option(
                    "-s|--sources",
                    Strings.AddPkg_SourcesDescription,
                    CommandOptionType.SingleValue);

                var packageDirectory = addpkg.Option(
                    "--package-directory",
                    Strings.AddPkg_PackageDirectoryDescription,
                    CommandOptionType.SingleValue);

                addpkg.OnExecute(() =>
                {
                    ValidateArgument(id);
                    ValidateArgument(version);
                    ValidateArgument(dotnetPath);
                    ValidateArgument(projectPath);

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

        private static void ValidateArgument(CommandOption arg)
        {
            if ((arg.Values.Count < 1) || string.IsNullOrWhiteSpace(arg.Values[0]))
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.AddPkg_MissingArgument, nameof(arg)));
            }
        }
    }
}