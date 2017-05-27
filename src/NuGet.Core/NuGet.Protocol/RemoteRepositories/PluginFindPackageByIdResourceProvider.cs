// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Plugins;

namespace NuGet.Protocol
{
    /// <summary>
    /// A FindPackageByIdResource provider for plugins.
    /// </summary>
    public sealed class PluginFindPackageByIdResourceProvider : ResourceProvider
    {
        /// <summary>
        /// Instantiates a new <see cref="PluginFindPackageByIdResourceProvider" /> class.
        /// </summary>
        public PluginFindPackageByIdResourceProvider()
            : base(typeof(FindPackageByIdResource),
                nameof(PluginFindPackageByIdResourceProvider),
                before: nameof(HttpFileSystemBasedFindPackageByIdResourceProvider))
        {
        }

        /// <summary>
        /// Asynchronously attempts to create a resource for the specified source repository.
        /// </summary>
        /// <param name="source">A source repository.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.
        /// The task result (<see cref="Task{TResult}.Result" />) returns a Tuple&lt;bool, INuGetResource&gt;</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> is <c>null</c>.</exception>
        /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken"/>
        /// is cancelled.</exception>
        public override async Task<Tuple<bool, INuGetResource>> TryCreate(
            SourceRepository source,
            CancellationToken cancellationToken)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            cancellationToken.ThrowIfCancellationRequested();

            PluginFindPackageByIdResource resource = null;

            var pluginResource = await source.GetResourceAsync<PluginResource>(cancellationToken);

            if (pluginResource != null)
            {
                var serviceIndexResource = await source.GetResourceAsync<ServiceIndexResourceV3>(cancellationToken);
                var httpHandlerResource = await source.GetResourceAsync<HttpHandlerResource>(cancellationToken);

                if (serviceIndexResource != null && httpHandlerResource != null)
                {
                    var result = await pluginResource.GetPluginAsync(OperationClaim.DownloadPackage, cancellationToken);

                    if (result != null)
                    {
                        var credentialsProvider = new PluginCredentialsProvider(
                            result.Plugin,
                            source.PackageSource,
                            httpHandlerResource.ClientHandler?.Proxy,
                            HttpHandlerResourceV3.CredentialService);

                        resource = new PluginFindPackageByIdResource(
                            result.Plugin,
                            result.PluginMulticlientUtilities,
                            source.PackageSource,
                            credentialsProvider);
                    }
                }
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }
    }
}