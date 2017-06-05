// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Plugins;

namespace NuGet.Test.PackageDownloadPlugin
{
    internal abstract class RequestHandler<TRequest, TResponse> : IRequestHandler, IDisposable
        where TRequest : class
        where TResponse : class
    {
        private readonly CancellationTokenSource _cancellationTokenSource;

        CancellationToken IRequestHandler.CancellationToken => _cancellationTokenSource.Token;

        internal RequestHandler()
        {
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            try
            {
                using (_cancellationTokenSource)
                {
                    _cancellationTokenSource.Cancel();
                }
            }
            catch (Exception)
            {
            }
        }

        internal abstract Task CancelAsync(
            IConnection connection,
            TRequest request,
            CancellationToken cancellationToken);

        internal abstract Task<TResponse> RespondAsync(
            IConnection connection,
            TRequest request,
            CancellationToken cancellationToken);

        Task IRequestHandler.HandleCancelAsync(
            IConnection connection,
            Message request,
            IResponseHandler responseHandler,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        async Task IRequestHandler.HandleResponseAsync(
            IConnection connection,
            Message request,
            IResponseHandler responseHandler,
            CancellationToken cancellationToken)
        {
            var requestPayload = MessageUtilities.DeserializePayload<TRequest>(request);
            var progressInterval = TimeSpan.FromSeconds(connection.Options.RequestTimeout.TotalSeconds / 3);

            using (var progressReporter = AutomaticProgressReporter.Create(
                connection,
                request,
                progressInterval,
                cancellationToken))
            {
                var responsePayload = await RespondAsync(connection, requestPayload, cancellationToken);

                await responseHandler.SendResponseAsync(request, responsePayload,cancellationToken);
            }
        }
    }
}