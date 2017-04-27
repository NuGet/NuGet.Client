// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Protocol.Plugins;

namespace NuGet.Protocol.Core.Types
{
    // Unsealed for testing purposes.
    public class PluginResourceProvider : ResourceProvider
    {
        private const string _environmentVariable = "NUGET_PLUGIN_PATHS";

        private static Lazy<PluginDiscoverer> _discoverer;
        private static IEnvironmentVariableReader _environmentVariableReader;
        private static PluginFactory _pluginFactory;
        private static ConcurrentDictionary<string, Task<IReadOnlyList<OperationClaim>>> _pluginOperationClaims;
        private static string _rawPluginPaths;

        public static IEnvironmentVariableReader EnvironmentVariableReader { get; private set; }

        static PluginResourceProvider()
        {
            Reinitialize(new EnvironmentVariableWrapper());
        }

        public PluginResourceProvider()
            : base(typeof(PluginResource), nameof(PluginResourceProvider))
        {
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(
            SourceRepository source,
            CancellationToken cancellationToken)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            cancellationToken.ThrowIfCancellationRequested();

            PluginResource resource = null;

            // Fast path
            if (IsPluginPossiblyAvailable())
            {
                var serviceIndex = await source.GetResourceAsync<ServiceIndexResourceV3>(cancellationToken);

                if (serviceIndex != null)
                {
                    var results = await _discoverer.Value.DiscoverAsync(cancellationToken);

                    var pluginCreationResults = await GetPluginsForPackageSourceAsync(
                        source.PackageSource.Source,
                        serviceIndex,
                        results,
                        cancellationToken);

                    if (pluginCreationResults.Any())
                    {
                        resource = new PluginResource(pluginCreationResults);
                    }
                }
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }

        /// <summary>
        /// Reinitializes static state.
        /// </summary>
        /// <remarks>This is non-private only to facilitate unit testing.</remarks>
        /// <param name="reader">An environment variable reader.</param>
        public static void Reinitialize(IEnvironmentVariableReader reader)
        {
            EnvironmentVariableReader = reader;
            _rawPluginPaths = reader?.GetEnvironmentVariable(_environmentVariable);
            _discoverer = new Lazy<PluginDiscoverer>(InitializeDiscoverer);
            _pluginFactory = new PluginFactory(PluginConstants.IdleTimeout);
            _pluginOperationClaims = new ConcurrentDictionary<string, Task<IReadOnlyList<OperationClaim>>>(StringComparer.OrdinalIgnoreCase);
        }

        private async Task<IEnumerable<PluginCreationResult>> GetPluginsForPackageSourceAsync(
            string packageSourceRepository,
            ServiceIndexResourceV3 serviceIndex,
            IEnumerable<PluginDiscoveryResult> results,
            CancellationToken cancellationToken)
        {
            var pluginCreationResults = new List<PluginCreationResult>();
            var serviceIndexJson = JObject.Parse(serviceIndex.Json);

            foreach (var result in results)
            {
                PluginCreationResult pluginCreationResult = null;

                var state = PluginUtilities.IsDebuggingPlugin() ? PluginFileState.Valid : result.PluginFile.State;

                if (state == PluginFileState.Valid)
                {
                    var plugin = await _pluginFactory.GetOrCreateAsync(
                        result.PluginFile.Path,
                        PluginConstants.PluginArguments,
                        new RequestHandlers(),
                        ConnectionOptions.CreateDefault(),
                        cancellationToken);

                    var operationClaims = await _pluginOperationClaims.GetOrAdd(
                        result.PluginFile.Path,
                        filePath => GetPluginOperationClaimsAsync(
                            plugin,
                            packageSourceRepository,
                            serviceIndexJson,
                            cancellationToken));

                    pluginCreationResult = new PluginCreationResult(plugin, operationClaims);
                }
                else
                {
                    pluginCreationResult = new PluginCreationResult(result.Message);
                }

                pluginCreationResults.Add(pluginCreationResult);
            }

            return pluginCreationResults;
        }

        private async Task<IReadOnlyList<OperationClaim>> GetPluginOperationClaimsAsync(
            IPlugin plugin,
            string packageSourceRepository,
            JObject serviceIndex,
            CancellationToken cancellationToken)
        {
            await InitializePluginAsync(plugin, cancellationToken);

            var payload = new GetOperationClaimsRequest(packageSourceRepository, serviceIndex);

            var response = await plugin.Connection.SendRequestAndReceiveResponseAsync<GetOperationClaimsRequest, GetOperationClaimsResponse>(
                MessageMethod.GetOperationClaims,
                payload,
                cancellationToken);

            return response.Claims;
        }

        private static async Task InitializePluginAsync(
            IPlugin plugin,
            CancellationToken cancellationToken)
        {
            var clientVersion = MinClientVersionUtility.GetNuGetClientVersion().ToNormalizedString();
            var culture = CultureInfo.CurrentCulture.Name;
            var payload = new InitializeRequest(clientVersion, culture, Verbosity.Detailed, TimeSpan.FromSeconds(30));

            var response = await plugin.Connection.SendRequestAndReceiveResponseAsync<InitializeRequest, InitializeResponse>(
                MessageMethod.Initialize,
                payload,
                cancellationToken);

            if (response.ResponseCode != MessageResponseCode.Success)
            {
                throw new PluginException(Strings.Plugin_InitializationFailed);
            }
        }

        private static PluginDiscoverer InitializeDiscoverer()
        {
            var verifier = EmbeddedSignatureVerifier.Create();

            return new PluginDiscoverer(_rawPluginPaths, verifier);
        }

        private static bool IsPluginPossiblyAvailable()
        {
            return !string.IsNullOrEmpty(_rawPluginPaths);
        }
    }
}