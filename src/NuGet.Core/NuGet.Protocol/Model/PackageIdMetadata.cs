// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Protocol
{
    /// <summary>
    /// Metadata declared at the root of a package's registration index, scoped to the package ID
    /// rather than to a single package version.
    /// </summary>
    public class PackageIdMetadata
    {
        private IReadOnlyList<string> _sponsorshipUrls = Array.Empty<string>();

        /// <summary>
        /// Sponsorship URLs the source advertises for this package, in the order returned.
        /// Empty when the source declares none.
        /// </summary>
        public IReadOnlyList<string> SponsorshipUrls
        {
            get => _sponsorshipUrls;
            init => _sponsorshipUrls = FilterSponsorshipUrls(value);
        }

        private static IReadOnlyList<string> FilterSponsorshipUrls(IReadOnlyList<string>? sponsorshipUrls)
        {
            if (sponsorshipUrls == null || sponsorshipUrls.Count == 0)
            {
                return Array.Empty<string>();
            }

            List<string>? filteredUrls = null;
            for (int i = 0; i < sponsorshipUrls.Count; i++)
            {
                string? sponsorshipUrl = sponsorshipUrls[i];
                if (string.IsNullOrWhiteSpace(sponsorshipUrl))
                {
                    if (filteredUrls == null)
                    {
                        filteredUrls = new List<string>(sponsorshipUrls.Count - 1);
                        for (int j = 0; j < i; j++)
                        {
                            filteredUrls.Add(sponsorshipUrls[j]);
                        }
                    }
                }
                else
                {
                    filteredUrls?.Add(sponsorshipUrl);
                }
            }

            return filteredUrls == null
                ? sponsorshipUrls
                : filteredUrls.Count == 0
                    ? Array.Empty<string>()
                    : filteredUrls;
        }
    }
}
