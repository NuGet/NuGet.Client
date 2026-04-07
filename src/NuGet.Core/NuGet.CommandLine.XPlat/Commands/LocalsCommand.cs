// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.CommandLine;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using NuGet.Commands;

namespace NuGet.CommandLine.XPlat
{
    internal static class LocalsCommand
    {
        private static readonly Option<bool> ClearOption = new Option<bool>("--clear", "-c")
        {
            Arity = ArgumentArity.Zero,
            Description = Strings.LocalsCommand_ClearDescription,
        };

        private static readonly Option<bool> ListOption = new Option<bool>("--list", "-l")
        {
            Arity = ArgumentArity.Zero,
            Description = Strings.LocalsCommand_ListDescription,
        };

        private static readonly Argument<string> CacheLocationArgument = new Argument<string>("Cache Location(s)")
        {
            Arity = ArgumentArity.ZeroOrOne,
            Description = Strings.LocalsCommand_ArgumentDescription,
        };

        internal static void Register(Command parent, Func<ILoggerWithColor> getLogger)
        {
            var localsCmd = new Command("locals", Strings.LocalsCommand_Description);

            localsCmd.Options.Add(ClearOption);
            localsCmd.Options.Add(ListOption);
            localsCmd.Arguments.Add(CacheLocationArgument);

            localsCmd.SetAction((parseResult, cancellationToken) =>
            {
                var logger = getLogger();
                var setting = XPlatUtility.GetSettingsForCurrentWorkingDirectory();

                string? cacheLocation = parseResult.GetValue(CacheLocationArgument);
                bool clear = parseResult.GetValue(ClearOption);
                bool list = parseResult.GetValue(ListOption);

                // Using both -clear and -list command options, or neither one of them, is not supported.
                // We use MinArgs = 0 even though the first argument is required,
                // to avoid throwing a command argument validation exception and
                // immediately show usage help for this command instead.
                if (string.IsNullOrWhiteSpace(cacheLocation))
                {
                    throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.LocalsCommand_NoArguments));
                }
                else if (clear && list)
                {
                    throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.LocalsCommand_MultipleOperations));
                }
                else if (!clear && !list)
                {
                    throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.LocalsCommand_NoOperation));
                }
                else
                {
                    var localsArgs = new LocalsArgs(new List<string> { cacheLocation },
                        setting,
                        logger.LogInformation,
                        logger.LogError,
                        clear,
                        list);

                    var localsCommandRunner = new LocalsCommandRunner();
                    localsCommandRunner.ExecuteCommand(localsArgs);
                }

                return Task.FromResult(0);
            });

            parent.Subcommands.Add(localsCmd);
        }
    }
}
