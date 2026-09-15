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
        public void StageCommand_ContainsPushCommand()
        {
            Command nugetCommand = new("nuget");

            StageCommand.Register(nugetCommand, _ => Task.FromResult(0));

            nugetCommand.Subcommands.Should().ContainSingle(command => command.Name == "stage");
            nugetCommand.Subcommands[0].Subcommands.Should().ContainSingle(command => command.Name == "push");
        }

        [Fact]
        public void StageCommand_HasDocumentationUrl()
        {
            Command nugetCommand = new("nuget");

            StageCommand.Register(nugetCommand, _ => Task.FromResult(0));

            DocumentedCommand stageCommand = nugetCommand.Subcommands[0].Should().BeOfType<DocumentedCommand>().Subject;
            stageCommand.HelpUrl.Should().NotBeNullOrEmpty();
            stageCommand.Subcommands[0].Should().BeOfType<DocumentedCommand>()
                .Which.HelpUrl.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void PushCommand_RequiresExactlyOnePackagePath()
        {
            RootCommand rootCommand = CreateCommand(_ => Task.FromResult(0));

            rootCommand.Parse("nuget stage push").Errors.Should().NotBeEmpty();
            rootCommand.Parse("nuget stage push one.nupkg two.nupkg").Errors.Should().NotBeEmpty();
        }

        [Theory]
        [InlineData("-s")]
        [InlineData("--source")]
        public void SourceOption_BindsShortAndLongForms(string option)
        {
            StagePushCommandArgs? capturedArgs = null;
            RootCommand rootCommand = CreateCommand(args =>
            {
                capturedArgs = args;
                return Task.FromResult(0);
            });

            ParseResult result = rootCommand.Parse($"nuget stage push package.nupkg {option} source");
            result.Errors.Should().BeEmpty();
            result.Invoke();

            capturedArgs!.Source.Should().Be("source");
        }

        [Theory]
        [InlineData("-k")]
        [InlineData("--api-key")]
        public void ApiKeyOption_BindsShortAndLongForms(string option)
        {
            StagePushCommandArgs? capturedArgs = null;
            RootCommand rootCommand = CreateCommand(args =>
            {
                capturedArgs = args;
                return Task.FromResult(0);
            });

            ParseResult result = rootCommand.Parse($"nuget stage push package.nupkg {option} key");
            result.Errors.Should().BeEmpty();
            result.Invoke();

            capturedArgs!.ApiKey.Should().Be("key");
        }

        [Fact]
        public void SupportedOptions_AreMappedToArguments()
        {
            StagePushCommandArgs? capturedArgs = null;
            RootCommand rootCommand = CreateCommand(args =>
            {
                capturedArgs = args;
                return Task.FromResult(0);
            });

            ParseResult result = rootCommand.Parse(
                "nuget stage push package.nupkg --group group --no-symbols --configfile NuGet.Config --interactive --allow-insecure-connections");
            result.Errors.Should().BeEmpty();
            result.Invoke();

            capturedArgs!.PackagePath.Should().Be("package.nupkg");
            capturedArgs.GroupId.Should().Be("group");
            capturedArgs.NoSymbols.Should().BeTrue();
            capturedArgs.ConfigFile.Should().Be("NuGet.Config");
            capturedArgs.Interactive.Should().BeTrue();
            capturedArgs.AllowInsecureConnections.Should().BeTrue();
        }

        [Fact]
        public void GroupOption_RequiresValue()
        {
            RootCommand rootCommand = CreateCommand(_ => Task.FromResult(0));

            rootCommand.Parse("nuget stage push package.nupkg --group").Errors.Should().NotBeEmpty();
        }

        [Fact]
        public void HelpOption_DoesNotInvokeAction()
        {
            RootCommand rootCommand = CreateCommand(_ => throw new InvalidOperationException("Action should not be invoked."));

            ParseResult result = rootCommand.Parse("nuget stage push --help");

            result.Errors.Should().BeEmpty();
            result.Action.Should().BeOfType<System.CommandLine.Help.HelpAction>();
        }

        private static RootCommand CreateCommand(Func<StagePushCommandArgs, Task<int>> action)
        {
            var rootCommand = new RootCommand();
            var nugetCommand = new Command("nuget");
            rootCommand.Subcommands.Add(nugetCommand);
            StageCommand.Register(nugetCommand, action);
            return rootCommand;
        }
    }
}
