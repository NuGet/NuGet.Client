// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Packaging;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Protocol
{
    /// <summary>
    /// Stores/caches a service index json file.
    /// </summary>
    public class ServiceIndexResourceV3 : INuGetResource
    {
        private readonly string _json;
        private readonly IDictionary<string, List<ServiceIndexEntry>> _index;
        private readonly DateTime _requestTime;
        private static readonly IReadOnlyList<ServiceIndexEntry> _emptyEntries = new List<ServiceIndexEntry>();
        private static readonly SemanticVersion _defaultVersion = new SemanticVersion(0, 0, 0);
        private bool _allowInsecureConnections = false; //by default don't allow non https service index

        public ServiceIndexResourceV3(JObject index, DateTime requestTime)
        {
            _json = index.ToString();
            _index = MakeLookup(index);
            _requestTime = requestTime;
        }

        /// <summary>
        /// Time the index was requested
        /// </summary>
        public virtual DateTime RequestTime
        {
            get { return _requestTime; }
        }

        /// <summary>
        /// All service index entries.
        /// </summary>
        public virtual IReadOnlyList<ServiceIndexEntry> Entries
        {
            get
            {
                return _index.SelectMany(e => e.Value).ToList();
            }
        }

        public virtual string Json
        {
            get
            {
                return _json;
            }
        }

        /// <summary>
        /// Get the list of service entries that best match the current clientVersion and type.
        /// </summary>
        public virtual IReadOnlyList<ServiceIndexEntry> GetServiceEntries(params string[] orderedTypes)
        {
            var clientVersion = MinClientVersionUtility.GetNuGetClientVersion();

            return GetServiceEntries(clientVersion, orderedTypes);
        }

        /// <summary>
        /// Get the list of service entries that best match the clientVersion and type.
        /// </summary>
        public virtual IReadOnlyList<ServiceIndexEntry> GetServiceEntries(NuGetVersion clientVersion, params string[] orderedTypes)
        {
            if (clientVersion == null)
            {
                throw new ArgumentNullException(nameof(clientVersion));
            }

            foreach (var type in orderedTypes)
            {
                List<ServiceIndexEntry> entries;
                if (_index.TryGetValue(type, out entries))
                {
                    var compatible = GetBestVersionMatchForType(clientVersion, entries);

                    if (compatible.Count > 0)
                    {
                        return compatible;
                    }
                }
            }

            return _emptyEntries;
        }

        private IReadOnlyList<ServiceIndexEntry> GetBestVersionMatchForType(NuGetVersion clientVersion, List<ServiceIndexEntry> entries)
        {
            var bestMatch = entries.FirstOrDefault(e => e.ClientVersion <= clientVersion);

            if (bestMatch == null)
            {
                // No compatible version
                return _emptyEntries;
            }
            else
            {
                // Find all entries with the same version.
                List<ServiceIndexEntry> matchingEntries = new List<ServiceIndexEntry>();

                foreach (var entry in entries)
                {
                    if (entry.ClientVersion == bestMatch.ClientVersion)
                    {
                        if (entry.Uri.Scheme == Uri.UriSchemeHttp && entry.Uri.Scheme != Uri.UriSchemeHttps && !_allowInsecureConnections)
                        {
                            throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.Error_HttpServiceIndexUsage, entry.Uri));
                        }

                        matchingEntries.Add(entry);
                    }
                }

                return matchingEntries;
            }
        }

        /// <summary>
        /// Get the best match service URI.
        /// </summary>
        public virtual Uri GetServiceEntryUri(params string[] orderedTypes)
        {
            var clientVersion = MinClientVersionUtility.GetNuGetClientVersion();

            return GetServiceEntryUris(clientVersion, orderedTypes).FirstOrDefault();
        }

        /// <summary>
        /// Get the best match service URI. Give the option to allow insecure connections or non https sources.
        /// Default is false.
        /// </summary>
        public virtual Uri GetServiceEntryUri(bool allowInsecureConnections, params string[] orderedTypes)
        {
            _allowInsecureConnections = allowInsecureConnections;
            return GetServiceEntryUri(orderedTypes);
        }

        /// <summary>
        /// Get the list of service URIs that best match the current clientVersion and type.
        /// </summary>
        public virtual IReadOnlyList<Uri> GetServiceEntryUris(params string[] orderedTypes)
        {
            var clientVersion = MinClientVersionUtility.GetNuGetClientVersion();

            return GetServiceEntryUris(clientVersion, orderedTypes);
        }

        /// <summary>
        /// Get the list of service URIs that best match the current clientVersion and type. Give the option to allow insecure connections or non https sources.
        /// Default is false.
        /// </summary>
        public virtual IReadOnlyList<Uri> GetServiceEntryUris(bool allowInsecureConnections, params string[] orderedTypes)
        {
            _allowInsecureConnections = allowInsecureConnections;
            return GetServiceEntryUris(orderedTypes);
        }

        /// <summary>
        /// Get the list of service URIs that best match the clientVersion and type.
        /// </summary>
        public virtual IReadOnlyList<Uri> GetServiceEntryUris(NuGetVersion clientVersion, params string[] orderedTypes)
        {
            if (clientVersion == null)
            {
                throw new ArgumentNullException(nameof(clientVersion));
            }

            return GetServiceEntries(clientVersion, orderedTypes).Select(e => e.Uri).ToList();
        }

        /// <summary>
        /// Get the list of service URIs that best match the clientVersion and type. Give the option to allow insecure connections or non https sources.
        /// Default is false.
        /// </summary>
        public virtual IReadOnlyList<Uri> GetServiceEntryUris(bool allowInsecureConnections, NuGetVersion clientVersion, params string[] orderedTypes)
        {
            _allowInsecureConnections = allowInsecureConnections;
            return GetServiceEntryUris(clientVersion, orderedTypes);
        }

        private static IDictionary<string, List<ServiceIndexEntry>> MakeLookup(JObject index)
        {
            var result = new Dictionary<string, List<ServiceIndexEntry>>(StringComparer.Ordinal);

            JToken resources;
            if (index.TryGetValue("resources", out resources))
            {
                foreach (var resource in resources)
                {
                    var id = GetValues(resource["@id"]).SingleOrDefault();

                    Uri uri;
                    if (string.IsNullOrEmpty(id) || !Uri.TryCreate(id, UriKind.Absolute, out uri))
                    {
                        // Skip invalid or missing @ids
                        continue;
                    }

                    var types = GetValues(resource["@type"]).ToArray();
                    var clientVersionToken = resource["clientVersion"];

                    var clientVersions = new List<SemanticVersion>();

                    if (clientVersionToken == null)
                    {
                        // For non-versioned services assume all clients are compatible
                        clientVersions.Add(_defaultVersion);
                    }
                    else
                    {
                        // Parse supported versions
                        foreach (var versionString in GetValues(clientVersionToken))
                        {
                            SemanticVersion version;
                            if (SemanticVersion.TryParse(versionString, out version))
                            {
                                clientVersions.Add(version);
                            }
                        }
                    }

                    // Create service entries
                    foreach (var type in types)
                    {
                        foreach (var version in clientVersions)
                        {
                            List<ServiceIndexEntry> entries;
                            if (!result.TryGetValue(type, out entries))
                            {
                                entries = new List<ServiceIndexEntry>();
                                result.Add(type, entries);
                            }

                            entries.Add(new ServiceIndexEntry(uri, type, version));
                        }
                    }
                }
            }

            // Order versions desc for faster lookup later.
            foreach (var type in result.Keys.ToArray())
            {
                result[type] = result[type].OrderByDescending(e => e.ClientVersion).ToList();
            }

            return result;
        }

        /// <summary>
        /// Read string values from an array or string.
        /// Returns an empty enumerable if the value is null.
        /// </summary>
        private static IEnumerable<string> GetValues(JToken token)
        {
            if (token?.Type == JTokenType.Array)
            {
                foreach (var entry in token)
                {
                    if (entry.Type == JTokenType.String)
                    {
                        yield return entry.ToObject<string>();
                    }
                }
            }
            else if (token?.Type == JTokenType.String)
            {
                yield return token.ToObject<string>();
            }
        }
    }
}
