// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Protocol.Core.v3
{
    /// <summary>
    /// Returns the full package metadata
    /// </summary>
    public class MetadataResourceV3 : MetadataResource
    {
        private RegistrationResourceV3 _regResource;
        private HttpClient _client;

        public MetadataResourceV3(HttpClient client, RegistrationResourceV3 regResource)
            : base()
        {
            if (client == null)
            {
                throw new ArgumentNullException("client");
            }

            if (regResource == null)
            {
                throw new ArgumentNullException("regResource");
            }

            _regResource = regResource;
            _client = client;
        }

        /// <summary>
        /// Find the latest version of the package
        /// </summary>
        /// <param name="includePrerelease">include versions with prerelease labels</param>
        /// <param name="includeUnlisted">not implemented yet</param>
        public override async Task<IEnumerable<KeyValuePair<string, NuGetVersion>>> GetLatestVersions(IEnumerable<string> packageIds, bool includePrerelease, bool includeUnlisted, CancellationToken token)
        {
            var results = new List<KeyValuePair<string, NuGetVersion>>();

            foreach (var id in packageIds)
            {
                IEnumerable<NuGetVersion> allVersions;
                try
                {
                    var catalogEntries = await _regResource.GetPackageMetadata(id, includePrerelease, includeUnlisted, token);
                    allVersions = catalogEntries.Select(p => NuGetVersion.Parse(p["version"].ToString()));
                }
                catch (Exception ex)
                {
                    throw new NuGetProtocolException(Strings.FormatProtocol_PackageMetadataError(id, _regResource.BaseUri), ex);
                }

                // find the latest
                var latest = allVersions.OrderByDescending(p => p, VersionComparer.VersionRelease).FirstOrDefault();

                results.Add(new KeyValuePair<string, NuGetVersion>(id, latest));
            }

            return results;
        }

        public override async Task<bool> Exists(PackageIdentity identity, bool includeUnlisted, CancellationToken token)
        {
            // TODO: get the url and just check the headers?
            var metadata = await _regResource.GetPackageMetadata(identity, token);

            // TODO: listed check
            return metadata != null;
        }

        public override async Task<bool> Exists(string packageId, bool includePrerelease, bool includeUnlisted, CancellationToken token)
        {
            var entries = await GetVersions(packageId, includePrerelease, includeUnlisted, token);

            return entries != null && entries.Any();
        }

        public override async Task<IEnumerable<NuGetVersion>> GetVersions(string packageId, bool includePrerelease, bool includeUnlisted, CancellationToken token)
        {
            var results = new List<NuGetVersion>();

            var entries = await _regResource.GetPackageEntries(packageId, includeUnlisted, token);

            foreach (var catalogEntry in entries)
            {
                NuGetVersion version = null;

                if (catalogEntry["version"] != null
                    && NuGetVersion.TryParse(catalogEntry["version"].ToString(), out version))
                {
                    if (includePrerelease || !version.IsPrerelease)
                    {
                        results.Add(version);
                    }
                }
            }

            return results;
        }
    }
}
