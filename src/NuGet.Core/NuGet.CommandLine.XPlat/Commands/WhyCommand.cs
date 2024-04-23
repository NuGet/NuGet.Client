// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Common;
using NuGet.Frameworks;

namespace NuGet.CommandLine.XPlat
{
    internal static class WhyCommand
    {
        public static void Register(CommandLineApplication app, Func<ILogger> getLogger, Func<IWhyCommandRunner> getCommandRunner)
        {
            app.Command("why", why =>
            {
                why.Description = Strings.WhyCommand_Description;
                why.HelpOption(XPlatUtility.HelpOption);

                CommandArgument path = why.Argument(
                    "<PROJECT>|<SOLUTION>",
                    Strings.WhyCommand_PathArgument_Description,
                    multipleValues: false);

                CommandArgument package = why.Argument(
                    "<PACKAGE_NAME>",
                    Strings.WhyCommand_PackageArgument_Description,
                    multipleValues: false);

                CommandOption frameworks = why.Option(
                    "-f|--framework",
                    Strings.WhyCommand_FrameworkOption_Description,
                    CommandOptionType.MultipleValue);

                why.OnExecute(() =>
                {
                    ValidatePathArgument(path);
                    ValidatePackageArgument(package);
                    ValidateFrameworksOption(frameworks);

                    var whyCommandRunner = getCommandRunner();
                    var logger = getLogger();
                    var whyCommandArgs = new WhyCommandArgs(
                        path.Value,
                        package.Value,
                        frameworks.Values,
                        logger);

                    whyCommandRunner.ExecuteCommand(whyCommandArgs);

                    return 0;
                });
            });
        }

        private static void ValidatePathArgument(CommandArgument path)
        {
            if (string.IsNullOrEmpty(path.Value))
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.CurrentCulture,
                    Strings.WhyCommand_Error_ArgumentCannotBeEmpty,
                    path.Name));
            }

            if (!File.Exists(path.Value)
                || (!path.Value.EndsWith("proj", StringComparison.OrdinalIgnoreCase) && !path.Value.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.CurrentCulture,
                    Strings.WhyCommand_Error_PathIsMissingOrInvalid,
                    path.Value));
            }
        }

        private static void ValidatePackageArgument(CommandArgument package)
        {
            if (string.IsNullOrEmpty(package.Value))
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.CurrentCulture,
                    Strings.WhyCommand_Error_ArgumentCannotBeEmpty,
                    package.Name));
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
                throw new ArgumentException(
                    string.Format(CultureInfo.CurrentCulture,
                    Strings.WhyCommand_Error_InvalidFramework,
                    framework.Template));
            }
        }
    }
}
