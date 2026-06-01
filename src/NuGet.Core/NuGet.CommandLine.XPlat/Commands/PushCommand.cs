// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Help;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.CommandLine.XPlat.Commands;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Credentials;

namespace NuGet.CommandLine.XPlat
{
    internal static class PushCommand
    {
        // Registers a placeholder on the legacy CommandLineApplication so that `dotnet nuget --help`
        // still lists `push`. The command is implemented with System.CommandLine (see overload below).
        internal static void Register(CommandLineApplication app)
        {
            app.Command("push", push =>
            {
                push.Description = Strings.Push_Description;
            });
        }

        internal static void Register(Command rootCommand, Func<ILogger> getLogger)
        {
            var pushCommand = new DocumentedCommand("push", Strings.Push_Description, "https://aka.ms/dotnet/nuget/push");

            var arguments = new Argument<List<string>>("[root]")
            {
                Description = Strings.Push_Package_ApiKey_Description,
                Arity = ArgumentArity.ZeroOrMore,
            };

            var source = new Option<string>("--source", "-s")
            {
                Description = Strings.Source_Description,
                Arity = ArgumentArity.ExactlyOne,
            };

            var allowInsecureConnections = new Option<bool>("--allow-insecure-connections")
            {
                Description = Strings.AllowInsecureConnections_Description,
                Arity = ArgumentArity.Zero,
            };

            var symbolSource = new Option<string>("--symbol-source", "-ss")
            {
                Description = Strings.SymbolSource_Description,
                Arity = ArgumentArity.ExactlyOne,
            };

            var timeout = new Option<string>("--timeout", "-t")
            {
                Description = Strings.Push_Timeout_Description,
                Arity = ArgumentArity.ExactlyOne,
            };

            var apikey = new Option<string>("--api-key", "-k")
            {
                Description = Strings.ApiKey_Description,
                Arity = ArgumentArity.ExactlyOne,
            };

            var symbolApiKey = new Option<string>("--symbol-api-key", "-sk")
            {
                Description = Strings.SymbolApiKey_Description,
                Arity = ArgumentArity.ExactlyOne,
            };

            var disableBuffering = new Option<bool>("--disable-buffering", "-d")
            {
                Description = Strings.DisableBuffering_Description,
                Arity = ArgumentArity.Zero,
            };

            var noSymbols = new Option<bool>("--no-symbols", "-n")
            {
                Description = Strings.NoSymbols_Description,
                Arity = ArgumentArity.Zero,
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

            var skipDuplicate = new Option<bool>("--skip-duplicate")
            {
                Description = Strings.PushCommandSkipDuplicateDescription,
                Arity = ArgumentArity.Zero,
            };

            var configurationFile = new Option<string>("--configfile")
            {
                Description = Strings.Option_ConfigFile,
                Arity = ArgumentArity.ExactlyOne,
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

            pushCommand.Arguments.Add(arguments);
            pushCommand.Options.Add(source);
            pushCommand.Options.Add(allowInsecureConnections);
            pushCommand.Options.Add(symbolSource);
            pushCommand.Options.Add(timeout);
            pushCommand.Options.Add(apikey);
            pushCommand.Options.Add(symbolApiKey);
            pushCommand.Options.Add(disableBuffering);
            pushCommand.Options.Add(noSymbols);
            pushCommand.Options.Add(noServiceEndpoint);
            pushCommand.Options.Add(interactive);
            pushCommand.Options.Add(skipDuplicate);
            pushCommand.Options.Add(configurationFile);
            pushCommand.Options.Add(forceEnglishOutput);
            pushCommand.Options.Add(help);

            pushCommand.SetAction(async (parseResult, cancellationToken) =>
            {
                List<string>? packagePaths = parseResult.GetValue(arguments);

                if (packagePaths == null || packagePaths.Count < 1)
                {
                    throw new ArgumentException(Strings.Push_MissingArguments);
                }

                string? sourcePath = parseResult.GetValue(source);
                string? apiKeyValue = parseResult.GetValue(apikey);
                string? symbolSourcePath = parseResult.GetValue(symbolSource);
                string? symbolApiKeyValue = parseResult.GetValue(symbolApiKey);
                bool disableBufferingValue = parseResult.GetValue(disableBuffering);
                bool noSymbolsValue = parseResult.GetValue(noSymbols);
                bool noServiceEndpointValue = parseResult.GetValue(noServiceEndpoint);
                bool skipDuplicateValue = parseResult.GetValue(skipDuplicate);
                bool allowInsecureConnectionsValue = parseResult.GetValue(allowInsecureConnections);
                bool interactiveValue = parseResult.GetValue(interactive);
                string? timeoutValue = parseResult.GetValue(timeout);
                int timeoutSeconds = 0;

                if (!string.IsNullOrEmpty(timeoutValue) && !int.TryParse(timeoutValue, out timeoutSeconds))
                {
                    throw new ArgumentException(Strings.Push_InvalidTimeout);
                }

#pragma warning disable CS0618 // Type or member is obsolete
                var sourceProvider = new PackageSourceProvider(XPlatUtility.ProcessConfigFile(parseResult.GetValue(configurationFile)), enablePackageSourcesChangedEvent: false);
#pragma warning restore CS0618 // Type or member is obsolete

                try
                {
                    DefaultCredentialServiceUtility.SetupDefaultCredentialService(getLogger(), !interactiveValue);

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
                        noServiceEndpointValue,
                        skipDuplicateValue,
                        allowInsecureConnectionsValue,
                        getLogger());
                }
                catch (TaskCanceledException ex)
                {
                    throw new AggregateException(ex, new Exception(Strings.Push_Timeout_Error));
                }

                return ExitCodes.Success;
            });

            rootCommand.Subcommands.Add(pushCommand);
        }
    }
}
