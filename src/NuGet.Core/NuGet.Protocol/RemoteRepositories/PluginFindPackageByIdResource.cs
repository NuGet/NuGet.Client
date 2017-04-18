// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Protocol.Plugins;
using NuGet.Versioning;

namespace NuGet.Protocol.Core.Types
{
    /// <summary>
    /// A FindPackageByIdResource for plugins.
    /// </summary>
    public sealed class PluginFindPackageByIdResource : FindPackageByIdResource
    {
        private readonly PluginResource _pluginResource;

        /// <summary>
        /// Instantiates a new <see cref="PluginFindPackageByIdResource" /> class.
        /// </summary>
        /// <param name="pluginResource">A plugin resource.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="pluginResource" />
        /// is <c>null</c>.</exception>
        public PluginFindPackageByIdResource(PluginResource pluginResource)
        {
            if (pluginResource == null)
            {
                throw new ArgumentNullException(nameof(pluginResource));
            }

            _pluginResource = pluginResource;
        }

        /// <summary>
        /// Asynchronously copies a package to the specified stream.
        /// </summary>
        /// <param name="id">The package ID.</param>
        /// <param name="version">The package version.</param>
        /// <param name="destination">The destination stream for the copy operation.</param>
        /// <param name="cacheContext">A source cache context.</param>
        /// <param name="logger">A logger.</param>
        /// <param name="token">A cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.
        /// The task result (<see cref="Task{TResult}.Result" />) returns a
        /// <see cref="bool" /> indicating the result of the copy operation.</returns>
        public override Task<bool> CopyNupkgToStreamAsync(
            string id,
            NuGetVersion version,
            Stream destination,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken token)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Asynchronously gets all versions of a package.
        /// </summary>
        /// <param name="id">The package ID.</param>
        /// <param name="cacheContext">A source cache context.</param>
        /// <param name="logger">A logger.</param>
        /// <param name="token">A cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.
        /// The task result (<see cref="Task{TResult}.Result" />) returns a
        /// <see cref="IEnumerable{NuGetVersion}" />.</returns>
        public override Task<IEnumerable<NuGetVersion>> GetAllVersionsAsync(
            string id,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken token)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Asynchronously gets package dependency information.
        /// </summary>
        /// <param name="id">The package ID.</param>
        /// <param name="version">The package version.</param>
        /// <param name="cacheContext">A source cache context.</param>
        /// <param name="logger">A logger.</param>
        /// <param name="token">A cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.
        /// The task result (<see cref="Task{TResult}.Result" />) returns a
        /// <see cref="FindPackageByIdDependencyInfo" />.</returns>
        public override async Task<FindPackageByIdDependencyInfo> GetDependencyInfoAsync(
            string id,
            NuGetVersion version,
            SourceCacheContext cacheContext,
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