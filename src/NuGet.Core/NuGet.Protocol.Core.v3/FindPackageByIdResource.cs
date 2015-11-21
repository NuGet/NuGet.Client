// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Logging;
using NuGet.Packaging;
using NuGet.Versioning;

namespace NuGet.Protocol.Core.Types
{
    public abstract class FindPackageByIdResource : INuGetResource
    {
        public virtual SourceCacheContext CacheContext { get; set; }

        public virtual ILogger Logger { get; set; }

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

        public abstract Task<Stream> GetNupkgStreamAsync(string id, NuGetVersion version, CancellationToken token);

        protected static FindPackageByIdDependencyInfo GetDependencyInfo(NuspecReader reader)
        {
            return new FindPackageByIdDependencyInfo(
                reader.GetDependencyGroups(),
                reader.GetFrameworkReferenceGroups());
        }

        protected static HttpSourceCacheContext CreateCacheContext(SourceCacheContext cacheContext, int retryCount)
        {
            if (retryCount == 0)
            {
                return new HttpSourceCacheContext(cacheContext);
            }
            else
            {
                return new HttpSourceCacheContext(cacheContext, TimeSpan.Zero);
            }
        }
    }
}
