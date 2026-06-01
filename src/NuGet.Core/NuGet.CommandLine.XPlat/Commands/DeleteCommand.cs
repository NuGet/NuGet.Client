// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Help;
using System.Globalization;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.CommandLine.XPlat.Commands;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Credentials;

namespace NuGet.CommandLine.XPlat
{
    internal static class DeleteCommand
    {
        // Registers a placeholder on the legacy CommandLineApplication so that `dotnet nuget --help`
        // still lists `delete`. The command is implemented with System.CommandLine (see overload below).
        internal static void Register(CommandLineApplication app)
        {
            app.Command("delete", delete =>
            {
                delete.Description = Strings.Delete_Description;
            });
        }

        internal static void Register(Command rootCommand, Func<ILogger> getLogger)
        {
            var deleteCommand = new DocumentedCommand("delete", Strings.Delete_Description, "https://aka.ms/dotnet/nuget/delete");

            var arguments = new Argument<List<string>>("[root]")
            {
                Description = Strings.Delete_PackageIdAndVersion_Description,
                Arity = ArgumentArity.ZeroOrMore,
            };

            var source = new Option<string>("--source", "-s")
            {
                Description = Strings.Source_Description,
                Arity = ArgumentArity.ExactlyOne,
            };

            var nonInteractive = new Option<bool>("--non-interactive")
            {
                Description = Strings.NonInteractive_Description,
                Arity = ArgumentArity.Zero,
            };

            var apikey = new Option<string>("--api-key", "-k")
            {
                Description = Strings.ApiKey_Description,
                Arity = ArgumentArity.ExactlyOne,
            };

            var noServiceEndpoint = new Option<bool>("--no-service-endpoint")
            {
                Description = Strings.NoServiceEndpoint_Description,
                Arity = ArgumentArity.Zero,
            };

            var interactive = new Option<bool>("--interactive")
            {
                Description = Strings.NuGetXplatCommand_Interactive,
                Arity = ArgumentArity.Zero,
            };

            var forceEnglishOutput = new Option<bool>(CommandConstants.ForceEnglishOutputOption)
            {
                Description = Strings.ForceEnglishOutput_Description,
                Arity = ArgumentArity.Zero,
            };

            var help = new HelpOption()
            {
                Arity = ArgumentArity.Zero,
            };

            deleteCommand.Arguments.Add(arguments);
            deleteCommand.Options.Add(source);
            deleteCommand.Options.Add(nonInteractive);
            deleteCommand.Options.Add(apikey);
            deleteCommand.Options.Add(noServiceEndpoint);
            deleteCommand.Options.Add(interactive);
            deleteCommand.Options.Add(forceEnglishOutput);
            deleteCommand.Options.Add(help);

            deleteCommand.SetAction(async (parseResult, cancellationToken) =>
            {
                List<string>? packageArguments = parseResult.GetValue(arguments);

                if (packageArguments == null || packageArguments.Count < 2)
                {
                    throw new ArgumentException(Strings.Delete_MissingArguments);
                }

                string packageId = packageArguments[0];
                string packageVersion = packageArguments[1];
                string? sourcePath = parseResult.GetValue(source);
                string? apiKeyValue = parseResult.GetValue(apikey);
                bool nonInteractiveValue = parseResult.GetValue(nonInteractive);
                bool noServiceEndpointValue = parseResult.GetValue(noServiceEndpoint);
                bool interactiveValue = parseResult.GetValue(interactive);

                DefaultCredentialServiceUtility.SetupDefaultCredentialService(getLogger(), !interactiveValue);

#pragma warning disable CS0618 // Type or member is obsolete
                PackageSourceProvider sourceProvider = new PackageSourceProvider(XPlatUtility.GetSettingsForCurrentWorkingDirectory(), enablePackageSourcesChangedEvent: false);
#pragma warning restore CS0618 // Type or member is obsolete

                await DeleteRunner.Run(
                    sourceProvider.Settings,
                    sourceProvider,
                    packageId,
                    packageVersion,
                    sourcePath,
                    apiKeyValue,
                    nonInteractiveValue,
                    noServiceEndpointValue,
                    Confirm,
                    getLogger());

                return ExitCodes.Success;
            });

            rootCommand.Subcommands.Add(deleteCommand);
        }

        private static bool Confirm(string description)
        {
            ConsoleColor currentColor = ConsoleColor.Gray;
            try
            {
                currentColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(string.Format(CultureInfo.CurrentCulture, Strings.ConsoleConfirmMessage, description));
                var result = Console.ReadLine();
                return result != null && result.StartsWith(Strings.ConsoleConfirmMessageAccept, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                Console.ForegroundColor = currentColor;
            }
        }
    }
}
