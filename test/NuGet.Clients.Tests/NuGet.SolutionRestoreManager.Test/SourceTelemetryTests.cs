// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Moq;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.PackageManagement.Telemetry;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.VisualStudio.Telemetry;
using Xunit;

namespace NuGet.SolutionRestoreManager.Test
{
    public class SourceTelemetryTests
    {
        private const string NumLocalFeeds = "NumLocalFeeds";
        private const string NumHTTPv2Feeds = "NumHTTPv2Feeds";
        private const string NumHTTPv3Feeds = "NumHTTPv3Feeds";
        private const string ParentId = "ParentId";
        private const string NuGetOrg = "NuGetOrg";
        private const string NotPresent = "NotPresent";
        private const string YesV2 = "YesV2";
        private const string YesV3 = "YesV3";
        private const string YesV3AndV2 = "YesV3AndV2";
        private const string VsOfflinePackages = "VsOfflinePackages";
        private const string DotnetCuratedFeed = "DotnetCuratedFeed";
        private const string ProtocolRequests = "protocol.requests";
        private const string ProtocolBytes = "protocol.bytes";
        private const string ProtocolDuration = "protocol.duration";
        private const string NumHTTPSv2Feeds = "NumHTTPSv2Feeds";
        private const string NumHTTPSv3Feeds = "NumHTTPSv3Feeds";
        private const string ServiceIndexCacheAvailable = "ServiceIndexCacheAvailable";
        private const string NumHTTPSSourcesWithAnHTTPResource = "NumHTTPSSourcesWithAnHTTPResource";

        private static readonly Guid Parent = Guid.Parse("33411664-388A-4C48-A607-A2C554171FCE");
        private static readonly PackageSourceTelemetry.Totals ProtocolDiagnosticTotals = new PackageSourceTelemetry.Totals(1, 2, TimeSpan.FromMilliseconds(3));

        [Fact]
        public void GivenEmptySourcesVerifyEventNameForRestore()
        {
            var sourceRepositories = new List<SourceRepository>();
            var summary = SourceTelemetry.GetRestoreSourceSummaryEvent(Parent, sourceRepositories, ProtocolDiagnosticTotals);

            summary.Name.Should().Be("RestorePackageSourceSummary");
        }
        [Fact]
        public void GivenEmptySourcesVerifyEventNameForSearch()
        {
            var sourceRepositories = new List<SourceRepository>();
            var summary = SourceTelemetry.GetSearchSourceSummaryEvent(Parent, sourceRepositories, ProtocolDiagnosticTotals);

            summary.Name.Should().Be("SearchPackageSourceSummary");
        }

        [Fact]
        public void GivenEmptySourcesVerifyZeroCounts()
        {
            var sourceRepositories = new List<SourceRepository>();
            var summary = SourceTelemetry.GetRestoreSourceSummaryEvent(Parent, sourceRepositories, ProtocolDiagnosticTotals);
            var summaryStrings = GetValuesAsStrings(summary);
            var summaryInts = GetValuesAsInts(summary);
            var summaryBools = GetValuesAsBools(summary);

            summaryInts[NumLocalFeeds].Should().Be(0);
            summaryInts[NumHTTPv2Feeds].Should().Be(0);
            summaryInts[NumHTTPv3Feeds].Should().Be(0);
            summaryInts[NumHTTPSv2Feeds].Should().Be(0);
            summaryInts[NumHTTPSv3Feeds].Should().Be(0);
            summaryStrings[ParentId].Should().Be(Parent.ToString());
            summaryStrings[NuGetOrg].Should().Be(NotPresent);
            summaryBools[VsOfflinePackages].Should().Be(false);
            summaryBools[DotnetCuratedFeed].Should().Be(false);
            summaryInts[NumHTTPSSourcesWithAnHTTPResource].Should().Be(0);
            summaryBools[ServiceIndexCacheAvailable].Should().Be(false);
        }

        [Theory]
        [InlineData("https://www.nuget.org/api/v2/curated-feeds/microsoftdotnet/")]
        [InlineData("https://nuget.org/api/v2/curated-feeds/microsoftdotnet/")]
        [InlineData("https://www.nuget.org/api/v2/curated-feeds/microsoftdotnet")]
        [InlineData("https://www.nuget.org/api/v2/curated-feeds/MICROSOFTDOTNET/")]
        public void GivenNuGetCuratedFeedVerifySummary(string source)
        {
            var sourceRepositories = new List<SourceRepository>()
            {
                Repository.Factory.GetCoreV3(new PackageSource(source))
            };

            var summary = SourceTelemetry.GetRestoreSourceSummaryEvent(Parent, sourceRepositories, ProtocolDiagnosticTotals);
            var summaryStrings = GetValuesAsStrings(summary);
            var summaryInts = GetValuesAsInts(summary);
            var summaryBools = GetValuesAsBools(summary);

            summaryInts[NumLocalFeeds].Should().Be(0);
            summaryInts[NumHTTPv2Feeds].Should().Be(1);
            summaryInts[NumHTTPv3Feeds].Should().Be(0);
            summaryInts[NumHTTPSv2Feeds].Should().Be(1);
            summaryInts[NumHTTPSv3Feeds].Should().Be(0);
            summaryStrings[ParentId].Should().Be(Parent.ToString());
            summaryStrings[NuGetOrg].Should().Be(NotPresent);
            summaryBools[VsOfflinePackages].Should().Be(false);
            summaryBools[DotnetCuratedFeed].Should().Be(true);
            summaryInts[NumHTTPSSourcesWithAnHTTPResource].Should().Be(0);
            summaryBools[ServiceIndexCacheAvailable].Should().Be(false);
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData(true)]
        [InlineData(false)]
        public void GivenVsPackagesSourceVerifySummary(bool trailingSlash)
        {
            // Specify both so this test works on 32-bit and 64-bit processes.
            var suffix = trailingSlash ? @"\" : string.Empty;
            var sourceRepos = new List<SourceRepository>()
            {
                Repository.Factory.GetCoreV3(new PackageSource(@"C:\Program Files (x86)\Microsoft SDKs\NuGetPackages" + suffix)),
                Repository.Factory.GetCoreV3(new PackageSource(@"C:\Program Files\Microsoft SDKs\NuGetPackages" + suffix))
            };

            var summary = SourceTelemetry.GetRestoreSourceSummaryEvent(Parent, sourceRepos, ProtocolDiagnosticTotals);
            var summaryStrings = GetValuesAsStrings(summary);
            var summaryInts = GetValuesAsInts(summary);
            var summaryBools = GetValuesAsBools(summary);

            summaryInts[NumLocalFeeds].Should().Be(2);
            summaryInts[NumHTTPv2Feeds].Should().Be(0);
            summaryInts[NumHTTPv3Feeds].Should().Be(0);
            summaryInts[NumHTTPSv2Feeds].Should().Be(0);
            summaryInts[NumHTTPSv3Feeds].Should().Be(0);
            summaryStrings[ParentId].Should().Be(Parent.ToString());
            summaryStrings[NuGetOrg].Should().Be(NotPresent);
            summaryBools[VsOfflinePackages].Should().Be(true);
            summaryBools[DotnetCuratedFeed].Should().Be(false);
            summaryInts[NumHTTPSSourcesWithAnHTTPResource].Should().Be(0);
            summaryBools[ServiceIndexCacheAvailable].Should().Be(false);
        }

        [Fact]
        public void GivenNuGetV2WithoutSubdomainVerifySummary()
        {
            var sources = new List<SourceRepository>()
            {
                Repository.Factory.GetCoreV3(new PackageSource("https://NuGet.org/api/v2/"))
            };

            var summary = SourceTelemetry.GetRestoreSourceSummaryEvent(Parent, sources, ProtocolDiagnosticTotals);
            var summaryStrings = GetValuesAsStrings(summary);
            var summaryInts = GetValuesAsInts(summary);
            var summaryBools = GetValuesAsBools(summary);

            summaryInts[NumLocalFeeds].Should().Be(0);
            summaryInts[NumHTTPv2Feeds].Should().Be(1);
            summaryInts[NumHTTPv3Feeds].Should().Be(0);
            summaryInts[NumHTTPSv2Feeds].Should().Be(1);
            summaryInts[NumHTTPSv3Feeds].Should().Be(0);
            summaryStrings[ParentId].Should().Be(Parent.ToString());
            summaryStrings[NuGetOrg].Should().Be(YesV2);
            summaryBools[VsOfflinePackages].Should().Be(false);
            summaryBools[DotnetCuratedFeed].Should().Be(false);
            summaryInts[NumHTTPSSourcesWithAnHTTPResource].Should().Be(0);
            summaryBools[ServiceIndexCacheAvailable].Should().Be(false);
        }

        [Fact]
        public void GivenOnlyNuGetV2VerifySummary()
        {
            var sources = new List<SourceRepository>()
            {
                Repository.Factory.GetCoreV3(new PackageSource("https://www.NuGet.org/api/v2/"))
            };

            var summary = SourceTelemetry.GetRestoreSourceSummaryEvent(Parent, sources, ProtocolDiagnosticTotals);
            var summaryStrings = GetValuesAsStrings(summary);
            var summaryInts = GetValuesAsInts(summary);
            var summaryBools = GetValuesAsBools(summary);

            summaryInts[NumLocalFeeds].Should().Be(0);
            summaryInts[NumHTTPv2Feeds].Should().Be(1);
            summaryInts[NumHTTPv3Feeds].Should().Be(0);
            summaryInts[NumHTTPSv2Feeds].Should().Be(1);
            summaryInts[NumHTTPSv3Feeds].Should().Be(0);
            summaryStrings[ParentId].Should().Be(Parent.ToString());
            summaryStrings[NuGetOrg].Should().Be(YesV2);
            summaryBools[VsOfflinePackages].Should().Be(false);
            summaryBools[DotnetCuratedFeed].Should().Be(false);
        }

        [Fact]
        public void GivenOnlyNuGetV3WithoutSubdomainVerifySummary()
        {
            var sources = new List<SourceRepository>()
            {
                Repository.Factory.GetCoreV3(new PackageSource("https://nuget.org/v3/index.JSON"))
            };

            var summary = SourceTelemetry.GetRestoreSourceSummaryEvent(Parent, sources, ProtocolDiagnosticTotals);
            var summaryStrings = GetValuesAsStrings(summary);
            var summaryInts = GetValuesAsInts(summary);
            var summaryBools = GetValuesAsBools(summary);

            summaryInts[NumLocalFeeds].Should().Be(0);
            summaryInts[NumHTTPv2Feeds].Should().Be(0);
            summaryInts[NumHTTPv3Feeds].Should().Be(1);
            summaryInts[NumHTTPSv2Feeds].Should().Be(0);
            summaryInts[NumHTTPSv3Feeds].Should().Be(1);
            summaryStrings[ParentId].Should().Be(Parent.ToString());
            summaryStrings[NuGetOrg].Should().Be(YesV3);
            summaryBools[VsOfflinePackages].Should().Be(false);
            summaryBools[DotnetCuratedFeed].Should().Be(false);
            summaryInts[NumHTTPSSourcesWithAnHTTPResource].Should().Be(0);
            summaryBools[ServiceIndexCacheAvailable].Should().Be(false);
        }

        [Fact]
        public void GivenOnlyNuGetV3VerifySummary()
        {
            var sources = new List<SourceRepository>()
            {
                Repository.Factory.GetCoreV3(new PackageSource("https://api.nuget.org/v3/index.JSON"))
            };

            var summary = SourceTelemetry.GetRestoreSourceSummaryEvent(Parent, sources, ProtocolDiagnosticTotals);
            var summaryStrings = GetValuesAsStrings(summary);
            var summaryInts = GetValuesAsInts(summary);
            var summaryBools = GetValuesAsBools(summary);

            summaryInts[NumLocalFeeds].Should().Be(0);
            summaryInts[NumHTTPv2Feeds].Should().Be(0);
            summaryInts[NumHTTPv3Feeds].Should().Be(1);
            summaryInts[NumHTTPSv2Feeds].Should().Be(0);
            summaryInts[NumHTTPSv3Feeds].Should().Be(1);
            summaryStrings[ParentId].Should().Be(Parent.ToString());
            summaryStrings[NuGetOrg].Should().Be(YesV3);
            summaryBools[VsOfflinePackages].Should().Be(false);
            summaryBools[DotnetCuratedFeed].Should().Be(false);
            summaryInts[NumHTTPSSourcesWithAnHTTPResource].Should().Be(0);
            summaryBools[ServiceIndexCacheAvailable].Should().Be(false);
        }

        [Fact]
        public void GivenOnlyNuGetV3andV2VerifySummary()
        {
            var sources = new List<SourceRepository>()
            {
                Repository.Factory.GetCoreV3(new PackageSource("https://api.nuget.org/v3/index.json")),
                Repository.Factory.GetCoreV3(new PackageSource("https://www.NuGet.org/api/v2/")),
            };

            var summary = SourceTelemetry.GetRestoreSourceSummaryEvent(Parent, sources, ProtocolDiagnosticTotals);
            var summaryStrings = GetValuesAsStrings(summary);
            var summaryInts = GetValuesAsInts(summary);
            var summaryBools = GetValuesAsBools(summary);

            summaryInts[NumLocalFeeds].Should().Be(0);
            summaryInts[NumHTTPv2Feeds].Should().Be(1);
            summaryInts[NumHTTPv3Feeds].Should().Be(1);
            summaryInts[NumHTTPSv2Feeds].Should().Be(1);
            summaryInts[NumHTTPSv3Feeds].Should().Be(1);
            summaryStrings[ParentId].Should().Be(Parent.ToString());
            summaryStrings[NuGetOrg].Should().Be(YesV3AndV2);
            summaryBools[VsOfflinePackages].Should().Be(false);
            summaryBools[DotnetCuratedFeed].Should().Be(false);
            summaryInts[NumHTTPSSourcesWithAnHTTPResource].Should().Be(0);
            summaryBools[ServiceIndexCacheAvailable].Should().Be(false);
        }

        [Fact]
        public void GivenOnlyNuGetV2andV3VerifySummary()
        {
            var sources = new List<SourceRepository>()
            {
                Repository.Factory.GetCoreV3(new PackageSource("https://www.NuGet.org/api/v2/")),
                Repository.Factory.GetCoreV3(new PackageSource("https://api.nuget.org/v3/index.json")),
            };

            var summary = SourceTelemetry.GetRestoreSourceSummaryEvent(Parent, sources, ProtocolDiagnosticTotals);
            var summaryStrings = GetValuesAsStrings(summary);
            var summaryInts = GetValuesAsInts(summary);
            var summaryBools = GetValuesAsBools(summary);

            summaryInts[NumLocalFeeds].Should().Be(0);
            summaryInts[NumHTTPv2Feeds].Should().Be(1);
            summaryInts[NumHTTPv3Feeds].Should().Be(1);
            summaryInts[NumHTTPSv2Feeds].Should().Be(1);
            summaryInts[NumHTTPSv3Feeds].Should().Be(1);
            summaryStrings[ParentId].Should().Be(Parent.ToString());
            summaryStrings[NuGetOrg].Should().Be(YesV3AndV2);
            summaryBools[VsOfflinePackages].Should().Be(false);
            summaryBools[DotnetCuratedFeed].Should().Be(false);
            summaryInts[NumHTTPSSourcesWithAnHTTPResource].Should().Be(0);
            summaryBools[ServiceIndexCacheAvailable].Should().Be(false);
        }

        [Fact]
        public void GivenDisabledFeedsVerifyCount()
        {
            var sources = new List<SourceRepository>()
            {
                Repository.Factory.GetCoreV3(new PackageSource("https://www.NuGet.org/api/v2/", "n1", isEnabled: false)),
                Repository.Factory.GetCoreV3(new PackageSource("https://api.nuget.org/v3/index.json", "n2", isEnabled: false)),
                Repository.Factory.GetCoreV3(new PackageSource("packages"))
            };

            var summary = SourceTelemetry.GetRestoreSourceSummaryEvent(Parent, sources, ProtocolDiagnosticTotals);
            var summaryStrings = GetValuesAsStrings(summary);
            var summaryInts = GetValuesAsInts(summary);
            var summaryBools = GetValuesAsBools(summary);

            summaryInts[NumLocalFeeds].Should().Be(1);
            summaryInts[NumHTTPv2Feeds].Should().Be(0);
            summaryInts[NumHTTPv3Feeds].Should().Be(0);
            summaryInts[NumHTTPSv2Feeds].Should().Be(0);
            summaryInts[NumHTTPSv3Feeds].Should().Be(0);
            summaryStrings[ParentId].Should().Be(Parent.ToString());
            summaryStrings[NuGetOrg].Should().Be(NotPresent);
            summaryBools[VsOfflinePackages].Should().Be(false);
            summaryBools[DotnetCuratedFeed].Should().Be(false);
            summaryInts[NumHTTPSSourcesWithAnHTTPResource].Should().Be(0);
            summaryBools[ServiceIndexCacheAvailable].Should().Be(false);
        }

        [Fact]
        public void VerifySubDomainNuGetOrgIsNotCounted()
        {
            var sources = new List<SourceRepository>()
            {
                Repository.Factory.GetCoreV3(new PackageSource("https://www.NuGet.org.myget.org/www.nuget.org/v2/")),
                Repository.Factory.GetCoreV3(new PackageSource("https://api.nuget.org.myget.org/api.nuget.org/index.json"))
            };

            var summary = SourceTelemetry.GetRestoreSourceSummaryEvent(Parent, sources, ProtocolDiagnosticTotals);
            var summaryStrings = GetValuesAsStrings(summary);
            var summaryInts = GetValuesAsInts(summary);
            var summaryBools = GetValuesAsBools(summary);

            summaryInts[NumLocalFeeds].Should().Be(0);
            summaryInts[NumHTTPv2Feeds].Should().Be(1);
            summaryInts[NumHTTPv3Feeds].Should().Be(1);
            summaryInts[NumHTTPSv2Feeds].Should().Be(1);
            summaryInts[NumHTTPSv3Feeds].Should().Be(1);
            summaryStrings[ParentId].Should().Be(Parent.ToString());
            summaryStrings[NuGetOrg].Should().Be(NotPresent);
            summaryBools[VsOfflinePackages].Should().Be(false);
            summaryBools[DotnetCuratedFeed].Should().Be(false);
            summaryInts[NumHTTPSSourcesWithAnHTTPResource].Should().Be(0);
            summaryBools[ServiceIndexCacheAvailable].Should().Be(false);
        }

        [Fact]
        public void LocalFeedsVerifyCount()
        {
            var sources = new List<SourceRepository>()
            {
                Repository.Factory.GetCoreV3(new PackageSource("packages")),
                Repository.Factory.GetCoreV3(new PackageSource(UriUtility.CreateSourceUri(Path.Combine(Path.GetTempPath(), "packages"), UriKind.Absolute).AbsoluteUri)),
                Repository.Factory.GetCoreV3(new PackageSource(@"\\share\packages")),
            };

            var summary = SourceTelemetry.GetRestoreSourceSummaryEvent(Parent, sources, ProtocolDiagnosticTotals);
            var summaryStrings = GetValuesAsStrings(summary);
            var summaryInts = GetValuesAsInts(summary);
            var summaryBools = GetValuesAsBools(summary);

            summaryInts[NumLocalFeeds].Should().Be(3);
            summaryInts[NumHTTPv2Feeds].Should().Be(0);
            summaryInts[NumHTTPv3Feeds].Should().Be(0);
            summaryInts[NumHTTPSv2Feeds].Should().Be(0);
            summaryInts[NumHTTPSv3Feeds].Should().Be(0);
            summaryStrings[ParentId].Should().Be(Parent.ToString());
            summaryStrings[NuGetOrg].Should().Be(NotPresent);
            summaryBools[VsOfflinePackages].Should().Be(false);
            summaryBools[DotnetCuratedFeed].Should().Be(false);
            summaryInts[NumHTTPSSourcesWithAnHTTPResource].Should().Be(0);
            summaryBools[ServiceIndexCacheAvailable].Should().Be(false);
        }

        [Fact]
        public void GetRestoreSourceSummaryEvent_WithAnHttpsSourceAndHttpResource_LogsNumHTTPSSourcesWithAnHTTPResource()
        {
            var mockRepository = new Mock<SourceRepository>();
            mockRepository.Setup(r => r.PackageSource).Returns(new PackageSource("https://test"));
            mockRepository
            .Setup(r => r.GetServiceIndexV3FromCache(out It.Ref<ServiceIndexResourceV3>.IsAny))
            .Returns(true)
            .Callback((out ServiceIndexResourceV3 result) =>
            {
                result = new ServiceIndexResourceV3(JObject.Parse(CreateServiceIndexWithFourResourceTypesTwoHTTP()), DateTime.Now);
            });

            var sources = new List<SourceRepository>()
            {
                mockRepository.Object
            };

            var summary = SourceTelemetry.GetRestoreSourceSummaryEvent(Parent, sources, ProtocolDiagnosticTotals);
            var summaryStrings = GetValuesAsStrings(summary);
            var summaryInts = GetValuesAsInts(summary);
            var summaryBools = GetValuesAsBools(summary);

            summaryInts[NumLocalFeeds].Should().Be(0);
            summaryInts[NumHTTPv2Feeds].Should().Be(1);
            summaryInts[NumHTTPv3Feeds].Should().Be(0);
            summaryInts[NumHTTPSv2Feeds].Should().Be(1);
            summaryInts[NumHTTPSv3Feeds].Should().Be(0);
            summaryStrings[ParentId].Should().Be(Parent.ToString());
            summaryStrings[NuGetOrg].Should().Be(NotPresent);
            summaryBools[VsOfflinePackages].Should().Be(false);
            summaryBools[DotnetCuratedFeed].Should().Be(false);
            summaryInts[NumHTTPSSourcesWithAnHTTPResource].Should().Be(1);
            summaryBools[ServiceIndexCacheAvailable].Should().Be(true);
        }

        [Fact]
        public void GetRestoreSourceSummaryEvent_WithTwoHttpsSourceAndHttpResource_LogsNumHTTPSSourcesWithAnHTTPResource()
        {
            var mockRepository = new Mock<SourceRepository>();
            mockRepository.Setup(r => r.PackageSource).Returns(new PackageSource("https://test"));
            mockRepository
            .Setup(r => r.GetServiceIndexV3FromCache(out It.Ref<ServiceIndexResourceV3>.IsAny))
            .Returns(true)
            .Callback((out ServiceIndexResourceV3 result) =>
            {
                result = new ServiceIndexResourceV3(JObject.Parse(CreateServiceIndexWithFourResourceTypesTwoHTTP()), DateTime.Now);
            });

            var mockRepository2 = new Mock<SourceRepository>();
            mockRepository2.Setup(r => r.PackageSource).Returns(new PackageSource("https://test2"));
            mockRepository2
            .Setup(r => r.GetServiceIndexV3FromCache(out It.Ref<ServiceIndexResourceV3>.IsAny))
            .Returns(true)
            .Callback((out ServiceIndexResourceV3 result) =>
            {
                result = new ServiceIndexResourceV3(JObject.Parse(CreateServiceIndexWithAllHttpsResources()), DateTime.Now);
            });

            var sources = new List<SourceRepository>()
            {
                mockRepository.Object,
                mockRepository2.Object
            };

            var summary = SourceTelemetry.GetRestoreSourceSummaryEvent(Parent, sources, ProtocolDiagnosticTotals);
            var summaryStrings = GetValuesAsStrings(summary);
            var summaryInts = GetValuesAsInts(summary);
            var summaryBools = GetValuesAsBools(summary);

            summaryInts[NumLocalFeeds].Should().Be(0);
            summaryInts[NumHTTPv2Feeds].Should().Be(2);
            summaryInts[NumHTTPv3Feeds].Should().Be(0);
            summaryInts[NumHTTPSv2Feeds].Should().Be(2);
            summaryInts[NumHTTPSv3Feeds].Should().Be(0);
            summaryStrings[ParentId].Should().Be(Parent.ToString());
            summaryStrings[NuGetOrg].Should().Be(NotPresent);
            summaryBools[VsOfflinePackages].Should().Be(false);
            summaryBools[DotnetCuratedFeed].Should().Be(false);
            summaryInts[NumHTTPSSourcesWithAnHTTPResource].Should().Be(1);
            summaryBools[ServiceIndexCacheAvailable].Should().Be(true);
        }

        [Fact]
        public void GivenAnInvalidSourceVerifyNoFailures()
        {
            var sources = new List<SourceRepository>()
            {
                Repository.Factory.GetCoreV3(new PackageSource("file:/bad!|source")),
                Repository.Factory.GetCoreV3(new PackageSource("https:/bad")),
            };

            var summary = SourceTelemetry.GetRestoreSourceSummaryEvent(Parent, sources, ProtocolDiagnosticTotals);
            var summaryStrings = GetValuesAsStrings(summary);
            var summaryInts = GetValuesAsInts(summary);
            var summaryBools = GetValuesAsBools(summary);

            summaryInts[NumLocalFeeds].Should().Be(2);
            summaryInts[NumHTTPv2Feeds].Should().Be(0);
            summaryInts[NumHTTPv3Feeds].Should().Be(0);
            summaryInts[NumHTTPSv2Feeds].Should().Be(0);
            summaryInts[NumHTTPSv3Feeds].Should().Be(0);
            summaryStrings[ParentId].Should().Be(Parent.ToString());
            summaryStrings[NuGetOrg].Should().Be(NotPresent);
            summaryBools[VsOfflinePackages].Should().Be(false);
            summaryBools[DotnetCuratedFeed].Should().Be(false);
            summaryInts[NumHTTPSSourcesWithAnHTTPResource].Should().Be(0);
            summaryBools[ServiceIndexCacheAvailable].Should().Be(false);
        }

        [Fact]
        public void VerifyV3Feed()
        {
            var sources = new List<SourceRepository>()
            {
                Repository.Factory.GetCoreV3(new PackageSource("http://tempuri.local/index.json"))
            };

            var summary = SourceTelemetry.GetRestoreSourceSummaryEvent(Parent, sources, ProtocolDiagnosticTotals);
            var summaryStrings = GetValuesAsStrings(summary);
            var summaryInts = GetValuesAsInts(summary);
            var summaryBools = GetValuesAsBools(summary);

            summaryInts[NumLocalFeeds].Should().Be(0);
            summaryInts[NumHTTPv2Feeds].Should().Be(0);
            summaryInts[NumHTTPv3Feeds].Should().Be(1);
            summaryInts[NumHTTPSv2Feeds].Should().Be(0);
            summaryInts[NumHTTPSv3Feeds].Should().Be(0);
            summaryStrings[ParentId].Should().Be(Parent.ToString());
            summaryStrings[NuGetOrg].Should().Be(NotPresent);
            summaryBools[VsOfflinePackages].Should().Be(false);
            summaryBools[DotnetCuratedFeed].Should().Be(false);
            summaryInts[NumHTTPSSourcesWithAnHTTPResource].Should().Be(0);
            summaryBools[ServiceIndexCacheAvailable].Should().Be(false);
        }

        [Fact]
        public void VerifyV2Feed()
        {
            var sources = new List<SourceRepository>()
            {
                Repository.Factory.GetCoreV3(new PackageSource("http://tempuri.local/packages/"))
            };

            var summary = SourceTelemetry.GetRestoreSourceSummaryEvent(Parent, sources, ProtocolDiagnosticTotals);
            var summaryStrings = GetValuesAsStrings(summary);
            var summaryInts = GetValuesAsInts(summary);
            var summaryBools = GetValuesAsBools(summary);

            summaryInts[NumLocalFeeds].Should().Be(0);
            summaryInts[NumHTTPv2Feeds].Should().Be(1);
            summaryInts[NumHTTPv3Feeds].Should().Be(0);
            summaryInts[NumHTTPSv2Feeds].Should().Be(0);
            summaryInts[NumHTTPSv3Feeds].Should().Be(0);
            summaryStrings[ParentId].Should().Be(Parent.ToString());
            summaryStrings[NuGetOrg].Should().Be(NotPresent);
            summaryBools[VsOfflinePackages].Should().Be(false);
            summaryBools[DotnetCuratedFeed].Should().Be(false);
            summaryInts[NumHTTPSSourcesWithAnHTTPResource].Should().Be(0);
            summaryBools[ServiceIndexCacheAvailable].Should().Be(false);
        }

        [Fact]
        public void VerifyProtocolDiagnosticTotals()
        {
            var sources = new List<SourceRepository>();
            var telemetry = SourceTelemetry.GetRestoreSourceSummaryEvent(Parent, sources, ProtocolDiagnosticTotals);

            telemetry[ProtocolRequests].Should().Be(1);
            telemetry[ProtocolBytes].Should().Be(2L);
            telemetry[ProtocolDuration].Should().Be(3.0);
        }

        [Fact]
        public void LocalAndHttpSources_WithHTTPSv2Feeds_FeedCountsAreCorrect()
        {
            var sources = new List<SourceRepository>()
            {
                Repository.Factory.GetCoreV3(new PackageSource(@"\\share\packages")),
                Repository.Factory.GetCoreV3(new PackageSource("http://nugettest.org/v3/index.JSON")),
                Repository.Factory.GetCoreV3(new PackageSource("http://www.nuget.org/api/v2/curated-feeds/microsoftdotnet")), //v2
                Repository.Factory.GetCoreV3(new PackageSource("http://tempuri.local/index.json")),
                Repository.Factory.GetCoreV3(new PackageSource("http://nuget.org/v3/index.JSON")),
                Repository.Factory.GetCoreV3(new PackageSource("https://www.NuGet.org/api/v2/")) //v2 and HTTPS
            };

            var summary = SourceTelemetry.GetRestoreSourceSummaryEvent(Parent, sources, ProtocolDiagnosticTotals);
            var summaryStrings = GetValuesAsStrings(summary);
            var summaryInts = GetValuesAsInts(summary);
            var summaryBools = GetValuesAsBools(summary);

            summaryStrings[ParentId].Should().Be(Parent.ToString());

            int? numLocalFeeds = summaryInts[NumLocalFeeds];
            int? numHTTPv2Feeds = summaryInts[NumHTTPv2Feeds];
            int? numHTTPv3Feeds = summaryInts[NumHTTPv3Feeds];
            int? numHTTPSv2Feeds = summaryInts[NumHTTPSv2Feeds];
            int? numHTTPSv3Feeds = summaryInts[NumHTTPSv3Feeds];

            numLocalFeeds.Should().Be(1);
            numHTTPv2Feeds.Should().Be(2);
            numHTTPv3Feeds.Should().Be(3);
            numHTTPSv2Feeds.Should().Be(1);
            numHTTPSv3Feeds.Should().Be(0);

            int? totalFeeds = numLocalFeeds + numHTTPv2Feeds + numHTTPv3Feeds;
            totalFeeds.Should().Be(sources.Count);
        }

        [Fact]
        public void LocalAndHttpSources_WithHTTPSv3Feeds_FeedCountsAreCorrect()
        {
            var sources = new List<SourceRepository>()
            {
                Repository.Factory.GetCoreV3(new PackageSource(@"\\share\packages")),
                Repository.Factory.GetCoreV3(new PackageSource("https://nugettest.org/v3/index.JSON")),
                Repository.Factory.GetCoreV3(new PackageSource("http://tempuri.local/index.json")),
                Repository.Factory.GetCoreV3(new PackageSource("https://nuget.org/v3/index.JSON"))
            };

            TelemetryEvent summary = SourceTelemetry.GetRestoreSourceSummaryEvent(Parent, sources, ProtocolDiagnosticTotals);
            Dictionary<string, string> summaryStrings = GetValuesAsStrings(summary);
            Dictionary<string, int?> summaryInts = GetValuesAsInts(summary);

            summaryStrings[ParentId].Should().Be(Parent.ToString());

            int? numLocalFeeds = summaryInts[NumLocalFeeds];
            int? numHTTPv2Feeds = summaryInts[NumHTTPv2Feeds];
            int? numHTTPv3Feeds = summaryInts[NumHTTPv3Feeds];
            int? numHTTPSv2Feeds = summaryInts[NumHTTPSv2Feeds];
            int? numHTTPSv3Feeds = summaryInts[NumHTTPSv3Feeds];

            numLocalFeeds.Should().Be(1);
            numHTTPv2Feeds.Should().Be(0);
            numHTTPv3Feeds.Should().Be(3);
            numHTTPSv2Feeds.Should().Be(0);
            numHTTPSv3Feeds.Should().Be(2);

            int? totalFeeds = numLocalFeeds + numHTTPv2Feeds + numHTTPv3Feeds;
            totalFeeds.Should().Be(sources.Count);
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

        private static Dictionary<string, bool?> GetValuesAsBools(TelemetryEvent item)
        {
            var values = new Dictionary<string, bool?>(StringComparer.OrdinalIgnoreCase);
            var e = item.GetEnumerator();

            while (e.MoveNext())
            {
                values.Add(e.Current.Key, e.Current.Value as bool?);
            }

            return values;
        }

        private static string CreateServiceIndexWithFourResourceTypesTwoHTTP()
        {
            var obj = new JObject
            {
                { "version", "3.1.0-beta" },
                { "resources", new JArray
                    {
                        new JObject
                        {
                            { "@type", "A" },
                            { "@id", "http://tempuri.org/A/5.0.0/2" },
                            { "clientVersion", "5.0.0" },
                        },
                        new JObject
                        {
                            { "@type", "B" },
                            { "@id", "http://tempuri.org/A/5.0.0/1" },
                            { "clientVersion", "5.0.0" },
                        },
                        new JObject
                        {
                            { "@type", "C" },
                            { "@id", "https://test" },
                            { "clientVersion", "4.0.0" },
                        },
                        new JObject
                        {
                            { "@type", "D" },
                            { "@id", "https://test" },
                            { "clientVersion", "5.0.0" },
                        },
                    }
                }
            };

            return obj.ToString();
        }

        private static string CreateServiceIndexWithAllHttpsResources()
        {
            var obj = new JObject
            {
                { "version", "3.1.0-beta" },
                { "resources", new JArray
                    {
                        new JObject
                        {
                            { "@type", "C" },
                            { "@id", "https://test" },
                            { "clientVersion", "4.0.0" },
                        },
                        new JObject
                        {
                            { "@type", "D" },
                            { "@id", "https://test" },
                            { "clientVersion", "5.0.0" },
                        },
                    }
                }
            };

            return obj.ToString();
        }
    }
}
