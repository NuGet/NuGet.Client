// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
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

        public DownloadResourceV3(HttpClient client, RegistrationResourceV3 regResource)
            : base()
        {
            if (client == null)
            {
                throw new ArgumentNullException("client");
            }

            if (regResource == null)
            {
                throw new ArgumentNullException("regResource");
            }

            _regResource = regResource;
            _client = client;
        }

        public override async Task<Uri> GetDownloadUrl(PackageIdentity identity, CancellationToken token)
        {
            Uri downloadUri = null;

            var blob = await _regResource.GetPackageMetadata(identity, token);

            if (blob != null
                && blob["packageContent"] != null)
            {
                downloadUri = new Uri(blob["packageContent"].ToString());
            }

            return downloadUri;
        }

        public override async Task<Stream> GetStream(PackageIdentity identity, CancellationToken token)
        {
            Stream stream = null;

            var uri = await GetDownloadUrl(identity, token);

            if (uri != null)
            {
                stream = await _client.GetStreamAsync(uri);
            }

            return stream;
        }
    }
}
