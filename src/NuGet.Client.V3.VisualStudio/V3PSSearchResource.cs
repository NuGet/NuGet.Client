using Newtonsoft.Json.Linq;
using NuGet.Client.VisualStudio;
using NuGet.PackagingCore;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Client.V3.VisualStudio
{
    public class V3PSSearchResource : PSSearchResource
    {
        private readonly V3UISearchResource _searchResource;

        public V3PSSearchResource(V3UISearchResource searchResource)
        {
            _searchResource = searchResource;
        }
         
        public override async Task<IEnumerable<PSSearchMetadata>> Search(string search, SearchFilter filters, int skip, int take, CancellationToken token)
        {
            // TODO: stop using UI search
            var searchResultJsonObjects = await _searchResource.Search(search, filters, skip, take, token);

            List<PSSearchMetadata> powerShellSearchResults = new List<PSSearchMetadata>();
            foreach (UISearchMetadata result in searchResultJsonObjects)
            {
                powerShellSearchResults.Add(new PSSearchMetadata(result.Identity, result.Versions, result.Summary));
            }

            return powerShellSearchResults;
        }
    }
}
