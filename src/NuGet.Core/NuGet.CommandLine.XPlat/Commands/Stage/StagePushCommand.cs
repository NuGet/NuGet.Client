// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.CommandLine;
using System.Threading.Tasks;

namespace NuGet.CommandLine.XPlat.Commands.Stage
{
    internal static class StagePushCommand
    {
        internal static void Register(
            Command stageCommand,
            Func<StagePushCommandArgs, Task<int>> action)
        {
            var pushCommand = new DocumentedCommand(
                "push",
                Strings.StagePushCommand_Description,
                "https://aka.ms/dotnet/nuget/stage/push");

            var packagePathArgument = new Argument<string>("PACKAGE_PATH")
            {
                Arity = ArgumentArity.ExactlyOne,
                Description = Strings.StagePushCommand_PackagePathArgument_Description,
            };
            var sourceOption = new Option<string>("--source", "-s")
            {
                Arity = ArgumentArity.ZeroOrOne,
                Description = Strings.Source_Description,
            };
            var apiKeyOption = new Option<string>("--api-key", "-k")
            {
                Arity = ArgumentArity.ZeroOrOne,
                Description = Strings.ApiKey_Description,
            };
            var groupOption = new Option<string>("--group")
            {
                Arity = ArgumentArity.ExactlyOne,
                Description = Strings.StagePushCommand_GroupOption_Description,
            };
            var noSymbolsOption = new Option<bool>("--no-symbols")
            {
                Arity = ArgumentArity.Zero,
                Description = Strings.StagePushCommand_NoSymbolsOption_Description,
            };
            var configFileOption = new Option<string>("--configfile")
            {
                Arity = ArgumentArity.ZeroOrOne,
                Description = Strings.Option_ConfigFile,
            };
            var interactiveOption = new Option<bool>("--interactive")
            {
                Arity = ArgumentArity.Zero,
                Description = Strings.NuGetXplatCommand_Interactive,
            };
            var allowInsecureConnectionsOption = new Option<bool>("--allow-insecure-connections")
            {
                Arity = ArgumentArity.Zero,
                Description = Strings.AllowInsecureConnections_Description,
            };

            pushCommand.Arguments.Add(packagePathArgument);
            pushCommand.Options.Add(sourceOption);
            pushCommand.Options.Add(apiKeyOption);
            pushCommand.Options.Add(groupOption);
            pushCommand.Options.Add(noSymbolsOption);
            pushCommand.Options.Add(configFileOption);
            pushCommand.Options.Add(interactiveOption);
            pushCommand.Options.Add(allowInsecureConnectionsOption);

            pushCommand.SetAction((parseResult, cancellationToken) =>
            {
                var args = new StagePushCommandArgs
                {
                    PackagePath = parseResult.GetValue(packagePathArgument)!,
                    Source = parseResult.GetValue(sourceOption),
                    ApiKey = parseResult.GetValue(apiKeyOption),
                    GroupId = parseResult.GetValue(groupOption),
                    NoSymbols = parseResult.GetValue(noSymbolsOption),
                    ConfigFile = parseResult.GetValue(configFileOption),
                    Interactive = parseResult.GetValue(interactiveOption),
                    AllowInsecureConnections = parseResult.GetValue(allowInsecureConnectionsOption),
                    CancellationToken = cancellationToken,
                };

                return action(args);
            });

            stageCommand.Subcommands.Add(pushCommand);
        }
    }
}
