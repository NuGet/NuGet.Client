using Newtonsoft.Json.Linq;
using NuGet.Configuration;
using NuGet.PackagingCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Client
{
    /// <summary>
    /// Provides the download metatdata for a given package from a V3 server endpoint.
    /// </summary>
    public class V3DownloadResource : DownloadResource
    {
        private readonly V3RegistrationResource _regResource;
        private readonly HttpClient _client;

        public V3DownloadResource(HttpClient client, V3RegistrationResource regResource)
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

            if (blob != null && blob["packageContent"] != null)
            {
                downloadUri = new Uri(blob["packageContent"].ToString());
            }

            return downloadUri;
        }

        public override async Task<Stream> GetStream(PackageIdentity identity, CancellationToken token)
        {
            Stream stream = null;

            Uri uri = await GetDownloadUrl(identity, token);

            if (uri != null)
            {
                stream = await _client.GetStreamAsync(uri);
            }

            return stream;
        }
    }
}
