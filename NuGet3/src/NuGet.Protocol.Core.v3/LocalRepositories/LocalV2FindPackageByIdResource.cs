// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Protocol.Core.v3.LocalRepositories
{
    public class LocalV2FindPackageByIdResource : FindPackageByIdResource
    {
        private readonly ConcurrentDictionary<string, List<CachedPackageInfo>> _packageInfoCache;
        private readonly string _source;

        public LocalV2FindPackageByIdResource(PackageSource packageSource,
            ConcurrentDictionary<string, List<CachedPackageInfo>> packageInfoCache)
        {
            _source = packageSource.Source;
            _packageInfoCache = packageInfoCache;
        }

        public override Task<IEnumerable<NuGetVersion>> GetAllVersionsAsync(string id, CancellationToken token)
        {
            var infos = GetPackageInfos(id);
            return Task.FromResult(infos.Select(p => p.Reader.GetVersion()));
        }

        public override Task<Stream> GetNupkgStreamAsync(string id, NuGetVersion version, CancellationToken token)
        {
            var info = GetPackageInfo(id, version);
            if (info != null)
            {
                return Task.FromResult<Stream>(File.OpenRead(info.Path));
            }

            return Task.FromResult<Stream>(null);
        }

        public override Task<FindPackageByIdDependencyInfo> GetDependencyInfoAsync(string id, NuGetVersion version, CancellationToken token)
        {
            FindPackageByIdDependencyInfo dependencyInfo = null;
            var info = GetPackageInfo(id, version);
            if (info != null)
            {
                dependencyInfo = GetDependencyInfo(info.Reader);
            }

            return Task.FromResult(dependencyInfo);
        }

        private CachedPackageInfo GetPackageInfo(string id, NuGetVersion version)
        {
            return GetPackageInfos(id).FirstOrDefault(package => package.Reader.GetVersion() == version);
        }

        private List<CachedPackageInfo> GetPackageInfos(string id)
        {
            List<CachedPackageInfo> cachedPackageInfos;

            if (_packageInfoCache.TryGetValue(id, out cachedPackageInfos))
            {
                cachedPackageInfos = cachedPackageInfos.ToList();
            }

            var result = new List<CachedPackageInfo>();

            // packages\{packageId}.{version}.nupkg
            foreach (var nupkgInfo in GetNupkgFiles(_source, id))
            {
                var cachedPackageInfo = cachedPackageInfos?.FirstOrDefault(package => string.Equals(package.Path, nupkgInfo.FullName, StringComparison.OrdinalIgnoreCase));
                if (cachedPackageInfo != null
                    && cachedPackageInfo.LastWriteTimeUtc == nupkgInfo.LastWriteTimeUtc)
                {
                    result.Add(cachedPackageInfo);
                }

                using (var stream = nupkgInfo.OpenRead())
                {
                    var packageReader = new PackageReader(stream);
                    NuspecReader reader;
                    try
                    {
                        reader = new NuspecReader(packageReader.GetNuspec());
                    }
                    catch (XmlException ex)
                    {
                        var message = string.Format(CultureInfo.CurrentCulture, Strings.Protocol_PackageMetadataError, nupkgInfo.Name, _source);
                        throw new NuGetProtocolException(message, ex);
                    }
                    catch (PackagingException ex)
                    {
                        var message = string.Format(CultureInfo.CurrentCulture, Strings.Protocol_PackageMetadataError, nupkgInfo.Name, _source);
                        throw new NuGetProtocolException(message, ex);
                    }

                    if (string.Equals(reader.GetId(), id, StringComparison.Ordinal))
                    {
                        result.Add(new CachedPackageInfo { Path = nupkgInfo.FullName, Reader = reader });
                    }
                }
            }

            _packageInfoCache.TryAdd(id, result);

            return result;
        }

        internal static IEnumerable<FileInfo> GetNupkgFiles(string sourceDir, string id)
        {
            // Check for package files one level deep.
            var rootDirectoryInfo = new DirectoryInfo(sourceDir);
            if (!rootDirectoryInfo.Exists)
            {
                yield break;
            }

            var filter = id + "*.nupkg";
            foreach (var dir in rootDirectoryInfo.EnumerateDirectories(filter))
            {
                foreach (var path in dir.EnumerateFiles(filter))
                {
                    yield return path;
                }
            }

            // Check top level directory
            foreach (var path in rootDirectoryInfo.EnumerateFiles(filter))
            {
                yield return path;
            }
        }
    }
}
