// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Common;
using NuGet.VisualStudio.Telemetry;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol;

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
            IEnumerable<SourceRepository> sourceRepositories,
            PackageSourceTelemetry.Totals protocolDiagnosticTotals)
        {
            return GetSourceSummaryEvent(
                "RestorePackageSourceSummary",
                parentId,
                sourceRepositories,
                protocolDiagnosticTotals);
        }

        public static TelemetryEvent GetSearchSourceSummaryEvent(
            Guid parentId,
            IEnumerable<SourceRepository> sourceRepositories,
            PackageSourceTelemetry.Totals protocolDiagnosticTotals)
        {
            return GetSourceSummaryEvent(
                "SearchPackageSourceSummary",
                parentId,
                sourceRepositories,
                protocolDiagnosticTotals);
        }

        /// <summary>
        /// Create a SourceSummaryEvent event with counts of local vs http and v2 vs v3 feeds.
        /// </summary>
        private static TelemetryEvent GetSourceSummaryEvent(
            string eventName,
            Guid parentId,
            IEnumerable<SourceRepository> sourceRepositories,
            PackageSourceTelemetry.Totals protocolDiagnosticTotals)
        {
            var local = 0;
            var httpV2 = 0;
            var httpV3 = 0;
            var nugetOrg = HttpStyle.NotPresent;
            var vsOfflinePackages = false;
            var dotnetCuratedFeed = false;
            var httpsV2 = 0;
            var httpsV3 = 0;
            int numberOfHTTPSSourcesWithHTTPResource = 0;
            bool serviceIndexCacheAvailable = false;

            if (sourceRepositories != null)
            {
                foreach (var sourceRepository in sourceRepositories)
                {
                    var source = sourceRepository.PackageSource;

                    if (sourceRepository.GetServiceIndexV3FromCache(out ServiceIndexResourceV3 resource))
                    {
                        serviceIndexCacheAvailable |= true;
                        numberOfHTTPSSourcesWithHTTPResource += DoesServiceIndexHaveHttpResource(resource) && source.IsHttps ? 1 : 0;
                    }

                    // Ignore disabled sources
                    if (source.IsEnabled)
                    {
                        if (source.IsHttp)
                        {
                            if (TelemetryUtility.IsHttpV3(source))
                            {
                                // Http V3 feed
                                httpV3++;

                                if (source.IsHttps)
                                {
                                    httpsV3++;
                                }

                                if (UriUtility.IsNuGetOrg(source.Source))
                                {
                                    nugetOrg |= HttpStyle.YesV3;
                                }
                            }
                            else
                            {
                                // Http V2 feed
                                httpV2++;

                                if (source.IsHttps)
                                {
                                    httpsV2++;
                                }

                                if (UriUtility.IsNuGetOrg(source.Source))
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
                protocolDiagnosticTotals,
                httpsV2,
                httpsV3,
                serviceIndexCacheAvailable,
                numberOfHTTPSSourcesWithHTTPResource);
        }

        private static bool DoesServiceIndexHaveHttpResource(ServiceIndexResourceV3 resource)
        {
            foreach (var entry in resource.Entries)
            {
                var resourceUri = entry.Uri;

                if (resourceUri.Scheme == Uri.UriSchemeHttp && resourceUri.Scheme != Uri.UriSchemeHttps)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// NumLocalFeeds(c:\ or \\ or file:///)
        /// NumHTTPv2Feeds (includes HTTP and HTTPS)
        /// NumHTTPv3Feeds (includes HTTP and HTTPS)
        /// NuGetOrg: [NotPresent | YesV2 | YesV3]
        /// VsOfflinePackages: [true | false]
        /// DotnetCuratedFeed: [true | false]
        /// ParentId
        /// protocol.requests
        /// protocol.bytes
        /// protocol.duration
        /// NumHTTPSv2Feeds
        /// NumHTTPSv3Feeds
        /// ServiceIndexCacheAvailable
        /// NumHTTPSSourcesWithAnHTTPResource
        /// </summary>
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
                PackageSourceTelemetry.Totals protocolDiagnosticTotals,
                int httpsV2,
                int httpsV3,
                bool serviceIndexCacheAvailable,
                int numberOfHttpsSourcesWithHttpResource)
                : base(eventName)
            {
                this["NumLocalFeeds"] = local;
                this["NumHTTPv2Feeds"] = httpV2;
                this["NumHTTPv3Feeds"] = httpV3;
                this["NumHTTPSv2Feeds"] = httpsV2;
                this["NumHTTPSv3Feeds"] = httpsV3;
                this["NuGetOrg"] = nugetOrg;
                this["VsOfflinePackages"] = vsOfflinePackages;
                this["DotnetCuratedFeed"] = dotnetCuratedFeed;
                this["ParentId"] = parentId.ToString();
                this["protocol.requests"] = protocolDiagnosticTotals.Requests;
                this["protocol.bytes"] = protocolDiagnosticTotals.Bytes;
                this["protocol.duration"] = protocolDiagnosticTotals.Duration.TotalMilliseconds;
                this["ServiceIndexCacheAvailable"] = serviceIndexCacheAvailable;
                this["NumHTTPSSourcesWithAnHTTPResource"] = numberOfHttpsSourcesWithHttpResource;
            }
        }
    }
}
