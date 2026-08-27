// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System.Collections.Generic;
using System.Linq;
using NuGet.CommandLine.XPlat;
using NuGet.CommandLine.XPlat.ListPackage;
using Xunit;

namespace NuGet.CommandLine.Xplat.Tests
{
    public class SponsorReportAggregatorTests
    {
        [Fact]
        public void CollapseFrameworks_DeduplicatesAcrossFrameworksIgnoringCaseAndOrdersById()
        {
            // Arrange
            ListPackageProjectModel project = Project(
                "a.csproj",
                Framework("net8.0", topLevel: new List<ListReportPackage> { Package("zeta"), Package("Beta") }),
                Framework("net472", topLevel: new List<ListReportPackage> { Package("ZETA"), Package("alpha") }));

            // Act
            (List<ListReportPackage> topLevel, List<ListReportPackage> transitive) = SponsorReportAggregator.CollapseFrameworks(project);

            // Assert
            // "ZETA" collapses into the first-seen "zeta", and ordering ignores case: Ordinal would sort "Beta" first.
            Assert.Equal(new[] { "alpha", "Beta", "zeta" }, topLevel.Select(p => p.PackageId));
            Assert.Empty(transitive);
        }

        [Fact]
        public void CollapseFrameworks_PackageTopLevelInOneFrameworkTransitiveInAnother_ReportsTopLevelOnly()
        {
            // Arrange
            ListPackageProjectModel project = Project(
                "a.csproj",
                Framework("net8.0", topLevel: new List<ListReportPackage> { Package("Shared") }),
                Framework("net472", transitive: new List<ListReportPackage> { Package("shared"), Package("OnlyTransitive") }));

            // Act
            (List<ListReportPackage> topLevel, List<ListReportPackage> transitive) = SponsorReportAggregator.CollapseFrameworks(project);

            // Assert
            Assert.Equal("Shared", Assert.Single(topLevel).PackageId);
            Assert.Equal("OnlyTransitive", Assert.Single(transitive).PackageId);
        }

        [Fact]
        public void CollapseFrameworks_NoPackages_ReturnsEmptyLists()
        {
            // Arrange - the two null shapes the aggregator guards against.
            ListPackageProjectModel nullFrameworks = Project("a.csproj", (ListPackageReportFrameworkPackage[])null);
            ListPackageProjectModel nullPackageLists = Project("b.csproj", Framework("net8.0"));

            // Act
            (List<ListReportPackage> topLevel, List<ListReportPackage> transitive) = SponsorReportAggregator.CollapseFrameworks(nullFrameworks);
            (List<ListReportPackage> topLevelOfEmptyFramework, List<ListReportPackage> transitiveOfEmptyFramework) = SponsorReportAggregator.CollapseFrameworks(nullPackageLists);

            // Assert
            Assert.Empty(topLevel);
            Assert.Empty(transitive);
            Assert.Empty(topLevelOfEmptyFramework);
            Assert.Empty(transitiveOfEmptyFramework);
        }

        [Fact]
        public void CollapseProjects_PackageUsedByTwoProjects_ReturnsSingleEntryWithBothProjects()
        {
            // Arrange
            ListPackageProjectModel projectA = Project(
                "a.csproj",
                Framework("net8.0", topLevel: new List<ListReportPackage> { Package("Shared", "https://sponsor/shared") }));
            ListPackageProjectModel projectB = Project(
                "b.csproj",
                Framework("net8.0",
                    topLevel: new List<ListReportPackage> { Package("OnlyInB") },
                    transitive: new List<ListReportPackage> { Package("shared") }));

            // Act
            List<SponsorReportAggregator.SponsorReportPackage> packages = SponsorReportAggregator.CollapseProjects(new[] { projectA, projectB });

            // Assert
            Assert.Equal(new[] { "OnlyInB", "Shared" }, packages.Select(p => p.PackageId));

            SponsorReportAggregator.SponsorReportPackage shared = packages.Single(p => p.PackageId == "Shared");
            Assert.Equal(new[] { ("a.csproj", true), ("b.csproj", false) }, shared.Projects);
            Assert.Equal("https://sponsor/shared", Assert.Single(Assert.Single(shared.Sponsorships).Urls));
        }

        private static ListPackageProjectModel Project(string projectPath, params ListPackageReportFrameworkPackage[] frameworks)
        {
            return new ListPackageProjectModel(projectPath)
            {
                TargetFrameworkPackages = frameworks?.ToList(),
            };
        }

        private static ListPackageReportFrameworkPackage Framework(
            string framework,
            List<ListReportPackage> topLevel = null,
            List<ListReportPackage> transitive = null)
        {
            return new ListPackageReportFrameworkPackage(framework, framework)
            {
                TopLevelPackages = topLevel,
                TransitivePackages = transitive,
            };
        }

        private static ListReportPackage Package(string packageId, params string[] sponsorshipUrls)
        {
            return new ListReportPackage(
                packageId: packageId,
                resolvedVersion: "1.0.0",
                latestVersion: null,
                vulnerabilities: null,
                deprecationReasons: null,
                alternativePackage: null,
                requestedVersion: "1.0.0",
                autoReference: false,
                sponsorships: sponsorshipUrls.Length == 0
                    ? null
                    : new[] { new PackageSponsorship("https://source", sponsorshipUrls) });
        }
    }
}
