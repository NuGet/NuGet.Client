// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.CommandLine;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.CommandLine.XPlat.Commands.Package.PackageDownload;
using NuGet.Common;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Xplat.Tests.Commands.Package.Download
{
    public class PackageDownloadCommandTests
    {
        [Fact]
        public async Task NoArguments_HasDefaultOptions()
        {
            // Arrange
            string args = "package download";

            // Act
            var result = await RunAsync(args);

            // Assert
            result.Should().NotBeNull();
            result.Packages.Should().BeEmpty();
            result.Sources.Should().BeEmpty();
            result.OutputDirectory.Should().BeNull();
            result.ConfigFile.Should().BeNull();
            result.IncludePrerelease.Should().BeFalse();
            result.DownloadOnly.Should().BeFalse();
            result.AllowInsecureConnections.Should().BeFalse();
            result.Interactive.Should().BeFalse();
            result.LogLevel.Should().Be(LogLevel.Information);
        }

        [Fact]
        public async Task WithSinglePackage_ShouldParsePackageId()
        {
            // Arrange
            string args = "package download Contoso.Utils";

            // Act
            var result = await RunAsync(args);

            // Assert
            result.Should().NotBeNull();
            result.Packages.Should().HaveCount(1);
            result.Packages[0].Id.Should().Be("Contoso.Utils");
            result.Packages[0].VersionRange.Should().BeNull();
        }

        [Fact]
        public async Task WithMultiplePackages_ShouldParseAllPackageIds()
        {
            // Arrange
            string args = "package download Contoso.Utils Contoso.Framework";

            // Act
            var result = await RunAsync(args);

            // Assert
            result.Should().NotBeNull();
            result.Packages.Should().HaveCount(2);
            result.Packages[0].Id.Should().Be("Contoso.Utils");
            result.Packages[0].VersionRange.Should().BeNull();
            result.Packages[1].Id.Should().Be("Contoso.Framework");
            result.Packages[1].VersionRange.Should().BeNull();
        }

        [Fact]
        public async Task WithPackageAndVersion_ShouldParsePackageWithVersionRange()
        {
            // Arrange
            string args = "package download Contoso.Utils@2.1.0";

            // Act
            var result = await RunAsync(args);

            // Assert
            result.Should().NotBeNull();
            result.Packages.Should().HaveCount(1);
            result.Packages[0].Id.Should().Be("Contoso.Utils");
            result.Packages[0].VersionRange.Should().NotBeNull();
            result.Packages[0].VersionRange!.ToString().Should().Be("[2.1.0, )");
        }

        [Fact]
        public async Task WithPackageAndVersionRange_ShouldParsePackageWithVersionRange()
        {
            // Arrange
            string args = "package download Contoso.Utils@[2.0.0,3.0.0)";

            // Act
            var result = await RunAsync(args);

            // Assert
            result.Should().NotBeNull();
            result.Packages.Should().HaveCount(1);
            result.Packages[0].Id.Should().Be("Contoso.Utils");
            result.Packages[0].VersionRange.Should().NotBeNull();
            result.Packages[0].VersionRange!.ToString().Should().Be("[2.0.0, 3.0.0)");
        }

        [Fact]
        public async Task WithMixedPackages_ShouldParseMixedPackagesCorrectly()
        {
            // Arrange
            string args = "package download Contoso.Utils@2.1.0 Contoso.Framework";

            // Act
            var result = await RunAsync(args);

            // Assert
            result.Should().NotBeNull();
            result.Packages.Should().HaveCount(2);
            result.Packages[0].Id.Should().Be("Contoso.Utils");
            result.Packages[0].VersionRange.Should().NotBeNull();
            result.Packages[1].Id.Should().Be("Contoso.Framework");
            result.Packages[1].VersionRange.Should().BeNull();
        }

        [Fact]
        public async Task WithOutputAndConfig_ShouldBindPaths()
        {
            // Arrange
            using var pathContext = new SimpleTestPathContext();
            string outDir = Path.Combine(pathContext.WorkingDirectory, "out");
            string cfg = Path.Combine(pathContext.WorkingDirectory, "nuget.config");

            string args = $"package download --output-directory \"{outDir}\" --configfile \"{cfg}\"";

            // Act
            var result = await RunAsync(args);

            // Assert
            result.Should().NotBeNull();
            result.OutputDirectory.Should().Be(outDir);
            result.ConfigFile.Should().Be(cfg);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task WithInteractiveOption_ShouldSetCorrectInteractiveValue(bool value)
        {
            // Arrange
            string args = $"package download --interactive:{value}";

            // Act
            var result = await RunAsync(args);

            // Assert
            result.Should().NotBeNull();
            result.Interactive.Should().Be(value);
        }

        [Fact]
        public async Task WithBooleanFlags_ShouldSetAllFlagsTrue()
        {
            // Arrange
            string args = "package download --prerelease --download-only --allow-insecure-connections --interactive";

            // Act
            var result = await RunAsync(args);

            // Assert
            result.IncludePrerelease.Should().BeTrue();
            result.DownloadOnly.Should().BeTrue();
            result.AllowInsecureConnections.Should().BeTrue();
            result.Interactive.Should().BeTrue();
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
            string args = $"package download {verbosityArgs}";

            // Act
            var result = await RunAsync(args);

            // Assert
            result.Should().NotBeNull();
            result.LogLevel.Should().Be(expectedLogLevel);
        }

        [Fact]
        public void WithInvalidVersionRange_ShouldHaveParseErrors()
        {
            // Arrange
            string args = "package download Contoso.Utils@invalid-version";

            // Act
            var result = Parse(args);

            // Assert
            result.Errors.Count.Should().BeGreaterThan(0);
        }

        [Fact]
        public void WithEmptyVersionAfterAt_ShouldHaveParseErrors()
        {
            // Arrange
            string args = "package download Contoso.Utils@";

            // Act
            var result = Parse(args);

            // Assert
            result.Errors.Count.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task WithAllOptions_ShouldParseAllOptionsCorrectly()
        {
            // Arrange
            using var pathContext = new SimpleTestPathContext();
            string outDir = Path.Combine(pathContext.WorkingDirectory, "out");
            string cfg = Path.Combine(pathContext.WorkingDirectory, "nuget.config");

            string args = $"package download Contoso.Utils@2.1.0 --output-directory \"{outDir}\" --configfile \"{cfg}\" --prerelease --download-only --allow-insecure-connections --source s1 --source s2 --verbosity detailed --interactive";

            // Act
            var result = await RunAsync(args);

            // Assert
            result.Should().NotBeNull();
            result.Packages.Should().HaveCount(1);
            result.Packages[0].Id.Should().Be("Contoso.Utils");
            result.Packages[0].VersionRange.Should().NotBeNull();
            result.OutputDirectory.Should().Be(outDir);
            result.ConfigFile.Should().Be(cfg);
            result.IncludePrerelease.Should().BeTrue();
            result.DownloadOnly.Should().BeTrue();
            result.AllowInsecureConnections.Should().BeTrue();
            result.Sources.Should().ContainInOrder("s1", "s2");
            result.LogLevel.Should().Be(LogLevel.Verbose);
            result.Interactive.Should().BeTrue();
        }

        private ParseResult Parse(string commandLine, Func<PackageDownloadArgs, CancellationToken, Task<int>> action = null)
        {
            RootCommand rootCommand = new RootCommand();

            var packageCommand = new Command("package");
            rootCommand.Subcommands.Add(packageCommand);

            // Simulate SDK-provided interactive option
            var interactiveOption = new Option<bool>("--interactive");

            if (action == null)
            {
                action = (_, _) => throw new NotImplementedException("No action provided for command execution.");
            }

            PackageDownloadCommand.Register(packageCommand, interactiveOption, action);

            var parser = rootCommand.Parse(commandLine);
            return parser;
        }

        private async Task<PackageDownloadArgs> RunAsync(string commandLine)
        {
            PackageDownloadArgs commandArgs = null;

            var parseResult = Parse(commandLine, (args, cancellationToken) =>
            {
                commandArgs = args;
                return Task.FromResult(0);
            });

            using var output = new StringWriter();
            var commandLineConfiguration = new InvocationConfiguration
            {
                Output = output,
                Error = output,
            };

            await parseResult.InvokeAsync(commandLineConfiguration);

            if (commandArgs is null)
            {
                throw new InvalidOperationException("Command arguments were not set during command execution. Output:" + output.ToString());
            }

            return commandArgs;
        }
    }
}
