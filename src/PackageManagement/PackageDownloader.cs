using NuGet.Packaging.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement
{
    /// <summary>
    /// Abstracts the logic to get a package stream for a given package identity from a given source repository
    /// </summary>
    public static class PackageDownloader
    {
        /// <summary>
        /// Sets <param name="targetPackageStream"></param> for a given <param name="packageIdentity"></param> 
        /// from the given <param name="sourceRepository"></param>. If successfully set, returns true. Otherwise, false
        /// </summary>
        public static async Task GetPackageStreamAsync(SourceRepository sourceRepository, PackageIdentity packageIdentity, Stream targetPackageStream, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            using (var downloadStream = await GetDownloadStreamAsync(sourceRepository, packageIdentity, token))
            {
                token.ThrowIfCancellationRequested();

                await downloadStream.CopyToAsync(targetPackageStream);
            }
        }

        private static async Task<Stream> GetDownloadStreamAsync(SourceRepository sourceRepository, PackageIdentity packageIdentity, CancellationToken token)
        {
            DownloadResource downloadResource = await sourceRepository.GetResourceAsync<DownloadResource>(token);

            if (downloadResource == null)
            {
                throw new InvalidOperationException("Download resource not found");
            }

            var downloadStream = await downloadResource.GetStream(packageIdentity, token);
            if (downloadStream == null)
            {
                throw new InvalidOperationException("Download stream is null");
            }

            return downloadStream;
        }
    }
}
