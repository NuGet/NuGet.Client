// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Common;
using NuGet.Frameworks;

namespace NuGet.CommandLine.XPlat
{
    internal class WhyCommand
    {
        public static void Register(CommandLineApplication app, Func<ILogger> getLogger, Func<IWhyCommandRunner> getCommandRunner)
        {
            app.Command("why", why =>
            {
                why.Description = Strings.WhyCommand_Description;
                why.HelpOption(XPlatUtility.HelpOption);

                CommandArgument path = why.Argument(
                    "<PROJECT | SOLUTION>",
                    Strings.WhyCommand_PathArgument_Description,
                    multipleValues: false);

                CommandArgument package = why.Argument(
                    "<PACKAGE_NAME>",
                    Strings.WhyCommand_PackageArgument_Description,
                    multipleValues: false);

                CommandOption frameworks = why.Option(
                    "--framework",
                    Strings.WhyCommand_FrameworkArgument_Description,
                    CommandOptionType.MultipleValue);

                why.OnExecute(() =>
                {
                    // TODO: Can path be empty?
                    ValidatePackageArgument(package);
                    ValidateFrameworksOption(frameworks);

                    var logger = getLogger();
                    var whyCommandArgs = new WhyCommandArgs(
                        path.Value,
                        package.Value,
                        frameworks.Values,
                        logger);

                    var whyCommandRunner = getCommandRunner();
                    whyCommandRunner.ExecuteCommandAsync(whyCommandArgs);
                    return 0;
                });
            });
        }

        private static void ValidatePackageArgument(CommandArgument package)
        {
            if (string.IsNullOrEmpty(package.Value))
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.WhyCommand_Error_ArgumentCannotBeEmpty, package.Name));
            }
        }

        private static void ValidateFrameworksOption(CommandOption framework)
        {
            var frameworks = framework.Values.Select(f =>
                                NuGetFramework.Parse(
                                    f.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                                     .Select(s => s.Trim())
                                     .ToArray()[0]));

            if (frameworks.Any(f => f.Framework.Equals("Unsupported", StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException(Strings.ListPkg_InvalidFramework, nameof(framework));
            }
        }
    }
}
