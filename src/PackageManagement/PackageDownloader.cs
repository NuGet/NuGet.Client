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
        /// Gets a package stream for a given <param name="packageIdentity"></param> from one of the given <param name="sourceRepositories"></param>
        /// </summary>
        public static async Task<Stream> GetPackageStream(IEnumerable<SourceRepository> sourceRepositories, PackageIdentity packageIdentity)
        {
            // TODO: Tie up machine cache with CacheClient?!

            // Get the download url for packageIdentity from one of the source repositories
            foreach(var sourceRepo in sourceRepositories)
            {
                var packageStream = await GetPackageStream(sourceRepo, packageIdentity);
                if(packageStream != null)
                {
                    return packageStream;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets a package stream for a given <param name="packageIdentity"></param> from a given <param name="sourceRepository"></param>
        /// </summary>
        public static async Task<Stream> GetPackageStream(SourceRepository sourceRepository, PackageIdentity packageIdentity)
        {
            // TODO: Tie up machine cache with CacheClient?!

            // Step-1 : Get the download url for packageIdentity
            Uri downloadUrl = await GetDownloadUrl(sourceRepository, packageIdentity);

            // Step-2: Download the package using the downloadUrl
            // TODO: Need to check usage here and likely not create CacheHttpClient everytime
            // TODO: Also, need to pass in a HttpMessageHandler/INuGetRequestModifier to set UserAgent
            return await GetPackageStream(downloadUrl);
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

        private static async Task<Stream> GetPackageStream(/* HttpClient ,*/ Uri downloadUrl)
        {
            if(downloadUrl == null)
            {
                return null;
            }
            var httpClient = new HttpClient();
            var packageStream = await httpClient.GetStreamAsync(downloadUrl);
            return packageStream;
        }
    }
}
