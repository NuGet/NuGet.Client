// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Common;

namespace NuGet.CommandLine.XPlat
{
    public class RemovePackageReferenceCommand
    {
        public static void Register(CommandLineApplication app, Func<ILogger> getLogger,
            Func<IPackageReferenceCommandRunner> getCommandRunner)
        {
            app.Command("remove", removePkg =>
            {
                removePkg.Description = Strings.RemovePkg_Description;
                removePkg.HelpOption(XPlatUtility.HelpOption);

                removePkg.Option(
                    CommandConstants.ForceEnglishOutputOption,
                    Strings.ForceEnglishOutput_Description,
                    CommandOptionType.NoValue);

                var id = removePkg.Option(
                    "--package",
                    Strings.RemovePkg_PackageIdDescription,
                    CommandOptionType.SingleValue);

                var projectPath = removePkg.Option(
                    "-p|--project",
                    Strings.RemovePkg_ProjectPathDescription,
                    CommandOptionType.SingleValue);

                removePkg.OnExecute(() =>
                {
                    ValidateArgument(id, id.Template);
                    ValidateArgument(projectPath, projectPath.Template);
                    var logger = getLogger();
                    var packageRefArgs = new PackageReferenceArgs(projectPath.Value(), id.Value(), logger);
                    var msBuild = new MSBuildAPIUtility();
                    var removePackageRefCommandRunner = getCommandRunner();
                    return removePackageRefCommandRunner.ExecuteCommand(packageRefArgs, msBuild);
                });
            });
        }

        private static void ValidateArgument(CommandOption arg, string argName)
        {
            if (arg.Values.Count < 1)
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.RemovePkg_MissingArgument, argName));
            }
        }
    }
}