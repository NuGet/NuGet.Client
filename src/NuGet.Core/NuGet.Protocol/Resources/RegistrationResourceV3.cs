// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Protocol.Converters;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Model;
using NuGet.Shared;
using NuGet.Versioning;

namespace NuGet.Protocol
{
    /// <summary>
    /// Registration blob reader
    /// </summary>
    public class RegistrationResourceV3 : INuGetResource
    {
        private readonly HttpSource _client;
        private readonly IEnvironmentVariableReader _environmentVariableReader;

        public RegistrationResourceV3(HttpSource client, Uri baseUrl)
            : this(client, baseUrl, supportsPackageIdMetadata: false, EnvironmentVariableWrapper.Instance)
        {
        }

        internal RegistrationResourceV3(HttpSource client, Uri baseUrl, IEnvironmentVariableReader environmentVariableReader)
            : this(client, baseUrl, supportsPackageIdMetadata: false, environmentVariableReader)
        {
        }

        internal RegistrationResourceV3(HttpSource client, Uri baseUrl, bool supportsPackageIdMetadata)
            : this(client, baseUrl, supportsPackageIdMetadata, EnvironmentVariableWrapper.Instance)
        {
        }

        internal RegistrationResourceV3(
            HttpSource client,
            Uri baseUrl,
            bool supportsPackageIdMetadata,
            IEnvironmentVariableReader environmentVariableReader)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            if (baseUrl == null)
            {
                throw new ArgumentNullException(nameof(baseUrl));
            }

            _client = client;
            BaseUri = baseUrl;
            SupportsPackageIdMetadata = supportsPackageIdMetadata;
            _environmentVariableReader = environmentVariableReader ?? EnvironmentVariableWrapper.Instance;
        }

        /// <summary>
        /// Gets the <see cref="Uri"/> for the source backing this resource.
        /// </summary>
        public Uri BaseUri { get; }

        /// <summary>
        /// Gets whether the source supports package ID-level metadata on the registration index.
        /// </summary>
        public virtual bool SupportsPackageIdMetadata { get; }

        /// <summary>
        /// Constructs the URI of a registration index blob
        /// </summary>
        public virtual Uri GetUri(string packageId)
        {
            if (string.IsNullOrEmpty(packageId))
            {
                throw new InvalidOperationException();
            }

            PackageIdValidator.Validate(packageId);

            return new Uri(string.Format(CultureInfo.InvariantCulture, "{0}/{1}/index.json",
                BaseUri.AbsoluteUri.TrimEnd('/'), packageId.ToLowerInvariant()));
        }

        /// <summary>
        /// Constructs the URI of a registration blob with a specific version
        /// </summary>
        public virtual Uri GetUri(string id, NuGetVersion version)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(id));
            }

            if (version == null)
            {
                throw new ArgumentNullException(nameof(version));
            }

            return GetUri(new PackageIdentity(id, version));
        }

        /// <summary>
        /// Constructs the URI of a registration blob with a specific version
        /// </summary>
        public virtual Uri GetUri(PackageIdentity package)
        {
            if (package == null
                || package.Id == null
                || package.Version == null)
            {
                throw new InvalidOperationException();
            }

            PackageIdValidator.Validate(package.Id);

            return new Uri(string.Format(CultureInfo.InvariantCulture, "{0}/{1}/{2}.json", BaseUri.AbsoluteUri.TrimEnd('/'),
                package.Id.ToLowerInvariant(), package.Version.ToNormalizedString().ToLowerInvariant()));
        }

        /// <summary>
        /// Returns the registration blob for the id and version
        /// </summary>
        /// <remarks>The inlined entries are potentially going away soon</remarks>
        public virtual async Task<JObject?> GetPackageMetadata(PackageIdentity identity, SourceCacheContext cacheContext, Common.ILogger log, CancellationToken token)
        {
            return (await GetPackageMetadata(identity.Id, new VersionRange(identity.Version, true, identity.Version, true), true, true, cacheContext, log, token)).SingleOrDefault();
        }

        /// <summary>
        /// Returns inlined catalog entry items for each registration blob
        /// </summary>
        /// <remarks>The inlined entries are potentially going away soon</remarks>
        public virtual async Task<IEnumerable<JObject>> GetPackageMetadata(string packageId, bool includePrerelease, bool includeUnlisted, SourceCacheContext cacheContext, Common.ILogger log, CancellationToken token)
        {
            return await GetPackageMetadata(packageId, VersionRange.All, includePrerelease, includeUnlisted, cacheContext, log, token);
        }

        /// <summary>
        /// Returns inlined catalog entry items for each registration blob
        /// </summary>
        /// <remarks>The inlined entries are potentially going away soon</remarks>
        public virtual async Task<IEnumerable<JObject>> GetPackageMetadata(
            string packageId,
            VersionRange range,
            bool includePrerelease,
            bool includeUnlisted,
            SourceCacheContext cacheContext,
            Common.ILogger log,
            CancellationToken token)
        {
            var results = new List<JObject>();

            var registrationUri = GetUri(packageId);

            var ranges = await RegistrationUtility.LoadRanges(_client, registrationUri, packageId, range, cacheContext, log, token);

            foreach (var rangeObj in ranges)
            {
                if (rangeObj == null)
                {
                    throw new InvalidDataException(registrationUri.AbsoluteUri);
                }

                foreach (JObject packageObj in rangeObj["items"]!)
                {
                    var catalogEntry = (JObject)packageObj["catalogEntry"]!;
                    var version = NuGetVersion.Parse(catalogEntry["version"]!.ToString());
                    var listed = catalogEntry.GetBoolean("listed") ?? true;

                    if (range.Satisfies(version)
                        && (includePrerelease || !version.IsPrerelease)
                        && (includeUnlisted || listed))
                    {
                        // add in the download url
                        if (packageObj["packageContent"] != null)
                        {
                            catalogEntry["packageContent"] = packageObj["packageContent"];
                        }

                        results.Add(catalogEntry);
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Returns all index entries of type Package within the given range and filters
        /// </summary>
        public virtual Task<IEnumerable<JObject>> GetPackageEntries(string packageId, bool includeUnlisted, SourceCacheContext cacheContext, Common.ILogger log, CancellationToken token)
        {
            return GetPackageMetadata(packageId, VersionRange.All, true, includeUnlisted, cacheContext, log, token);
        }

        /// <summary>
        /// Strongly typed, System.Text.Json based equivalent of <see cref="GetPackageMetadata(string, VersionRange, bool, bool, SourceCacheContext, Common.ILogger, CancellationToken)"/>.
        /// Returns the registration leaf items (catalog entry plus package content url) matching the filters. Used when
        /// the <c>NuGet.UseSystemTextJsonDeserialization</c> feature switch is enabled so that the Newtonsoft.Json based
        /// JObject path can be trimmed by the linker.
        /// </summary>
        internal virtual async Task<IReadOnlyList<RegistrationLeafItem>> GetPackageMetadataItemsAsync(
            string packageId,
            VersionRange range,
            bool includePrerelease,
            bool includeUnlisted,
            SourceCacheContext cacheContext,
            Common.ILogger log,
            CancellationToken token)
        {
            var results = new List<RegistrationLeafItem>();

            Uri registrationUri = GetUri(packageId);

            IReadOnlyList<RegistrationPage?> ranges = await RegistrationUtility.LoadRangesAsItemsAsync(_client, registrationUri, packageId, range, cacheContext, log, token);

            // NoAllocEnumerate can't be used on nullable types, so avoid allocating an enumerator by using a for loop.
            for (int i = 0; i < ranges.Count; i++)
            {
                RegistrationPage? page = ranges[i];

                if (page is null || page.Items is null)
                {
                    throw new InvalidDataException(registrationUri.AbsoluteUri);
                }

                foreach (RegistrationLeafItem leaf in page.Items)
                {
                    if (leaf is null || leaf.CatalogEntry is null)
                    {
                        throw new InvalidDataException(registrationUri.AbsoluteUri);
                    }

                    PackageSearchMetadataRegistration catalogEntry = leaf.CatalogEntry;
                    NuGetVersion version = catalogEntry.Version;
                    bool listed = catalogEntry.IsListed;

                    if (range.Satisfies(version)
                        && (includePrerelease || !version.IsPrerelease)
                        && (includeUnlisted || listed))
                    {
                        results.Add(leaf);
                    }
                }
            }

            return results;
        }

        internal virtual async Task<RegistrationLeafItem?> GetPackageMetadataItemAsync(PackageIdentity identity, SourceCacheContext cacheContext, Common.ILogger log, CancellationToken token)
        {
            return (await GetPackageMetadataItemsAsync(identity.Id, new VersionRange(identity.Version, true, identity.Version, true), true, true, cacheContext, log, token)).SingleOrDefault();
        }

        /// <summary>
        /// Gets the package-ID-scoped metadata declared at the root of a package's registration
        /// index, without enumerating any version pages.
        /// </summary>
        /// <param name="packageId">The package ID to look up.</param>
        /// <param name="cacheContext">Cache context.</param>
        /// <param name="log">Logger.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>
        /// The root metadata, or <see langword="null" /> when the source has no registration index
        /// for this package.
        /// </returns>
        /// <remarks>
        /// The other retrieval methods on this resource return version-scoped data and discard the
        /// index root. This is the package-scoped counterpart.
        /// </remarks>
        public virtual async Task<PackageIdMetadata?> GetPackageIdMetadataAsync(
            string packageId,
            SourceCacheContext cacheContext,
            Common.ILogger log,
            CancellationToken token)
        {
            Uri registrationUri = GetUri(packageId);
            string packageIdLowerCase = packageId.ToLowerInvariant();
            HttpSourceCacheContext httpSourceCacheContext = HttpSourceCacheContext.Create(cacheContext, retryCount: 0);

            RegistrationIndex? index = await _client.GetAsync(
                new HttpSourceCachedRequest(
                    registrationUri.OriginalString,
                    $"list_{packageIdLowerCase}_index",
                    httpSourceCacheContext)
                {
                    IgnoreNotFounds = true,
                },
                httpSourceResult => DeserializeRegistrationIndexAsync(httpSourceResult.Stream, token),
                log,
                token);

            if (index == null)
            {
                return null;
            }

            return new PackageIdMetadata(index.Metadata?.SponsorshipUrls);
        }

        private async Task<RegistrationIndex?> DeserializeRegistrationIndexAsync(Stream? stream, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (stream == null)
            {
                return null;
            }

            if (NuGetFeatureFlags.UseSystemTextJsonDeserializationFeatureSwitch
                || NuGetFeatureFlags.IsSystemTextJsonDeserializationEnabledByEnvironment(_environmentVariableReader))
            {
                var typeInfo = (JsonTypeInfo<RegistrationIndex>)PackageSearchJsonContext.Default.GetTypeInfo(typeof(RegistrationIndex))!;
                return await System.Text.Json.JsonSerializer.DeserializeAsync(stream, typeInfo, token);
            }

            using var streamReader = new StreamReader(stream);
            using var jsonReader = new Newtonsoft.Json.JsonTextReader(streamReader);
            return JsonExtensions.JsonObjectSerializer.Deserialize<RegistrationIndex>(jsonReader);
        }
    }
}
