// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Plugins;

namespace NuGet.Test.PackageDownloadPlugin
{
    internal sealed class SetCredentialsRequestHandler
        : RequestHandler<SetCredentialsRequest, SetCredentialsResponse>
    {
        private readonly CredentialsService _credentialsService;

        internal SetCredentialsRequestHandler(CredentialsService credentialsService)
        {
            Assert.IsNotNull(credentialsService, nameof(credentialsService));

            _credentialsService = credentialsService;
        }

        internal override Task CancelAsync(
            IConnection connection,
            SetCredentialsRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        internal override Task<SetCredentialsResponse> RespondAsync(
            IConnection connection,
            SetCredentialsRequest request,
            CancellationToken cancellationToken)
        {
            _credentialsService.PackageSourceCredentials.UpdateCredential(
                request.PackageSourceRepository,
                new NetworkCredential(request.Username, request.Password));

            _credentialsService.ProxyCredentials.UpdateCredential(
                request.PackageSourceRepository,
                new NetworkCredential(request.ProxyUsername, request.ProxyPassword));

            return Task.FromResult(new SetCredentialsResponse(MessageResponseCode.Success));
        }
    }
}