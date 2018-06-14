// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Protocol.Plugins;
using NuGet.Shared;

namespace NuGet.Protocol.Core.Types
{
    /// <summary>
    /// A plugin manager. This manages all the live plugins and their operation claims.
    /// Invoked in by both the credential provider and the PluginResourceProvider
    /// </summary>
    public sealed class PluginManager : IPluginManager, IDisposable
    {
        private static readonly Lazy<IPluginManager> Lazy = new Lazy<IPluginManager>(() => new PluginManager());
        public static IPluginManager Instance => Lazy.Value;

        private const string _idleTimeoutEnvironmentVariable = "NUGET_PLUGIN_IDLE_TIMEOUT_IN_SECONDS";
        private const string _pluginPathsEnvironmentVariable = "NUGET_PLUGIN_PATHS";

        private ConnectionOptions _connectionOptions;
        private Lazy<IPluginDiscoverer> _discoverer;
        private bool _isDisposed;
        private IPluginFactory _pluginFactory;
        private ConcurrentDictionary<PluginRequestKey, Lazy<Task<IReadOnlyList<OperationClaim>>>> _pluginOperationClaims;
        private ConcurrentDictionary<string, Lazy<IPluginMulticlientUtilities>> _pluginUtilities;
        private string _rawPluginPaths;

        private static Lazy<int> _currentProcessId = new Lazy<int>(GetCurrentProcessId);

        /// <summary>
        /// Gets an environment variable reader.
        /// </summary>
        /// <remarks>This is non-private only to facilitate testing.</remarks>
        public IEnvironmentVariableReader EnvironmentVariableReader { get; private set; }

        private PluginManager()
        {
            Initialize(
                new EnvironmentVariableWrapper(),
                new Lazy<IPluginDiscoverer>(InitializeDiscoverer),
                (TimeSpan idleTimeout) => new PluginFactory(idleTimeout));
        }

        /// <summary>
        /// Creates a new plugin manager
        /// </summary>
        /// <remarks>This is public to facilitate unit testing. This should not be called from product code</remarks>
        /// <param name="reader">An environment variable reader.</param>
        /// <param name="pluginDiscoverer">A lazy plugin discoverer.</param>
        /// <param name="pluginFactoryCreator">A plugin factory creator.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="reader" /> is <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="pluginDiscoverer" />
        /// is <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="pluginFactoryCreator" />
        /// is <c>null</c>.</exception>
        public PluginManager(IEnvironmentVariableReader reader,
            Lazy<IPluginDiscoverer> pluginDiscoverer,
            Func<TimeSpan, IPluginFactory> pluginFactoryCreator)
        {
            Initialize(
                reader,
                pluginDiscoverer,
                pluginFactoryCreator);
        }

        /// <summary>
        /// Disposes of this instance.
        /// This should not be called in production code as this is a singleton instance.
        /// The pattern is implemented because the plugin manager transitively owns objects
        /// that need to implement IDisposable because they potentially have managed and unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (!_isDisposed)
            {
                if (_discoverer.IsValueCreated)
                {
                    _discoverer.Value.Dispose();
                }

                _pluginFactory.Dispose();

                GC.SuppressFinalize(this);

                _isDisposed = true;
            }
        }

        /// <summary>
        /// Find all available plugins on the machine
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns>PluginDiscoveryResults</returns>
        public async Task<IEnumerable<PluginDiscoveryResult>> FindAvailablePluginsAsync(CancellationToken cancellationToken)
        {
            return await _discoverer.Value.DiscoverAsync(cancellationToken);
        }

        /// <summary>
        /// Create plugins appropriate for the given source
        /// </summary>
        /// <param name="source"></param>
        /// <param name="cancellationToken"></param>
        /// <exception cref="ArgumentNullException">Throw if <paramref name="source"/> is null </exception>
        /// <returns>PluginCreationResults</returns>
        public async Task<IEnumerable<PluginCreationResult>> CreatePluginsAsync(
            SourceRepository source,
            CancellationToken cancellationToken)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var pluginCreationResults = new List<PluginCreationResult>();

            // Fast path
            if (source.PackageSource.IsHttp && IsPluginPossiblyAvailable())
            {
                var serviceIndex = await source.GetResourceAsync<ServiceIndexResourceV3>(cancellationToken);

                if (serviceIndex != null)
                {
                    var serviceIndexJson = JObject.Parse(serviceIndex.Json);

                    var results = await FindAvailablePluginsAsync(cancellationToken);

                    foreach (var result in results)
                    {
                        var pluginCreationResult = await CreatePluginAsync(
                            result,
                            OperationClaim.DownloadPackage,
                            new PluginRequestKey(result.PluginFile.Path, source.PackageSource.Source),
                            source.PackageSource.Source,
                            serviceIndexJson,
                            cancellationToken);

                        pluginCreationResults.Add(pluginCreationResult);

                    }
                }
            }
            return pluginCreationResults;
        }

        /// <summary>
        /// Creates a plugin from the given pluginDiscoveryResult.
        /// This plugin's operations will be source agnostic ones
        /// </summary>
        /// <param name="pluginDiscoveryResult"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>A PluginCreationResult</returns>
        public Task<PluginCreationResult> CreateSourceAgnosticPluginAsync(PluginDiscoveryResult pluginDiscoveryResult, CancellationToken cancellationToken)
        {
            if (pluginDiscoveryResult == null)
            {
                throw new ArgumentNullException(nameof(pluginDiscoveryResult));
            }
            // Provide requested Plugin Operation
            // here check if a plugin contains non source agnostic 
            return CreatePluginAsync(pluginDiscoveryResult, OperationClaim.Authentication, new PluginRequestKey(pluginDiscoveryResult.PluginFile.Path, "Source-Agnostic"), null, null, cancellationToken);
        }

        private Lazy<string> _pluginsCacheDirectory = new Lazy<string>(() => NuGetEnvironment.GetFolderPath(NuGetFolderPath.NuGetPluginsCacheDirectory));

        // TODO NK - Moved this into a shared utility
        private static string ComputeHash(string value)
        {
            var trailing = value.Length > 32 ? value.Substring(value.Length - 32) : value;
            byte[] hash;
            using (var sha = SHA1.Create())
            {
                hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
            }

            const string hex = "0123456789abcdef";
            return hash.Aggregate("$" + trailing, (result, ch) => "" + hex[ch / 0x10] + hex[ch % 0x10] + result);
        }
        private async Task<PluginCreationResult> CreatePluginAsync(
            PluginDiscoveryResult result,
            OperationClaim requestedOperationClaim,
            PluginRequestKey requestKey,
            string packageSourceRepository,
            JObject serviceIndex,
            CancellationToken cancellationToken)
        {
            PluginCreationResult pluginCreationResult = null;
            var context = new PluginCacheContext(_pluginsCacheDirectory.Value, result.PluginFile.Path);
            var cacheResult = new PluginCacheResult(context.RootFolder, context.CacheFileName);

            return await ConcurrencyUtilities.ExecuteWithFileLockedAsync(
                context.CacheFileName,
                action: async lockedToken =>
                {

                    IList<OperationClaim> cachedOperationClaims;
                    Stream content = null;
                    try
                    {
                        content = HttpCacheUtility.TryReadCacheFile(context.MaxAge, context.CacheFileName);
                        if (content != null)
                        {
                            cacheResult.ProcessContent(content);
                        }
                    }
                    catch
                    {
                        content?.Dispose();
                    }
                    finally
                    {
                        content?.Dispose();
                    }

                    cachedOperationClaims = cacheResult.GetOperationClaims(requestKey);
                    if (cachedOperationClaims == null || cachedOperationClaims.Contains(requestedOperationClaim))
                    {
                        if (result.PluginFile.State.Value == PluginFileState.Valid)
                        {
                            var plugin = await _pluginFactory.GetOrCreateAsync(
                                result.PluginFile.Path,
                                PluginConstants.PluginArguments,
                                new RequestHandlers(),
                                _connectionOptions,
                                cancellationToken);

                            var utilities = await PerformOneTimePluginInitializationAsync(plugin, cancellationToken);

                            // We still make the GetOperationClaims call even if we have the operation claims cached. This is a way to self-update the cache.
                             var lazyOperationClaims = _pluginOperationClaims.GetOrAdd(
                                    requestKey,
                                    key => new Lazy<Task<IReadOnlyList<OperationClaim>>>(() =>
                                    GetPluginOperationClaimsAsync(
                                        plugin,
                                        packageSourceRepository,
                                        serviceIndex,
                                        cancellationToken)));

                            // Why does this not work?
                            var operationClaims = await lazyOperationClaims.Value;

                            if (!EqualityUtility.SequenceEqualWithNullCheck(operationClaims, cachedOperationClaims))
                            {
                                cacheResult.AddOrUpdateOperationClaims(requestKey, operationClaims.AsList());
                                await cacheResult.UpdateCacheFileAsync();
                            }

                            pluginCreationResult = new PluginCreationResult(
                                plugin,
                                utilities.Value,
                                operationClaims);
                        }
                        else
                        {
                            pluginCreationResult = new PluginCreationResult(result.Message);
                        }
                    }

                    // Return a dummy uninitialized result if the plugin was not started because the supported operation is not available.
                    return pluginCreationResult ?? new PluginCreationResult(
                                NoOpPlugin.Instance,
                                NoOpIPluginMulticlientUtilities.Instance,
                                Array.Empty<OperationClaim>());
                },
                token: cancellationToken
                );
        }

        private async Task<Lazy<IPluginMulticlientUtilities>> PerformOneTimePluginInitializationAsync(IPlugin plugin, CancellationToken cancellationToken)
        {
            var utilities = _pluginUtilities.GetOrAdd(
                                plugin.Id,
                                path => new Lazy<IPluginMulticlientUtilities>(
                                    () => new PluginMulticlientUtilities()));

            await utilities.Value.DoOncePerPluginLifetimeAsync(
                MessageMethod.MonitorNuGetProcessExit.ToString(),
                () => plugin.Connection.SendRequestAndReceiveResponseAsync<MonitorNuGetProcessExitRequest, MonitorNuGetProcessExitResponse>(
                    MessageMethod.MonitorNuGetProcessExit,
                    new MonitorNuGetProcessExitRequest(_currentProcessId.Value),
                    cancellationToken),
                cancellationToken);

            await utilities.Value.DoOncePerPluginLifetimeAsync(
                MessageMethod.Initialize.ToString(),
                () => InitializePluginAsync(plugin, _connectionOptions.RequestTimeout, cancellationToken),
                cancellationToken);
            return utilities;
        }

        private void Initialize(IEnvironmentVariableReader reader,
            Lazy<IPluginDiscoverer> pluginDiscoverer,
            Func<TimeSpan, IPluginFactory> pluginFactoryCreator)
        {
            EnvironmentVariableReader = reader ?? throw new ArgumentNullException(nameof(reader));
            _discoverer = pluginDiscoverer ?? throw new ArgumentNullException(nameof(pluginDiscoverer));

            if (pluginFactoryCreator == null)
            {
                throw new ArgumentNullException(nameof(pluginFactoryCreator));
            }

            _rawPluginPaths = reader.GetEnvironmentVariable(_pluginPathsEnvironmentVariable);

            _connectionOptions = ConnectionOptions.CreateDefault(reader);

            var idleTimeoutInSeconds = EnvironmentVariableReader.GetEnvironmentVariable(_idleTimeoutEnvironmentVariable);
            var idleTimeout = TimeoutUtilities.GetTimeout(idleTimeoutInSeconds, PluginConstants.IdleTimeout);

            _pluginFactory = pluginFactoryCreator(idleTimeout);
            _pluginOperationClaims = new ConcurrentDictionary<PluginRequestKey, Lazy<Task<IReadOnlyList<OperationClaim>>>>();
            _pluginUtilities = new ConcurrentDictionary<string, Lazy<IPluginMulticlientUtilities>>(
                StringComparer.OrdinalIgnoreCase);
        }

        private async Task<IReadOnlyList<OperationClaim>> GetPluginOperationClaimsAsync(
            IPlugin plugin,
            string packageSourceRepository,
            JObject serviceIndex,
            CancellationToken cancellationToken)
        {
            if (plugin.Connection.ProtocolVersion.Equals(Plugins.ProtocolConstants.Version100) && (string.IsNullOrEmpty(packageSourceRepository) || serviceIndex == null))
            {
                throw new ArgumentException("Cannot invoke get operation claims with null arguments on a " + Plugins.ProtocolConstants.Version100 + " plugin");
            }

            var payload = new GetOperationClaimsRequest(packageSourceRepository, serviceIndex);

            var response = await plugin.Connection.SendRequestAndReceiveResponseAsync<GetOperationClaimsRequest, GetOperationClaimsResponse>(
                MessageMethod.GetOperationClaims,
                payload,
                cancellationToken);
            if (response == null)
            {
                return Array.Empty<OperationClaim>();
            }

            return response.Claims;
        }

        private PluginDiscoverer InitializeDiscoverer()
        {
            var verifier = EmbeddedSignatureVerifier.Create();

            return new PluginDiscoverer(_rawPluginPaths, verifier);
        }

        private bool IsPluginPossiblyAvailable()
        {
            return !string.IsNullOrEmpty(_rawPluginPaths);
        }

        private static int GetCurrentProcessId()
        {
            using (var process = Process.GetCurrentProcess())
            {
                return process.Id;
            }
        }

        private static async Task InitializePluginAsync(
            IPlugin plugin,
            TimeSpan requestTimeout,
            CancellationToken cancellationToken)
        {
            var clientVersion = MinClientVersionUtility.GetNuGetClientVersion().ToNormalizedString();
            var culture = CultureInfo.CurrentCulture.Name;
            var payload = new InitializeRequest(
                clientVersion,
                culture,
                requestTimeout);

            var response = await plugin.Connection.SendRequestAndReceiveResponseAsync<InitializeRequest, InitializeResponse>(
                MessageMethod.Initialize,
                payload,
                cancellationToken);

            if (response != null && response.ResponseCode != MessageResponseCode.Success)
            {
                throw new PluginException(Strings.Plugin_InitializationFailed);
            }

            plugin.Connection.Options.SetRequestTimeout(requestTimeout);
        }

        private sealed class NoOpPlugin : IPlugin
        {
            internal static readonly NoOpPlugin Instance = new NoOpPlugin();

            private static string UniqueName = Guid.NewGuid().ToString();

            public IConnection Connection => throw new NotImplementedException();

            public string FilePath => UniqueName;

            public string Id => UniqueName;

            public string Name => UniqueName;

            public event EventHandler BeforeClose
            {
                add
                {
                }
                remove
                {
                }
            }

            public event EventHandler Closed
            {
                add
                {
                }
                remove
                {
                }
            }

            public void Close()
            {
                // do nothing
            }

            public void Dispose()
            {
                // do nothing
            }
        }

        private sealed class NoOpIPluginMulticlientUtilities : IPluginMulticlientUtilities
        {

            internal static readonly NoOpIPluginMulticlientUtilities Instance = new NoOpIPluginMulticlientUtilities();

            public Task DoOncePerPluginLifetimeAsync(string key, Func<Task> taskFunc, CancellationToken cancellationToken)
            {
                // do nothing
                return Task.CompletedTask;
            }
        }
        private sealed class PluginCacheContext
        {
            public TimeSpan MaxAge { get; set; } = TimeSpan.FromDays(30);
            public string RootFolder { get; }
            public string CacheFileName { get; }

            public PluginCacheContext(string rootCacheFolder, string pluginFilePath)
            {
                RootFolder = rootCacheFolder;
                CacheFileName = Path.Combine(rootCacheFolder, ComputeHash(pluginFilePath) + ".dat");
            }
        }

        private sealed class PluginCacheResult
        {
            private const int BufferSize = 8192;

            public PluginCacheResult(string rootFolder, string cacheFileName)
            {
                RootFolder = rootFolder;
                CacheFileName = cacheFileName;
                NewCacheFileName = cacheFileName + "-new";
            }

            internal string RootFolder { get; }
            internal string CacheFileName { get; }
            internal string NewCacheFileName { get; }

            private Dictionary<string, IList<OperationClaim>> _operationClaims;

            internal void ProcessContent(Stream content)
            {
                var serializer = new JsonSerializer();
                using (var sr = new StreamReader(content))
                using (var jsonTextReader = new JsonTextReader(sr))
                {
                    _operationClaims = serializer.Deserialize<Dictionary<string, IList<OperationClaim>>>(jsonTextReader);
                }
            }

            internal IList<OperationClaim> GetOperationClaims(PluginRequestKey requestKey)
            {
                IList<OperationClaim> claims = null;
                _operationClaims?.TryGetValue(requestKey.PackageSourceRepository, out claims);
                return claims;
            }

            internal void AddOrUpdateOperationClaims(PluginRequestKey requestKey, IList<OperationClaim> operationClaims)
            {
                if (_operationClaims == null)
                {
                    _operationClaims = new Dictionary<string, IList<OperationClaim>>();
                }
                _operationClaims[requestKey.PackageSourceRepository] = operationClaims;
            }

            internal async Task UpdateCacheFileAsync()
            {
                // Make sure the cache file directory is created before writing a file to it.
                DirectoryUtility.CreateSharedDirectory(RootFolder);

                // The update of a cached file is divided into two steps:
                // 1) Delete the old file.
                // 2) Create a new file with the same name.
                using (var fileStream = new FileStream(
                    NewCacheFileName,
                    FileMode.Create,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    BufferSize,
                    useAsync: true))
                {
                    var json = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(_operationClaims, Formatting.Indented));
                    await fileStream.WriteAsync(json, 0, json.Length);
                    //fileStream.FlushAsync();
                }

                if (File.Exists(CacheFileName))
                {
                    // Process B can perform deletion on an opened file if the file is opened by process A
                    // with FileShare.Delete flag. However, the file won't be actually deleted until A close it.
                    // This special feature can cause race condition, so we never delete an opened file.
                    if (!IsFileAlreadyOpen(CacheFileName))
                    {
                        File.Delete(CacheFileName);
                    }
                }

                // If the destination file doesn't exist, we can safely perform moving operation.
                // Otherwise, moving operation will fail.
                if (!File.Exists(CacheFileName))
                {
                    File.Move(
                        NewCacheFileName,
                        CacheFileName);
                }
            }

            private static bool IsFileAlreadyOpen(string filePath)
            {
                FileStream stream = null;

                try
                {
                    stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                }
                catch
                {
                    return true;
                }
                finally
                {
                    if (stream != null)
                    {
                        stream.Dispose();
                    }
                }

                return false;
            }
        }

        private sealed class PluginRequestKey : IEquatable<PluginRequestKey>
        {
            internal string PluginFilePath { get; }
            internal string PackageSourceRepository { get; }

            internal PluginRequestKey(string pluginFilePath, string packageSourceRepository)
            {
                PluginFilePath = pluginFilePath;
                PackageSourceRepository = packageSourceRepository;
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as PluginRequestKey);
            }

            public override int GetHashCode()
            {
                return HashCodeCombiner.GetHashCode(PluginFilePath, PackageSourceRepository);
            }

            public bool Equals(PluginRequestKey other)
            {
                if (ReferenceEquals(this, other))
                {
                    return true;
                }

                if (ReferenceEquals(null, other))
                {
                    return false;
                }

                return PathUtility.GetStringComparerBasedOnOS().Equals(PluginFilePath, other.PluginFilePath)
                    && string.Equals(
                        PackageSourceRepository,
                        other.PackageSourceRepository,
                        StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}