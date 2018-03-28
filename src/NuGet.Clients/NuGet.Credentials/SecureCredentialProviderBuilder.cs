// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Plugins;

namespace NuGet.Credentials
{
    /// <summary>
    /// Builder for credential providers that are based on the secure plugin model (Version 2.0.0)
    /// </summary>
    public class SecureCredentialProviderBuilder
    {
        private Common.ILogger _logger;

        public SecureCredentialProviderBuilder(Common.ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Creates credential providers for each valid plugin (regardless if it supports authentication or not)
        /// </summary>
        /// <returns>credential providers</returns>
        public async Task<IEnumerable<ICredentialProvider>> BuildAll()
        {
            var availablePlugins = await PluginManager.Instance.FindAvailablePluginsAsync(CancellationToken.None);

            var plugins = new List<ICredentialProvider>();
            foreach (var pluginDiscoveryResult in availablePlugins)
            {
                if (pluginDiscoveryResult.PluginFile.State == PluginFileState.Valid)
                {
                    _logger.LogDebug($"Will attempt to use {pluginDiscoveryResult.PluginFile.Path} as a credential provider");
                    plugins.Add(new SecurePluginCredentialProvider(pluginDiscoveryResult, _logger));
                }
                else
                {
                    _logger.LogDebug($"Skipping {pluginDiscoveryResult.PluginFile.Path} as a credential provider.\n{pluginDiscoveryResult.Message}");
                }
            }

            return plugins;
        }

    }
}
