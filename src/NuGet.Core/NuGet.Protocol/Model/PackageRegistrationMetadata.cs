// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;

namespace NuGet.Protocol
{
    /// <summary>
    /// Metadata declared at the root of a package's registration index, scoped to the package ID
    /// rather than to a single package version.
    /// </summary>
    /// <remarks>
    /// Version-scoped metadata is exposed through <see cref="Core.Types.PackageMetadataResource"/>.
    /// This type is the package-scoped counterpart: future package-level properties added to the
    /// registration root belong here.
    /// </remarks>
    public class PackageRegistrationMetadata
    {
        /// <summary>
        /// Sponsorship URLs the source advertises for this package, in the order returned.
        /// Empty when the source declares none.
        /// </summary>
        public IReadOnlyList<string> SponsorshipUrls { get; }

        public PackageRegistrationMetadata(IReadOnlyList<string>? sponsorshipUrls)
        {
            SponsorshipUrls = sponsorshipUrls ?? Array.Empty<string>();
        }
    }
}
