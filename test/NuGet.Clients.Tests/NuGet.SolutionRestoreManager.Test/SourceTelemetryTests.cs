// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using NuGet.Common;
using NuGet.Configuration;
using Xunit;

namespace NuGet.SolutionRestoreManager.Test
{
    public class SourceTelemetryTests
    {
        private static readonly Guid Parent = Guid.Parse("33411664-388A-4C48-A607-A2C554171FCE");

        [Fact]
        public void SourceTelemetry_GivenEmptySourcesVerifyZeroCounts()
        {
            var sources = new List<PackageSource>();
            var summary = SourceTelemetry.GetSourceSummaryEvent(Parent, sources);
            var summaryStrings = GetValuesAsStrings(summary);
            var summaryInts = GetValuesAsInts(summary);

            summaryInts["NumLocalFeeds"].Should().Be(0);
            summaryInts["NumHTTPv2Feeds"].Should().Be(0);
            summaryInts["NumHTTPv3Feeds"].Should().Be(0);
            summaryStrings["ParentId"].Should().Be(Parent.ToString());
            summaryStrings["NuGetOrg"].Should().Be("NotPresent");
        }

        [Fact]
        public void SourceTelemetry_GivenOnlyNuGetV2VerifySummary()
        {
            var sources = new List<PackageSource>()
            {
                new PackageSource("https://www.NuGet.org/api/v2/")
            };

            var summary = SourceTelemetry.GetSourceSummaryEvent(Parent, sources);
            var summaryStrings = GetValuesAsStrings(summary);
            var summaryInts = GetValuesAsInts(summary);

            summaryInts["NumLocalFeeds"].Should().Be(0);
            summaryInts["NumHTTPv2Feeds"].Should().Be(1);
            summaryInts["NumHTTPv3Feeds"].Should().Be(0);
            summaryStrings["ParentId"].Should().Be(Parent.ToString());
            summaryStrings["NuGetOrg"].Should().Be("YesV2");
        }

        [Fact]
        public void SourceTelemetry_GivenOnlyNuGetV3VerifySummary()
        {
            var sources = new List<PackageSource>()
            {
                new PackageSource("https://api.nuget.org/v3/index.JSON")
            };

            var summary = SourceTelemetry.GetSourceSummaryEvent(Parent, sources);
            var summaryStrings = GetValuesAsStrings(summary);
            var summaryInts = GetValuesAsInts(summary);

            summaryInts["NumLocalFeeds"].Should().Be(0);
            summaryInts["NumHTTPv2Feeds"].Should().Be(0);
            summaryInts["NumHTTPv3Feeds"].Should().Be(1);
            summaryStrings["ParentId"].Should().Be(Parent.ToString());
            summaryStrings["NuGetOrg"].Should().Be("YesV3");
        }

        [Fact]
        public void SourceTelemetry_GivenOnlyNuGetV2andV3VerifySummary()
        {
            var sources = new List<PackageSource>()
            {
                new PackageSource("https://www.NuGet.org/api/v2/"),
                new PackageSource("https://api.nuget.org/v3/index.json")
            };

            var summary = SourceTelemetry.GetSourceSummaryEvent(Parent, sources);
            var summaryStrings = GetValuesAsStrings(summary);
            var summaryInts = GetValuesAsInts(summary);

            summaryInts["NumHTTPv2Feeds"].Should().Be(1);
            summaryInts["NumHTTPv3Feeds"].Should().Be(1);
            summaryStrings["NuGetOrg"].Should().Be("YesV3");
        }

        [Fact]
        public void SourceTelemetry_GivenDisabledFeedsVerifyCount()
        {
            var sources = new List<PackageSource>()
            {
                new PackageSource("https://www.NuGet.org/api/v2/", "n1", isEnabled: false),
                new PackageSource("https://api.nuget.org/v3/index.json", "n2", isEnabled: false),
                new PackageSource("packages")
            };

            var summary = SourceTelemetry.GetSourceSummaryEvent(Parent, sources);
            var summaryStrings = GetValuesAsStrings(summary);
            var summaryInts = GetValuesAsInts(summary);

            summaryInts["NumLocalFeeds"].Should().Be(1);
            summaryInts["NumHTTPv2Feeds"].Should().Be(0);
            summaryInts["NumHTTPv3Feeds"].Should().Be(0);
            summaryStrings["NuGetOrg"].Should().Be("NotPresent");
        }

        [Fact]
        public void SourceTelemetry_VerifySubDomainNuGetOrgIsNotCounted()
        {
            var sources = new List<PackageSource>()
            {
                new PackageSource("https://www.NuGet.org.myget.org/www.nuget.org/v2/"),
                new PackageSource("https://api.nuget.org.myget.org/api.nuget.org/index.json")
            };

            var summary = SourceTelemetry.GetSourceSummaryEvent(Parent, sources);
            var summaryStrings = GetValuesAsStrings(summary);
            var summaryInts = GetValuesAsInts(summary);

            summaryStrings["NuGetOrg"].Should().Be("NotPresent");
        }

        [Fact]
        public void SourceTelemetry_LocalFeedsVerifyCount()
        {
            var sources = new List<PackageSource>()
            {
                new PackageSource("packages"),
                new PackageSource(UriUtility.CreateSourceUri(Path.Combine(Path.GetTempPath(), "packages"), UriKind.Absolute).AbsoluteUri),
                new PackageSource(@"\\share\packages"),
            };

            var summary = SourceTelemetry.GetSourceSummaryEvent(Parent, sources);
            var summaryStrings = GetValuesAsStrings(summary);
            var summaryInts = GetValuesAsInts(summary);

            summaryStrings["NuGetOrg"].Should().Be("NotPresent");
            summaryInts["NumLocalFeeds"].Should().Be(3);
            summaryInts["NumHTTPv2Feeds"].Should().Be(0);
            summaryInts["NumHTTPv3Feeds"].Should().Be(0);
        }

        [Fact]
        public void SourceTelemetry_GivenAnInvalidSourceVerifyNoFailures()
        {
            var sources = new List<PackageSource>()
            {
                new PackageSource("file:/bad!|source"),
                new PackageSource("https:/bad"),
            };

            var summary = SourceTelemetry.GetSourceSummaryEvent(Parent, sources);
            var summaryStrings = GetValuesAsStrings(summary);
            var summaryInts = GetValuesAsInts(summary);

            summaryStrings["NuGetOrg"].Should().Be("NotPresent");
            summaryInts["NumLocalFeeds"].Should().Be(2);
            summaryInts["NumHTTPv2Feeds"].Should().Be(0);
            summaryInts["NumHTTPv3Feeds"].Should().Be(0);
        }

        [Fact]
        public void SourceTelemetry_VerifyV3Feed()
        {
            var sources = new List<PackageSource>()
            {
                new PackageSource("http://tempuri.local/index.json")
            };

            var summary = SourceTelemetry.GetSourceSummaryEvent(Parent, sources);
            var summaryStrings = GetValuesAsStrings(summary);
            var summaryInts = GetValuesAsInts(summary);

            summaryStrings["NuGetOrg"].Should().Be("NotPresent");
            summaryInts["NumLocalFeeds"].Should().Be(0);
            summaryInts["NumHTTPv2Feeds"].Should().Be(0);
            summaryInts["NumHTTPv3Feeds"].Should().Be(1);
        }

        [Fact]
        public void SourceTelemetry_VerifyV2Feed()
        {
            var sources = new List<PackageSource>()
            {
                new PackageSource("http://tempuri.local/packages/")
            };

            var summary = SourceTelemetry.GetSourceSummaryEvent(Parent, sources);
            var summaryStrings = GetValuesAsStrings(summary);
            var summaryInts = GetValuesAsInts(summary);

            summaryStrings["NuGetOrg"].Should().Be("NotPresent");
            summaryInts["NumLocalFeeds"].Should().Be(0);
            summaryInts["NumHTTPv2Feeds"].Should().Be(1);
            summaryInts["NumHTTPv3Feeds"].Should().Be(0);
        }

        private static Dictionary<string, string> GetValuesAsStrings(TelemetryEvent item)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var e = item.GetEnumerator();

            while (e.MoveNext())
            {
                values.Add(e.Current.Key, e.Current.Value as string);
            }

            return values;
        }

        private static Dictionary<string, int?> GetValuesAsInts(TelemetryEvent item)
        {
            var values = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);
            var e = item.GetEnumerator();

            while (e.MoveNext())
            {
                values.Add(e.Current.Key, e.Current.Value as int?);
            }

            return values;
        }
    }
}
