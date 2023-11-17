// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Credentials;

namespace NuGet.CommandLine.XPlat
{
    internal static class PushCommand
    {
        internal static CliCommand Register(CliCommand app, Func<ILogger> getLogger)
        {
            var PushCmd = new CliCommand(name: "push", description: Strings.Push_Description);

            // Options directly under the verb 'push'

            // Options under sub-command: push
            RegisterOptionsForCommandPush(PushCmd, getLogger);

            app.Subcommands.Add(PushCmd);

            return PushCmd;
        }

        private static void RegisterOptionsForCommandPush(CliCommand cmd, Func<ILogger> getLogger)
        {
            var root_Argument = new CliArgument<string[]>(name: "[root]")
            {
                Arity = ArgumentArity.ZeroOrMore,
                Description = Strings.Push_Package_ApiKey_Description,
            };
            cmd.Add(root_Argument);
            var source_Option = new CliOption<string>(name: "--source", aliases: new[] { "-s" })
            {
                Arity = ArgumentArity.ZeroOrOne,
                Description = Strings.Source_Description,
            };
            cmd.Add(source_Option);
            var symbolSource_Option = new CliOption<string>(name: "--symbol-source", aliases: new[] { "-ss" })
            {
                Arity = ArgumentArity.ZeroOrOne,
                Description = Strings.SymbolSource_Description,
            };
            cmd.Add(symbolSource_Option);
            var timeout_Option = new CliOption<string>(name: "--timeout", aliases: new[] { "-t" })
            {
                Arity = ArgumentArity.ZeroOrOne,
                Description = Strings.Push_Timeout_Description,
            };
            cmd.Add(timeout_Option);
            var apiKey_Option = new CliOption<string>(name: "--api-key", aliases: new[] { "-k" })
            {
                Arity = ArgumentArity.ZeroOrOne,
                Description = Strings.ApiKey_Description,
            };
            cmd.Add(apiKey_Option);
            var symbolApiKey_Option = new CliOption<string>(name: "--symbol-api-key", aliases: new[] { "-sk" })
            {
                Arity = ArgumentArity.ZeroOrOne,
                Description = Strings.SymbolApiKey_Description,
            };
            cmd.Add(symbolApiKey_Option);
            var disableBuffering_Option = new CliOption<bool>(name: "--disable-buffering", aliases: new[] { "-d" })
            {
                Arity = ArgumentArity.Zero,
                Description = Strings.DisableBuffering_Description,
            };
            cmd.Add(disableBuffering_Option);
            var noSymbols_Option = new CliOption<bool>(name: "--no-symbols", aliases: new[] { "-n" })
            {
                Arity = ArgumentArity.Zero,
                Description = Strings.NoSymbols_Description,
            };
            cmd.Add(noSymbols_Option);
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
            var skipDuplicate_Option = new CliOption<bool>(name: "--skip-duplicate")
            {
                Arity = ArgumentArity.Zero,
                Description = Strings.PushCommandSkipDuplicateDescription,
            };
            cmd.Add(skipDuplicate_Option);
            var forceEnglishOutput_Option = new CliOption<bool>(name: CommandConstants.ForceEnglishOutputOption)
            {
                Arity = ArgumentArity.Zero,
                Description = Strings.ForceEnglishOutput_Description,
            };
            cmd.Add(forceEnglishOutput_Option);
            // Create handler delegate handler for cmd
            cmd.SetAction((async (parseResult, CancellationToken) =>
            {
                if (parseResult.GetValue(root_Argument).Length < 1)
                {
                    throw new ArgumentException(Strings.Push_MissingArguments);
                }

                IList<string> packagePaths = parseResult.GetValue<string[]>(root_Argument);
                string sourcePath = parseResult.GetValue(source_Option);
                string apiKeyValue = parseResult.GetValue(apiKey_Option);
                string symbolSourcePath = parseResult.GetValue(symbolSource_Option);
                string symbolApiKeyValue = parseResult.GetValue(symbolApiKey_Option);
                bool disableBufferingValue = parseResult.GetValue(disableBuffering_Option);
                bool noSymbolsValue = parseResult.GetValue(noSymbols_Option);
                bool noServiceEndpoint = parseResult.GetValue(noServiceEndpoint_Option);
                bool skipDuplicateValue = parseResult.GetValue(skipDuplicate_Option);
                int timeoutSeconds = 0;

                string timeout = parseResult.GetValue(timeout_Option);
                bool interactive = parseResult.GetValue(interactive_Option);

                if (timeout != null && !int.TryParse(timeout, out timeoutSeconds))
                {
                    throw new ArgumentException(Strings.Push_InvalidTimeout);
                }

#pragma warning disable CS0618 // Type or member is obsolete
                var sourceProvider = new PackageSourceProvider(XPlatUtility.GetSettingsForCurrentWorkingDirectory(), enablePackageSourcesChangedEvent: false);
#pragma warning restore CS0618 // Type or member is obsolete

                try
                {
                    DefaultCredentialServiceUtility.SetupDefaultCredentialService(getLogger(), !interactive);
                    await PushRunner.Run(
                        sourceProvider.Settings,
                        sourceProvider,
                        packagePaths,
                        sourcePath,
                        apiKeyValue,
                        symbolSourcePath,
                        symbolApiKeyValue,
                        timeoutSeconds,
                        disableBufferingValue,
                        noSymbolsValue,
                        noServiceEndpoint,
                        skipDuplicateValue,
                        getLogger());
                }
                catch (TaskCanceledException ex)
                {
                    throw new AggregateException(ex, new Exception(Strings.Push_Timeout_Error));
                }

                return 0;
            }));
        }

        public static void Register(CommandLineApplication app, Func<ILogger> getLogger)
        {
            app.Command("push", push =>
            {
                push.Description = Strings.Push_Description;
                push.HelpOption(XPlatUtility.HelpOption);

                push.Option(
                    CommandConstants.ForceEnglishOutputOption,
                    Strings.ForceEnglishOutput_Description,
                    CommandOptionType.NoValue);

                var source = push.Option(
                    "-s|--source <source>",
                    Strings.Source_Description,
                    CommandOptionType.SingleValue);

                var symbolSource = push.Option(
                    "-ss|--symbol-source <source>",
                    Strings.SymbolSource_Description,
                    CommandOptionType.SingleValue);

                var timeout = push.Option(
                    "-t|--timeout <timeout>",
                    Strings.Push_Timeout_Description,
                    CommandOptionType.SingleValue);

                var apikey = push.Option(
                    "-k|--api-key <apiKey>",
                    Strings.ApiKey_Description,
                    CommandOptionType.SingleValue);

                var symbolApiKey = push.Option(
                    "-sk|--symbol-api-key <apiKey>",
                    Strings.SymbolApiKey_Description,
                    CommandOptionType.SingleValue);

                var disableBuffering = push.Option(
                    "-d|--disable-buffering",
                    Strings.DisableBuffering_Description,
                    CommandOptionType.NoValue);

                var noSymbols = push.Option(
                    "-n|--no-symbols",
                    Strings.NoSymbols_Description,
                    CommandOptionType.NoValue);

                var arguments = push.Argument(
                    "[root]",
                    Strings.Push_Package_ApiKey_Description,
                    multipleValues: true);

                var noServiceEndpointDescription = push.Option(
                    "--no-service-endpoint",
                    Strings.NoServiceEndpoint_Description,
                    CommandOptionType.NoValue);

                var interactive = push.Option(
                    "--interactive",
                    Strings.NuGetXplatCommand_Interactive,
                    CommandOptionType.NoValue);

                var skipDuplicate = push.Option(
                    "--skip-duplicate",
                    Strings.PushCommandSkipDuplicateDescription,
                    CommandOptionType.NoValue);

                push.OnExecute(async () =>
                {
                    if (arguments.Values.Count < 1)
                    {
                        throw new ArgumentException(Strings.Push_MissingArguments);
                    }

                    IList<string> packagePaths = arguments.Values;
                    string sourcePath = source.Value();
                    string apiKeyValue = apikey.Value();
                    string symbolSourcePath = symbolSource.Value();
                    string symbolApiKeyValue = symbolApiKey.Value();
                    bool disableBufferingValue = disableBuffering.HasValue();
                    bool noSymbolsValue = noSymbols.HasValue();
                    bool noServiceEndpoint = noServiceEndpointDescription.HasValue();
                    bool skipDuplicateValue = skipDuplicate.HasValue();
                    int timeoutSeconds = 0;

                    if (timeout.HasValue() && !int.TryParse(timeout.Value(), out timeoutSeconds))
                    {
                        throw new ArgumentException(Strings.Push_InvalidTimeout);
                    }

#pragma warning disable CS0618 // Type or member is obsolete
                    var sourceProvider = new PackageSourceProvider(XPlatUtility.GetSettingsForCurrentWorkingDirectory(), enablePackageSourcesChangedEvent: false);
#pragma warning restore CS0618 // Type or member is obsolete

                    try
                    {
                        DefaultCredentialServiceUtility.SetupDefaultCredentialService(getLogger(), !interactive.HasValue());
                        await PushRunner.Run(
                            sourceProvider.Settings,
                            sourceProvider,
                            packagePaths,
                            sourcePath,
                            apiKeyValue,
                            symbolSourcePath,
                            symbolApiKeyValue,
                            timeoutSeconds,
                            disableBufferingValue,
                            noSymbolsValue,
                            noServiceEndpoint,
                            skipDuplicateValue,
                            getLogger());
                    }
                    catch (TaskCanceledException ex)
                    {
                        throw new AggregateException(ex, new Exception(Strings.Push_Timeout_Error));
                    }

                    return 0;
                });
            });
        }
    }
}
