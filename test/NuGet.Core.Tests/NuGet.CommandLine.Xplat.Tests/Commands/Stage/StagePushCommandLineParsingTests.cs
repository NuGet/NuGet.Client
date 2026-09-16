// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.CommandLine;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.CommandLine.XPlat.Commands;
using NuGet.CommandLine.XPlat.Commands.Stage;
using Xunit;

namespace NuGet.CommandLine.Xplat.Tests.Commands.Stage
{
    public class StagePushCommandLineParsingTests
    {
        [Fact]
        public void StageCommand_HasHelpUrl()
        {
            Command rootCommand = new("nuget");

            StageCommand.Register(rootCommand, NullLoggerWithColor.GetInstance);

            rootCommand.Subcommands.Should().ContainSingle();
            rootCommand.Subcommands[0].Should().BeAssignableTo<DocumentedCommand>();
            ((DocumentedCommand)rootCommand.Subcommands[0]).HelpUrl.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void PushCommand_WithAllOptions_SetsArguments()
        {
            Command rootCommand = new("nuget");
            bool actionInvoked = false;

            RegisterPushCommand(rootCommand, args =>
            {
                actionInvoked = true;
                args.PackagePath.Should().Be(@"artifacts\Example.1.0.0.nupkg");
                args.Source.Should().Be("staging-source");
                args.ApiKey.Should().Be("secret");
                args.GroupId.Should().Be("release-group");
                args.NoSymbols.Should().BeTrue();
                args.ConfigFile.Should().Be(@"config\NuGet.Config");
                args.Interactive.Should().BeTrue();
                args.AllowInsecureConnections.Should().BeTrue();
                return Task.FromResult(0);
            });

            ParseResult result = rootCommand.Parse(
                @"nuget stage push artifacts\Example.1.0.0.nupkg -s staging-source -k secret --group release-group --no-symbols --configfile config\NuGet.Config --interactive --allow-insecure-connections");

            result.Errors.Should().BeEmpty();
            result.Invoke();
            actionInvoked.Should().BeTrue();
        }

        [Theory]
        [InlineData("-s", "-k")]
        [InlineData("--source", "--api-key")]
        public void PushCommand_SourceAndApiKeyAliases_AreAccepted(string sourceOption, string apiKeyOption)
        {
            Command rootCommand = new("nuget");

            RegisterPushCommand(rootCommand, args =>
            {
                args.Source.Should().Be("source");
                args.ApiKey.Should().Be("key");
                return Task.FromResult(0);
            });

            ParseResult result = rootCommand.Parse(
                $"nuget stage push package.nupkg {sourceOption} source {apiKeyOption} key");

            result.Errors.Should().BeEmpty();
            result.Invoke();
        }

        [Theory]
        [InlineData("nuget stage push")]
        [InlineData("nuget stage push one.nupkg two.nupkg")]
        [InlineData("nuget stage push package.nupkg --group")]
        public void PushCommand_InvalidArguments_HasParseError(string commandLine)
        {
            Command rootCommand = new("nuget");

            RegisterPushCommand(rootCommand, _ =>
                throw new InvalidOperationException("The action should not be invoked."));

            ParseResult result = rootCommand.Parse(commandLine);

            result.Errors.Should().NotBeEmpty();
        }

        [Fact]
        public void PushCommand_Help_DoesNotInvokeAction()
        {
            RootCommand rootCommand = new();
            Command nugetCommand = new("nuget");
            rootCommand.Subcommands.Add(nugetCommand);

            RegisterPushCommand(nugetCommand, _ =>
                throw new InvalidOperationException("The action should not be invoked."));

            ParseResult result = rootCommand.Parse("nuget stage push --help");

            result.Errors.Should().BeEmpty();
            result.Action.Should().BeOfType<System.CommandLine.Help.HelpAction>();
        }

        private static void RegisterPushCommand(
            Command rootCommand,
            Func<StagePushCommandArgs, Task<int>> action)
        {
            var stageCommand = new Command("stage");
            StagePushCommand.Register(stageCommand, NullLoggerWithColor.GetInstance, action);
            rootCommand.Subcommands.Add(stageCommand);
        }
    }
}
