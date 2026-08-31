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
        public void CollapseFrameworks_DeduplicatesIgnoringCase_OrdersById_AndPrefersTopLevel()
        {
            // Arrange
            ListPackageProjectModel project = Project(
                "a.csproj",
                Framework("net8.0",
                    topLevel: new List<ListReportPackage> { Package("zeta"), Package("Beta"), Package("Shared") }),
                Framework("net472",
                    topLevel: new List<ListReportPackage> { Package("ZETA"), Package("alpha") },
                    transitive: new List<ListReportPackage> { Package("shared"), Package("OnlyTransitive") }));

            // Act
            (List<ListReportPackage> topLevel, List<ListReportPackage> transitive) = SponsorReportAggregator.CollapseFrameworks(project);

            // Assert
            Assert.Equal(new[] { "alpha", "Beta", "Shared", "zeta" }, topLevel.Select(p => p.PackageId));
            Assert.Equal("OnlyTransitive", Assert.Single(transitive).PackageId);
        }

        [Fact]
        public void CollapseFrameworks_NoPackages_ReturnsEmptyLists()
        {
            foreach (ListPackageProjectModel project in new[]
            {
                Project("a.csproj", (ListPackageReportFrameworkPackage[])null),
                Project("b.csproj", Framework("net8.0")),
            })
            {
                (List<ListReportPackage> topLevel, List<ListReportPackage> transitive) = SponsorReportAggregator.CollapseFrameworks(project);

                Assert.Empty(topLevel);
                Assert.Empty(transitive);
            }
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

        [Theory]
        [InlineData("s1=a,b; s2=a,b", "s1,s2=>a,b")]
        [InlineData("s1=a,b; s2=b,a", "s1=>a,b | s2=>b,a")]
        [InlineData("s1=a; s2=other; s3=a", "s1,s3=>a | s2=>other")]
        [InlineData("s1=Alpha; s2=alpha", "s1=>Alpha | s2=>alpha")]
        public void MergeBySponsorshipUrls_MergesOnlySourcesReturningTheSameOrderedUrls(string input, string expected)
        {
            IReadOnlyList<PackageSponsorship> sponsorships = input
                .Split(';')
                .Select(entry => entry.Split('='))
                .Select(parts => new PackageSponsorship(parts[0].Trim(), parts[1].Split(',').Select(url => url.Trim()).ToArray()))
                .ToList();

            string actual = string.Join(" | ", SponsorReportAggregator.MergeBySponsorshipUrls(sponsorships)
                .Select(mergedSponsorship => string.Join(",", mergedSponsorship.Sources) + "=>" + string.Join(",", mergedSponsorship.Urls)));

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void MergeBySponsorshipUrls_NullOrEmptySponsorships_ProduceNoEntries()
        {
            Assert.Empty(SponsorReportAggregator.MergeBySponsorshipUrls(null));
            Assert.Empty(SponsorReportAggregator.MergeBySponsorshipUrls(new List<PackageSponsorship>()));
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
