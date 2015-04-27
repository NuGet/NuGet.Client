using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Protocol.Core.v3.LocalRepositories
{
    public class LocalV3FindPackageByIdResource : FindPackageByIdResource
    {
        private readonly object _lock = new object();
        private readonly Dictionary<string, List<PackageInfo>> _cache = new Dictionary<string, List<PackageInfo>>(StringComparer.Ordinal);
        private readonly string _source;

        public LocalV3FindPackageByIdResource(PackageSource source)
        {
            _source = source.Source;
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
                result = File.OpenRead(Path.Combine(info.Path, $"{id}.{version}.nupkg"));
            }

            return Task.FromResult(result);
        }

        public override Task<NuspecReader> GetNuspecReaderAsync(string id, NuGetVersion version, CancellationToken token)
        {
            var info = GetPackageInfo(id, version);
            NuspecReader nuspecReader = null;
            if (info != null)
            {
                var nuspecPath = Path.Combine(info.Path, $"{id}.nuspec");
                using (var stream = File.OpenRead(nuspecPath))
                {
                    nuspecReader = new NuspecReader(stream);
                }
            }

            return Task.FromResult(nuspecReader);
        }

        private PackageInfo GetPackageInfo(string id, NuGetVersion version)
        {
            return GetPackageInfos(id).FirstOrDefault(package => package.NuGetVersion == version);
        }

        private List<PackageInfo> GetPackageInfos(string id)
        {
            List<PackageInfo> packages;

            lock (_lock)
            {
                if (_cache.TryGetValue(id, out packages))
                {
                    return packages;
                }
            }

            packages = new List<PackageInfo>();
            var idDir = new DirectoryInfo(Path.Combine(_source, id));

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
                        continue;
                    }

                    if (!versionDir.GetFiles("*.nupkg.sha512").Any())
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

            lock (_lock)
            {
                _cache[id] = packages;
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
