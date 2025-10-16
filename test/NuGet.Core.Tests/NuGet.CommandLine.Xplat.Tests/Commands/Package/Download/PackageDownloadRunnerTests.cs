// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.CommandLine.Xplat.Tests;

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.CommandLine.XPlat;
using NuGet.CommandLine.XPlat.Commands.Package;
using NuGet.CommandLine.XPlat.Commands.Package.PackageDownload;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

public class PackageDownloadRunnerTests
{
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
