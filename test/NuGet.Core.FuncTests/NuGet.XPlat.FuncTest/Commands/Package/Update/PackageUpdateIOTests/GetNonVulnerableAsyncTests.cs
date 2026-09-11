// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.CommandLine.XPlat;
using NuGet.CommandLine.XPlat.Commands.Package.Update;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Model;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Test.Utility;
using Xunit;

namespace NuGet.XPlat.FuncTest.Commands.Package.Update.PackageUpdateIOTests;

public class GetNonVulnerableAsyncTests
{
    private static PackageUpdateIO CreatePackageUpdateIO(string solutionRoot)
    {
        var msbuildUtility = new MSBuildAPIUtility(NullLogger.Instance, virtualProjectBuilder: null);
        var packageUpdateIO = new PackageUpdateIO(solutionRoot, msbuildUtility, TestEnvironmentVariableReader.EmptyInstance);
        return packageUpdateIO;
    }

    [Fact]
    public async Task GetNonVulnerableAsync_WithVulnerableMinVersion_ReturnsNextNonVulnerableVersion()
    {
        // Arrange
        using var testContext = new SimpleTestPathContext();
        var packageId = "TestPackage.F";

        var packages = new[]
        {
            new SimpleTestPackageContext(packageId, "1.0.0"),
            new SimpleTestPackageContext(packageId, "1.1.0"),
            new SimpleTestPackageContext(packageId, "2.0.0")
        };

        await SimpleTestPackageUtility.CreatePackagesAsync(testContext.PackageSource, packages);

        using var packageUpdateIO = CreatePackageUpdateIO(testContext.SolutionRoot);

        var vulnerabilities = new Dictionary<string, IReadOnlyList<PackageVulnerabilityInfo>>
        {
            [packageId] = new List<PackageVulnerabilityInfo>
            {
                new PackageVulnerabilityInfo(
                    url: new Uri("https://example.com/vuln"),
                    severity: PackageVulnerabilitySeverity.High,
                    versions: VersionRange.Parse("[1.0.0, 1.1.0]"))
            }
        };

        var knownVulnerabilities = new List<IReadOnlyDictionary<string, IReadOnlyList<PackageVulnerabilityInfo>>>
        {
            vulnerabilities
        };

        // Act
        var result = await packageUpdateIO.GetNonVulnerableAsync(
            packageId,
            allowedSources: null,
            new NuGetVersion("1.0.0"),
            NullLogger.Instance,
            knownVulnerabilities,
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(new NuGetVersion("2.0.0"));
    }

    [Fact]
    public async Task GetNonVulnerableAsync_WithAllVersionsVulnerable_ReturnsNull()
    {
        // Arrange
        using var testContext = new SimpleTestPathContext();
        var packageId = "TestPackage.G";

        var packages = new[]
        {
            new SimpleTestPackageContext(packageId, "1.0.0"),
            new SimpleTestPackageContext(packageId, "1.1.0")
        };

        await SimpleTestPackageUtility.CreatePackagesAsync(testContext.PackageSource, packages);

        using var packageUpdateIO = CreatePackageUpdateIO(testContext.SolutionRoot);

        var vulnerabilities = new Dictionary<string, IReadOnlyList<PackageVulnerabilityInfo>>
        {
            [packageId] = new List<PackageVulnerabilityInfo>
            {
                new PackageVulnerabilityInfo(
                    url: new Uri("https://example.com/vuln"),
                    severity: PackageVulnerabilitySeverity.High,
                    versions: VersionRange.Parse("[1.0.0, 2.0.0]"))
            }
        };

        var knownVulnerabilities = new List<IReadOnlyDictionary<string, IReadOnlyList<PackageVulnerabilityInfo>>>
        {
            vulnerabilities
        };

        // Act
        var result = await packageUpdateIO.GetNonVulnerableAsync(
            packageId,
            allowedSources: null,
            new NuGetVersion("1.0.0"),
            NullLogger.Instance,
            knownVulnerabilities,
            CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetNonVulnerableAsync_WhenOnlyNonVulnerableVersionIsInCooldown_ReturnsNull()
    {
        // Arrange
        using var testContext = new SimpleTestPathContext();
        const string packageId = "TestPackage.CooldownVulnerability";
        DateTimeOffset utcNow = new(2026, 9, 11, 0, 0, 0, TimeSpan.Zero);

        await SimpleTestPackageUtility.CreatePackagesAsync(
            testContext.PackageSource,
            new SimpleTestPackageContext(packageId, "1.0.0"),
            new SimpleTestPackageContext(packageId, "2.0.0"));

        File.SetLastWriteTimeUtc(
            Path.Combine(testContext.PackageSource, $"{packageId}.1.0.0.nupkg"),
            utcNow.AddHours(-25).UtcDateTime);
        File.SetLastWriteTimeUtc(
            Path.Combine(testContext.PackageSource, $"{packageId}.2.0.0.nupkg"),
            utcNow.AddHours(-23).UtcDateTime);

        File.WriteAllText(
            Path.Combine(testContext.SolutionRoot, "nuget.config"),
            $"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="test" value="{testContext.PackageSource}" minPublishAgeHours="24" />
              </packageSources>
            </configuration>
            """);

        var msbuildUtility = new MSBuildAPIUtility(NullLogger.Instance, virtualProjectBuilder: null);
        using var packageUpdateIO = new PackageUpdateIO(
            testContext.SolutionRoot,
            msbuildUtility,
            TestEnvironmentVariableReader.EmptyInstance,
            () => utcNow);

        var vulnerabilities = new Dictionary<string, IReadOnlyList<PackageVulnerabilityInfo>>
        {
            [packageId] =
            [
                new PackageVulnerabilityInfo(
                    url: new Uri("https://example.com/vuln"),
                    severity: PackageVulnerabilitySeverity.High,
                    versions: VersionRange.Parse("[1.0.0]"))
            ]
        };

        // Act
        NuGetVersion? result = await packageUpdateIO.GetNonVulnerableAsync(
            packageId,
            allowedSources: null,
            new NuGetVersion("1.0.0"),
            NullLogger.Instance,
            [vulnerabilities],
            CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }
}
