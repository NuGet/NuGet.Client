// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Common;

namespace NuGet.CommandLine.XPlat
{
    internal class WhyCommand
    {
        public static void Register(CommandLineApplication app, Func<ILogger> getLogger, Func<IWhyPackageCommandRunner> getCommandRunner)
        {
            app.Command("why", why =>
            {
                why.Description = Strings.Why_Description;
                why.HelpOption(XPlatUtility.HelpOption);

                CommandArgument path = why.Argument(
                    "<PROJECT | SOLUTION>",
                    Strings.Why_PathDescription,
                    multipleValues: false);

                CommandArgument package = why.Argument(
                    "<PACKAGE_NAME>",
                    Strings.WhyCommandPackageDescription,
                    multipleValues: false);

                CommandOption frameworks = why.Option(
                    "--framework",
                    Strings.WhyFrameworkDescription,
                    CommandOptionType.MultipleValue);

                why.OnExecute(async () =>
                {
                    ValidatePackage(package);

                    var logger = getLogger();
                    var WhyPackageArgs = new WhyPackageArgs(
                        path.Value,
                        package.Value,
                        frameworks.Values,
                        logger);

                    var WhyPackageCommandRunner = getCommandRunner();
                    await WhyPackageCommandRunner.ExecuteCommandAsync(WhyPackageArgs);
                    return 0;
                });
            });
        }

        private static void ValidatePackage(CommandArgument argument)
        {
            if (string.IsNullOrEmpty(argument.Value))
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.Error_PkgMissingArgument,
                    "Why",
                    argument.Name));
            }
        }
    }
}
