// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Plugins;

namespace NuGet.Test.PackageDownloadPlugin
{
    internal sealed class SetLogLevelRequestHandler
        : RequestHandler<SetLogLevelRequest, SetLogLevelResponse>
    {
        private readonly Logger _logger;

        internal SetLogLevelRequestHandler(Logger logger)
        {
            Assert.IsNotNull(logger, nameof(logger));

            _logger = logger;
        }

        internal override Task CancelAsync(
            IConnection connection,
            SetLogLevelRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        internal override Task<SetLogLevelResponse> RespondAsync(
            IConnection connection,
            SetLogLevelRequest request,
            CancellationToken cancellationToken)
        {
            _logger.SetLogLevel(request.LogLevel);

            return Task.FromResult(new SetLogLevelResponse(MessageResponseCode.Success));
        }
    }
}