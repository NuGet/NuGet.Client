// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;
using NuGet.Versioning;

namespace NuGet.Protocol.VisualStudio
{
    public class PowerShellSearchResourceV3 : PSSearchResource
    {
        private readonly RawSearchResourceV3 _searchResource;

        public PowerShellSearchResourceV3(RawSearchResourceV3 searchResource)
        {
            _searchResource = searchResource;
        }

        public override async Task<IEnumerable<PSSearchMetadata>> Search(string searchTerm,
                                                                         SearchFilter filters,
                                                                         int skip,
                                                                         int take,
                                                                         CancellationToken cancellationToken)
        {
            var searchResults = new List<PSSearchMetadata>();

            var searchResultJsonObjects = await _searchResource.Search(searchTerm, filters, skip, take, cancellationToken);

            foreach (var searchResultJson in searchResultJsonObjects)
            {
                searchResults.Add(GetSearchResult(searchResultJson, filters.IncludePrerelease, cancellationToken));
            }

            return searchResults;
        }

        private PSSearchMetadata GetSearchResult(JObject jObject, bool includePrerelease, CancellationToken token)
        {
            var id = jObject.Value<string>(Properties.PackageId);
            var version = NuGetVersion.Parse(jObject.Value<string>(Properties.Version));
            var topPackage = new PackageIdentity(id, version);
            var summary = jObject.Value<string>(Properties.Summary);

            if (string.IsNullOrWhiteSpace(summary))
            {
                // summary is empty. Use its description instead.
                summary = jObject.Value<string>(Properties.Description);
            }

            // get other versions
            var versions = GetLazyVersionList(jObject, includePrerelease, version);

            var searchResult = new PSSearchMetadata(topPackage, versions, summary);
            return searchResult;
        }

        private static Lazy<Task<IEnumerable<NuGetVersion>>> GetLazyVersionList(JObject package,
                                                                                bool includePrerelease,
                                                                                NuGetVersion topVersion)
        {
            return new Lazy<Task<IEnumerable<NuGetVersion>>>(() =>
            {
                var versionList = GetVersionList(package, includePrerelease, topVersion);

                return Task.FromResult(versionList);
            });
        }

        private static IEnumerable<NuGetVersion> GetVersionList(JObject package,
                                                                bool includePrerelease,
                                                                NuGetVersion version)
        {
            var versions = package.Value<JArray>(Properties.Versions);

            if (versions == null)
            {
                return new[] { version };
            }

            var versionList = versions
                .Select(v => NuGetVersion.Parse(v.Value<string>("version")))
                .Where(v => v.IsPrerelease && !includePrerelease)
                .ToList();

            if (!versionList.Contains(version))
            {
                versionList.Add(version);
            }

            return versionList;
        }
    }
}
