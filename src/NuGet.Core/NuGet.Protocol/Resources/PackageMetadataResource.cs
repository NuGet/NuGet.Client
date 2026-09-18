// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Core;

namespace NuGet.Protocol.Core.Types
{
    public abstract class PackageMetadataResource : INuGetResource
    {
        /// <summary>
        /// Gets whether the source supports package ID-level metadata.
        /// </summary>
        public virtual bool SupportsPackageIdMetadata => false;

        /// <summary>
        /// Gets metadata scoped to a package ID rather than a package version.
        /// </summary>
        public virtual Task<PackageIdMetadata?> GetPackageIdMetadataAsync(
            string packageId,
            SourceCacheContext sourceCacheContext,
            Common.ILogger log,
            CancellationToken token)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Returns all versions of a package
        /// </summary>
        public abstract Task<IEnumerable<IPackageSearchMetadata>> GetMetadataAsync(
            string packageId,
            bool includePrerelease,
            bool includeUnlisted,
            SourceCacheContext sourceCacheContext,
            Common.ILogger log,
            CancellationToken token);

        /// <summary>
        /// Return package metadata for the input PackageIdentity
        /// </summary>
        public abstract Task<IPackageSearchMetadata?> GetMetadataAsync(
            PackageIdentity package,
            SourceCacheContext sourceCacheContext,
            Common.ILogger log,
            CancellationToken token);
    }
}
