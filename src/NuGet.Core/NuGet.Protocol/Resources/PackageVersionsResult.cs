// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Versioning;

namespace NuGet.Protocol.Core.Types
{
    /// <summary>
    /// The package versions a source knows about, and whether that listing is authoritative for the current cache session.
    /// </summary>
    /// <remarks>
    /// This is a value type so that the default value is a meaningful answer: no versions, and no evidence that the
    /// source itself was asked during this session.
    /// </remarks>
    public readonly struct PackageVersionsResult
    {
        /// <summary>
        /// Initializes a new <see cref="PackageVersionsResult" />.
        /// </summary>
        /// <param name="versions">The versions the source lists for the package, if any.</param>
        /// <param name="isFresh">
        /// <see langword="true" /> if the listing was produced exclusively from source responses made in the current
        /// cache session; otherwise, <see langword="false" />.
        /// </param>
        public PackageVersionsResult(IEnumerable<NuGetVersion>? versions, bool isFresh)
        {
            Versions = versions;
            IsFresh = isFresh;
        }

        /// <summary>
        /// Gets the versions the source lists for the package, or <see langword="null" /> if it lists none.
        /// </summary>
        public IEnumerable<NuGetVersion>? Versions { get; }

        /// <summary>
        /// Gets a value indicating whether the listing is authoritative for the current cache session.
        /// </summary>
        /// <remarks>
        /// A listing that is not fresh came at least partly from a cache that predates this session, so a version
        /// missing from it may simply not have been published yet the last time the source was asked.
        /// </remarks>
        public bool IsFresh { get; }
    }
}
