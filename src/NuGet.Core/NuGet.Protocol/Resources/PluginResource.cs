// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Protocol.Plugins;

namespace NuGet.Protocol.Core.Types
{
    /// <summary>
    /// Represents a plugin resource.
    /// </summary>
    public sealed class PluginResource : INuGetResource
    {
        private readonly IReadOnlyList<PluginCreationResult> _pluginCreationResults;
        private bool _hasLoggedWarnings;

        /// <summary>
        /// Instantiates a new <see cref="PluginResource" /> class.
        /// </summary>
        /// <param name="pluginCreationResults">Plugin creation results.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="pluginCreationResults" />
        /// is <c>null</c>.</exception>
        public PluginResource(IEnumerable<PluginCreationResult> pluginCreationResults)
        {
            if (pluginCreationResults == null)
            {
                throw new ArgumentNullException(nameof(pluginCreationResults));
            }

            _pluginCreationResults = pluginCreationResults.ToArray();
        }

        /// <summary>
        /// Gets the first plugin satisfying the required operation claims for the current package source.
        /// </summary>
        /// <param name="requiredClaim">The required operation claim.</param>
        /// <param name="logger">A logger.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.
        /// The task result (<see cref="Task{TResult}.Result" />) returns a <see cref="IPlugin" />.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="logger" /> is <c>null</c>.</exception>
        /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken" />
        /// is cancelled.</exception>
        public Task<IPlugin> GetPluginAsync(
            OperationClaim requiredClaim,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            cancellationToken.ThrowIfCancellationRequested();

            for (var i = 0; i < _pluginCreationResults.Count; ++i)
            {
                var result = _pluginCreationResults[i];

                if (!_hasLoggedWarnings && !string.IsNullOrEmpty(result.Message))
                {
                    logger.LogWarning(result.Message);
                }
                else if (result.Claims.Contains(requiredClaim))
                {
                    _hasLoggedWarnings = true;

                    return Task.FromResult<IPlugin>(result.Plugin);
                }
            }

            return Task.FromResult<IPlugin>(null);
        }
    }
}