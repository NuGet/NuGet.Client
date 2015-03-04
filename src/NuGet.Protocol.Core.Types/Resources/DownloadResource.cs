using NuGet.Packaging.Core;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Core.Types
{
    /// <summary>
    /// Finds the download url of a nupkg
    /// </summary>
    public abstract class DownloadResource : INuGetResource
    {
        public async Task<Uri> GetDownloadUrl(PackageIdentity identity)
        {
            return await GetDownloadUrl(identity, CancellationToken.None);
        }

        public abstract Task<Uri> GetDownloadUrl(PackageIdentity identity, CancellationToken token);


        public abstract Task<Stream> GetStream(PackageIdentity identity, CancellationToken token);


        public event EventHandler<PackageProgressEventArgs> Progress;
    }
}
