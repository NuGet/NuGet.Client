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

namespace NuGet.Protocol
{
    public class PackageMetadataResourceV3 : PackageMetadataResource
    {
        private readonly RegistrationResourceV3 _regResource;
        private readonly ReportAbuseResourceV3 _reportAbuseResource;
        private readonly HttpSource _client;

        public PackageMetadataResourceV3(HttpSource client, RegistrationResourceV3 regResource, ReportAbuseResourceV3 reportAbuseResource)
        {
            _regResource = regResource;
            _client = client;
            _reportAbuseResource = reportAbuseResource;
        }

        public override async Task<IEnumerable<IPackageSearchMetadata>> GetMetadataAsync(string packageId, bool includePrerelease, bool includeUnlisted, Logging.ILogger log, CancellationToken token)
        {
            var metadataList = await _regResource.GetPackageMetadata(packageId, includePrerelease, includeUnlisted, log, token);
            return metadataList.Select(ParseMetadata);
        }

        private IPackageSearchMetadata ParseMetadata(JObject metadata)
        {
            var parsed = metadata.FromJToken<PackageSearchMetadata>();
            parsed.ReportAbuseUrl = _reportAbuseResource?.GetReportAbuseUrl(parsed.PackageId, parsed.Version);
            return parsed;
        }
    }
}