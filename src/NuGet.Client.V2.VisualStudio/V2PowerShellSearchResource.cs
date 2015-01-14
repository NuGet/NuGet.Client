using NuGet.Client.VisualStudio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client.V2.VisualStudio
{
    public class V2PowerShellSearchResource : PSSearchResource
    {
        private readonly UISearchResource uiSearchResource;
        public V2PowerShellSearchResource(UISearchResource search)
        {
            uiSearchResource = search;
        }
        public async override Task<IEnumerable<PSSearchMetadata>> Search(string search, SearchFilter filters, int skip, int take, System.Threading.CancellationToken token)
        {
           IEnumerable<UISearchMetadata> searchResults = await uiSearchResource.Search(search, filters, skip, take, token);
           return searchResults.Select(item => GetPSSearch(item));
        }

        private PSSearchMetadata GetPSSearch(UISearchMetadata item)
        {
            return new PSSearchMetadata(item.Identity, item.Versions,item.Summary);
        }
    }
}
