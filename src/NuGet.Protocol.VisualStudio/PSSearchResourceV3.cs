// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

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

            var powerShellSearchResults = new List<PSSearchMetadata>();
            foreach (var result in searchResultJsonObjects)
            {
                powerShellSearchResults.Add(new PSSearchMetadata(result.Identity, result.Versions.Select(v => v.Version), result.Summary));
            }

            return powerShellSearchResults;
        }
    }
}
