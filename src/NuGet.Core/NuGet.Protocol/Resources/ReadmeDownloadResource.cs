// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Protocol
{
    /// <summary>
    /// Provides the download metatdata for a given package from a V3 server endpoint.
    /// </summary>
    public class ReadmeDownloadResource : INuGetResource
    {
        private readonly string _source;
        //private readonly RegistrationResourceV3 _regResource;
        private readonly HttpSource _client;
        private readonly string _packageBaseAddressUrl;

        /// <summary>
        /// Download package readme using the package base address container resource.
        /// </summary>
        public ReadmeDownloadResource(string source, HttpSource client, string packageBaseAddress)
            : this(client)
        {
            if (packageBaseAddress == null)
            {
                throw new ArgumentNullException(nameof(packageBaseAddress));
            }

            _source = source;
            _packageBaseAddressUrl = packageBaseAddress.TrimEnd('/');
        }

        private ReadmeDownloadResource(HttpSource client)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            _client = client;
        }

        public async Task<string> DownloadReadmeAsync(string packageId, NuGetVersion packageVersion, ILogger logger, CancellationToken cancellationToken)
        {
            using (var sourceCacheContext = new SourceCacheContext())
            {
                var id = packageId.ToLowerInvariant();
                var version = packageVersion.ToNormalizedString().ToLowerInvariant();
                var cacheContext = HttpSourceCacheContext.Create(sourceCacheContext, isFirstAttempt: true);
                var request = new HttpSourceCachedRequest(
                    GetReadmeDownloadUrl(id, version).AbsoluteUri,
                    $"readme_${id}_${version}",
                    cacheContext)
                {
                    IgnoreNotFounds = true
                };

                var result = await _client.GetAsync<string>(
                    request,
                    async response =>
                    {
                        if (response.Status == HttpSourceResultStatus.NotFound || response.Status == HttpSourceResultStatus.NoContent)
                        {
                            return null;
                        }
                        using StreamReader reader = new StreamReader(response.Stream);
                        return await reader.ReadToEndAsync();
                    },
                    logger,
                    cancellationToken
                );

                return result;
            }
        }

        /// <summary>
        /// Get the download url of the package.
        /// 1. If the identity is a SourcePackageDependencyInfo the SourcePackageDependencyInfo.DownloadUri is used.
        /// 2. A url will be constructed for the flat container location if the source has that resource.
        /// 3. The download url will be found in the registration blob as a fallback.
        /// </summary>
        private Uri GetReadmeDownloadUrl(string id, string version)
        {
            Uri downloadUri = null;

            // Construct the url

            var url = $"{_packageBaseAddressUrl}/{id}/{version}/readme";
            downloadUri = new Uri(url);

            return downloadUri;
        }
    }
}
