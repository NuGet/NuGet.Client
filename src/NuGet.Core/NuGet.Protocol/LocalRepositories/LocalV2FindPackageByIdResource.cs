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
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Protocol
{
    public class LocalV2FindPackageByIdResource : FindPackageByIdResource
    {
        private readonly ConcurrentDictionary<string, IReadOnlyList<LocalPackageInfo>> _packageInfoCache
            = new ConcurrentDictionary<string, IReadOnlyList<LocalPackageInfo>>(StringComparer.OrdinalIgnoreCase);

        private readonly string _source;

        public LocalV2FindPackageByIdResource(PackageSource packageSource)
        {
            var rootDirInfo = LocalFolderUtility.GetAndVerifyRootDirectory(packageSource.Source);

            _source = rootDirInfo.FullName;
        }

        public override Task<IEnumerable<NuGetVersion>> GetAllVersionsAsync(
            string id,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken token)
        {
            var infos = GetPackageInfos(id, cacheContext, logger);
            return Task.FromResult(infos.Select(p => p.Identity.Version));
        }

        public override async Task<bool> CopyNupkgToStreamAsync(
            string id,
            NuGetVersion version,
            Stream destination,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken token)
        {
            var info = GetPackageInfo(id, version, cacheContext, logger);

            if (info != null)
            {
                using (var fileStream = File.OpenRead(info.Path))
                {
                    await fileStream.CopyToAsync(destination, token);
                    return true;
                }
            }

            return false;
        }

        public override Task<FindPackageByIdDependencyInfo> GetDependencyInfoAsync(
            string id,
            NuGetVersion version,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken token)
        {
            FindPackageByIdDependencyInfo dependencyInfo = null;
            var info = GetPackageInfo(id, version, cacheContext, logger);
            if (info != null)
            {
                dependencyInfo = GetDependencyInfo(info.Nuspec);
            }

            return Task.FromResult(dependencyInfo);
        }

        private LocalPackageInfo GetPackageInfo(string id, NuGetVersion version, SourceCacheContext cacheContext, ILogger logger)
        {
            return GetPackageInfos(id, cacheContext, logger).FirstOrDefault(package => package.Identity.Version == version);
        }

        private IReadOnlyList<LocalPackageInfo> GetPackageInfos(string id, SourceCacheContext cacheContext, ILogger logger)
        {
            IReadOnlyList<LocalPackageInfo> results = null;

            Func<string, IReadOnlyList<LocalPackageInfo>> findPackages = (packageId) => GetPackageInfosCore(packageId, logger);

            if (cacheContext.RefreshMemoryCache)
            {
                results = _packageInfoCache.AddOrUpdate(id, findPackages, (k, v) => findPackages(k));
            }
            else
            {
                results = _packageInfoCache.GetOrAdd(id, findPackages);
            }

            return results;
        }

        private IReadOnlyList<LocalPackageInfo> GetPackageInfosCore(string id, ILogger logger)
        {
            var result = new List<LocalPackageInfo>();

            // packages\{packageId}.{version}.nupkg
            var nupkgFiles = LocalFolderUtility.GetNupkgsFromFlatFolder(_source, logger)
                .Where(path => LocalFolderUtility.IsPossiblePackageMatch(path, id));

            foreach (var nupkgInfo in nupkgFiles)
            {
                using (var stream = nupkgInfo.OpenRead())
                using (var packageReader = new PackageArchiveReader(stream))
                {
                    NuspecReader reader;
                    try
                    {
                        reader = new NuspecReader(packageReader.GetNuspec());
                    }
                    catch (XmlException ex)
                    {
                        var message = string.Format(CultureInfo.CurrentCulture, Strings.Protocol_PackageMetadataError, nupkgInfo.Name, _source);

                        throw new FatalProtocolException(message, ex);
                    }
                    catch (PackagingException ex)
                    {
                        var message = string.Format(CultureInfo.CurrentCulture, Strings.Protocol_PackageMetadataError, nupkgInfo.Name, _source);

                        throw new FatalProtocolException(message, ex);
                    }

                    var identity = reader.GetIdentity();

                    if (string.Equals(identity.Id, id, StringComparison.OrdinalIgnoreCase))
                    {
                        var cachePackage = new LocalPackageInfo(
                            identity,
                            nupkgInfo.FullName,
                            nupkgInfo.LastWriteTimeUtc,
                            new Lazy<NuspecReader>(() => reader),
                            new Func<PackageReaderBase>(() => new PackageArchiveReader(nupkgInfo.FullName))
                        );

                        result.Add(cachePackage);
                    }
                }
            }

            return result;
        }
    }
}
