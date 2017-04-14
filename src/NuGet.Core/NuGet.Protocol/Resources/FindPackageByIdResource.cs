// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Versioning;

namespace NuGet.Protocol.Core.Types
{
    public abstract class FindPackageByIdResource : INuGetResource
    {
        public abstract Task<IEnumerable<NuGetVersion>> GetAllVersionsAsync(
            string id,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken token);

        /// <summary>
        /// Gets the <see cref="FindPackageByIdDependencyInfo" /> for a specific package.
        /// </summary>
        /// <param name="id">The packag id.</param>
        /// <param name="version">The package version.</param>
        /// <param name="cacheContext">The source cache context.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="token">The <see cref="CancellationToken" />.</param>
        /// <returns>
        /// A <see cref="Task" /> that on completion returns a <see cref="FindPackageByIdDependencyInfo" /> of the
        /// package, if found,
        /// <c>null</c> otherwise.
        /// </returns>
        public abstract Task<FindPackageByIdDependencyInfo> GetDependencyInfoAsync(
            string id,
            NuGetVersion version,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken token);

        public abstract Task<bool> CopyNupkgToStreamAsync(
            string id,
            NuGetVersion version,
            Stream destination,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken token);

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
                reader.GetIdentity(),
                reader.GetDependencyGroups(),
                reader.GetFrameworkReferenceGroups());
        }
    }
}
