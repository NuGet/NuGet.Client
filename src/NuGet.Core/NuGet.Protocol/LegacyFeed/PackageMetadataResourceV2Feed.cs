// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol
{
    public class PackageMetadataResourceV2Feed : PackageMetadataResource
    {
        private readonly HttpSource _httpSource;
        private readonly Configuration.PackageSource _packageSource;
        private readonly V2FeedParser _feedParser;

        public PackageMetadataResourceV2Feed(
            HttpSourceResource httpSourceResource,
            string baseAddress,
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
            _feedParser = new V2FeedParser(_httpSource, baseAddress, packageSource.Source);
        }

        public override async Task<IEnumerable<IPackageSearchMetadata>> GetMetadataAsync(
            string packageId,
            bool includePrerelease,
            bool includeUnlisted,
            Common.ILogger log,
            CancellationToken token)
        {
            var packages = await _feedParser.FindPackagesByIdAsync(packageId, includeUnlisted, includePrerelease, log, token);

            var metadataCache = new MetadataReferenceCache();
            return packages.Select(p => new PackageSearchMetadataV2Feed(p, metadataCache)).ToList();
        }

        public override async Task<IPackageSearchMetadata> GetMetadataAsync(
            PackageIdentity package,
            Common.ILogger log,
            CancellationToken token)
        {
            var v2Package = await _feedParser.GetPackage(package, log, token);

            if (v2Package != null)
            {
                return new PackageSearchMetadataV2Feed(v2Package);
            }
            return null;
        }
    }
}
