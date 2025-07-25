// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System.CommandLine;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.CommandLine.XPlat.Commands.Package.Update;
using NuGet.Common;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Xplat.Tests.Commands.Package.Update;

public class CliParserTests
{
    [Fact]
    public async Task NoArguments_ShouldUpdateAllPackages()
    {
        // Arrange
        string args = "package update";

        // Act
        var result = await RunAsync(args);

        // Assert
        result.ParseResult.Errors.Count.Should().Be(0);
        result.CommandArgs.Should().NotBeNull();
        result.CommandArgs!.Packages.Should().BeEmpty();
        result.CommandArgs.Interactive.Should().BeFalse();
        result.CommandArgs.LogLevel.Should().Be(LogLevel.Information);
    }

    [Fact]
    public async Task WithSinglePackage_ShouldParsePackageId()
    {
        // Arrange
        string args = "package update Contoso.Utils";

        // Act
        var result = await RunAsync(args);

        // Assert
        result.ParseResult.Errors.Count.Should().Be(0);
        result.CommandArgs.Should().NotBeNull();
        result.CommandArgs!.Packages.Should().HaveCount(1);
        result.CommandArgs.Packages[0].Id.Should().Be("Contoso.Utils");
        result.CommandArgs.Packages[0].VersionRange.Should().BeNull();
    }

    [Fact]
    public async Task WithMultiplePackages_ShouldParseAllPackageIds()
    {
        // Arrange
        string args = "package update Contoso.Utils Contoso.Framework";

        // Act
        var result = await RunAsync(args);

        // Assert
        result.ParseResult.Errors.Count.Should().Be(0);
        result.CommandArgs.Should().NotBeNull();
        result.CommandArgs!.Packages.Should().HaveCount(2);
        result.CommandArgs.Packages[0].Id.Should().Be("Contoso.Utils");
        result.CommandArgs.Packages[0].VersionRange.Should().BeNull();
        result.CommandArgs.Packages[1].Id.Should().Be("Contoso.Framework");
        result.CommandArgs.Packages[1].VersionRange.Should().BeNull();
    }

    [Fact]
    public async Task WithPackageAndVersion_ShouldParsePackageWithVersionRange()
    {
        // Arrange
        string args = "package update Contoso.Utils@2.1.0";

        // Act
        var result = await RunAsync(args);

        // Assert
        result.ParseResult.Errors.Count.Should().Be(0);
        result.CommandArgs.Should().NotBeNull();
        result.CommandArgs!.Packages.Should().HaveCount(1);
        result.CommandArgs.Packages[0].Id.Should().Be("Contoso.Utils");
        result.CommandArgs.Packages[0].VersionRange.Should().NotBeNull();
        result.CommandArgs.Packages[0].VersionRange!.ToString().Should().Be("[2.1.0, )");
    }

    [Fact]
    public async Task WithPackageAndVersionRange_ShouldParsePackageWithVersionRange()
    {
        // Arrange
        string args = "package update Contoso.Utils@[2.0.0,3.0.0)";

        // Act
        var result = await RunAsync(args);

        // Assert
        result.ParseResult.Errors.Count.Should().Be(0);
        result.CommandArgs.Should().NotBeNull();
        result.CommandArgs!.Packages.Should().HaveCount(1);
        result.CommandArgs.Packages[0].Id.Should().Be("Contoso.Utils");
        result.CommandArgs.Packages[0].VersionRange.Should().NotBeNull();
        result.CommandArgs.Packages[0].VersionRange!.ToString().Should().Be("[2.0.0, 3.0.0)");
    }

    [Fact]
    public async Task WithMixedPackages_ShouldParseMixedPackagesCorrectly()
    {
        // Arrange
        string args = "package update Contoso.Utils@2.1.0 Contoso.Framework";

        // Act
        var result = await RunAsync(args);

        // Assert
        result.ParseResult.Errors.Count.Should().Be(0);
        result.CommandArgs.Should().NotBeNull();
        result.CommandArgs!.Packages.Should().HaveCount(2);
        result.CommandArgs.Packages[0].Id.Should().Be("Contoso.Utils");
        result.CommandArgs.Packages[0].VersionRange.Should().NotBeNull();
        result.CommandArgs.Packages[1].Id.Should().Be("Contoso.Framework");
        result.CommandArgs.Packages[1].VersionRange.Should().BeNull();
    }

    [Fact]
    public async Task WithProjectOption_ShouldSetProjectPath()
    {
        // Arrange
        using var pathContext = new SimpleTestPathContext();
        string projectPath = Path.Combine(pathContext.WorkingDirectory, "test.csproj");
        File.WriteAllText(projectPath, "<Project />");

        string args = $"package update --project \"{projectPath}\"";

        // Act
        var result = await RunAsync(args);

        // Assert
        result.ParseResult.Errors.Count.Should().Be(0);
        result.CommandArgs.Should().NotBeNull();
        result.CommandArgs!.Project.Should().Be(projectPath);
    }

    [Fact]
    public async Task WithProjectThatDoesNotExist_ShouldHaveError()
    {
        // Arrange
        using var pathContext = new SimpleTestPathContext();
        string projectPath = Path.Combine(pathContext.WorkingDirectory, "test.csproj");

        string args = $"package update --project \"{projectPath}\"";

        // Act
        var result = await RunAsync(args);

        // Assert
        result.ParseResult.Errors.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task WithInteractiveOption_ShouldSetInteractiveToTrue()
    {
        // Arrange
        string args = "package update --interactive";

        // Act
        var result = await RunAsync(args);

        // Assert
        result.ParseResult.Errors.Count.Should().Be(0);
        result.CommandArgs.Should().NotBeNull();
        result.CommandArgs!.Interactive.Should().BeTrue();
    }

    [Theory]
    [InlineData("--verbosity quiet", LogLevel.Warning)]
    [InlineData("--verbosity q", LogLevel.Warning)]
    [InlineData("--verbosity minimal", LogLevel.Minimal)]
    [InlineData("--verbosity m", LogLevel.Minimal)]
    [InlineData("--verbosity normal", LogLevel.Information)]
    [InlineData("--verbosity n", LogLevel.Information)]
    [InlineData("--verbosity detailed", LogLevel.Verbose)]
    [InlineData("--verbosity d", LogLevel.Verbose)]
    [InlineData("--verbosity diagnostic", LogLevel.Debug)]
    [InlineData("--verbosity diag", LogLevel.Debug)]
    [InlineData("-v quiet", LogLevel.Warning)]
    [InlineData("-v minimal", LogLevel.Minimal)]
    [InlineData("-v normal", LogLevel.Information)]
    [InlineData("-v detailed", LogLevel.Verbose)]
    [InlineData("-v diagnostic", LogLevel.Debug)]
    public async Task WithVerbosityOption_ShouldSetCorrectLogLevel(string verbosityArgs, LogLevel expectedLogLevel)
    {
        // Arrange
        string args = $"package update {verbosityArgs}";

        // Act
        var result = await RunAsync(args);

        // Assert
        result.ParseResult.Errors.Count.Should().Be(0);
        result.CommandArgs.Should().NotBeNull();
        result.CommandArgs!.LogLevel.Should().Be(expectedLogLevel);
    }

    [Fact]
    public async Task WithAllOptions_ShouldParseAllOptionsCorrectly()
    {
        // Arrange
        using var pathContext = new SimpleTestPathContext();
        string projectPath = Path.Combine(pathContext.WorkingDirectory, "test.csproj");
        File.WriteAllText(projectPath, "<Project />");

        string args = $"package update Contoso.Utils@2.1.0 --project \"{projectPath}\" --interactive --verbosity detailed";

        // Act
        var result = await RunAsync(args);

        // Assert
        result.ParseResult.Errors.Count.Should().Be(0);
        result.CommandArgs.Should().NotBeNull();
        result.CommandArgs!.Packages.Should().HaveCount(1);
        result.CommandArgs.Packages[0].Id.Should().Be("Contoso.Utils");
        result.CommandArgs.Packages[0].VersionRange.Should().NotBeNull();
        result.CommandArgs.Project.Should().Be(projectPath);
        result.CommandArgs.Interactive.Should().BeTrue();
        result.CommandArgs.LogLevel.Should().Be(LogLevel.Verbose);
    }

    [Fact]
    public async Task WithInvalidVersionRange_ShouldHaveParseErrors()
    {
        // Arrange
        string args = "package update Contoso.Utils@invalid-version";

        // Act
        var result = await RunAsync(args);

        // Assert
        result.ParseResult.Errors.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task WithEmptyVersionAfterAt_ShouldHaveParseErrors()
    {
        // Arrange
        string args = "package update Contoso.Utils@";

        // Act
        var result = await RunAsync(args);

        // Assert
        result.ParseResult.Errors.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task WithNonExistentProject_ShouldHaveParseErrors()
    {
        // Arrange
        string args = "package update --project non-existent-file.csproj";

        // Act
        var result = await RunAsync(args);

        // Assert
        result.ParseResult.Errors.Count.Should().BeGreaterThan(0);
    }

    private async Task<(ParseResult ParseResult, PackageUpdateArgs? CommandArgs)> RunAsync(string commandLine)
    {
        RootCommand rootCommand = new RootCommand();

        var packageCommand = new Command("package");
        rootCommand.Subcommands.Add(packageCommand);

        var interactiveOption = new Option<bool>("--interactive");

        PackageUpdateArgs? commandArgs = null;

        PackageUpdateCommand.Register(packageCommand, interactiveOption, (packageUpdateArgs, _) =>
        {
            commandArgs = packageUpdateArgs;
            return Task.FromResult(0);
        });

        var parser = rootCommand.Parse(commandLine);
        if (parser.Errors.Count == 0)
        {
            await parser.InvokeAsync();
        }
        return (parser, commandArgs);
    }
}
