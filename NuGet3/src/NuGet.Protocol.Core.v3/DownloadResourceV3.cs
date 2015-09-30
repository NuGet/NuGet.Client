// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.Core.v3
{
    /// <summary>
    /// Provides the download metatdata for a given package from a V3 server endpoint.
    /// </summary>
    public class DownloadResourceV3 : DownloadResource
    {
        private readonly RegistrationResourceV3 _regResource;
        private readonly HttpClient _client;
        private readonly string _flatContainerBaseUrl;

        /// <summary>
        /// Download packages using the download url found in the registration resource.
        /// </summary>
        public DownloadResourceV3(HttpClient client, RegistrationResourceV3 regResource)
            : this(client)
        {
            if (regResource == null)
            {
                throw new ArgumentNullException(nameof(regResource));
            }

            _regResource = regResource;
        }

        /// <summary>
        /// Download packages using the flat container resource.
        /// </summary>
        public DownloadResourceV3(HttpClient client, string flatContainerBaseUrl)
            : this(client)
        {
            if (flatContainerBaseUrl == null)
            {
                throw new ArgumentNullException(nameof(flatContainerBaseUrl));
            }

            _flatContainerBaseUrl = flatContainerBaseUrl.TrimEnd('/');
        }

        private DownloadResourceV3(HttpClient client)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            _client = client;
        }

        /// <summary>
        /// Get the download url of the package.
        /// 1. If the identity is a SourcePackageDependencyInfo the SourcePackageDependencyInfo.DownloadUri is used.
        /// 2. A url will be constructed for the flat container location if the source has that resource.
        /// 3. The download url will be found in the registration blob as a fallback.
        /// </summary>
        private async Task<Uri> GetDownloadUrl(PackageIdentity identity, CancellationToken token)
        {
            Uri downloadUri = null;
            var sourcePackage = identity as SourcePackageDependencyInfo;

            if (sourcePackage?.DownloadUri != null)
            {
                // Read the already provided url
                downloadUri = sourcePackage?.DownloadUri;
            }
            else if (_flatContainerBaseUrl != null)
            {
                // Construct the url
                var id = identity.Id.ToLowerInvariant();
                var version = identity.Version.ToNormalizedString().ToLowerInvariant();

                var url = $"{_flatContainerBaseUrl}/{id}/{version}/{id}.{version}.nupkg";
                downloadUri = new Uri(url);
            }
            else if (_regResource != null)
            {
                // Read the url from the registration information
                var blob = await _regResource.GetPackageMetadata(identity, token);

                if (blob != null
                    && blob["packageContent"] != null)
                {
                    downloadUri = new Uri(blob["packageContent"].ToString());
                }
            }

            return downloadUri;
        }

        public override async Task<DownloadResourceResult> GetDownloadResourceResultAsync(PackageIdentity identity,
            ISettings settings,
            CancellationToken token)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var uri = await GetDownloadUrl(identity, token);
            if (uri != null)
            {
                return await GetDownloadResultAsync(identity, uri, settings, token);
            }

            return null;
        }

        private async Task<DownloadResourceResult> GetDownloadResultAsync(
            PackageIdentity identity,
            Uri uri,
            ISettings settings,
            CancellationToken token)
        {
            // Uri is not null, so the package exists in the source
            // Now, check if it is in the global packages folder, before, getting the package stream
            var packageFromGlobalPackages = GlobalPackagesFolderUtility.GetPackage(identity, settings);

            if (packageFromGlobalPackages != null)
            {
                return packageFromGlobalPackages;
            }

            using (var packageStream = await _client.GetStreamAsync(uri))
            {
                var downloadResult = await GlobalPackagesFolderUtility.AddPackageAsync(identity,
                    packageStream,
                    settings,
                    token);

                return downloadResult;
            }
        }
    }
}
