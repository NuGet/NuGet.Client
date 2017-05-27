// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Plugins;

namespace NuGet.Protocol
{
    /// <summary>
    /// A download resource for plugins.
    /// </summary>
    public sealed class DownloadResourcePlugin : DownloadResource
    {
        private PluginCredentialsProvider _credentialsProvider;
        private readonly IPlugin _plugin;
        private readonly PackageSource _packageSource;
        private readonly IPluginMulticlientUtilities _utilities;

        /// <summary>
        /// Instantiates a new <see cref="DownloadResourcePlugin" /> class.
        /// </summary>
        /// <param name="plugin">A plugin.</param>
        /// <param name="utilities">A plugin multiclient utilities.</param>
        /// <param name="packageSource">A package source.</param>
        /// <param name="credentialsProvider">A plugin credentials provider.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="plugin" />
        /// is <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="utilities" />
        /// is <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="packageSource" />
        /// is <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="credentialsProvider" />
        /// is <c>null</c>.</exception>
        public DownloadResourcePlugin(
            IPlugin plugin,
            IPluginMulticlientUtilities utilities,
            PackageSource packageSource,
            PluginCredentialsProvider credentialsProvider)
        {
            if (plugin == null)
            {
                throw new ArgumentNullException(nameof(plugin));
            }

            if (utilities == null)
            {
                throw new ArgumentNullException(nameof(utilities));
            }

            if (packageSource == null)
            {
                throw new ArgumentNullException(nameof(packageSource));
            }

            if (credentialsProvider == null)
            {
                throw new ArgumentNullException(nameof(credentialsProvider));
            }

            _plugin = plugin;
            _utilities = utilities;
            _packageSource = packageSource;
            _credentialsProvider = credentialsProvider;
        }

        /// <summary>
        /// Asynchronously downloads a package.
        /// </summary>
        /// <param name="identity">The package identity.</param>
        /// <param name="downloadContext">A package download context.</param>
        /// <param name="globalPackagesFolder">The path to the global packages folder.</param>
        /// <param name="logger">A logger.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.
        /// The task result (<see cref="Task{TResult}.Result" />) returns
        /// a <see cref="DownloadResourceResult" />.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="identity" /> is <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="downloadContext" />
        /// is <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="logger" /> is <c>null</c>.</exception>
        /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken" />
        /// is cancelled.</exception>
        public async override Task<DownloadResourceResult> GetDownloadResourceResultAsync(
            PackageIdentity identity,
            PackageDownloadContext downloadContext,
            string globalPackagesFolder,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            if (downloadContext == null)
            {
                throw new ArgumentNullException(nameof(downloadContext));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            cancellationToken.ThrowIfCancellationRequested();

            AddOrUpdateLogger(_plugin, logger);

            _credentialsProvider = TryUpdateCredentialProvider(_plugin, _credentialsProvider);

            await _utilities.DoOncePerPluginLifetimeAsync(
                MessageMethod.SetLogLevel.ToString(),
                () => SetLogLevelAsync(logger, cancellationToken),
                cancellationToken);

            var response = await _plugin.Connection.SendRequestAndReceiveResponseAsync<PrefetchPackageRequest, PrefetchPackageResponse>(
                MessageMethod.PrefetchPackage,
                new PrefetchPackageRequest(_packageSource.Source, identity.Id, identity.Version.ToNormalizedString()),
                cancellationToken);

            if (response != null)
            {
                if (response.ResponseCode == MessageResponseCode.Success)
                {
                    var packageReader = new PluginPackageReader(_plugin, identity, _packageSource.Source);

                    return new DownloadResourceResult(packageReader, _packageSource.Source);
                }

                if (response.ResponseCode == MessageResponseCode.NotFound)
                {
                    return new DownloadResourceResult(DownloadResourceResultStatus.NotFound);
                }
            }

            throw new PluginException(
                string.Format(CultureInfo.CurrentCulture,
                Strings.Plugin_PackageDownloadFailed,
                _plugin.Name,
                $"{identity.Id}.{identity.Version.ToNormalizedString()}"));
        }

        private void AddOrUpdateLogger(IPlugin plugin, ILogger logger)
        {
            plugin.Connection.MessageDispatcher.RequestHandlers.AddOrUpdate(
                MessageMethod.Log,
                () => new LogRequestHandler(logger),
                existingHandler =>
                    {
                        ((LogRequestHandler)existingHandler).SetLogger(logger);

                        return existingHandler;
                    });
        }

        private async Task SetLogLevelAsync(ILogger logger, CancellationToken cancellationToken)
        {
            var logLevel = LogRequestHandler.GetLogLevel(logger);

            await _plugin.Connection.SendRequestAndReceiveResponseAsync<SetLogLevelRequest, SetLogLevelResponse>(
                MessageMethod.SetLogLevel,
                new SetLogLevelRequest(logLevel),
                cancellationToken);
        }

        private static PluginCredentialsProvider TryUpdateCredentialProvider(
            IPlugin plugin,
            PluginCredentialsProvider credentialProvider)
        {
            if (plugin.Connection.MessageDispatcher.RequestHandlers.TryAdd(MessageMethod.GetCredentials, credentialProvider))
            {
                return credentialProvider;
            }

            IRequestHandler handler;

            if (plugin.Connection.MessageDispatcher.RequestHandlers.TryGet(MessageMethod.GetCredentials, out handler))
            {
                return (PluginCredentialsProvider)handler;
            }

            throw new InvalidOperationException();
        }
    }
}