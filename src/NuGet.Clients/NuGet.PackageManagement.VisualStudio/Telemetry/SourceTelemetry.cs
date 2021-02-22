// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.VisualStudio.Telemetry;

namespace NuGet.PackageManagement.Telemetry
{
    public static class SourceTelemetry
    {
        [Flags]
        private enum HttpStyle
        {
            NotPresent = 0,

            /// <summary>
            /// This is not set for the "microsoftdotnet" nuget.org curated feed.
            /// </summary>
            YesV2 = 1,

            YesV3 = 2,

            YesV3AndV2 = YesV3 | YesV2,
        }

        public static TelemetryEvent GetRestoreSourceSummaryEvent(
            Guid parentId,
            IEnumerable<PackageSource> packageSources,
            PackageSourceTelemetry.Totals protocolDiagnosticTotals)
        {
            return GetSourceSummaryEvent(
                "RestorePackageSourceSummary",
                parentId,
                packageSources,
                protocolDiagnosticTotals);
        }

        public static TelemetryEvent GetSearchSourceSummaryEvent(
            Guid parentId,
            IEnumerable<PackageSource> packageSources,
            PackageSourceTelemetry.Totals protocolDiagnosticTotals)
        {
            return GetSourceSummaryEvent(
                "SearchPackageSourceSummary",
                parentId,
                packageSources,
                protocolDiagnosticTotals);
        }

        /// <summary>
        /// Create a SourceSummaryEvent event with counts of local vs http and v2 vs v3 feeds.
        /// </summary>
        private static TelemetryEvent GetSourceSummaryEvent(
            string eventName,
            Guid parentId,
            IEnumerable<PackageSource> packageSources,
            PackageSourceTelemetry.Totals protocolDiagnosticTotals)
        {
            var local = 0;
            var httpV2 = 0;
            var httpV3 = 0;
            var nugetOrg = HttpStyle.NotPresent;
            var vsOfflinePackages = false;
            var dotnetCuratedFeed = false;

            if (packageSources != null)
            {
                foreach (var source in packageSources)
                {
                    // Ignore disabled sources
                    if (source.IsEnabled)
                    {
                        if (source.IsHttp)
                        {
                            if (TelemetryUtility.IsHttpV3(source))
                            {
                                // Http V3 feed
                                httpV3++;

                                if (TelemetryUtility.IsNuGetOrg(source.Source))
                                {
                                    nugetOrg |= HttpStyle.YesV3;
                                }
                            }
                            else
                            {
                                // Http V2 feed
                                httpV2++;

                                if (TelemetryUtility.IsNuGetOrg(source.Source))
                                {
                                    if (source.Source.IndexOf(
                                        "api/v2/curated-feeds/microsoftdotnet",
                                        StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        dotnetCuratedFeed = true;
                                    }
                                    else
                                    {
                                        nugetOrg |= HttpStyle.YesV2;
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Local or UNC feed
                            local++;

                            if (TelemetryUtility.IsVsOfflineFeed(source))
                            {
                                vsOfflinePackages = true;
                            }
                        }
                    }
                }
            }

            return new SourceSummaryTelemetryEvent(
                eventName,
                parentId,
                local,
                httpV2,
                httpV3,
                nugetOrg.ToString(),
                vsOfflinePackages,
                dotnetCuratedFeed,
                protocolDiagnosticTotals);
        }

        // NumLocalFeeds(c:\ or \\ or file:///)
        // NumHTTPv2Feeds
        // NumHTTPv3Feeds
        // NuGetOrg: [NotPresent | YesV2 | YesV3]
        // VsOfflinePackages: [true | false]
        // DotnetCuratedFeed: [true | false]
        private class SourceSummaryTelemetryEvent : TelemetryEvent
        {
            public SourceSummaryTelemetryEvent(
                string eventName,
                Guid parentId,
                int local,
                int httpV2,
                int httpV3,
                string nugetOrg,
                bool vsOfflinePackages,
                bool dotnetCuratedFeed,
                PackageSourceTelemetry.Totals protocolDiagnosticTotals)
                : base(eventName)
            {
                this["NumLocalFeeds"] = local;
                this["NumHTTPv2Feeds"] = httpV2;
                this["NumHTTPv3Feeds"] = httpV3;
                this["NuGetOrg"] = nugetOrg;
                this["VsOfflinePackages"] = vsOfflinePackages;
                this["DotnetCuratedFeed"] = dotnetCuratedFeed;
                this["ParentId"] = parentId.ToString();
                this["protocol.requests"] = protocolDiagnosticTotals.Requests;
                this["protocol.bytes"] = protocolDiagnosticTotals.Bytes;
                this["protocol.duration"] = protocolDiagnosticTotals.Duration.TotalMilliseconds;
            }
        }
    }
}
