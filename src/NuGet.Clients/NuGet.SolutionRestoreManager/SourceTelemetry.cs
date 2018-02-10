// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Common;
using NuGet.Configuration;

namespace NuGet.SolutionRestoreManager
{
    public static class SourceTelemetry
    {
        /// <summary>
        /// Create a SourceSummaryEvent event with counts of local vs http and v2 vs v3 feeds.
        /// </summary>
        public static TelemetryEvent GetSourceSummaryEvent(Guid parentId, IEnumerable<PackageSource> packageSources)
        {
            var local = 0;
            var httpV2 = 0;
            var httpV3 = 0;
            var hasNuGetOrgV3 = false;
            var nugetOrg = "NotPresent";

            if (packageSources != null)
            {
                foreach (var source in packageSources)
                {
                    // Ignore disabled sources
                    if (source.IsEnabled)
                    {
                        if (source.IsHttp)
                        {
                            if (IsHttpV3(source))
                            {
                                // Http V3 feed
                                httpV3++;

                                if (IsHttpNuGetOrg(source))
                                {
                                    hasNuGetOrgV3 = true;
                                    nugetOrg = "YesV3";
                                }
                            }
                            else
                            {
                                // Http V2 feed
                                httpV2++;

                                // Prefer v3 over v2 if v3 is found
                                if (!hasNuGetOrgV3 && IsHttpNuGetOrg(source))
                                {
                                    nugetOrg = "YesV2";
                                }
                            }
                        }
                        else
                        {
                            // Local or UNC feed
                            local++;
                        }
                    }
                }
            }

            return new SourceSummaryEvent(parentId, local, httpV2, httpV3, nugetOrg);
        }

        /// <summary>
        /// True if the source is http and ends with index.json
        /// </summary>
        private static bool IsHttpV3(PackageSource source)
        {
            return source.IsHttp &&
                (source.Source.EndsWith("index.json", StringComparison.OrdinalIgnoreCase)
                || source.ProtocolVersion == 3);
        }

        /// <summary>
        /// True if the source is http and has a *.nuget.org host.
        /// </summary>
        private static bool IsHttpNuGetOrg(PackageSource source)
        {
            return (source.IsHttp && source.TrySourceAsUri?.Host.EndsWith(".nuget.org", StringComparison.OrdinalIgnoreCase) == true);
        }

        // NumLocalFeeds(c:\ or \\ or file:///)
        // NumHTTPv2Feeds
        // NumHTTPv3Feeds
        // NuGetOrg: [NotPresent | YesV2 | YesV3]
        private class SourceSummaryEvent : TelemetryEvent
        {
            public SourceSummaryEvent(Guid parentId, int local, int httpV2, int httpV3, string nugetOrg)
                : base("RestorePackageSourceSummary")
            {
                this["NumLocalFeeds"] = local;
                this["NumHTTPv2Feeds"] = httpV2;
                this["NumHTTPv3Feeds"] = httpV3;
                this["NuGetOrg"] = nugetOrg;
                this["ParentId"] = parentId.ToString();
            }
        }
    }
}
