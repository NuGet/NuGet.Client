// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Help;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.CommandLine.XPlat.Commands;
using NuGet.Commands;
using NuGet.Common;

namespace NuGet.CommandLine.XPlat
{
    internal static class LocalsCommand
    {
        // Registers a placeholder on the legacy CommandLineApplication so that `dotnet nuget --help`
        // still lists `locals`. The command is implemented with System.CommandLine (see overloads below).
        internal static void Register(CommandLineApplication app)
        {
            app.Command("locals", locals =>
            {
                locals.Description = Strings.LocalsCommand_Description;
            });
        }

        internal static void Register(Command rootCommand, Func<ILogger> getLogger)
        {
            Register(rootCommand, getLogger, () => new LocalsCommandRunner());
        }

        internal static void Register(Command rootCommand, Func<ILogger> getLogger, Func<ILocalsCommandRunner> getCommandRunner)
        {
            var localsCommand = new DocumentedCommand("locals", Strings.LocalsCommand_Description, "https://aka.ms/dotnet/nuget/locals");

            var cacheLocationArgument = new Argument<string>("Cache Location(s)")
            {
                Description = Strings.LocalsCommand_ArgumentDescription,
                Arity = ArgumentArity.ZeroOrOne,
            };

            var clearOption = new Option<bool>("--clear", "-c")
            {
                Description = Strings.LocalsCommand_ClearDescription,
                Arity = ArgumentArity.Zero,
            };

            var listOption = new Option<bool>("--list", "-l")
            {
                Description = Strings.LocalsCommand_ListDescription,
                Arity = ArgumentArity.Zero,
            };

            var forceEnglishOutputOption = new Option<bool>(CommandConstants.ForceEnglishOutputOption)
            {
                Description = Strings.ForceEnglishOutput_Description,
                Arity = ArgumentArity.Zero,
            };

            var helpOption = new HelpOption()
            {
                Arity = ArgumentArity.Zero,
            };

            localsCommand.Arguments.Add(cacheLocationArgument);
            localsCommand.Options.Add(clearOption);
            localsCommand.Options.Add(listOption);
            localsCommand.Options.Add(forceEnglishOutputOption);
            localsCommand.Options.Add(helpOption);

            localsCommand.SetAction((parseResult, cancellationToken) =>
            {
                var logger = getLogger();

                try
                {
                    var settings = XPlatUtility.GetSettingsForCurrentWorkingDirectory();
                    string? cacheLocation = parseResult.GetValue(cacheLocationArgument);
                    bool clear = parseResult.GetValue(clearOption);
                    bool list = parseResult.GetValue(listOption);

                    // Using both --clear and --list, or neither one of them, is not supported.
                    // The cache location argument is optional at parse time so we can surface a
                    // NuGet-specific usage message instead of System.CommandLine's generic error.
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

                    var localsArgs = new LocalsArgs(
                        new List<string>() { cacheLocation },
                        settings,
                        logger.LogInformation,
                        logger.LogError,
                        clear,
                        list);

                    getCommandRunner().ExecuteCommand(localsArgs);

                    return Task.FromResult(ExitCodes.Success);
                }
                catch (ArgumentException ex)
                {
                    logger.LogError(ex.Message);
                    return Task.FromResult(ExitCodes.InvalidArguments);
                }
            });

            rootCommand.Subcommands.Add(localsCommand);
        }
    }
}
