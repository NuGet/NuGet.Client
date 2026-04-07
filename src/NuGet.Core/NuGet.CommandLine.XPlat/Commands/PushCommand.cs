// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.CommandLine;
using System.Threading.Tasks;
using NuGet.Commands;
using NuGet.Configuration;
using NuGet.Credentials;

namespace NuGet.CommandLine.XPlat
{
    internal static class PushCommand
    {
        private static readonly Option<string> SourceOption = new Option<string>("--source", "-s")
        {
            Arity = ArgumentArity.ZeroOrOne,
            Description = Strings.Source_Description,
        };

        private static readonly Option<bool> AllowInsecureConnectionsOption = new Option<bool>("--allow-insecure-connections")
        {
            Arity = ArgumentArity.Zero,
            Description = Strings.AllowInsecureConnections_Description,
        };

        private static readonly Option<string> SymbolSourceOption = new Option<string>("--symbol-source", "-ss")
        {
            Arity = ArgumentArity.ZeroOrOne,
            Description = Strings.SymbolSource_Description,
        };

        private static readonly Option<string> TimeoutOption = new Option<string>("--timeout", "-t")
        {
            Arity = ArgumentArity.ZeroOrOne,
            Description = Strings.Push_Timeout_Description,
        };

        private static readonly Option<string> ApiKeyOption = new Option<string>("--api-key", "-k")
        {
            Arity = ArgumentArity.ZeroOrOne,
            Description = Strings.ApiKey_Description,
        };

        private static readonly Option<string> SymbolApiKeyOption = new Option<string>("--symbol-api-key", "-sk")
        {
            Arity = ArgumentArity.ZeroOrOne,
            Description = Strings.SymbolApiKey_Description,
        };

        private static readonly Option<bool> DisableBufferingOption = new Option<bool>("--disable-buffering", "-d")
        {
            Arity = ArgumentArity.Zero,
            Description = Strings.DisableBuffering_Description,
        };

        private static readonly Option<bool> NoSymbolsOption = new Option<bool>("--no-symbols", "-n")
        {
            Arity = ArgumentArity.Zero,
            Description = Strings.NoSymbols_Description,
        };

        private static readonly Option<bool> NoServiceEndpointOption = new Option<bool>("--no-service-endpoint")
        {
            Arity = ArgumentArity.Zero,
            Description = Strings.NoServiceEndpoint_Description,
        };

        private static readonly Option<bool> InteractiveOption = new Option<bool>("--interactive")
        {
            Arity = ArgumentArity.Zero,
            Description = Strings.NuGetXplatCommand_Interactive,
        };

        private static readonly Option<bool> SkipDuplicateOption = new Option<bool>("--skip-duplicate")
        {
            Arity = ArgumentArity.Zero,
            Description = Strings.PushCommandSkipDuplicateDescription,
        };

        private static readonly Option<string> ConfigFileOption = new Option<string>("--configfile")
        {
            Arity = ArgumentArity.ZeroOrOne,
            Description = Strings.Option_ConfigFile,
        };

        private static readonly Argument<string[]> PackagePathsArgument = new Argument<string[]>("package-paths")
        {
            Arity = ArgumentArity.OneOrMore,
            Description = Strings.Push_Package_ApiKey_Description,
        };

        internal static void Register(Command parent, Func<ILoggerWithColor> getLogger)
        {
            var pushCmd = new Command("push", Strings.Push_Description);

            pushCmd.Options.Add(SourceOption);
            pushCmd.Options.Add(AllowInsecureConnectionsOption);
            pushCmd.Options.Add(SymbolSourceOption);
            pushCmd.Options.Add(TimeoutOption);
            pushCmd.Options.Add(ApiKeyOption);
            pushCmd.Options.Add(SymbolApiKeyOption);
            pushCmd.Options.Add(DisableBufferingOption);
            pushCmd.Options.Add(NoSymbolsOption);
            pushCmd.Options.Add(NoServiceEndpointOption);
            pushCmd.Options.Add(InteractiveOption);
            pushCmd.Options.Add(SkipDuplicateOption);
            pushCmd.Options.Add(ConfigFileOption);
            pushCmd.Arguments.Add(PackagePathsArgument);

            pushCmd.SetAction(async (parseResult, cancellationToken) =>
            {
                string[]? packagePaths = parseResult.GetValue(PackagePathsArgument);
                if (packagePaths == null || packagePaths.Length < 1)
                {
                    throw new ArgumentException(Strings.Push_MissingArguments);
                }

                string? sourcePath = parseResult.GetValue(SourceOption);
                string? apiKeyValue = parseResult.GetValue(ApiKeyOption);
                string? symbolSourcePath = parseResult.GetValue(SymbolSourceOption);
                string? symbolApiKeyValue = parseResult.GetValue(SymbolApiKeyOption);
                bool disableBufferingValue = parseResult.GetValue(DisableBufferingOption);
                bool noSymbolsValue = parseResult.GetValue(NoSymbolsOption);
                bool noServiceEndpoint = parseResult.GetValue(NoServiceEndpointOption);
                bool skipDuplicateValue = parseResult.GetValue(SkipDuplicateOption);
                bool allowInsecureConnectionsValue = parseResult.GetValue(AllowInsecureConnectionsOption);
                bool interactiveValue = parseResult.GetValue(InteractiveOption);
                string? timeoutValue = parseResult.GetValue(TimeoutOption);
                string? configFile = parseResult.GetValue(ConfigFileOption);
                int timeoutSeconds = 0;

                if (!string.IsNullOrEmpty(timeoutValue) && !int.TryParse(timeoutValue, out timeoutSeconds))
                {
                    throw new ArgumentException(Strings.Push_InvalidTimeout);
                }

#pragma warning disable CS0618 // Type or member is obsolete
                var sourceProvider = new PackageSourceProvider(XPlatUtility.ProcessConfigFile(configFile!), enablePackageSourcesChangedEvent: false);
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
                        noServiceEndpoint,
                        skipDuplicateValue,
                        allowInsecureConnectionsValue,
                        getLogger());
                }
                catch (TaskCanceledException ex)
                {
                    throw new AggregateException(ex, new Exception(Strings.Push_Timeout_Error));
                }

                return 0;
            });

            parent.Subcommands.Add(pushCmd);
        }
    }
}
