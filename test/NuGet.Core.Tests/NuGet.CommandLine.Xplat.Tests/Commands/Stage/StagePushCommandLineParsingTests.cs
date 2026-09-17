// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.CommandLine;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.CommandLine.XPlat;
using NuGet.CommandLine.XPlat.Commands.Stage;
using Xunit;

namespace NuGet.CommandLine.Xplat.Tests.Commands.Stage
{
    public class StagePushCommandLineParsingTests
    {
        public sealed record ExpectedStagePushArguments(
            string PackagePath,
            string? Source = null,
            string? ApiKey = null,
            string? GroupId = null,
            bool NoSymbols = false,
            string? ConfigFile = null,
            bool Interactive = false,
            bool AllowInsecureConnections = false);

        public static TheoryData<string, ExpectedStagePushArguments> ValidArgumentsTestData => new()
        {
            {
                "nuget stage push package.nupkg",
                new ExpectedStagePushArguments(PackagePath: "package.nupkg")
            },
            {
                "nuget stage push package.nupkg --source source",
                new ExpectedStagePushArguments(
                    PackagePath: "package.nupkg",
                    Source: "source")
            },
            {
                "nuget stage push -s source package.nupkg",
                new ExpectedStagePushArguments(
                    PackagePath: "package.nupkg",
                    Source: "source")
            },
            {
                "nuget stage push package.nupkg --api-key key",
                new ExpectedStagePushArguments(
                    PackagePath: "package.nupkg",
                    ApiKey: "key")
            },
            {
                "nuget stage push package.nupkg -k key",
                new ExpectedStagePushArguments(
                    PackagePath: "package.nupkg",
                    ApiKey: "key")
            },
            {
                "nuget stage push package.nupkg --group release-group",
                new ExpectedStagePushArguments(
                    PackagePath: "package.nupkg",
                    GroupId: "release-group")
            },
            {
                "nuget stage push package.nupkg --no-symbols",
                new ExpectedStagePushArguments(
                    PackagePath: "package.nupkg",
                    NoSymbols: true)
            },
            {
                "nuget stage push package.nupkg --configfile NuGet.Config",
                new ExpectedStagePushArguments(
                    PackagePath: "package.nupkg",
                    ConfigFile: "NuGet.Config")
            },
            {
                "nuget stage push package.nupkg --interactive --allow-insecure-connections",
                new ExpectedStagePushArguments(
                    PackagePath: "package.nupkg",
                    Interactive: true,
                    AllowInsecureConnections: true)
            },
            {
                "nuget stage push \"path with spaces\\package.nupkg\" --group \"release group\"",
                new ExpectedStagePushArguments(
                    PackagePath: "path with spaces\\package.nupkg",
                    GroupId: "release group")
            },
            {
                "nuget stage push artifacts\\Contoso.1.0.0.nupkg -s staging-source -k secret --group release-group --no-symbols --configfile config\\NuGet.Config --interactive --allow-insecure-connections",
                new ExpectedStagePushArguments(
                    PackagePath: "artifacts\\Contoso.1.0.0.nupkg",
                    Source: "staging-source",
                    ApiKey: "secret",
                    GroupId: "release-group",
                    NoSymbols: true,
                    ConfigFile: "config\\NuGet.Config",
                    Interactive: true,
                    AllowInsecureConnections: true)
            },
        };

        public static TheoryData<string> InvalidArgumentsTestData => new()
        {
            "nuget stage push",
            "nuget stage push one.nupkg two.nupkg",
            "nuget stage push package.nupkg --source",
            "nuget stage push package.nupkg --api-key",
            "nuget stage push package.nupkg --group",
            "nuget stage push package.nupkg --configfile",
            "nuget stage push package.nupkg --no-symbols true",
            "nuget stage push package.nupkg --unknown",
        };

        [Theory]
        [MemberData(nameof(ValidArgumentsTestData))]
        public void PushCommand_WithValidArguments_SetsExpectedValues(
            string commandLine,
            ExpectedStagePushArguments expected)
        {
            // Arrange
            Command rootCommand = new("nuget");
            bool actionInvoked = false;
            RegisterPushCommand(rootCommand, args =>
            {
                actionInvoked = true;
                args.PackagePath.Should().Be(expected.PackagePath);
                args.Source.Should().Be(expected.Source);
                args.ApiKey.Should().Be(expected.ApiKey);
                args.GroupId.Should().Be(expected.GroupId);
                args.NoSymbols.Should().Be(expected.NoSymbols);
                args.ConfigFile.Should().Be(expected.ConfigFile);
                args.Interactive.Should().Be(expected.Interactive);
                args.AllowInsecureConnections.Should().Be(expected.AllowInsecureConnections);
                return Task.FromResult(0);
            });

            // Act
            ParseResult result = rootCommand.Parse(commandLine);
            result.Invoke();

            // Assert
            result.Errors.Should().BeEmpty();
            actionInvoked.Should().BeTrue();
        }

        [Theory]
        [MemberData(nameof(InvalidArgumentsTestData))]
        public void PushCommand_WithInvalidArguments_HasParseError(string commandLine)
        {
            // Arrange
            Command rootCommand = new("nuget");
            bool actionInvoked = false;
            RegisterPushCommand(rootCommand, _ =>
            {
                actionInvoked = true;
                return Task.FromResult(0);
            });

            // Act
            ParseResult result = rootCommand.Parse(commandLine);

            // Assert
            result.Errors.Should().NotBeEmpty();
            actionInvoked.Should().BeFalse();
        }

        [Fact]
        public void NuGetCommandsAdd_RegistersStageWithProvidedInteractiveOption()
        {
            // Arrange
            RootCommand rootCommand = new();
            var nugetCommand = new Command("nuget");
            rootCommand.Subcommands.Add(nugetCommand);
            var interactiveOption = new Option<bool>("--interactive")
            {
                DefaultValueFactory = _ => true,
            };

            // Act
            NuGetCommands.Add(
                rootCommand,
                interactiveOption,
                virtualProjectBuilder: null);
            ParseResult result = rootCommand.Parse("nuget stage push package.nupkg");

            // Assert
            result.Errors.Should().BeEmpty();
            nugetCommand.Subcommands.Should().ContainSingle();
            nugetCommand.Subcommands[0].Name.Should().Be("stage");
            nugetCommand.Subcommands[0].Subcommands
                .Should().ContainSingle(command => command.Name == "push");
            result.GetValue(interactiveOption).Should().BeTrue();
        }

        private static void RegisterPushCommand(
            Command rootCommand,
            Func<StagePushCommandArgs, Task<int>> action)
        {
            var stageCommand = new Command("stage");
            StagePushCommand.Register(
                stageCommand,
                new Option<bool>("--interactive"),
                NullLoggerWithColor.GetInstance,
                action);
            rootCommand.Subcommands.Add(stageCommand);
        }
    }
}
