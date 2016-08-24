// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.Protocol.Core.Types
{
    public abstract class FindPackageByIdResource : INuGetResource
    {
        public virtual SourceCacheContext CacheContext { get; set; }

        public virtual ILogger Logger { get; set; } = new NullLogger();

        public abstract Task<IEnumerable<NuGetVersion>> GetAllVersionsAsync(string id, CancellationToken token);

        /// <summary>
        /// Gets the <see cref="FindPackageByIdDependencyInfo" /> for a specific package.
        /// </summary>
        /// <param name="id">The packag id.</param>
        /// <param name="version">The package version.</param>
        /// <param name="token">The <see cref="CancellationToken" />.</param>
        /// <returns>
        /// A <see cref="Task" /> that on completion returns a <see cref="FindPackageByIdDependencyInfo" /> of the
        /// package, if found,
        /// <c>null</c> otherwise.
        /// </returns>
        public abstract Task<FindPackageByIdDependencyInfo> GetDependencyInfoAsync(string id, NuGetVersion version, CancellationToken token);

        public abstract Task<bool> CopyNupkgToStreamAsync(
            string id,
            NuGetVersion version,
            Stream destination,
            CancellationToken token);

        /// <summary>
        /// Gets the original ID and version for a package. This is useful when finding the
        /// canonical casing for a package ID. Note that the casing of a package ID can vary from
        /// version to version.
        /// </summary>
        /// <param name="id">The package ID. This value is case insensitive.</param>
        /// <param name="version">The version.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>The package identity, with the ID having the case provided by the package author.</returns>
        public abstract Task<PackageIdentity> GetOriginalIdentityAsync(string id, NuGetVersion version, CancellationToken token);

        /// <summary>
        /// Read dependency info from a nuspec.
        /// </summary>
        /// <remarks>This also verifies minClientVersion.</remarks>
        protected static FindPackageByIdDependencyInfo GetDependencyInfo(NuspecReader reader)
        {
            // Since this is the first place a package is read after selecting it as the best version
            // check the minClientVersion here to verify we are okay to read this package.
            MinClientVersionUtility.VerifyMinClientVersion(reader);

            // Create dependency info
            return new FindPackageByIdDependencyInfo(
                reader.GetDependencyGroups(),
                reader.GetFrameworkReferenceGroups());
        }
    }
}
