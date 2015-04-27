using System;
using System.Collections.Concurrent;
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

        public override Task<NuspecReader> GetNuspecReaderAsync(string id, NuGetVersion version, CancellationToken token)
        {
            var info = GetPackageInfo(id, version);
            if (info != null)
            {
                return Task.FromResult(info.Reader);
            }

            return Task.FromResult<NuspecReader>(null);
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
                if (cachedPackageInfo != null && cachedPackageInfo.LastWriteTimeUtc == nupkgInfo.LastWriteTimeUtc)
                {
                    result.Add(cachedPackageInfo);
                }

                using (var stream = nupkgInfo.OpenRead())
                {
                    var packageReader = new PackageReader(stream);
                    var reader = new NuspecReader(packageReader.GetNuspec());

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
            var filter = id + "*.nupkg";
            foreach (var dir in rootDirectoryInfo.EnumerateDirectories(sourceDir))
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
