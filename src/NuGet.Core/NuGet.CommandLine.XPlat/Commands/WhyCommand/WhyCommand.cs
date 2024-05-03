// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Help;

namespace NuGet.CommandLine.XPlat
{
    internal static class WhyCommand
    {
        public static void Register(CliCommand rootCommand, Func<ILoggerWithColor> getLogger)
        {
            var whyCommand = new CliCommand("why", Strings.WhyCommand_Description);

            var path = new CliArgument<string>("<PROJECT>|<SOLUTION>")
            {
                Description = Strings.WhyCommand_PathArgument_Description,
                Arity = ArgumentArity.ExactlyOne // ZeroOrOne
            };

            var package = new CliArgument<string>("PACKAGE_NAME")
            {
                Description = Strings.WhyCommand_PackageArgument_Description,
                Arity = ArgumentArity.ExactlyOne // ZeroOrOne
            };

            var frameworks = new CliOption<List<string>>("-f|--framework")
            {
                Description = Strings.WhyCommand_FrameworksOption_Description,
                Arity = ArgumentArity.OneOrMore
            };

            var help = new HelpOption()
            {
                Arity = ArgumentArity.Zero
            };

            whyCommand.Arguments.Add(path);
            whyCommand.Arguments.Add(package);
            whyCommand.Options.Add(frameworks);
            whyCommand.Options.Add(help);

            whyCommand.SetAction((parseResult) =>
            {
                ILoggerWithColor logger = getLogger();

                try
                {
                    var whyCommandArgs = new WhyCommandArgs(
                        parseResult.GetValue(path),
                        parseResult.GetValue(package),
                        parseResult.GetValue(frameworks),
                        logger);

                    WhyCommandRunner.ExecuteCommand(whyCommandArgs);

                    return 0;
                }
                catch (ArgumentException ex)
                {
                    logger.LogError(ex.Message);
                    return ExitCodes.InvalidArguments;
                }
            });

            rootCommand.Subcommands.Add(whyCommand);
        }
    }
}
