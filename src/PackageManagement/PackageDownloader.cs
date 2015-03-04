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
        public static async Task<bool> GetPackageStream(SourceRepository sourceRepository, PackageIdentity packageIdentity, Stream targetPackageStream, CancellationToken token)
        {
            // TODO: Tie up machine cache with CacheClient?!

            try
            {
                token.ThrowIfCancellationRequested();
                // Step-1 : Get the download stream for packageIdentity
                Stream downloadStream = await GetDownloadStream(sourceRepository, packageIdentity, token);

                if(downloadStream == null)
                {
                    return false;
                }

                token.ThrowIfCancellationRequested();
                // Step-2: Copy download stream to targetPackageStream if it is not null
                await downloadStream.CopyToAsync(targetPackageStream);
                return true;
            }
            catch (Exception)
            {
                return false;
            } 
        }

        private static async Task<Stream> GetDownloadStream(SourceRepository sourceRepository, PackageIdentity packageIdentity, CancellationToken token)
        {
            Stream downloadStream = null;
            DownloadResource downloadResource = await sourceRepository.GetResourceAsync<DownloadResource>(token);
            if(downloadResource != null)
            {
                downloadStream = await downloadResource.GetStream(packageIdentity, token);
                return downloadStream;
            }

            return null;
        }
    }
}
