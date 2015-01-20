using NuGet.Client;
using NuGet.PackagingCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

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
        public static async Task<bool> GetPackageStream(SourceRepository sourceRepository, PackageIdentity packageIdentity, Stream targetPackageStream)
        {
            // TODO: Tie up machine cache with CacheClient?!

            try
            {
                // Step-1 : Get the download stream for packageIdentity
                Stream downloadStream = await GetDownloadStream(sourceRepository, packageIdentity);

                // Step-2: Copy download stream to targetPackageStream
                await downloadStream.CopyToAsync(targetPackageStream);
                return true;
            }
            catch (Exception)
            {
                return false;
            } 
        }

        private static async Task<Stream> GetDownloadStream(SourceRepository sourceRepository, PackageIdentity packageIdentity)
        {
            Stream downloadStream = null;
            DownloadResource downloadResource = await sourceRepository.GetResourceAsync<DownloadResource>();
            if(downloadResource != null)
            {
                downloadStream = await downloadResource.GetStream(packageIdentity, CancellationToken.None);
                return downloadStream;
            }

            return null;
        }
    }
}
