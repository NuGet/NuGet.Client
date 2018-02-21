// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;

namespace NuGet.CommandLine.XPlat
{
    internal static class PushCommand
    {
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
                    CommandOptionType.SingleValue);

                var noSymbols = push.Option(
                    "-n|--no-symbols",
                    Strings.NoSymbols_Description,
                    CommandOptionType.SingleValue);

                var arguments = push.Argument(
                    "[root]",
                    Strings.Push_Package_ApiKey_Description,
                    multipleValues: true);

                var noServiceEndpointDescription = push.Option(
                    "--no-service-endpoint",
                    Strings.NoServiceEndpoint_Description,
                    CommandOptionType.NoValue);

                push.OnExecute(async () =>
                {
                    if (arguments.Values.Count < 1)
                    {
                        throw new ArgumentException(Strings.Push_MissingArguments);
                    }

                    string packagePath = arguments.Values[0];
                    string sourcePath = source.Value();
                    string apiKeyValue = apikey.Value();
                    string symbolSourcePath = symbolSource.Value();
                    string symbolApiKeyValue = symbolApiKey.Value();
                    bool disableBufferingValue = disableBuffering.HasValue();
                    bool noSymbolsValue = noSymbols.HasValue();
                    bool noServiceEndpoint = noServiceEndpointDescription.HasValue();
                    int timeoutSeconds = 0;

                    if (timeout.HasValue() && !int.TryParse(timeout.Value(), out timeoutSeconds))
                    {
                        throw new ArgumentException(Strings.Push_InvalidTimeout);
                    }

                    PackageSourceProvider sourceProvider = new PackageSourceProvider(XPlatUtility.CreateDefaultSettings());

                    try
                    {
                        await PushRunner.Run(
                            sourceProvider.Settings,
                            sourceProvider,
                            packagePath,
                            sourcePath,
                            apiKeyValue,
                            symbolSourcePath,
                            symbolApiKeyValue,
                            timeoutSeconds,
                            disableBufferingValue,
                            noSymbolsValue,
                            noServiceEndpoint,
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