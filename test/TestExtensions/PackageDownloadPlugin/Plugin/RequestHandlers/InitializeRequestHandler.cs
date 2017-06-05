// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Plugins;

namespace NuGet.Test.PackageDownloadPlugin
{
    internal sealed class InitializeRequestHandler
        : RequestHandler<InitializeRequest, InitializeResponse>
    {
        internal override Task CancelAsync(
            IConnection connection,
            InitializeRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        internal override Task<InitializeResponse> RespondAsync(
            IConnection connection,
            InitializeRequest request,
            CancellationToken cancellationToken)
        {
            connection.Options.SetRequestTimeout(request.RequestTimeout);

            return Task.FromResult(new InitializeResponse(MessageResponseCode.Success));
        }
    }
}