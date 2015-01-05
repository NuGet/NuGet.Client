using NuGet.Client;
using NuGet.Data;
using NuGet.PackagingCore;
using System;
using System.IO;
using System.Threading.Tasks;

namespace NuGet.PackageManagement
{
    /// <summary>
    /// Abstracts the logic to get a package stream for a given package identity from a given source repository
    /// </summary>
    public static class PackageDownloader
    {
        /// <summary>
        /// Gets a package stream for a given <param name="packageIdentity"></param> from a given <param name="sourceRepository"></param>
        /// </summary>
        public static async Task<Stream> GetPackage(SourceRepository sourceRepository, PackageIdentity packageIdentity)
        {
            // TODO: Tie up machine cache with CacheClient?!

            // Step-1 : Get the download url for packageIdentity
            DownloadResource downloadResource = await sourceRepository.GetResource<DownloadResource>();
            Uri downloadUrl = await downloadResource.GetDownloadUrl(packageIdentity);

            // Step-2: Download the package using the downloadUrl
            // TODO: Need to check usage here and likely not create CacheHttpClient everytime
            // TODO: Also, need to pass in a HttpMessageHandler/INuGetRequestModifier to set UserAgent
            var cacheHttpClient = new CacheHttpClient();
            var packageStream = await cacheHttpClient.GetStreamAsync(downloadUrl);
            return packageStream;
        }
    }
}
