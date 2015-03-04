using NuGet.Protocol.Core.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Protocol.VisualStudio
{
    public class PowerShellSearchResourceV2 : PSSearchResource
    {
        private readonly UISearchResource uiSearchResource;
        public PowerShellSearchResourceV2(UISearchResource search)
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
            return new PSSearchMetadata(item.Identity, item.Versions.Select(v => v.Version), item.Summary);
        }
    }
}
