// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.CommandLine;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Credentials;
using System.Collections.Generic;

namespace NuGet.CommandLine.XPlat
{
    internal static class DeleteCommand
    {
        internal static CliCommand Register(CliCommand app, Func<ILogger> getLogger)
        {
            var DeleteCmd = new CliCommand(name: "delete", description: Strings.Delete_Description);

            // Options directly under the verb 'delete'

            // Options under sub-command: delete
            RegisterOptionsForCommandDelete(DeleteCmd, getLogger);

            app.Subcommands.Add(DeleteCmd);

            return DeleteCmd;
        }

        private static void RegisterOptionsForCommandDelete(CliCommand cmd, Func<ILogger> getLogger)
        {
            var root_Argument = new CliArgument<string[]>(name: "[root]")
            {
                Arity = ArgumentArity.ZeroOrMore,
                Description = Strings.Delete_PackageIdAndVersion_Description,
            };
            cmd.Add(root_Argument);
            var source_Option = new CliOption<string>(name: "--source", aliases: new[] { "-s" })
            {
                Arity = ArgumentArity.ZeroOrOne,
                Description = Strings.Source_Description,
            };
            cmd.Add(source_Option);
            var nonInteractive_Option = new CliOption<bool>(name: "--non-interactive")
            {
                Arity = ArgumentArity.Zero,
                Description = Strings.NonInteractive_Description,
            };
            cmd.Add(nonInteractive_Option);
            var apiKey_Option = new CliOption<string>(name: "--api-key", aliases: new[] { "-k" })
            {
                Arity = ArgumentArity.ZeroOrOne,
                Description = Strings.ApiKey_Description,
            };
            cmd.Add(apiKey_Option);
            var noServiceEndpoint_Option = new CliOption<bool>(name: "--no-service-endpoint")
            {
                Arity = ArgumentArity.Zero,
                Description = Strings.NoServiceEndpoint_Description,
            };
            cmd.Add(noServiceEndpoint_Option);
            var interactive_Option = new CliOption<bool>(name: "--interactive")
            {
                Arity = ArgumentArity.Zero,
                Description = Strings.NuGetXplatCommand_Interactive,
            };
            cmd.Add(interactive_Option);
            var forceEnglishOutput_Option = new CliOption<bool>(name: CommandConstants.ForceEnglishOutputOption)
            {
                Arity = ArgumentArity.Zero,
                Description = Strings.ForceEnglishOutput_Description,
            };
            cmd.Add(forceEnglishOutput_Option);
            // Create handler delegate handler for cmd
            cmd.SetAction((async (parseResult, CancellationToken) =>
            {
                IList<string> packagePaths = parseResult.GetValue<string[]>(root_Argument);
                string sourcePath = parseResult.GetValue(source_Option);
                bool nonInteractiveValue = parseResult.GetValue(nonInteractive_Option);
                string apiKeyValue = parseResult.GetValue(apiKey_Option);
                bool noServiceEndpoint = parseResult.GetValue(noServiceEndpoint_Option);
                bool interactive = parseResult.GetValue(interactive_Option);

                if (packagePaths.Count < 2)
                {
                    throw new ArgumentException(Strings.Delete_MissingArguments);
                }

                string packageId = packagePaths[0];
                string packageVersion = packagePaths[1];

                DefaultCredentialServiceUtility.SetupDefaultCredentialService(getLogger(), !interactive);

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
                    noServiceEndpoint,
                    Confirm,
                    getLogger());

                return 0;
            }));
        }

        public static void Register(CommandLineApplication app, Func<ILogger> getLogger)
        {
            app.Command("delete", delete =>
            {
                delete.Description = Strings.Delete_Description;
                delete.HelpOption(XPlatUtility.HelpOption);

                delete.Option(
                    CommandConstants.ForceEnglishOutputOption,
                    Strings.ForceEnglishOutput_Description,
                    CommandOptionType.NoValue);

                var source = delete.Option(
                    "-s|--source <source>",
                    Strings.Source_Description,
                    CommandOptionType.SingleValue);

                var nonInteractive = delete.Option(
                    "--non-interactive",
                    Strings.NonInteractive_Description,
                    CommandOptionType.NoValue);

                var apikey = delete.Option(
                    "-k|--api-key <apiKey>",
                    Strings.ApiKey_Description,
                    CommandOptionType.SingleValue);

                var arguments = delete.Argument(
                    "[root]",
                    Strings.Delete_PackageIdAndVersion_Description,
                    multipleValues: true);

                var noServiceEndpointDescription = delete.Option(
                    "--no-service-endpoint",
                    Strings.NoServiceEndpoint_Description,
                    CommandOptionType.NoValue);

                var interactive = delete.Option(
                    "--interactive",
                    Strings.NuGetXplatCommand_Interactive,
                    CommandOptionType.NoValue);

                delete.OnExecute(async () =>
                {
                    if (arguments.Values.Count < 2)
                    {
                        throw new ArgumentException(Strings.Delete_MissingArguments);
                    }

                    string packageId = arguments.Values[0];
                    string packageVersion = arguments.Values[1];
                    string sourcePath = source.Value();
                    string apiKeyValue = apikey.Value();
                    bool nonInteractiveValue = nonInteractive.HasValue();
                    bool noServiceEndpoint = noServiceEndpointDescription.HasValue();

                    DefaultCredentialServiceUtility.SetupDefaultCredentialService(getLogger(), !interactive.HasValue());

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
                        noServiceEndpoint,
                        Confirm,
                        getLogger());

                    return 0;
                });
            });
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
                return result.StartsWith(Strings.ConsoleConfirmMessageAccept, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                Console.ForegroundColor = currentColor;
            }
        }
    }
}
