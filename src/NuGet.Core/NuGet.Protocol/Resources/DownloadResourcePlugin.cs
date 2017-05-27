// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
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
        private readonly PluginResource _pluginResource;

        /// <summary>
        /// Instantiates a new <see cref="DownloadResourcePlugin" /> class.
        /// </summary>
        /// <param name="pluginResource">A plugin resource.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="pluginResource" />
        /// is <c>null</c>.</exception>
        public DownloadResourcePlugin(PluginResource pluginResource)
        {
            if (pluginResource == null)
            {
                throw new ArgumentNullException(nameof(pluginResource));
            }

            _pluginResource = pluginResource;
        }

        /// <summary>
        /// Asynchronously downloads a package.
        /// </summary>
        /// <param name="identity">The package identity.</param>
        /// <param name="downloadContext">A package download context.</param>
        /// <param name="globalPackagesFolder">The path to the global packages folder.</param>
        /// <param name="logger">A logger.</param>
        /// <param name="token">A cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.
        /// The task result (<see cref="Task{TResult}.Result" />) returns
        /// a <see cref="DownloadResourceResult" />.</returns>
        public override async Task<DownloadResourceResult> GetDownloadResourceResultAsync(
            PackageIdentity identity,
            PackageDownloadContext downloadContext,
            string globalPackagesFolder,
            ILogger logger,
            CancellationToken token)
        {
            using (var plugin = await _pluginResource.GetPluginAsync(OperationClaim.DownloadPackage, logger, token))
            {
                throw new NotImplementedException();
            }
        }
    }
}