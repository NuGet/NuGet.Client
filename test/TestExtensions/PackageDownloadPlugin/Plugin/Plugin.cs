// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Plugins;

namespace NuGet.Test.PackageDownloadPlugin
{
    internal sealed class Plugin
    {
        private readonly ServiceContainer _serviceContainer;

        internal Plugin(ServiceContainer serviceContainer)
        {
            Assert.IsNotNull(serviceContainer, nameof(serviceContainer));

            _serviceContainer = serviceContainer;
        }

        internal async Task RunAsync()
        {
            using (var cancellationTokenSource = new CancellationTokenSource())
            using (var closedEvent = new SemaphoreSlim(initialCount: 0))
            {
                var requestHandlers = CreateRequestHandlers();
                var options = ConnectionOptions.CreateDefault();

                var plugin = await PluginFactory.CreateFromCurrentProcessAsync(
                    requestHandlers,
                    options,
                    cancellationTokenSource.Token);

                if (plugin.Connection.ProtocolVersion != ProtocolConstants.CurrentVersion)
                {
                    throw new NotSupportedException();
                }

                var logger = _serviceContainer.GetInstance<Logger>();
                var credentialsService = _serviceContainer.GetInstance<CredentialsService>();

                logger.SetPlugin(plugin);
                credentialsService.SetPlugin(plugin);

                plugin.Connection.Faulted += (sender, args) =>
                {
                    Console.Error.WriteLine($"Faulted on message: {args.Message?.Type} {args.Message?.Method} {args.Message?.RequestId}");
                    Console.Error.WriteLine(args.Exception.ToString());
                };

                plugin.Closed += (sender, args) => closedEvent.Release();

                await closedEvent.WaitAsync();
            }
        }

        private RequestHandlers CreateRequestHandlers()
        {
            var handlers = new RequestHandlers();

            var credentialsService = _serviceContainer.GetInstance<CredentialsService>();
            var logger = _serviceContainer.GetInstance<Logger>();
            var pluginConfiguration = _serviceContainer.GetInstance<PluginConfiguration>();

            handlers.TryAdd(MessageMethod.CopyFilesInPackage, new CopyFilesInPackageRequestHandler(_serviceContainer));
            handlers.TryAdd(MessageMethod.CopyNupkgFile, new CopyNupkgFileRequestHandler(_serviceContainer));
            handlers.TryAdd(MessageMethod.GetFilesInPackage, new GetFilesInPackageRequestHandler(_serviceContainer));
            handlers.TryAdd(MessageMethod.GetOperationClaims, new GetOperationClaimsRequestHandler(pluginConfiguration.PluginPackageSources));
            handlers.TryAdd(MessageMethod.GetPackageHash, new GetPackageHashRequestHandler(_serviceContainer));
            handlers.TryAdd(MessageMethod.GetPackageVersions, new GetPackageVersionsRequestHandler(_serviceContainer));
            handlers.TryAdd(MessageMethod.Initialize, new InitializeRequestHandler());
            handlers.TryAdd(MessageMethod.PrefetchPackage, new PrefetchPackageRequestHandler(_serviceContainer));
            handlers.TryAdd(MessageMethod.SetCredentials, new SetCredentialsRequestHandler(credentialsService));
            handlers.TryAdd(MessageMethod.SetLogLevel, new SetLogLevelRequestHandler(logger));

            return handlers;
        }
    }
}