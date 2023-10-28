// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine.XPlat
{
    internal class PackageSearchResultRendererJson : IPackageSearchResultRenderer
    {
        private PackageSearchArgs _args;
        private List<PackageSearchResult> _packageSearchResults;

        public PackageSearchResultRendererJson(PackageSearchArgs packageSearchArgs)
        {
            _args = packageSearchArgs;
        }

        public async Task Add(PackageSource source, Task<IEnumerable<IPackageSearchMetadata>> completedSearchTask)
        {
            if (completedSearchTask == null)
            {
                return;
            }

            IEnumerable<IPackageSearchMetadata> searchResult = await completedSearchTask;

            PackageSearchResult packageSearchResult = new PackageSearchResult(source.Name);

            if (_args.ExactMatch)
            {
                var firstResult = searchResult.FirstOrDefault();
                if (firstResult != null)
                {
                    await PopulateSearchResultWithPackages(packageSearchResult, new[] { firstResult });
                }
            }
            else
            {
                await PopulateSearchResultWithPackages(packageSearchResult, searchResult);
            }

            _packageSearchResults.Add(packageSearchResult);
        }

        private async Task PopulateSearchResultWithPackages(PackageSearchResult packageSearchResult, IEnumerable<IPackageSearchMetadata> searchResults)
        {
            foreach (IPackageSearchMetadata searchResult in searchResults)
            {
                PackageDeprecationMetadata packageDeprecationMetadata = await searchResult.GetDeprecationMetadataAsync();
                Package package = new Package(searchResult, (packageDeprecationMetadata == null) ? null : packageDeprecationMetadata.Message);
                packageSearchResult.AddPackage(package);
            }
        }

        public void Finish()
        {
            var json = JsonConvert.SerializeObject(_packageSearchResults, Formatting.Indented);
            _args.Logger.LogMinimal(json);
        }

        public void Start()
        {
            _packageSearchResults = new List<PackageSearchResult>();
        }
    }
}
