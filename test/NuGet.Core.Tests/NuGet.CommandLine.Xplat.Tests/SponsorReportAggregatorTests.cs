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
        [Theory]
        [InlineData("s1=a,b; s2=a,b", "s1,s2=>a,b")]
        [InlineData("s1=a,b; s2=b,a", "s1=>a,b | s2=>b,a")]
        [InlineData("s1=Alpha; s2=alpha", "s1=>Alpha | s2=>alpha")]
        public void MergeBySponsorshipUrls_MergesOnlySourcesReturningTheSameOrderedUrls(string input, string expected)
        {
            IReadOnlyList<PackageSponsorship> sponsorships = input
                .Split(';')
                .Select(entry => entry.Split('='))
                .Select(parts =>
                {
                    string source = parts[0].Trim();
                    string[] urls = parts[1].Split(',').Select(url => url.Trim()).ToArray();
                    return new PackageSponsorship(source, urls);
                })
                .ToList();

            IEnumerable<string> values = SponsorReportAggregator.MergeBySponsorshipUrls(sponsorships)
                .Select(mergedSponsorship => string.Join(",", mergedSponsorship.Sources) + "=>" + string.Join(",", mergedSponsorship.Urls));
            string actual = string.Join(" | ", values);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void CollapseProjectsForConsole_GroupsPackagesByIdAndPrefersTopLevel()
        {
            ListReportPackage topLevelPackage = ListPackageTestHelper.CreateSponsoredPackage("Package.A");
            ListReportPackage transitiveDuplicate = ListPackageTestHelper.CreateSponsoredPackage("package.a");
            ListReportPackage transitivePackage = ListPackageTestHelper.CreateSponsoredPackage("Package.B");
            var projectA = new ListPackageProjectModel("a.csproj", "A")
            {
                TargetFrameworkPackages =
                [
                    new("net8.0", "net8.0")
                    {
                        TopLevelPackages = [topLevelPackage],
                        TransitivePackages = [transitivePackage],
                    },
                ],
            };
            var projectB = new ListPackageProjectModel("b.csproj", "B")
            {
                TargetFrameworkPackages =
                [
                    new("net8.0", "net8.0")
                    {
                        TransitivePackages = [transitiveDuplicate, transitivePackage],
                    },
                ],
            };

            (List<ListReportPackage> topLevel, List<ListReportPackage> transitive) =
                SponsorReportAggregator.CollapseProjectsForConsole([projectA, projectB]);

            Assert.Equal(new[] { "Package.A" }, topLevel.Select(package => package.PackageId));
            Assert.Equal(new[] { "Package.B" }, transitive.Select(package => package.PackageId));
        }
    }
}
