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
            var options = CreateConnectionOptions();

            _plugin = await PluginFactory.CreateFromCurrentProcessAsync(requestHandlers, options, cancellationToken);

            _plugin.Connection.MessageReceived += OnMessageReceived;

            cancellationToken.Register(() =>
            {
                _plugin.Connection.MessageReceived -= OnMessageReceived;
            });

            if (_plugin.Connection.ProtocolVersion != ProtocolConstants.CurrentVersion)
            {
                throw new NotSupportedException();
            }

            await Task.Delay(TimeSpan.FromMilliseconds(-1), cancellationToken);
        }

        private void OnMessageReceived(object sender, MessageEventArgs e)
        {
            var response = _responses.Take();
            var message = new Message(e.Message.RequestId, response.Type, response.Method, response.Payload);

            Task.Run(() => _plugin.Connection.SendAsync(message, CancellationToken), CancellationToken);
        }

        private IRequestHandlers CreateRequestHandlers()
        {
            var handlers = new RequestHandlers();

            handlers.TryAdd(MessageMethod.Initialize, this);
            handlers.TryAdd(MessageMethod.GetOperationClaims, this);

            return handlers;
        }

        private static ConnectionOptions CreateConnectionOptions()
        {
            return new ConnectionOptions(
                ProtocolConstants.CurrentVersion,
                ProtocolConstants.CurrentVersion,
                ProtocolConstants.MaxTimeout,
                ProtocolConstants.MaxTimeout);
        }

        public Task HandleCancelAsync(Message message, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task HandleProgressAsync(Message message, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task HandleResponseAsync(Message message, IResponseHandler responseHandler, CancellationToken cancellationToken)
        {
            switch (message.Method)
            {
                case MessageMethod.Initialize:
                    return HandleInitializeRequestAsync(message, responseHandler, cancellationToken);

                case MessageMethod.GetOperationClaims:
                    return HandleOperationClaimsRequestAsync(message, responseHandler, cancellationToken);

                default:
                    throw new NotImplementedException();
            }
        }

        private Task HandleOperationClaimsRequestAsync(Message message, IResponseHandler responseHandler, CancellationToken cancellationToken)
        {
            var claimsRequest = JsonSerializationUtilities.ToObject<GetOperationClaimsRequest>(message.Payload);
            var payload = new GetOperationClaimsResponse(new OperationClaim[] { OperationClaim.DownloadPackage });

            return responseHandler.SendResponseAsync(message, payload, cancellationToken);
        }

        private Task HandleInitializeRequestAsync(Message message, IResponseHandler responseHandler, CancellationToken cancellationToken)
        {
            var initializeRequest = JsonSerializationUtilities.ToObject<InitializeRequest>(message.Payload);

            _plugin.Connection.Options.SetRequestTimeout(initializeRequest.RequestTimeout);

            var payload = new InitializeResponse(MessageResponseCode.Success);

            return responseHandler.SendResponseAsync(message, payload, cancellationToken);
        }
    }
}