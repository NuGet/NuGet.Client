// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Protocol
{
    /// <summary>
    /// Metadata declared at the root of a package's registration index, scoped to the package ID
    /// rather than to a single package version.
    /// </summary>
    public class PackageIdMetadata
    {
        /// <summary>
        /// Sponsorship URLs the source advertises for this package, in the order returned.
        /// Empty when the source declares none.
        /// </summary>
        public IReadOnlyList<string> SponsorshipUrls { get; }

        public PackageIdMetadata(IReadOnlyList<string>? sponsorshipUrls)
        {
            SponsorshipUrls = sponsorshipUrls?
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .ToArray()
                ?? Array.Empty<string>();
        }
    }
}
