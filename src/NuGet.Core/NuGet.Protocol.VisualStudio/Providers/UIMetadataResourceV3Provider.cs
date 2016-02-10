// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;

namespace NuGet.Protocol.VisualStudio
{
    public class UIMetadataResourceV3Provider : ResourceProvider
    {
        public UIMetadataResourceV3Provider()
            : base(typeof(PackageMetadataResource), "UIMetadataResourceV3Provider", "UIMetadataResourceV2Provider")
        {
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            PackageMetadataResourceV3 curResource = null;

            if (await source.GetResourceAsync<ServiceIndexResourceV3>(token) != null)
            {
                var regResource = await source.GetResourceAsync<RegistrationResourceV3>();
                var reportAbuseResource = await source.GetResourceAsync<ReportAbuseResourceV3>();

                var httpSourceResource = await source.GetResourceAsync<HttpSourceResource>(token);

                // construct a new resource
                curResource = new PackageMetadataResourceV3(httpSourceResource.HttpSource, regResource, reportAbuseResource);
            }

            return new Tuple<bool, INuGetResource>(curResource != null, curResource);
        }
    }
}
