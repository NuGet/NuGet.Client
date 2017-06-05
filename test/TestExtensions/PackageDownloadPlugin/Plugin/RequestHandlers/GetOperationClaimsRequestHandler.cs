// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Plugins;

namespace NuGet.Test.PackageDownloadPlugin
{
    internal sealed class GetOperationClaimsRequestHandler
        : RequestHandler<GetOperationClaimsRequest, GetOperationClaimsResponse>
    {
        private readonly IEnumerable<PluginPackageSource> _pluginPackageSources;

        internal GetOperationClaimsRequestHandler(IEnumerable<PluginPackageSource> pluginPackageSources)
        {
            Assert.IsNotNull(pluginPackageSources, nameof(pluginPackageSources));

            _pluginPackageSources = pluginPackageSources;
        }

        internal override Task CancelAsync(
            IConnection connection,
            GetOperationClaimsRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        internal override Task<GetOperationClaimsResponse> RespondAsync(
            IConnection connection,
            GetOperationClaimsRequest request,
            CancellationToken cancellationToken)
        {
            var claims = new List<OperationClaim>();

            if (_pluginPackageSources.Any(
                pluginPackageSource => string.Equals(
                    pluginPackageSource.PackageSource.Source,
                    request.PackageSourceRepository,
                    StringComparison.OrdinalIgnoreCase)))
            {
                claims.Add(OperationClaim.DownloadPackage);
            }

            return Task.FromResult(new GetOperationClaimsResponse(claims.ToArray()));
        }
    }
}