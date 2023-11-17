// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.CommandLine;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Commands;
using NuGet.Common;

namespace NuGet.CommandLine.XPlat
{
    internal static class LocalsCommand
    {
        internal static CliCommand Register(CliCommand app, Func<ILogger> getLogger)
        {
            var LocalsCmd = new CliCommand(name: "locals", description: Strings.LocalsCommand_Description);

            // Options directly under the verb 'push'

            // Options under sub-command: push
            RegisterOptionsForCommandLocals(LocalsCmd, getLogger);

            app.Subcommands.Add(LocalsCmd);

            return LocalsCmd;
        }

        private static void RegisterOptionsForCommandLocals(CliCommand cmd, Func<ILogger> getLogger)
        {
            var cacheLocations_Argument = new CliArgument<string[]>(name: "CacheLocations")
            {
                Arity = ArgumentArity.ZeroOrMore,
                Description = Strings.LocalsCommand_ArgumentDescription,
            };
            cmd.Add(cacheLocations_Argument);
            var list_Option = new CliOption<bool>(name: "--list", aliases: new[] { "-l" })
            {
                Arity = ArgumentArity.Zero,
                Description = Strings.LocalsCommand_ListDescription,
            };
            cmd.Add(list_Option);
            var clear_Option = new CliOption<bool>(name: "--clear", aliases: new[] { "-c" })
            {
                Arity = ArgumentArity.Zero,
                Description = Strings.LocalsCommand_ClearDescription,
            };
            cmd.Add(clear_Option);
            var forceEnglishOutput_Option = new CliOption<bool>(name: CommandConstants.ForceEnglishOutputOption)
            {
                Arity = ArgumentArity.Zero,
                Description = Strings.ForceEnglishOutput_Description,
            };
            cmd.Add(forceEnglishOutput_Option);
            // Create handler delegate handler for cmd
            cmd.SetAction(parseResult =>
            {
                var logger = getLogger();
                var setting = XPlatUtility.GetSettingsForCurrentWorkingDirectory();

                var arguments = parseResult.GetValue(cacheLocations_Argument);
                var clear = parseResult.GetValue(clear_Option);
                var list = parseResult.GetValue(list_Option);

                // Using both -clear and -list command options, or neither one of them, is not supported.
                // We use MinArgs = 0 even though the first argument is required,
                // to avoid throwing a command argument validation exception and
                // immediately show usage help for this command instead.
                if ((arguments.Length < 1) || string.IsNullOrWhiteSpace(arguments[0]))
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
                    var localsArgs = new LocalsArgs(arguments,
                        setting,
                        logger.LogInformation,
                        logger.LogError,
                        clear,
                        list);

                    var localsCommandRunner = new LocalsCommandRunner();
                    localsCommandRunner.ExecuteCommand(localsArgs);
                }

                return 0;
            });
        }

        public static void Register(CommandLineApplication app, Func<ILogger> getLogger)
        {
            app.Command("locals", locals =>
            {
                locals.Description = Strings.LocalsCommand_Description;
                locals.HelpOption(XPlatUtility.HelpOption);

                locals.Option(
                    CommandConstants.ForceEnglishOutputOption,
                    Strings.ForceEnglishOutput_Description,
                    CommandOptionType.NoValue);

                var clear = locals.Option(
                    "-c|--clear",
                    Strings.LocalsCommand_ClearDescription,
                    CommandOptionType.NoValue);

                var list = locals.Option(
                    "-l|--list",
                    Strings.LocalsCommand_ListDescription,
                    CommandOptionType.NoValue);

                var arguments = locals.Argument(
                    "Cache Location(s)",
                    Strings.LocalsCommand_ArgumentDescription,
                    multipleValues: false);

                locals.OnExecute(() =>
                {
                    var logger = getLogger();
                    var setting = XPlatUtility.GetSettingsForCurrentWorkingDirectory();

                    // Using both -clear and -list command options, or neither one of them, is not supported.
                    // We use MinArgs = 0 even though the first argument is required,
                    // to avoid throwing a command argument validation exception and
                    // immediately show usage help for this command instead.
                    if ((arguments.Values.Count < 1) || string.IsNullOrWhiteSpace(arguments.Values[0]))
                    {
                        throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.LocalsCommand_NoArguments));
                    }
                    else if (clear.HasValue() && list.HasValue())
                    {
                        throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.LocalsCommand_MultipleOperations));
                    }
                    else if (!clear.HasValue() && !list.HasValue())
                    {
                        throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.LocalsCommand_NoOperation));
                    }
                    else
                    {
                        var localsArgs = new LocalsArgs(arguments.Values,
                            setting,
                            logger.LogInformation,
                            logger.LogError,
                            clear.HasValue(),
                            list.HasValue());

                        var localsCommandRunner = new LocalsCommandRunner();
                        localsCommandRunner.ExecuteCommand(localsArgs);
                    }

                    return 0;
                });
            });
        }
    }
}
