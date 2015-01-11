using NuGet.Client;
using NuGet.PackagingCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
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
        /// from one of the given <param name="sourceRepositories"></param>. If successfully set, returns true. Otherwise, false
        /// </summary>
        public static async Task<bool> GetPackageStream(HttpClient httpClient, IEnumerable<SourceRepository> sourceRepositories, PackageIdentity packageIdentity, Stream targetPackageStream)
        {
            // Get the download url for packageIdentity from one of the source repositories
            foreach(var sourceRepo in sourceRepositories)
            {
                if(await GetPackageStream(httpClient, sourceRepo, packageIdentity, targetPackageStream))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Sets <param name="targetPackageStream"></param> for a given <param name="packageIdentity"></param> 
        /// from the given <param name="sourceRepository"></param>. If successfully set, returns true. Otherwise, false
        /// </summary>
        public static async Task<bool> GetPackageStream(HttpClient httpClient, SourceRepository sourceRepository, PackageIdentity packageIdentity, Stream targetPackageStream)
        {
            // TODO: Tie up machine cache with CacheClient?!

            // Step-1 : Get the download url for packageIdentity
            Uri downloadUrl = await GetDownloadUrl(sourceRepository, packageIdentity);

            // Step-2: Download the package using the downloadUrl
            // TODO: Need to check usage here and likely not create CacheHttpClient everytime
            // TODO: Also, need to pass in a HttpMessageHandler/INuGetRequestModifier to set UserAgent
            return await GetPackageStream(httpClient, downloadUrl, targetPackageStream);
        }

        private static async Task<Uri> GetDownloadUrl(SourceRepository sourceRepository, PackageIdentity packageIdentity)
        {
            Uri downloadUrl = null;
            DownloadResource downloadResource = await sourceRepository.GetResourceAsync<DownloadResource>();
            if(downloadResource != null)
            {
                downloadUrl = await downloadResource.GetDownloadUrl(packageIdentity);
            }

            return downloadUrl;
        }

        public static async Task<bool> GetPackageStream(HttpClient httpClient, Uri downloadUrl, Stream targetPackageStream)
        {
            if(downloadUrl == null)
            {
                return false;
            }

            try
            {
                using (var responseStream = await httpClient.GetStreamAsync(downloadUrl))
                {
                    await responseStream.CopyToAsync(targetPackageStream);
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
