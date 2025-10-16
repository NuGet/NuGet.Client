// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.CommandLine.Xplat.Tests;

using System;
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
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

public class PackageDownloadRunnerTests
{
    [Fact]
    public async Task RunAsync_ExplicitVersionFromLocalFolderSource_SucceedsAsync()
    {
        // Arrange
        using var context = new SimpleTestPathContext();
        var sourceDir = Path.Combine(context.WorkingDirectory, "src");
        var outputDir = Path.Combine(context.WorkingDirectory, "packages");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(outputDir);

        var id = "Contoso.Lib";
        var version = "1.2.3";
        await SimpleTestPackageUtility.CreateFullPackageAsync(sourceDir, id, version);

        var logger = new Mock<ILoggerWithColor>(MockBehavior.Loose);
        var settings = new Mock<ISettings>(MockBehavior.Loose);

        var args = new PackageDownloadArgs()
        {
            Packages = [new PackageWithNuGetVersion { Id = id, NuGetVersion = NuGetVersion.Parse(version) }],
            OutputDirectory = outputDir,
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
        var installDir = Path.Combine(outputDir, id.ToLowerInvariant(), version);
        Directory.Exists(installDir).Should().BeTrue();
        Directory.EnumerateFiles(installDir, "*.nupkg").Any().Should().BeTrue();
        File.Exists(Path.Combine(installDir, $"{id.ToLowerInvariant()}.{version}.nupkg")).Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_NoVersionWhenPrereleaseNotIncluded_PicksLatestStable()
    {
        // Arrange
        using var context = new SimpleTestPathContext();
        var sourceDir = Path.Combine(context.WorkingDirectory, "src");
        var outputDir = Path.Combine(context.WorkingDirectory, "packages");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(outputDir);

        var id = "Contoso.Core";
        await SimpleTestPackageUtility.CreateFullPackageAsync(sourceDir, id, "1.0.0");
        await SimpleTestPackageUtility.CreateFullPackageAsync(sourceDir, id, "1.1.0");
        await SimpleTestPackageUtility.CreateFullPackageAsync(sourceDir, id, "2.0.0-beta");

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
            [new PackageSource(sourceDir)],
            settings.Object,
            CancellationToken.None);

        // Assert
        result.Should().Be(PackageDownloadRunner.ExitCodeSuccess);

        var chosen = Path.Combine(outputDir, id.ToLowerInvariant(), "1.1.0");
        var notChosen = Path.Combine(outputDir, id.ToLowerInvariant(), "2.0.0-beta");
        Directory.Exists(chosen).Should().BeTrue("latest stable (1.1.0) should be chosen");
        Directory.Exists(notChosen).Should().BeFalse("prerelease (2.0.0-beta) should not be chosen");
        File.Exists(Path.Combine(chosen, $"{id.ToLowerInvariant()}.1.1.0.nupkg")).Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_NoVersionWithPrereleaseTrue_PicksHighestIncludingPrerelease()
    {
        // Arrange
        using var context = new SimpleTestPathContext();
        var sourceDir = Path.Combine(context.WorkingDirectory, "src");
        var outputDir = Path.Combine(context.WorkingDirectory, "packages");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(outputDir);

        var id = "Contoso.Preview";
        await SimpleTestPackageUtility.CreateFullPackageAsync(sourceDir, id, "1.3.0");
        await SimpleTestPackageUtility.CreateFullPackageAsync(sourceDir, id, "2.0.0-beta.2");

        var logger = new Mock<ILoggerWithColor>(MockBehavior.Loose);
        var settings = new Mock<ISettings>(MockBehavior.Loose);

        var args = new PackageDownloadArgs()
        {
            Packages = [new PackageWithNuGetVersion { Id = id, NuGetVersion = null }],
            OutputDirectory = outputDir,
            IncludePrerelease = true
        };

        // Act
        var result = await PackageDownloadRunner.RunAsync(
            args,
            logger.Object,
            [new PackageSource(sourceDir)],
            settings.Object,
            CancellationToken.None);

        // Assert
        result.Should().Be(PackageDownloadRunner.ExitCodeSuccess);

        var chosen = Path.Combine(outputDir, id.ToLowerInvariant(), "2.0.0-beta.2");
        Directory.Exists(chosen).Should().BeTrue("IncludePrerelease should allow picking 2.0.0-beta.2");
        File.Exists(Path.Combine(chosen, $"{id.ToLowerInvariant()}.2.0.0-beta.2.nupkg")).Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_NoVersion_PicksHighestAcrossMultipleSources()
    {
        // Arrange
        using var context = new SimpleTestPathContext();
        var srcA = Path.Combine(context.WorkingDirectory, "srcA");
        var srcB = Path.Combine(context.WorkingDirectory, "srcB");
        var outputDir = Path.Combine(context.WorkingDirectory, "packages");
        Directory.CreateDirectory(srcA);
        Directory.CreateDirectory(srcB);
        Directory.CreateDirectory(outputDir);

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
        var sourceDir = Path.Combine(context.WorkingDirectory, "src");
        var outputDir = Path.Combine(context.WorkingDirectory, "packages");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(outputDir);

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

        var outputDir = Path.Combine(context.WorkingDirectory, "packages");
        Directory.CreateDirectory(outputDir);

        var httpSource = "http://contoso/v3/index.json";
        var logger = new Mock<ILoggerWithColor>(MockBehavior.Loose);
        var settings = new Mock<ISettings>(MockBehavior.Loose);

        var args = new PackageDownloadArgs()
        {
            Packages = [new PackageWithNuGetVersion { Id = "Contoso.Lib", NuGetVersion = null }],
            OutputDirectory = outputDir,
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
        var srcA = Path.Combine(context.WorkingDirectory, "emptyA");
        var srcB = Path.Combine(context.WorkingDirectory, "emptyB");
        var outputDir = Path.Combine(context.WorkingDirectory, "packages");
        Directory.CreateDirectory(srcA);
        Directory.CreateDirectory(srcB);
        Directory.CreateDirectory(outputDir);

        var id = "Missing.Package";
        var v = "9.9.9";

        var logger = new Mock<ILoggerWithColor>(MockBehavior.Loose);
        var settings = new Mock<ISettings>(MockBehavior.Loose);

        var args = new PackageDownloadArgs()
        {
            Packages = [new PackageWithNuGetVersion { Id = id, NuGetVersion = NuGetVersion.Parse(v) }],
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
        result.Should().Be(PackageDownloadRunner.ExitCodeError);
        logger.Verify(l => l.LogError(It.IsAny<string>()), Times.AtLeastOnce);

        File.Exists(Path.Combine(outputDir, $"{id.ToLowerInvariant()}.{v}.nupkg"))
            .Should().BeFalse("Package does not exist in sources");
    }

    [Fact]
    public async Task ResolvePackageDownloadVersion_ExactVersionAtFirstSource_ReturnsEarly()
    {
        // Arrange
        using var context = new SimpleTestPathContext();
        var v123 = new NuGetVersion("1.2.3");
        var package = new PackageWithNuGetVersion { Id = "Contoso", NuGetVersion = NuGetVersion.Parse(v123.OriginalVersion) };
        var sourceDir = Path.Combine(context.WorkingDirectory, "src");
        Directory.CreateDirectory(sourceDir);
        await SimpleTestPackageUtility.CreateFullPackageAsync(sourceDir, package.Id, package.NuGetVersion.ToNormalizedString());
        var sources = new[] { Repository.Factory.GetCoreV3(new PackageSource(sourceDir)) };

        var logger = new Mock<ILoggerWithColor>(MockBehavior.Loose);

        // Act
        (NuGetVersion resolved, SourceRepository resolvedRepo) = await PackageDownloadRunner.ResolvePackageDownloadVersion(
            package,
            sources,
            new SourceCacheContext(),
            logger.Object,
            includePrerelease: false,
            CancellationToken.None);

        // Assert
        Assert.Equal(v123, resolved);
        Assert.Equal(sourceDir, resolvedRepo.PackageSource.Source);
    }

    [Fact]
    public async Task ResolvePackageDownloadVersion_ExactVersionMissingInFirstPresentInSecond_ReturnsSecondSource()
    {
        // Arrange
        using var context = new SimpleTestPathContext();
        var v123 = new NuGetVersion("1.2.3");
        var v100 = new NuGetVersion("1.0.0");
        var package = new PackageWithNuGetVersion { Id = "Contoso", NuGetVersion = NuGetVersion.Parse(v123.OriginalVersion) };

        var sourceA = Path.Combine(context.WorkingDirectory, "srcA");
        var sourceB = Path.Combine(context.WorkingDirectory, "srcB");
        Directory.CreateDirectory(sourceA);
        Directory.CreateDirectory(sourceB);

        await SimpleTestPackageUtility.CreateFullPackageAsync(sourceA, package.Id, v100.OriginalVersion);
        await SimpleTestPackageUtility.CreateFullPackageAsync(sourceB, package.Id, v123.OriginalVersion);

        var sources = new[] { Repository.Factory.GetCoreV3(new PackageSource(sourceA)), Repository.Factory.GetCoreV3(new PackageSource(sourceB)) };
        var logger = new Mock<ILoggerWithColor>(MockBehavior.Loose);

        // Act
        (NuGetVersion resolved, SourceRepository resolvedRepo) = await PackageDownloadRunner.ResolvePackageDownloadVersion(
            package, sources, new SourceCacheContext(), logger.Object, includePrerelease: false, CancellationToken.None);

        // Assert
        Assert.Equal(v123, resolved);
        Assert.Equal(sourceB, resolvedRepo.PackageSource.Source);
    }

    [Fact]
    public async Task ResolvePackageDownloadVersion_NoVersionSpecifiedExcludePrerelease_PicksHighestStableAcrossSources()
    {
        // Arrange
        using var context = new SimpleTestPathContext();
        var package = new PackageWithNuGetVersion { Id = "Contoso", NuGetVersion = null };
        var sourceA = Path.Combine(context.WorkingDirectory, "srcA");
        var sourceB = Path.Combine(context.WorkingDirectory, "srcB");
        Directory.CreateDirectory(sourceA);
        Directory.CreateDirectory(sourceB);

        await SimpleTestPackageUtility.CreateFullPackageAsync(sourceA, package.Id, "1.0.0");
        await SimpleTestPackageUtility.CreateFullPackageAsync(sourceA, package.Id, "1.5.0-alpha"); // prerelease; should be ignored
        await SimpleTestPackageUtility.CreateFullPackageAsync(sourceB, package.Id, "2.0.0");       // highest stable

        var sources = new[] { Repository.Factory.GetCoreV3(new PackageSource(sourceA)), Repository.Factory.GetCoreV3(new PackageSource(sourceB)) };
        var logger = new Mock<ILoggerWithColor>(MockBehavior.Loose);

        // Act
        (NuGetVersion resolved, SourceRepository resolvedRepo) = await PackageDownloadRunner.ResolvePackageDownloadVersion(
            package, sources, new SourceCacheContext(), logger.Object, includePrerelease: false, CancellationToken.None);

        // Assert
        Assert.Equal(new NuGetVersion("2.0.0"), resolved);
        Assert.Equal(sourceB, resolvedRepo.PackageSource.Source);
    }

    [Fact]
    public async Task ResolvePackageDownloadVersion_NoVersionSpecifiedIncludePrerelease_PicksHighestIncludingPrerelease()
    {
        // Arrange
        using var context = new SimpleTestPathContext();
        var package = new PackageWithNuGetVersion { Id = "Contoso", NuGetVersion = null }; // pickLatest
        var sourceA = Path.Combine(context.WorkingDirectory, "srcA");
        var sourceB = Path.Combine(context.WorkingDirectory, "srcB");
        Directory.CreateDirectory(sourceA);
        Directory.CreateDirectory(sourceB);

        await SimpleTestPackageUtility.CreateFullPackageAsync(sourceA, package.Id, "2.0.0");          // stable
        await SimpleTestPackageUtility.CreateFullPackageAsync(sourceB, package.Id, "2.1.0-rc.1");     // higher (prerelease)

        var sources = new[] { Repository.Factory.GetCoreV3(new PackageSource(sourceA)), Repository.Factory.GetCoreV3(new PackageSource(sourceB)) };
        var logger = new Mock<ILoggerWithColor>(MockBehavior.Loose);

        // Act
        (NuGetVersion resolved, SourceRepository resolvedRepo) = await PackageDownloadRunner.ResolvePackageDownloadVersion(
            package, sources, new SourceCacheContext(), logger.Object, includePrerelease: true, CancellationToken.None);

        // Assert
        Assert.Equal(new NuGetVersion("2.1.0-rc.1"), resolved);
        Assert.Equal(sourceB, resolvedRepo.PackageSource.Source);
    }

    [Fact]
    public async Task ResolvePackageDownloadVersion_NoMatchesAnywhere_ReturnsNull()
    {
        using var context = new SimpleTestPathContext();

        // Arrange
        var package = new PackageWithNuGetVersion { Id = "Contoso", NuGetVersion = null }; // pickLatest
        var sourceA = Path.Combine(context.WorkingDirectory, "emptyA");
        var sourceB = Path.Combine(context.WorkingDirectory, "emptyB");
        Directory.CreateDirectory(sourceA);
        Directory.CreateDirectory(sourceB);
        var sources = new[] { Repository.Factory.GetCoreV3(new PackageSource(sourceA)), Repository.Factory.GetCoreV3(new PackageSource(sourceB)) };

        var logger = new Mock<ILoggerWithColor>(MockBehavior.Loose);
        logger.Setup(l => l.LogError(It.IsAny<string>()));

        // Act
        (NuGetVersion resolved, SourceRepository resolvedRepo) = await PackageDownloadRunner.ResolvePackageDownloadVersion(
            package, sources, new SourceCacheContext(), logger.Object, includePrerelease: false, CancellationToken.None);

        // Assert
        Assert.Null(resolved);
        Assert.Null(resolvedRepo);
    }

    [Fact]
    public async Task ResolvePackageDownloadVersion_ExactVersionNotFoundAnywhere_ReturnsNull()
    {
        // Arrange
        using var context = new SimpleTestPathContext();
        var v123 = new NuGetVersion("1.2.3");
        var package = new PackageWithNuGetVersion { Id = "Contoso", NuGetVersion = NuGetVersion.Parse(v123.OriginalVersion) };
        var sourceDir = Path.Combine(context.WorkingDirectory, "src");
        Directory.CreateDirectory(sourceDir);

        await SimpleTestPackageUtility.CreateFullPackageAsync(sourceDir, package.Id, "1.0.0");

        var sources = new[] { Repository.Factory.GetCoreV3(new PackageSource(sourceDir)) };
        var logger = new Mock<ILoggerWithColor>(MockBehavior.Loose);
        logger.Setup(l => l.LogError(It.IsAny<string>()));

        // Act
        (NuGetVersion resolved, SourceRepository resolvedRepo) = await PackageDownloadRunner.ResolvePackageDownloadVersion(
            package,
            sources,
            new SourceCacheContext(),
            logger.Object,
            includePrerelease: false,
            CancellationToken.None);

        // Assert
        Assert.Null(resolved);
        Assert.Null(resolvedRepo);
    }

    [Fact]
    public async Task ResolvePackageDownloadVersion_OnlyPrereleaseExistsAndExcludePrerelease_ReturnsNull()
    {
        using var context = new SimpleTestPathContext();

        // Arrange
        var package = new PackageWithNuGetVersion { Id = "Contoso", NuGetVersion = null }; // pickLatest = true
        var sourceDir = Path.Combine(context.WorkingDirectory, "src");
        Directory.CreateDirectory(sourceDir);

        await SimpleTestPackageUtility.CreateFullPackageAsync(sourceDir, package.Id, "3.0.0-beta.1"); // prerelease only

        var sources = new[] { Repository.Factory.GetCoreV3(new PackageSource(sourceDir)) };
        var logger = new Mock<ILoggerWithColor>(MockBehavior.Loose);
        logger.Setup(l => l.LogError(It.IsAny<string>()));

        // Act
        (NuGetVersion resolved, SourceRepository resolvedRepo) = await PackageDownloadRunner.ResolvePackageDownloadVersion(
            package,
            sources,
            new SourceCacheContext(),
            logger.Object,
            includePrerelease: false, // do not allow prerelease
            CancellationToken.None);

        // Assert
        Assert.Null(resolved);
        Assert.Null(resolvedRepo);
    }
}
