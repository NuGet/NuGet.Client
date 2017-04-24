// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Plugins;

namespace NuGet.Test.TestExtensions.TestablePlugin
{
    internal sealed class TestablePlugin : IRequestHandler, IDisposable
    {
        private bool _isDisposed;
        private IPlugin _plugin;
        private readonly BlockingCollection<Response> _responses;

        public CancellationToken CancellationToken { get; private set; }

        internal TestablePlugin(BlockingCollection<Response> responses)
        {
            _responses = responses;
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _plugin.Dispose();

                GC.SuppressFinalize(this);

                _isDisposed = true;
            }
        }

        internal async Task StartAsync(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;

            var requestHandlers = CreateRequestHandlers();
            var options = ConnectionOptions.CreateDefault();

            _plugin = await PluginFactory.CreateFromCurrentProcessAsync(requestHandlers, options, cancellationToken);

            if (_plugin.Connection.ProtocolVersion != ProtocolConstants.CurrentVersion)
            {
                throw new NotSupportedException();
            }

            await Task.Delay(TimeSpan.FromMilliseconds(-1), cancellationToken);
        }

        private IRequestHandlers CreateRequestHandlers()
        {
            var handlers = new RequestHandlers();

            handlers.TryAdd(MessageMethod.Initialize, this);
            handlers.TryAdd(MessageMethod.GetOperationClaims, this);

            return handlers;
        }

        public Task HandleCancelAsync(Message message, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task HandleProgressAsync(Message message, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public async Task HandleResponseAsync(Message message, IResponseHandler responseHandler, CancellationToken cancellationToken)
        {
            var response = _responses.Take();

            if (message.Type == MessageType.Request && message.Method == MessageMethod.Initialize)
            {
                var initializeRequest = JsonSerializationUtilities.ToObject<InitializeRequest>(message.Payload);

                _plugin.Connection.Options.SetRequestTimeout(initializeRequest.RequestTimeout);
            }

            await responseHandler.SendResponseAsync(message, response.Payload, cancellationToken);
        }
    }
}