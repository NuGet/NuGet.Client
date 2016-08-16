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

namespace NuGet.Protocol
{
    public class LocalV3FindPackageByIdResource : FindPackageByIdResource
    {
        // Use cache insensitive compare for windows
        private readonly ConcurrentDictionary<string, List<NuGetVersion>> _cache
            = new ConcurrentDictionary<string, List<NuGetVersion>>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<PackageIdentity, PackageIdentity> _packageIdentityCache
            = new ConcurrentDictionary<PackageIdentity, PackageIdentity>();

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

        public override Task<IEnumerable<NuGetVersion>> GetAllVersionsAsync(string id, CancellationToken token)
        {
            return Task.FromResult(GetVersions(id).AsEnumerable());
        }

        public override async Task<bool> CopyNupkgToStreamAsync(
            string id,
            NuGetVersion version,
            Stream destination,
            CancellationToken token)
        {
            var matchedVersion = GetVersion(id, version);

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

        public override Task<PackageIdentity> GetOriginalIdentityAsync(string id, NuGetVersion version, CancellationToken token)
        {
            var matchedVersion = GetVersion(id, version);
            PackageIdentity outputIdentity = null;
            if (matchedVersion != null)
            {
                outputIdentity = _packageIdentityCache.GetOrAdd(
                   new PackageIdentity(id, matchedVersion),
                   inputIdentity =>
                   {
                       return ProcessNuspecReader(
                           inputIdentity.Id,
                           inputIdentity.Version,
                           nuspecReader => nuspecReader.GetIdentity());
                   });
            }

            return Task.FromResult(outputIdentity);
        }

        public override Task<FindPackageByIdDependencyInfo> GetDependencyInfoAsync(string id, NuGetVersion version, CancellationToken token)
        {
            var matchedVersion = GetVersion(id, version);
            FindPackageByIdDependencyInfo dependencyInfo = null;
            if (matchedVersion != null)
            {
                dependencyInfo = ProcessNuspecReader(
                    id,
                    matchedVersion,
                    nuspecReader =>
                    {
                        // Populate the package identity cache while we have the .nuspec open.
                        var identity = nuspecReader.GetIdentity();
                        _packageIdentityCache.TryAdd(identity, identity);

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

        private NuGetVersion GetVersion(string id, NuGetVersion version)
        {
            return GetVersions(id).FirstOrDefault(v => v == version);
        }

        private List<NuGetVersion> GetVersions(string id)
        {
            return _cache.GetOrAdd(id, keyId => GetVersionsCore(keyId));
        }

        private List<NuGetVersion> GetVersionsCore(string id)
        {
            var versions = new List<NuGetVersion>();
            var idDir = new DirectoryInfo(_resolver.GetVersionListPath(id));

            if (!Directory.Exists(_source))
            {
                var message = string.Format(CultureInfo.CurrentCulture, Strings.Log_FailedToRetrievePackage, _source);

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
                        Logger.LogWarning(string.Format(
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
