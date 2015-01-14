using Newtonsoft.Json.Linq;
using NuGet.Client.VisualStudio;
using NuGet.Data;
using NuGet.PackagingCore;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Client.V3.VisualStudio
{
    public class V3PSAutoCompleteResource : PSAutoCompleteResource
    {
        private readonly V3RegistrationResource _regResource;
        private readonly V3ServiceIndexResource _serviceIndex;
        private readonly DataClient _client;

        public V3PSAutoCompleteResource(DataClient client, V3ServiceIndexResource serviceIndex, V3RegistrationResource regResource)
            : base()
        {
            _regResource = regResource;
            _serviceIndex = serviceIndex;
            _client = client;
        }

        public override async Task<IEnumerable<string>> IdStartsWith(string packageIdPrefix, bool includePrerelease, CancellationToken token)
        {
            Uri searchUrl = _serviceIndex.Index["resources"].Where(j => ((string)j["@type"]) == "SearchAutocompleteService").Select(o => o["@id"].ToObject<Uri>()).FirstOrDefault();

            if (searchUrl == null)
            {
                throw new NuGetProtocolException(Strings.Protocol_MissingSearchService);
            }

            // Construct the query
            var queryUrl = new UriBuilder(searchUrl.AbsoluteUri);
            string queryString =
                "q=" + packageIdPrefix;

            queryUrl.Query = queryString;

            var queryUri = queryUrl.Uri;
            var results = await _client.GetJObjectAsync(queryUri);
            token.ThrowIfCancellationRequested();
            if (results == null)
            {
                return Enumerable.Empty<string>();
            }
            var data = results.Value<JArray>("data");
            if (data == null)
            {
                return Enumerable.Empty<string>();
            }

            // Resolve all the objects
            List<string> outputs = new List<string>();
            foreach (var result in data)
            {
                if (result != null)
                {
                    outputs.Add(result.ToString());
                }
            }

            return outputs.Where(item => item.StartsWith(packageIdPrefix,StringComparison.OrdinalIgnoreCase));
        }

        public override async Task<IEnumerable<NuGetVersion>> VersionStartsWith(string packageId, string versionPrefix, bool includePrerelease, CancellationToken token)
        {
            //*TODOs : Take prerelease as parameter. Also it should return both listed and unlisted for powershell ? 
            IEnumerable<JObject> packages = await _regResource.Get(packageId, includePrerelease, false, token);
            List<NuGetVersion> versions = new List<NuGetVersion>();
            foreach (var package in packages)
            {
                string version = (string)package["version"];
                if(version.StartsWith(versionPrefix,StringComparison.OrdinalIgnoreCase))
                   versions.Add(new NuGetVersion(version));
            }
            return versions;
        }

    }
}
