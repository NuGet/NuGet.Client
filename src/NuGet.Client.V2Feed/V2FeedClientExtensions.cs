// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Client
{
    public static class V2FeedClientExtensions
    {
        public static Task<DataServicePackageRepository> CreateV2FeedClient(this NuGetRepository self)
        {
            return CreateV2FeedClient(self, CancellationToken.None);
        }

        public static async Task<DataServicePackageRepository> CreateV2FeedClient(this NuGetRepository self, CancellationToken cancellationToken)
        {
            // Get the V2Feed service definition
            var v2FeedClient = await self.CreateClient("v2feed");
            cancellationToken.ThrowIfCancellationRequested();

            // Use the URL to create a DataServicePackageRepository using an adaptor IHttpClient
            return new DataServicePackageRepository(v2FeedClient.Service.RootUrl);
        }
    }
}
