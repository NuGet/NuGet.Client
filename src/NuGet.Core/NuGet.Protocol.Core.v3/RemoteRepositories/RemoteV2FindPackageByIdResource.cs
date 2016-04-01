// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Logging;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Protocol.Core.v3.RemoteRepositories
{
    public class RemoteV2FindPackageByIdResource : FindPackageByIdResource
    {
        private readonly HttpSource _httpSource;
        private readonly Dictionary<string, Task<IReadOnlyList<V2FeedPackageInfo>>> _packageVersionsCache = new Dictionary<string, Task<IReadOnlyList<V2FeedPackageInfo>>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Task<NupkgEntry>> _nupkgCache = new Dictionary<string, Task<NupkgEntry>>(StringComparer.OrdinalIgnoreCase);
        private readonly V2FeedParser _feedParser;

        public RemoteV2FindPackageByIdResource(HttpSourceResource httpSourceResource, PackageSource packageSource)
        {
            if (httpSourceResource == null)
            {
                throw new ArgumentNullException(nameof(httpSourceResource));
            }

            if (packageSource == null)
            {
                throw new ArgumentNullException(nameof(packageSource));
            }

            _feedParser = new V2FeedParser(httpSourceResource.HttpSource, packageSource.Source);
            _httpSource = httpSourceResource.HttpSource;
            PackageSource = packageSource;
        }

        public PackageSource PackageSource { get; }

        public override ILogger Logger
        {
            get { return base.Logger; }
            set
            {
                base.Logger = value;
            }
        }

        public override async Task<IEnumerable<NuGetVersion>> GetAllVersionsAsync(string id, CancellationToken cancellationToken)
        {
            var result = await EnsurePackagesAsync(id, cancellationToken);
            return result.Select(item => item.Version);
        }

        public override async Task<FindPackageByIdDependencyInfo> GetDependencyInfoAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            var packageInfos = await EnsurePackagesAsync(id, cancellationToken);
            var packageInfo = packageInfos.FirstOrDefault(p => p.Version == version);
            if (packageInfo == null)
            {
                Logger.LogWarning($"Unable to find package {id}{version}");
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

        private Task<IReadOnlyList<V2FeedPackageInfo>> EnsurePackagesAsync(string id, CancellationToken cancellationToken)
        {
            Task<IReadOnlyList<V2FeedPackageInfo>> task;

            lock (_packageVersionsCache)
            {
                if (!_packageVersionsCache.TryGetValue(id, out task))
                {
                    task = _feedParser.FindPackagesByIdAsync(id, Logger, "list_{0}_page{1}", CacheContext,  cancellationToken); ;
                    _packageVersionsCache[id] = task;
                }
            }

            return task;
        }

        private async Task<Stream> OpenNupkgStreamAsync(V2FeedPackageInfo package, CancellationToken cancellationToken)
        {
            Task<NupkgEntry> task;
            lock (_nupkgCache)
            {
                if (!_nupkgCache.TryGetValue(package.DownloadUrl, out task))
                {
                    task = _nupkgCache[package.DownloadUrl] = OpenNupkgStreamAsyncCore(package, cancellationToken);
                }
            }

            var result = await task;
            if (result == null)
            {
                return null;
            }

            // Acquire the lock on a file before we open it to prevent this process
            // from opening a file deleted by the logic in HttpSource.GetAsync() in another process
            return await ConcurrencyUtilities.ExecuteWithFileLockedAsync(result.TempFileName,
                action: token =>
                {
                    return Task.FromResult(
                        new FileStream(result.TempFileName, FileMode.Open, FileAccess.Read,
                            FileShare.ReadWrite | FileShare.Delete));
                },
                token: cancellationToken);
        }

        private async Task<NupkgEntry> OpenNupkgStreamAsyncCore(V2FeedPackageInfo package, CancellationToken cancellationToken)
        {
            for (var retry = 0; retry != 3; ++retry)
            {
                try
                {
                    using (var data = await _httpSource.GetAsync(
                        package.DownloadUrl,
                        "nupkg_" + package.Id + "." + package.Version,
                        CreateCacheContext(retry),
                        Logger,
                        ignoreNotFounds: false,
                        ensureValidContents: stream => HttpStreamValidation.ValidateNupkg(package.DownloadUrl, stream),
                        cancellationToken: cancellationToken))
                    {
                        return new NupkgEntry
                        {
                            TempFileName = data.CacheFileName
                        };
                    }
                }
                catch (FatalProtocolException)
                {
                    throw;
                }
                catch (TaskCanceledException) when (retry < 2)
                {
                    // Requests can get cancelled if we got the data from elsewhere, no reason to warn.
                    string message = string.Format(CultureInfo.CurrentCulture, Strings.Log_CanceledNupkgDownload, package.DownloadUrl);
                    Logger.LogMinimal(message);
                }
                catch (Exception ex)
                {
                    var message = string.Format(CultureInfo.CurrentCulture, Strings.Log_FailedToDownloadPackage, package.DownloadUrl)
                        + Environment.NewLine
                        + ExceptionUtilities.DisplayMessage(ex);
                    if (retry == 2)
                    {
                        Logger.LogError(message);
                    }
                    else
                    {
                        Logger.LogMinimal(message);
                    }
                }
            }

            return null;
        }

        private class NupkgEntry
        {
            public string TempFileName { get; set; }
        }

    }
}
