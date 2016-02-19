// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;

namespace NuGet.Protocol
{
    public class PackageMetadataResourceV2Feed : PackageMetadataResource
    {
        private readonly HttpSource _httpSource;
        private readonly Configuration.PackageSource _packageSource;
        private readonly V2FeedParser _feedParser;

        public PackageMetadataResourceV2Feed(
            HttpSourceResource httpSourceResource,
            Configuration.PackageSource packageSource)
        {
            if (httpSourceResource == null)
            {
                throw new ArgumentNullException(nameof(httpSourceResource));
            }

            if (packageSource == null)
            {
                throw new ArgumentNullException(nameof(packageSource));
            }

            _httpSource = httpSourceResource.HttpSource;
            _packageSource = packageSource;
            _feedParser = new V2FeedParser(_httpSource, packageSource);
        }

        public override async Task<IEnumerable<IPackageSearchMetadata>> GetMetadataAsync(
            string packageId,
            bool includePrerelease,
            bool includeUnlisted,
            Logging.ILogger log,
            CancellationToken token)
        {
            var packages = await _feedParser.FindPackagesByIdAsync(packageId, includeUnlisted, includePrerelease, log, token);

            return packages.Select(p => new PackageSearchMetadataV2Feed(p)).ToList();
        }
    }
}
