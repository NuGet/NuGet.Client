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
                .Select(parts => new PackageSponsorship(parts[0].Trim(), parts[1].Split(',').Select(url => url.Trim()).ToArray()))
                .ToList();

            string actual = string.Join(" | ", SponsorReportAggregator.MergeBySponsorshipUrls(sponsorships)
                .Select(mergedSponsorship => string.Join(",", mergedSponsorship.Sources) + "=>" + string.Join(",", mergedSponsorship.Urls)));

            Assert.Equal(expected, actual);
        }
    }
}
