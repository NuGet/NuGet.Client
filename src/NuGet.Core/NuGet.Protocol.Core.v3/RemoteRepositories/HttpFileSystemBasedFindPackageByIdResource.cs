// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Logging;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Protocol.Core.v3.RemoteRepositories
{
    /// <summary>
    /// A <see cref="FindPackageByIdResource" /> for a Http-based file system where files are laid out in the
    /// format
    /// /root/
    /// PackageA/
    /// Version0/
    /// PackageA.nuspec
    /// PackageA.Version0.nupkg
    /// and are accessible via HTTP Gets.
    /// </summary>
    public class HttpFileSystemBasedFindPackageByIdResource : FindPackageByIdResource
    {
        private const int MaxRetries = 3;
        private readonly HttpSource _httpSource;
        private readonly Dictionary<string, Task<IEnumerable<PackageInfo>>> _packageInfoCache = new Dictionary<string, Task<IEnumerable<PackageInfo>>>();
        private readonly Dictionary<string, Task<NupkgEntry>> _nupkgCache = new Dictionary<string, Task<NupkgEntry>>();
        private readonly IReadOnlyList<Uri> _baseUris;
        private bool _ignored;

        public HttpFileSystemBasedFindPackageByIdResource(IReadOnlyList<Uri> baseUris, Func<Task<HttpHandlerResource>> handlerFactory)
        {
            if (baseUris == null)
            {
                throw new ArgumentNullException(nameof(baseUris));
            }

            if (baseUris.Count < 1)
            {
                throw new ArgumentException(Strings.OneOrMoreUrisMustBeSpecified, nameof(baseUris));
            }

            _baseUris = baseUris
                .Take(MaxRetries)
                .Select(uri => uri.OriginalString.EndsWith("/", StringComparison.Ordinal) ? uri : new Uri(uri.OriginalString + "/"))
                .ToList();
            _httpSource = new HttpSource(_baseUris[0].OriginalString, handlerFactory);
        }

        public override ILogger Logger
        {
            get { return base.Logger; }
            set
            {
                base.Logger = value;
                _httpSource.Logger = value;
            }
        }

        public override SourceCacheContext CacheContext
        {
            get { return base.CacheContext; }
            set { base.CacheContext = value; }
        }

        public bool IgnoreFailure { get; set; }

        public override async Task<IEnumerable<NuGetVersion>> GetAllVersionsAsync(string id, CancellationToken cancellationToken)
        {
            var packageInfos = await EnsurePackagesAsync(id, cancellationToken);
            return packageInfos.Select(p => p.Version);
        }

        public override async Task<FindPackageByIdDependencyInfo> GetDependencyInfoAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            var packageInfos = await EnsurePackagesAsync(id, cancellationToken);
            var packageInfo = packageInfos.FirstOrDefault(p => p.Version == version);
            if (packageInfo == null)
            {
                return null;
            }

            var reader = await PackageUtilities.OpenNuspecFromNupkgAsync(
                packageInfo.Id,
                OpenNupkgStreamAsync(packageInfo, cancellationToken),
                Logger);

            return GetDependencyInfo(reader);
        }

        public override async Task<Stream> GetNupkgStreamAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            var packageInfos = await EnsurePackagesAsync(id, cancellationToken);
            var packageInfo = packageInfos.FirstOrDefault(p => p.Version == version);
            if (packageInfo == null)
            {
                return null;
            }

            return await OpenNupkgStreamAsync(packageInfo, cancellationToken);
        }

        private Task<IEnumerable<PackageInfo>> EnsurePackagesAsync(string id, CancellationToken cancellationToken)
        {
            Task<IEnumerable<PackageInfo>> task;

            lock (_packageInfoCache)
            {
                if (!_packageInfoCache.TryGetValue(id, out task))
                {
                    task = FindPackagesByIdAsync(id, cancellationToken);
                    _packageInfoCache[id] = task;
                }
            }

            return task;
        }

        private async Task<IEnumerable<PackageInfo>> FindPackagesByIdAsync(string id, CancellationToken cancellationToken)
        {
            for (var retry = 0; retry != 3; ++retry)
            {
                if (_ignored)
                {
                    return Enumerable.Empty<PackageInfo>();
                }

                var baseUri = _baseUris[retry % _baseUris.Count].OriginalString;

                try
                {
                    var uri = baseUri + id.ToLowerInvariant() + "/index.json";
                    var results = new List<PackageInfo>();

                    using (var data = await _httpSource.GetAsync(uri,
                        $"list_{id}",
                        CreateCacheContext(CacheContext, retry),
                        ignoreNotFounds: true,
                        cancellationToken: cancellationToken))
                    {
                        if (data.Stream == null)
                        {
                            return Enumerable.Empty<PackageInfo>();
                        }

                        try
                        {
                            JObject doc;
                            using (var reader = new StreamReader(data.Stream))
                            {
                                doc = JObject.Load(new JsonTextReader(reader));
                            }

                            var result = doc["versions"]
                                .Select(x => BuildModel(baseUri, id, x.ToString()))
                                .Where(x => x != null);

                            results.AddRange(result);
                        }
                        catch
                        {
                            Logger.LogInformation(Strings.FormatLog_FileIsCorrupt(data.CacheFileName));
                            throw;
                        }
                    }

                    return results;
                }
                catch (Exception ex) when (retry < 2)
                {
                    var message = Strings.FormatLog_RetryingFindPackagesById(nameof(FindPackagesByIdAsync), baseUri) + Environment.NewLine + ex.Message;
                    Logger.LogInformation(message);
                }
                catch (Exception ex) when (retry == 2)
                {
                    // Fail silently by returning empty result list
                    var message = Strings.FormatLog_FailedToRetrievePackage(baseUri);
                    if (IgnoreFailure)
                    {
                        _ignored = true;
                        Logger.LogWarning(message);
                        return Enumerable.Empty<PackageInfo>();
                    }

                    Logger.LogError(message + Environment.NewLine + ex.Message);

                    throw new NuGetProtocolException(message, ex);
                }
            }

            return null;
        }

        private PackageInfo BuildModel(string baseUri, string id, string version)
        {
            var parsedVersion = NuGetVersion.Parse(version);
            var normalizedVersionString = parsedVersion.ToNormalizedString();
            return new PackageInfo
                {
                    // If 'Id' element exist, use its value as accurate package Id
                    // Otherwise, use the value of 'title' if it exist
                    // Use the given Id as final fallback if all elements above don't exist
                    Id = id,
                    Version = parsedVersion,
                    ContentUri = baseUri + id.ToLowerInvariant() + "/" + normalizedVersionString + "/" + id.ToLowerInvariant() + "." + normalizedVersionString + ".nupkg",
                };
        }

        private async Task<Stream> OpenNupkgStreamAsync(PackageInfo package, CancellationToken cancellationToken)
        {
            Task<NupkgEntry> task;
            lock (_nupkgCache)
            {
                if (!_nupkgCache.TryGetValue(package.ContentUri, out task))
                {
                    task = _nupkgCache[package.ContentUri] = OpenNupkgStreamAsyncCore(package, cancellationToken);
                }
            }

            var result = await task;
            if (result == null)
            {
                return null;
            }

            // Acquire the lock on a file before we open it to prevent this process
            // from opening a file deleted by the logic in HttpSource.GetAsync() in another process
            return await ConcurrencyUtilities.ExecuteWithFileLocked(result.TempFileName,
                action: token =>
                {
                    return Task.FromResult(
                        new FileStream(result.TempFileName, FileMode.Open, FileAccess.Read,
                            FileShare.ReadWrite | FileShare.Delete));
                },
                token: cancellationToken);
        }

        private async Task<NupkgEntry> OpenNupkgStreamAsyncCore(PackageInfo package, CancellationToken cancellationToken)
        {
            for (var retry = 0; retry != 3; ++retry)
            {
                try
                {
                    using (var data = await _httpSource.GetAsync(
                        package.ContentUri,
                        "nupkg_" + package.Id + "." + package.Version.ToNormalizedString(),
                        CreateCacheContext(CacheContext, retry),
                        cancellationToken))
                    {
                        return new NupkgEntry
                            {
                                TempFileName = data.CacheFileName
                            };
                    }
                }
                catch (Exception ex) when (retry < 2)
                {
                    var message = Strings.FormatLog_FailedToDownloadPackage(package.ContentUri) + Environment.NewLine + ex.Message;
                    Logger.LogInformation(message);
                }
                catch (Exception ex) when (retry == 2)
                {
                    var message = Strings.FormatLog_FailedToDownloadPackage(package.ContentUri) + Environment.NewLine + ex.Message;
                    Logger.LogError(message);
                }
            }

            return null;
        }

        private class NupkgEntry
        {
            public string TempFileName { get; set; }
        }

        private class PackageInfo
        {
            public string Id { get; set; }

            public string Path { get; set; }

            public string ContentUri { get; set; }

            public NuGetVersion Version { get; set; }
        }
    }
}
