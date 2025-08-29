// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.CommandLine.Xplat.Tests;

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NuGet.CommandLine.XPlat;
using NuGet.CommandLine.XPlat.Commands.PackageDownload;
using NuGet.Common;
using NuGet.Test.Utility;
using Xunit;

public class PackageDownloadRunnerTests
{
    [Fact]
    public async Task RunAsync_ExplicitVersionFromLocalFolderSource_SucceedsAsync()
    {
        using var context = new SimpleTestPathContext();

        // Arrange
        var sourceDir = Path.Combine(context.WorkingDirectory, "src");
        var outputDir = Path.Combine(context.WorkingDirectory, "packages");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(outputDir);

        var id = "Contoso.Lib";
        var version = "1.2.3";
        await SimpleTestPackageUtility.CreateFullPackageAsync(sourceDir, id, version);

        var logger = new Mock<ILogger>(MockBehavior.Loose);

        var args = new PackageDownloadArgs(
            packageId: id,
            sources: new[] { sourceDir }.ToList(),
            outputDirectory: outputDir,
            logger: logger.Object)
        {
            Version = version,
            ConfigFile = context.NuGetConfig,
            AllowInsecureConnections = true,
            Verbosity = Verbosity.Detailed
        };


        // Act
        var result = await PackageDownloadRunner.RunAsync(args, System.Threading.CancellationToken.None);

        // Assert
        result.Should().Be(ExitCodes.Success);

        var installDir = Path.Combine(outputDir, id.ToLowerInvariant(), version);
        Directory.Exists(installDir).Should().BeTrue();
        Directory.EnumerateFiles(installDir, "*.nupkg").Any().Should().BeTrue();
        File.Exists(Path.Combine(installDir, $"{id.ToLowerInvariant()}.{version}.nupkg")).Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_NoVersionWhenPrereleaseNotIncluded_PicksLatestStable()
    {
        using var context = new SimpleTestPathContext();

        // Arrange
        var sourceDir = Path.Combine(context.WorkingDirectory, "src");
        var outputDir = Path.Combine(context.WorkingDirectory, "packages");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(outputDir);

        var id = "Contoso.Core";
        await SimpleTestPackageUtility.CreateFullPackageAsync(sourceDir, id, "1.0.0");
        await SimpleTestPackageUtility.CreateFullPackageAsync(sourceDir, id, "1.1.0");
        await SimpleTestPackageUtility.CreateFullPackageAsync(sourceDir, id, "2.0.0-beta"); // prerelease

        var logger = new Mock<ILogger>(MockBehavior.Loose);

        var args = new PackageDownloadArgs(
            packageId: id,
            sources: new[] { sourceDir }.ToList(),
            outputDirectory: outputDir,
            logger: logger.Object);

        // Act
        var result = await PackageDownloadRunner.RunAsync(args, System.Threading.CancellationToken.None);

        // Assert
        result.Should().Be(ExitCodes.Success);

        var chosen = Path.Combine(outputDir, id.ToLowerInvariant(), "1.1.0");
        var notChosen = Path.Combine(outputDir, id.ToLowerInvariant(), "2.0.0-beta");
        Directory.Exists(chosen).Should().BeTrue("latest stable (1.1.0) should be chosen");
        Directory.Exists(notChosen).Should().BeFalse("prerelease (2.0.0-beta) should not be chosen");
        File.Exists(Path.Combine(chosen, $"{id.ToLowerInvariant()}.1.1.0.nupkg")).Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_NoVersionWithPrereleaseTrue_PicksHighestIncludingPrerelease()
    {
        using var context = new SimpleTestPathContext();

        // Arrange
        var sourceDir = Path.Combine(context.WorkingDirectory, "src");
        var outputDir = Path.Combine(context.WorkingDirectory, "packages");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(outputDir);

        var id = "Contoso.Preview";
        await SimpleTestPackageUtility.CreateFullPackageAsync(sourceDir, id, "1.3.0");
        await SimpleTestPackageUtility.CreateFullPackageAsync(sourceDir, id, "2.0.0-beta.2");

        var logger = new Mock<ILogger>(MockBehavior.Loose);

        var args = new PackageDownloadArgs(
            packageId: id,
            sources: new[] { sourceDir }.ToList(),
            outputDirectory: outputDir,
            logger: logger.Object)
        {
            IncludePrerelease = true
        };

        // Act
        var result = await PackageDownloadRunner.RunAsync(args, System.Threading.CancellationToken.None);

        // Assert
        result.Should().Be(ExitCodes.Success);

        var chosen = Path.Combine(outputDir, id.ToLowerInvariant(), "2.0.0-beta.2");
        Directory.Exists(chosen).Should().BeTrue("IncludePrerelease=true should allow picking 2.0.0-beta.2");
        File.Exists(Path.Combine(chosen, $"{id.ToLowerInvariant()}.2.0.0-beta.2.nupkg")).Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_NoVersion_PicksHighestAcrossMultipleSources()
    {
        using var context = new SimpleTestPathContext();

        // Arrange
        var srcA = Path.Combine(context.WorkingDirectory, "srcA");
        var srcB = Path.Combine(context.WorkingDirectory, "srcB");
        var outputDir = Path.Combine(context.WorkingDirectory, "packages");
        Directory.CreateDirectory(srcA);
        Directory.CreateDirectory(srcB);
        Directory.CreateDirectory(outputDir);

        var id = "Contoso.Toolkit";
        await SimpleTestPackageUtility.CreateFullPackageAsync(srcA, id, "1.1.0");
        await SimpleTestPackageUtility.CreateFullPackageAsync(srcB, id, "1.2.0");

        var logger = new Mock<ILogger>(MockBehavior.Loose);

        var args = new PackageDownloadArgs(
            packageId: id,
            sources: [srcA, srcB],
            outputDirectory: outputDir,
            logger: logger.Object);

        // Act
        var result = await PackageDownloadRunner.RunAsync(args, System.Threading.CancellationToken.None);

        // Assert
        result.Should().Be(ExitCodes.Success);

        var chosen = Path.Combine(outputDir, id.ToLowerInvariant(), "1.2.0");
        Directory.Exists(chosen).Should().BeTrue("should choose the highest version found across all sources");
        File.Exists(Path.Combine(chosen, $"{id.ToLowerInvariant()}.1.2.0.nupkg")).Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_ExplicitVersionAlreadyInstalled_ShortCircuitsAndSucceeds()
    {
        using var context = new SimpleTestPathContext();

        // Arrange
        var sourceDir = Path.Combine(context.WorkingDirectory, "src");
        var outputDir = Path.Combine(context.WorkingDirectory, "packages");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(outputDir);

        var id = "Contoso.Utils";
        var v = "3.4.5";
        await SimpleTestPackageUtility.CreateFullPackageAsync(sourceDir, id, v);

        // First run: install it
        var logger1 = new Mock<ILogger>(MockBehavior.Loose);
        var args1 = new PackageDownloadArgs(
            packageId: id,
            sources: [sourceDir],
            outputDirectory: outputDir,
            logger: logger1.Object)
        {
            Version = v
        };

        var first = await PackageDownloadRunner.RunAsync(args1, System.Threading.CancellationToken.None);
        first.Should().Be(ExitCodes.Success);

        // Second run: should short-circuit
        var logger2 = new Mock<ILogger>(MockBehavior.Loose);
        var args2 = new PackageDownloadArgs(
            packageId: id,
            sources: [sourceDir],
            outputDirectory: outputDir,
            logger: logger2.Object)
        {
            Version = v
        };

        // Act
        var second = await PackageDownloadRunner.RunAsync(args2, System.Threading.CancellationToken.None);

        // Assert
        second.Should().Be(ExitCodes.Success);

        var installDir = Path.Combine(outputDir, id.ToLowerInvariant(), v);
        Directory.Exists(installDir).Should().BeTrue();
        File.Exists(Path.Combine(installDir, $"{id.ToLowerInvariant()}.{v}.nupkg")).Should().BeTrue();
        logger2.Verify(l => l.LogMinimal(It.Is<string>(s =>
            s.Contains("Skipping", System.StringComparison.OrdinalIgnoreCase))), Times.AtLeastOnce);
    }

    [Fact]
    public async Task RunAsync_WhenAllowInsecureConnectionsFalse_RejectsHttpSource()
    {
        using var context = new SimpleTestPathContext();

        var outputDir = Path.Combine(context.WorkingDirectory, "packages");
        Directory.CreateDirectory(outputDir);

        var httpSource = "http://contoso/v3/index.json";
        var logger = new Mock<ILogger>(MockBehavior.Loose);

        var args = new PackageDownloadArgs(
            packageId: "Contoso.Lib",
            sources: [httpSource],
            outputDirectory: outputDir,
            logger: logger.Object)
        {
            ConfigFile = context.NuGetConfig,
            AllowInsecureConnections = false,
            Verbosity = Verbosity.Detailed
        };

        // Act
        var result = await PackageDownloadRunner.RunAsync(args, System.Threading.CancellationToken.None);

        // Assert
        result.Should().Be(ExitCodes.Error);
        logger.Verify(l => l.LogError(It.Is<string>(s =>
            s.Contains(httpSource, System.StringComparison.OrdinalIgnoreCase))), Times.AtLeastOnce);
    }

    [Fact]
    public async Task RunAsync_PackageDoesNotExist_ReturnsError()
    {
        using var context = new SimpleTestPathContext();

        // Arrange
        var srcA = Path.Combine(context.WorkingDirectory, "emptyA");
        var srcB = Path.Combine(context.WorkingDirectory, "emptyB");
        var outputDir = Path.Combine(context.WorkingDirectory, "packages");
        Directory.CreateDirectory(srcA);
        Directory.CreateDirectory(srcB);
        Directory.CreateDirectory(outputDir);

        var id = "Missing.Package";
        var v = "9.9.9";

        var logger = new Mock<ILogger>(MockBehavior.Loose);

        var args = new PackageDownloadArgs(
            packageId: id,
            sources: [srcA, srcB],
            outputDirectory: outputDir,
            logger: logger.Object)
        {
            Version = v,
            ConfigFile = context.NuGetConfig,
            AllowInsecureConnections = true,
            Verbosity = Verbosity.Detailed
        };

        // Act
        var result = await PackageDownloadRunner.RunAsync(args, System.Threading.CancellationToken.None);

        // Assert
        result.Should().Be(ExitCodes.Error);
        logger.Verify(l => l.LogError(It.IsAny<string>()), Times.AtLeastOnce);

        File.Exists(Path.Combine(outputDir, $"{id.ToLowerInvariant()}.{v}.nupkg")).Should().BeFalse("Package does not exist in sources");
    }
}
