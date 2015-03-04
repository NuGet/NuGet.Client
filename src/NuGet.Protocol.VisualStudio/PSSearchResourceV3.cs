using Newtonsoft.Json.Linq;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.VisualStudio
{
    public class PSSearchResourceV3 : PSSearchResource
    {
        private readonly UISearchResourceV3 _searchResource;

        public PSSearchResourceV3(UISearchResourceV3 searchResource)
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
                powerShellSearchResults.Add(new PSSearchMetadata(result.Identity, result.Versions.Select(v => v.Version), result.Summary));
            }

            return powerShellSearchResults;
        }
    }
}
