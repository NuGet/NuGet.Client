// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using NuGet.Versioning;
using StjJsonPropertyNameAttribute = System.Text.Json.Serialization.JsonPropertyNameAttribute;
using StjJsonConverterAttribute = System.Text.Json.Serialization.JsonConverterAttribute;
using StjJsonIgnoreAttribute = System.Text.Json.Serialization.JsonIgnoreAttribute;
using StjJsonConstructorAttribute = System.Text.Json.Serialization.JsonConstructorAttribute;
using NuGet.Protocol.Converters;

namespace NuGet.Protocol.Core.Types
{
    public class VersionInfo
    {
        public VersionInfo(NuGetVersion version)
            : this(version, new long?())
        {
        }

        public VersionInfo(NuGetVersion version, string downloadCount)
        {
            Version = version;

            long count;
            if (long.TryParse(downloadCount, out count))
            {
                DownloadCount = count;
            }
        }

        [StjJsonConstructor]
        public VersionInfo(NuGetVersion version, long? downloadCount)
        {
            Version = version;
            DownloadCount = downloadCount;
        }

        [StjJsonPropertyName("version")]
        [StjJsonConverter(typeof(NuGetVersionStjConverter))]
        public NuGetVersion Version { get; init; }

        [StjJsonPropertyName("downloads")]
        public long? DownloadCount { get; init; }

        /// <summary>
        /// In V2, when finding the list of versions that a package ID has, we also get all of the metadata
        /// associated with each version. It would be wasteful to throw this away, so we store what we have
        /// here. For V3, the metadata property is null. Callers that receive this type need to be able to
        /// fetch this package metadata some other way if this property is null.
        /// </summary>
        [StjJsonIgnore]
        public IPackageSearchMetadata PackageSearchMetadata { get; set; }
    }
}
