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
    public class LocalV3FindPackageByIdResource : FindPackageByIdResource
    {
        // Use cache insensitive compare for windows
        private readonly ConcurrentDictionary<string, List<NuGetVersion>> _cache
            = new ConcurrentDictionary<string, List<NuGetVersion>>(StringComparer.OrdinalIgnoreCase);

        private readonly string _source;
        private readonly VersionFolderPathResolver _resolver;

        public LocalV3FindPackageByIdResource(PackageSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var rootDirInfo = LocalFolderUtility.GetAndVerifyRootDirectory(source.Source);

            _source = rootDirInfo.FullName;
            _resolver = new VersionFolderPathResolver(_source);
        }

        public override Task<IEnumerable<NuGetVersion>> GetAllVersionsAsync(
            string id,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken token)
        {
            return Task.FromResult(GetVersions(id, cacheContext, logger).AsEnumerable());
        }

        public override async Task<bool> CopyNupkgToStreamAsync(
            string id,
            NuGetVersion version,
            Stream destination,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken token)
        {
            var matchedVersion = GetVersion(id, version, cacheContext, logger);

            if (matchedVersion != null)
            {
                var packagePath = _resolver.GetPackageFilePath(id, matchedVersion);

                using (var fileStream = File.OpenRead(packagePath))
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
            var matchedVersion = GetVersion(id, version, cacheContext, logger);
            FindPackageByIdDependencyInfo dependencyInfo = null;
            if (matchedVersion != null)
            {
                var identity = new PackageIdentity(id, matchedVersion);

                dependencyInfo = ProcessNuspecReader(
                                                id,
                                                matchedVersion,
                                                nuspecReader =>
                                                {
                                                    return GetDependencyInfo(nuspecReader);
                                                });
            }

            return Task.FromResult(dependencyInfo);
        }

        private T ProcessNuspecReader<T>(string id, NuGetVersion version, Func<NuspecReader, T> process)
        {
            var nuspecPath = _resolver.GetManifestFilePath(id, version);
            using (var stream = File.OpenRead(nuspecPath))
            {
                NuspecReader nuspecReader;
                try
                {
                    nuspecReader = new NuspecReader(stream);
                }
                catch (XmlException ex)
                {
                    var message = string.Format(CultureInfo.CurrentCulture, Strings.Protocol_PackageMetadataError, id + "." + version, _source);
                    var inner = new PackagingException(message, ex);

                    throw new FatalProtocolException(message, inner);
                }
                catch (PackagingException ex)
                {
                    var message = string.Format(CultureInfo.CurrentCulture, Strings.Protocol_PackageMetadataError, id + "." + version, _source);

                    throw new FatalProtocolException(message, ex);
                }

                return process(nuspecReader);
            }
        }

        private NuGetVersion GetVersion(string id, NuGetVersion version, SourceCacheContext cacheContext, ILogger logger)
        {
            return GetVersions(id, cacheContext, logger).FirstOrDefault(v => v == version);
        }

        private List<NuGetVersion> GetVersions(string id, SourceCacheContext cacheContext, ILogger logger)
        {
            List<NuGetVersion> results = null;

            Func<string, List<NuGetVersion>> findPackages = (keyId) => GetVersionsCore(keyId, logger);

            if (cacheContext.RefreshMemoryCache)
            {
                results = _cache.AddOrUpdate(id, findPackages, (k, v) => findPackages(k));
            }
            else
            {
                results = _cache.GetOrAdd(id, findPackages);
            }

            return results;
        }

        private List<NuGetVersion> GetVersionsCore(string id, ILogger logger)
        {
            var versions = new List<NuGetVersion>();
            var idDir = new DirectoryInfo(_resolver.GetVersionListPath(id));

            if (!Directory.Exists(_source))
            {
                var message = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.Log_FailedToRetrievePackage,
                    id,
                    _source);

                throw new FatalProtocolException(message);
            }

            if (idDir.Exists)
            {
                // packages\{packageId}\{version}\{packageId}.nuspec
                foreach (var versionDir in idDir.EnumerateDirectories())
                {
                    var versionPart = versionDir.Name;

                    // Get the version part and parse it
                    NuGetVersion version;
                    if (!NuGetVersion.TryParse(versionPart, out version))
                    {
                        logger.LogWarning(string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.InvalidVersionFolder,
                            versionDir.FullName));

                        continue;
                    }

                    var hashPath = _resolver.GetHashPath(id, version);

                    if (!File.Exists(hashPath))
                    {
                        // Writing the marker file is the last operation performed by NuGetPackageUtils.InstallFromStream. We'll use the
                        // presence of the file to denote the package was successfully installed.
                        continue;
                    }

                    versions.Add(version);
                }
            }

            return versions;
        }
    }
}
