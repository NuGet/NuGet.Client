// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Logging;
using NuGet.Packaging;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Protocol.Core.v3.RemoteRepositories
{
    public class RemoteV2FindPackageByIdResourcce : FindPackageByIdResource
    {
        private static readonly XName _xnameEntry = XName.Get("entry", "http://www.w3.org/2005/Atom");
        private static readonly XName _xnameTitle = XName.Get("title", "http://www.w3.org/2005/Atom");
        private static readonly XName _xnameContent = XName.Get("content", "http://www.w3.org/2005/Atom");
        private static readonly XName _xnameLink = XName.Get("link", "http://www.w3.org/2005/Atom");
        private static readonly XName _xnameProperties = XName.Get("properties", "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata");
        private static readonly XName _xnameId = XName.Get("Id", "http://schemas.microsoft.com/ado/2007/08/dataservices");
        private static readonly XName _xnameVersion = XName.Get("Version", "http://schemas.microsoft.com/ado/2007/08/dataservices");
        private static readonly XName _xnamePublish = XName.Get("Published", "http://schemas.microsoft.com/ado/2007/08/dataservices");

        // An unlisted package's publish time must be 1900-01-01T00:00:00.
        private static readonly DateTime _unlistedPublishedTime = new DateTime(1900, 1, 1, 0, 0, 0);

        private readonly string _baseUri;
        private readonly HttpSource _httpSource;
        private readonly Dictionary<string, Task<IEnumerable<PackageInfo>>> _packageVersionsCache = new Dictionary<string, Task<IEnumerable<PackageInfo>>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Task<NupkgEntry>> _nupkgCache = new Dictionary<string, Task<NupkgEntry>>(StringComparer.OrdinalIgnoreCase);
        private bool _ignored;

        private TimeSpan _cacheAgeLimitList;
        private TimeSpan _cacheAgeLimitNupkg;

        public RemoteV2FindPackageByIdResourcce(PackageSource packageSource)
        {
            _baseUri = packageSource.Source.EndsWith("/") ? packageSource.Source : (packageSource.Source + "/");
            _httpSource = new HttpSource(new Uri(_baseUri), packageSource.UserName, packageSource.Password);

            PackageSource = packageSource;
        }

        public PackageSource PackageSource { get; }

        public override ILogger Logger
        {
            get { return base.Logger; }
            set
            {
                base.Logger = value;
                _httpSource.Logger = value;
            }
        }

        public override bool NoCache
        {
            get { return base.NoCache; }
            set
            {
                base.NoCache = value;
                if (value)
                {
                    _cacheAgeLimitList = TimeSpan.Zero;
                    _cacheAgeLimitNupkg = TimeSpan.Zero;
                }
                else
                {
                    _cacheAgeLimitList = TimeSpan.FromMinutes(30);
                    _cacheAgeLimitNupkg = TimeSpan.FromHours(24);
                }
            }
        }

        public bool IgnoreFailure { get; set; }

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
                Logger.LogWarning($"Unable to find package {id}{version}".Yellow());
                return null;
            }

            using (var stream = await PackageUtilities.OpenNuspecStreamFromNupkgAsync(
                packageInfo.Id,
                OpenNupkgStreamAsync(packageInfo, cancellationToken),
                Logger))
            {
                return GetDependencyInfo(new NuspecReader(stream));
            }
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

            lock (_packageVersionsCache)
            {
                if (!_packageVersionsCache.TryGetValue(id, out task))
                {
                    task = FindPackagesByIdAsyncCore(id, cancellationToken);
                    _packageVersionsCache[id] = task;
                }
            }

            return task;
        }

        private async Task<IEnumerable<PackageInfo>> FindPackagesByIdAsyncCore(string id, CancellationToken cancellationToken)
        {
            for (var retry = 0; retry != 3; ++retry)
            {
                if (_ignored)
                {
                    return new List<PackageInfo>();
                }

                try
                {
                    var uri = _baseUri + "FindPackagesById()?Id='" + id + "'";
                    var results = new List<PackageInfo>();
                    var page = 1;
                    while (true)
                    {
                        // TODO: Pages for a package Id are cached separately.
                        // So we will get inaccurate data when a page shrinks.
                        // However, (1) In most cases the pages grow rather than shrink;
                        // (2) cache for pages is valid for only 30 min.
                        // So we decide to leave current logic and observe.
                        using (var data = await _httpSource.GetAsync(uri, $"list_{id}_page{page}", retry == 0 ? _cacheAgeLimitList : TimeSpan.Zero, cancellationToken))
                        {
                            try
                            {
                                var doc = XDocument.Load(data.Stream);

                                var result = doc.Root
                                    .Elements(_xnameEntry)
                                    .Select(x => BuildModel(id, x))
                                    .Where(x => x != null);

                                results.AddRange(result);

                                // Example of what this looks like in the odata feed:
                                // <link rel="next" href="{nextLink}" />
                                var nextUri = (from e in doc.Root.Elements(_xnameLink)
                                    let attr = e.Attribute("rel")
                                    where attr != null && string.Equals(attr.Value, "next", StringComparison.OrdinalIgnoreCase)
                                    select e.Attribute("href")
                                    into nextLink
                                    where nextLink != null
                                    select nextLink.Value).FirstOrDefault();

                                // Stop if there's nothing else to GET
                                if (string.IsNullOrEmpty(nextUri))
                                {
                                    break;
                                }

                                uri = nextUri;
                                page++;
                            }
                            catch (XmlException)
                            {
                                Logger.LogInformation($"The XML file {data.CacheFileName.Yellow().Bold()} is corrupt.");
                                throw;
                            }
                        }
                    }

                    return results;
                }
                catch (Exception ex)
                {
                    if (retry == 2)
                    {
                        // Fail silently by returning empty result list
                        if (IgnoreFailure)
                        {
                            _ignored = true;
                            Logger.LogInformation(
                                string.Format("Failed to retrieve information from remote source '{0}'",
                                    _baseUri).Yellow().Bold());
                            return new List<PackageInfo>();
                        }

                        Logger.LogError(string.Format("Error: FindPackagesById: {1}\r\n  {0}",
                            ex.Message, id).Red().Bold());
                        throw;
                    }
                    else
                    {
                        Logger.LogInformation(string.Format("Warning: FindPackagesById: {1}\r\n  {0}", ex.Message, id).Yellow().Bold());
                    }
                }
            }
            return null;
        }

        private static PackageInfo BuildModel(string id, XElement element)
        {
            var properties = element.Element(_xnameProperties);
            var idElement = properties.Element(_xnameId);
            var titleElement = element.Element(_xnameTitle);

            var publishElement = properties.Element(_xnamePublish);
            if (publishElement != null)
            {
                DateTime publishDate;
                if (DateTime.TryParse(publishElement.Value, out publishDate)
                    && (publishDate == _unlistedPublishedTime))
                {
                    return null;
                }
            }

            return new PackageInfo
                {
                    // If 'Id' element exist, use its value as accurate package Id
                    // Otherwise, use the value of 'title' if it exist
                    // Use the given Id as final fallback if all elements above don't exist
                    Id = idElement?.Value ?? titleElement?.Value ?? id,
                    Version = NuGetVersion.Parse(properties.Element(_xnameVersion).Value),
                    ContentUri = element.Element(_xnameContent).Attribute("src").Value,
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
            return await ConcurrencyUtilities.ExecuteWithFileLocked(result.TempFileName, _ =>
                {
                    return Task.FromResult(
                        new FileStream(result.TempFileName, FileMode.Open, FileAccess.Read,
                            FileShare.ReadWrite | FileShare.Delete));
                });
        }

        private async Task<NupkgEntry> OpenNupkgStreamAsyncCore(PackageInfo package, CancellationToken cancellationToken)
        {
            for (var retry = 0; retry != 3; ++retry)
            {
                try
                {
                    using (var data = await _httpSource.GetAsync(
                        package.ContentUri,
                        "nupkg_" + package.Id + "." + package.Version,
                        retry == 0 ? _cacheAgeLimitNupkg : TimeSpan.Zero,
                        cancellationToken))
                    {
                        return new NupkgEntry
                            {
                                TempFileName = data.CacheFileName
                            };
                    }
                }
                catch (Exception ex)
                {
                    if (retry == 2)
                    {
                        Logger.LogError(string.Format("Error: DownloadPackageAsync: {1}\r\n  {0}", ex.Message, package.ContentUri.Red().Bold()));
                    }
                    else
                    {
                        Logger.LogInformation(string.Format("Warning: DownloadPackageAsync: {1}\r\n  {0}".Yellow().Bold(), ex.Message, package.ContentUri.Yellow().Bold()));
                    }
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
