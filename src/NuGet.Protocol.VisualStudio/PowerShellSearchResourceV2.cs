// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.VisualStudio
{
    public class PowerShellSearchResourceV2 : PSSearchResource
    {
        private readonly UISearchResource uiSearchResource;

        public PowerShellSearchResourceV2(UISearchResource search)
        {
            uiSearchResource = search;
        }

        public override async Task<IEnumerable<PSSearchMetadata>> Search(string search, SearchFilter filters, int skip, int take, CancellationToken token)
        {
            var searchResults = await uiSearchResource.Search(search, filters, skip, take, token);
            return searchResults.Select(item => GetPSSearch(item));
        }

        private PSSearchMetadata GetPSSearch(UISearchMetadata item)
        {
            return new PSSearchMetadata(item.Identity, item.Versions.Select(v => v.Version), item.Summary);
        }
    }
}
