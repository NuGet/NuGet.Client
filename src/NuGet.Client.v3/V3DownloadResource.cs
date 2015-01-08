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
            _regResource = regResource;
            _client = client;
        }

        public override async Task<Uri> GetDownloadUrl(PackageIdentity identity, CancellationToken token)
        {
            var blob = await _regResource.GetPackage(identity, token);

            return new Uri(blob["packageContent"].ToString());
        }

        public override async Task<Stream> GetStream(PackageIdentity identity, CancellationToken token)
        {
            return await _client.GetStreamAsync(await GetDownloadUrl(identity, token));
        }
    }
}
