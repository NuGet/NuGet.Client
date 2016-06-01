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

namespace NuGet.Protocol.Core.v3.LocalRepositories
{
    public class LocalV3FindPackageByIdResource : FindPackageByIdResource
    {
        // Use cache insensitive compare for windows
        private readonly ConcurrentDictionary<string, List<PackageInfo>> _cache 
            = new ConcurrentDictionary<string, List<PackageInfo>>(
                RuntimeEnvironmentHelper.IsWindows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        private readonly string _source;
        private readonly VersionFolderPathResolver _resolver;

        public LocalV3FindPackageByIdResource(PackageSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            _source = source.Source;
            _resolver = new VersionFolderPathResolver(source.Source);
        }

        public override Task<IEnumerable<NuGetVersion>> GetAllVersionsAsync(string id, CancellationToken token)
        {
            var versions = GetPackageInfos(id).Select(v => v.NuGetVersion);
            return Task.FromResult(versions);
        }

        public override Task<Stream> GetNupkgStreamAsync(string id, NuGetVersion version, CancellationToken token)
        {
            var info = GetPackageInfo(id, version);
            Stream result = null;
            if (info != null)
            {
                var packagePath = Path.Combine(info.Path, $"{id}.{version.ToNormalizedString()}.nupkg");
                result = File.OpenRead(packagePath);
            }

            return Task.FromResult(result);
        }

        public override Task<FindPackageByIdDependencyInfo> GetDependencyInfoAsync(string id, NuGetVersion version, CancellationToken token)
        {
            var info = GetPackageInfo(id, version);
            FindPackageByIdDependencyInfo dependencyInfo = null;
            if (info != null)
            {
                var nuspecPath = Path.Combine(info.Path, $"{id}.nuspec");
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

                    dependencyInfo = GetDependencyInfo(nuspecReader);
                }
            }

            return Task.FromResult(dependencyInfo);
        }

        private PackageInfo GetPackageInfo(string id, NuGetVersion version)
        {
            return GetPackageInfos(id).FirstOrDefault(package => package.NuGetVersion == version);
        }

        private List<PackageInfo> GetPackageInfos(string id)
        {
            return _cache.GetOrAdd(id, (keyId) => GetPackageInfosCore(keyId));
        }

        private List<PackageInfo> GetPackageInfosCore(string id)
        {
            var packages = new List<PackageInfo>();
            var idDir = new DirectoryInfo(Path.Combine(_source, id));

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

                    packages.Add(new PackageInfo
                    {
                        Path = versionDir.FullName,
                        NuGetVersion = version
                    });
                }
            }

            return packages;
        }

        private class PackageInfo
        {
            public string Path { get; set; }

            public NuGetVersion NuGetVersion { get; set; }
        }
    }
}
