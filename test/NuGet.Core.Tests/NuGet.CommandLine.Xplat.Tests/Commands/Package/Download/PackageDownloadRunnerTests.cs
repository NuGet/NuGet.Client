// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.CommandLine.Xplat.Tests;

using System.Collections.Generic;
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
    public sealed record SourceSpec(string Name, params string[] Versions);

    public sealed record ResolveScenario(
        string Id,
        IReadOnlyList<SourceSpec> Sources,
        string RequestedVersion,
        bool IncludePrerelease,
        string ExpectedSource,   // null => expect no resolution
        string ExpectedVersion); // null => expect no resolution

    public sealed class ResolveVersionData : TheoryData<ResolveScenario>
    {
        public ResolveVersionData()
        {
            // Exact version at first source -> returns early
            Add(new ResolveScenario(
                Id: "Contoso",
                Sources: [new SourceSpec("src", "1.2.3")],
                RequestedVersion: "1.2.3",
                IncludePrerelease: false,
                ExpectedSource: "src",
                ExpectedVersion: "1.2.3"));

            // Exact version missing in first, present in second -> pick second
            Add(new ResolveScenario(
                "Contoso",
                [new SourceSpec("srcA", "1.0.0"), new SourceSpec("srcB", "1.2.3")],
                "1.2.3",
                false,
                "srcB",
                "1.2.3"));

            // No version; exclude prerelease -> highest stable across sources
            Add(new ResolveScenario(
                "Contoso",
                [new SourceSpec("srcA", "1.0.0", "1.5.0-alpha"), new SourceSpec("srcB", "2.0.0")],
                null,
                false,
                "srcB",
                "2.0.0"));

            // No version; include prerelease -> highest including prerelease
            Add(new ResolveScenario(
                "Contoso",
                [new SourceSpec("srcA", "2.0.0"), new SourceSpec("srcB", "2.1.0-rc.1")],
                null,
                true,
                "srcB",
                "2.1.0-rc.1"));

            // No matches anywhere -> null
            Add(new ResolveScenario(
                "Contoso",
                [new SourceSpec("emptyA"), new SourceSpec("emptyB")],
                null,
                false,
                ExpectedSource: null,
                ExpectedVersion: null));

            // Exact version requested, but not found anywhere -> null
            Add(new ResolveScenario(
                "Contoso",
                [new SourceSpec("src", "1.0.0")],
                "1.2.3",
                false,
                null,
                null));
        }
    }

    [Theory]
    [ClassData(typeof(ResolveVersionData))]
    public async Task ResolvePackageDownloadVersion_Scenarios(ResolveScenario scenario)
    {
        // Arrange
        using var context = new SimpleTestPathContext();

        // Create source folders and packages
        var repos = new List<SourceRepository>();
        foreach (var src in scenario.Sources)
        {
            var folder = Path.Combine(context.WorkingDirectory, src.Name);
            Directory.CreateDirectory(folder);

            foreach (var version in src.Versions)
            {
                await SimpleTestPackageUtility.CreateFullPackageAsync(folder, scenario.Id, version);
            }

            repos.Add(Repository.Factory.GetCoreV3(new PackageSource(folder)));
        }

        var logger = new Mock<ILoggerWithColor>(MockBehavior.Loose);
        var package = new PackageWithNuGetVersion
        {
            Id = scenario.Id,
            NuGetVersion = scenario.RequestedVersion is null ? null : NuGetVersion.Parse(scenario.RequestedVersion)
        };

        // Act
        (NuGetVersion resolved, SourceRepository resolvedRepo) = await PackageDownloadRunner.ResolvePackageDownloadVersion(
            package,
            [.. repos],
            new SourceCacheContext(),
            logger.Object,
            includePrerelease: scenario.IncludePrerelease,
            CancellationToken.None);

        // Assert
        if (scenario.ExpectedVersion is null)
        {
            Assert.Null(resolved);
            Assert.Null(resolvedRepo);
            return;
        }

        Assert.Equal(new NuGetVersion(scenario.ExpectedVersion), resolved);
        Assert.NotNull(resolvedRepo);

        var expectedPath = Path.Combine(context.WorkingDirectory, scenario.ExpectedSource!);
        Assert.Equal(expectedPath, resolvedRepo.PackageSource.Source);
    }
}
