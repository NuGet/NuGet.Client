// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.CommandLine.Xplat.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NuGet.CommandLine.XPlat;
using NuGet.CommandLine.XPlat.Commands.Package;
using NuGet.CommandLine.XPlat.Commands.Package.PackageDownload;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

public class PackageDownloadRunnerTests
{
    public static IEnumerable<object[]> PackageTestData()
    {
        // Basic stable explicit version
        yield return new object[]
        {
            new List<(string, string)>
            {
                ("Contoso.Core", "1.0.0"),
                ("Contoso.Core", "1.1.0"),
                ("Contoso.Core", "2.0.0-beta")
            },
            "Contoso.Core",                       // downloadId argument
            "1.1.0",                              // downloadVersion argument
            false,                               // enablePrerelease
            ("Contoso.Core", "1.1.0")            // expected
        };

        // Mixed casing on the ID in the *download* argument 
        yield return new object[]
        {
            new List<(string, string)>
            {
                ("Contoso.Core", "1.1.0")
            },
            "contoso.core",                 // downloadId argument
            "1.1.0",                        // downloadVersion argument
            false,                          // enablePrerelease
            ("Contoso.Core", "1.1.0")        // expected
        };

        // prerelease with IncludePrerelease == true
        yield return new object[]
        {
            new List<(string, string)>
            {
                ("Contoso.Preview", "1.3.0"),
                ("Contoso.Preview", "2.0.0-beta.2")
            },
            "Contoso.Preview",        // downloadId argument
            null,                   // downloadVersion argument
            true,                   // enablePrerelease
            ("Contoso.Preview", "2.0.0-beta.2")  // expected
        };

        // chose stable with IncludePrerelease == false
        yield return new object[]
        {
            new List<(string, string)>
            {
                ("Contoso.Preview", "1.3.2"),
                ("Contoso.Preview", "1.3.0"),
                ("Contoso.Preview", "2.0.0-beta.2")
            },
            "Contoso.Preview",        // downloadId argument
            null,                   // downloadVersion argument
            false,                   // enablePrerelease
            ("Contoso.Preview", "1.3.2")  // expected
        };
    }


    [Theory]
    [MemberData(nameof(PackageTestData))]
    public async Task RunAsync_ExplicitVersionFromLocalFolderSource_SucceedsAsync(
        IReadOnlyList<(string, string)> sourcePackages,
        string downloadId,
        string downloadVersion,
        bool enablePrerelease,
        (string, string) expectedPackage)
    {
        // Arrange
        using var context = new SimpleTestPathContext();
        var sourceDir = context.PackageSource;
        var outputDir = context.WorkingDirectory;

        foreach (var (id, version) in sourcePackages)
        {
            await SimpleTestPackageUtility.CreateFullPackageAsync(sourceDir, id, version);
        }

        var logger = new Mock<ILoggerWithColor>(MockBehavior.Loose);
        var settings = new Mock<ISettings>(MockBehavior.Loose);

        var args = new PackageDownloadArgs()
        {
            Packages = [new PackageWithNuGetVersion { Id = downloadId, NuGetVersion = downloadVersion == null ? null : NuGetVersion.Parse(downloadVersion) }],
            OutputDirectory = outputDir,
            IncludePrerelease = enablePrerelease
        };


        // Act
        var result = await PackageDownloadRunner.RunAsync(
            args,
            logger.Object,
            [new(sourceDir)],
            settings.Object,
            CancellationToken.None);

        // Assert
        result.Should().Be(PackageDownloadRunner.ExitCodeSuccess);
        var installDir = Path.Combine(outputDir, expectedPackage.Item1.ToLowerInvariant(), expectedPackage.Item2);
        Directory.Exists(installDir).Should().BeTrue();
        Directory.EnumerateFiles(installDir, "*.nupkg").Any().Should().BeTrue();
        File.Exists(Path.Combine(installDir, $"{expectedPackage.Item1.ToLowerInvariant()}.{expectedPackage.Item2}.nupkg")).Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_NoVersion_PicksHighestAcrossMultipleSources()
    {
        // Arrange
        using var contextA = new SimpleTestPathContext();
        using var contextB = new SimpleTestPathContext();
        var srcA = contextA.PackageSource;
        var srcB = contextB.PackageSource;
        var outputDir = contextA.WorkingDirectory;

        var id = "Contoso.Toolkit";
        await SimpleTestPackageUtility.CreateFullPackageAsync(srcA, id, "1.1.0");
        await SimpleTestPackageUtility.CreateFullPackageAsync(srcB, id, "1.2.0");

        var logger = new Mock<ILoggerWithColor>(MockBehavior.Loose);
        var settings = new Mock<ISettings>(MockBehavior.Loose);

        var args = new PackageDownloadArgs()
        {
            Packages = [new PackageWithNuGetVersion { Id = id, NuGetVersion = null }],
            OutputDirectory = outputDir,
        };

        // Act
        var result = await PackageDownloadRunner.RunAsync(
            args,
            logger.Object,
            [new PackageSource(srcA), new PackageSource(srcB)],
            settings.Object,
            CancellationToken.None);

        // Assert
        result.Should().Be(PackageDownloadRunner.ExitCodeSuccess);

        var chosen = Path.Combine(outputDir, id.ToLowerInvariant(), "1.2.0");
        Directory.Exists(chosen).Should().BeTrue("should choose the highest version found across all sources");
        File.Exists(Path.Combine(chosen, $"{id.ToLowerInvariant()}.1.2.0.nupkg")).Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_ExplicitVersionAlreadyInstalled_ShortCircuitsAndSucceeds()
    {
        // Arrange
        using var context = new SimpleTestPathContext();
        var sourceDir = context.PackageSource;
        var outputDir = context.WorkingDirectory;

        var id = "Contoso.Utils";
        var v = "3.4.5";
        await SimpleTestPackageUtility.CreateFullPackageAsync(sourceDir, id, v);

        var logger = new Mock<ILoggerWithColor>(MockBehavior.Loose);
        var settings = new Mock<ISettings>(MockBehavior.Loose);

        // First run: install explicit version
        var args1 = new PackageDownloadArgs()
        {
            Packages = [new PackageWithNuGetVersion { Id = id, NuGetVersion = NuGetVersion.Parse(v) }],
            LogLevel = LogLevel.Verbose,
            OutputDirectory = outputDir,
        };

        var first = await PackageDownloadRunner.RunAsync(
            args1,
            logger.Object,
            [new PackageSource(sourceDir)],
            settings.Object,
            CancellationToken.None);
        first.Should().Be(ExitCodes.Success);

        // Second run: should short-circuit because already installed
        var args2 = new PackageDownloadArgs()
        {
            Packages = [new PackageWithNuGetVersion { Id = id, NuGetVersion = NuGetVersion.Parse(v) }],
            OutputDirectory = outputDir,
        };

        // Act
        var second = await PackageDownloadRunner.RunAsync(
            args2,
            logger.Object,
            [new PackageSource(sourceDir)],
            settings.Object,
            CancellationToken.None);

        // Assert
        second.Should().Be(PackageDownloadRunner.ExitCodeSuccess);
        var installDir = Path.Combine(outputDir, id.ToLowerInvariant(), v);
        Directory.Exists(installDir).Should().BeTrue();
        File.Exists(Path.Combine(installDir, $"{id.ToLowerInvariant()}.{v}.nupkg")).Should().BeTrue();
        logger.Verify(l => l.LogMinimal(It.Is<string>(s => s.Contains("Skipping", StringComparison.OrdinalIgnoreCase))), Times.AtLeastOnce);
    }

    [Fact]
    public async Task RunAsync_WhenAllowInsecureConnectionsFalse_RejectsHttpSource()
    {
        // Arrange
        using var context = new SimpleTestPathContext();
        var httpSource = "http://contoso/v3/index.json";
        var logger = new Mock<ILoggerWithColor>(MockBehavior.Loose);
        var settings = new Mock<ISettings>(MockBehavior.Loose);

        var args = new PackageDownloadArgs()
        {
            Packages = [new PackageWithNuGetVersion { Id = "Contoso.Lib", NuGetVersion = null }],
        };

        // Act
        var result = await PackageDownloadRunner.RunAsync(
            args,
            logger.Object,
            [new PackageSource(httpSource)],
            settings.Object,
            CancellationToken.None);

        // Assert
        result.Should().Be(PackageDownloadRunner.ExitCodeError);
        logger.Verify(l => l.LogError(It.Is<string>(s => s.Contains(httpSource, StringComparison.OrdinalIgnoreCase))), Times.AtLeastOnce);
    }

    [Fact]
    public async Task RunAsync_PackageDoesNotExist_ReturnsError()
    {
        // Arrange
        using var context = new SimpleTestPathContext();
        var id = "Missing.Package";
        var v = "9.9.9";
        var logger = new Mock<ILoggerWithColor>(MockBehavior.Loose);
        var settings = new Mock<ISettings>(MockBehavior.Loose);

        var args = new PackageDownloadArgs()
        {
            Packages = [new PackageWithNuGetVersion { Id = id, NuGetVersion = NuGetVersion.Parse(v) }],
            OutputDirectory = context.WorkingDirectory,
        };

        // Act
        var result = await PackageDownloadRunner.RunAsync(
            args,
            logger.Object,
            [new PackageSource(context.PackageSource)],
            settings.Object,
            CancellationToken.None);

        // Assert
        result.Should().Be(PackageDownloadRunner.ExitCodeError);
        logger.Verify(l => l.LogError(It.IsAny<string>()), Times.AtLeastOnce);

        File.Exists(Path.Combine(context.WorkingDirectory, $"{id.ToLowerInvariant()}.{v}.nupkg"))
            .Should().BeFalse("Package does not exist in sources");
    }
}
