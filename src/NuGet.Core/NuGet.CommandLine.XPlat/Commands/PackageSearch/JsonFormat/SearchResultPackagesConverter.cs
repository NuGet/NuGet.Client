// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using NuGet.Protocol;

namespace NuGet.CommandLine.XPlat
{
    internal class SearchResultPackagesConverter : JsonConverter<SearchResultPackage>
    {
        private readonly PackageSearchVerbosity _verbosity;
        private readonly bool _exactMatch;

        public SearchResultPackagesConverter(PackageSearchVerbosity verbosity, bool exactMatch)
        {
            _verbosity = verbosity;
            _exactMatch = exactMatch;
        }

        public override SearchResultPackage Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }

        public override void Write(Utf8JsonWriter writer, SearchResultPackage value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString(JsonProperties.PackageId, value.PackageSearchMetadata.Identity.Id);

            if (_exactMatch)
            {
                writer.WriteString(JsonProperties.Version, value.PackageSearchMetadata.Identity.Version.ToNormalizedString());
            }
            else
            {
                writer.WriteString(JsonProperties.LatestVersion, value.PackageSearchMetadata.Identity.Version.ToNormalizedString());
            }

            if (_verbosity == PackageSearchVerbosity.Normal || _verbosity == PackageSearchVerbosity.Detailed)
            {
                if (value.PackageSearchMetadata.DownloadCount.HasValue)
                {
                    writer.WriteNumber(JsonProperties.DownloadCount, (decimal)value.PackageSearchMetadata.DownloadCount);
                }
                else
                {
                    writer.WriteNull(JsonProperties.DownloadCount);
                }

                writer.WriteString(JsonProperties.Owners, value.PackageSearchMetadata.Owners);
            }

            if (_verbosity == PackageSearchVerbosity.Detailed)
            {
                writer.WriteString(JsonProperties.Description, value.PackageSearchMetadata.Description);

                if (value.PackageSearchMetadata.Vulnerabilities != null && value.PackageSearchMetadata.Vulnerabilities.Any())
                {
                    writer.WriteBoolean("vulnerable", true);
                }
                else
                {
                    writer.WriteNull("vulnerable");
                }

                writer.WriteString(JsonProperties.ProjectUrl, value.PackageSearchMetadata.ProjectUrl.ToString());
                writer.WriteString(JsonProperties.Deprecation, value.DeprecationMessage);
            }

            writer.WriteEndObject();
        }
    }
}
