using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Logging;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.Protocol.Core.Types
{
    //TODO, consider creating DeleteCommandResource for delete specific resorce.
    public class PushCommandResource : INuGetResource
    {
        private const string ServiceEndpoint = "/api/v2/package";
        private const string ApiKeyHeader = "X-NuGet-ApiKey";

        private HttpSource _httpSource;
        private Uri _source;

        public PushCommandResource(string pushEndpoint,
            HttpSource httpSource)
        {
            PushEndpoint = pushEndpoint;
            if (!string.IsNullOrEmpty(pushEndpoint))
            {
                _source = new Uri(pushEndpoint);
            }
            _httpSource = httpSource;
        }

        public string PushEndpoint { get; private set; }

        /// <summary>
        /// Pushes a package to the Source.
        /// </summary>
        /// <param name="apiKey">API key to be used to push the package.</param>
        /// <param name="pathToPackage">The path of package to be pushed.</param>
        /// <param name="packageSize">The size of package to be pushed.</param>
        /// <param name="logger">The logger</param>
        /// <param name="token">The cancellation token</param>
        public async Task PushPackage(string apiKey,
            string pathToPackage,
            long packageSize,
            ILogger logger,
            CancellationToken token)
        {
            if (_source.IsFile)
            {
                PushPackageToFileSystem(pathToPackage);
            }
            else
            {
                await PushPackageToServer(apiKey, pathToPackage, packageSize, logger, token);
            }
        }

        /// <summary>
        /// Pushes a package to the Http server.
        /// </summary>
        /// <param name="apiKey">API key to be used to push the package.</param>
        /// <param name="pathToPackage">The path of the package to be pushed </param>
        /// <param name="packageSzie">Size of the package to be pushed.</param>
        /// <param name="timeout">Time in milliseconds to timeout the server request.</param>
        /// <param name="logger">The logger</param>
        /// <param name="token">The cancellation token</param>
        private async Task PushPackageToServer(
            string apiKey,
            string pathToPackage,
            long packageSzie,
            ILogger logger,
            CancellationToken token)
        {
            HttpRequestMessage request = null;
            HttpResponseMessage response = null;

            try
            {
                request = CreateRequest(null, pathToPackage, apiKey);
                response = await _httpSource.SendAsync(request,
                    currentRequest => { return request = CreateRequest(currentRequest, pathToPackage, apiKey); },
                    logger,
                    token);
            }
            finally
            {
                if (request != null)
                {
                    request.Dispose();
                }
            };

            response.EnsureSuccessStatusCode();
        }

        private HttpRequestMessage CreateRequest(HttpRequestMessage currentRequest, 
            string pathToPackage,
            string apiKey)
        {
            if (currentRequest != null)
            {
                //this should dispose the content, the file stream underneath, and everything.
                currentRequest.Dispose();
            }
            var fileStream = new FileStream(pathToPackage, FileMode.Open, FileAccess.Read, FileShare.Read);
            var request = new HttpRequestMessage(HttpMethod.Put, GetServiceEndpointUrl(string.Empty));
            var content = new MultipartFormDataContent();
            var packageContent = new StreamContent(fileStream);
            packageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
            content.Add(packageContent, "package", "package.nupkg");
            request.Content = content;
            if (!string.IsNullOrEmpty(apiKey))
            {
                request.Headers.Add(ApiKeyHeader, apiKey);
            }
            return request;
        }

        /// <summary>
        /// Pushes a package to a FileSystem.
        /// </summary>
        /// <param name="pathToPackage">The path of package to be pushed.</param>
        private void PushPackageToFileSystem(string pathToPackage)
        {
            string root = _source.LocalPath;
            PackageArchiveReader reader = new PackageArchiveReader(pathToPackage);
            PackageIdentity packageIdentity = reader.GetIdentity();

            //TODD: support V3 repo style if detect it is
            var pathResolver = new PackagePathResolver(root, useSideBySidePaths: true);
            var packageFileName = pathResolver.GetPackageFileName(packageIdentity);

            string fullPath = Path.Combine(root, packageFileName);
            File.Copy(pathToPackage, fullPath, true);
        }

        /// <summary>
        /// Deletes a package from a Http server.
        /// </summary>
        /// <param name="apiKey">API key to be used to delete the package.</param>
        /// <param name="packageId">The package Id.</param>
        /// <param name="packageVersion">The package version.</param>
        /// <param name="logger">The logger</param>
        /// <param name="token">The cancellation token</param>
        public async Task DeletePackage(string apiKey,
            string packageId,
            string packageVersion,
            ILogger logger,
            CancellationToken token)
        {
            var sourceUri = GetServiceEndpointUrl(string.Empty);
            if (sourceUri.IsFile)
            {
                DeletePackageFromFileSystem(packageId, packageVersion, logger);
            }
            else
            {
                await DeletePackageFromServer(apiKey, packageId, packageVersion, logger, token);
            }
        }

        /// <summary>
        /// Deletes a package from a Http server
        /// </summary>
        /// <param name="apiKey">API key to be used to delete the package.</param>
        /// <param name="packageId">The package Id.</param>
        /// <param name="packageVersion">The package Id.</param>
        /// <param name="logger">The logger</param>
        /// <param name="token">The cancellation token</param>
        private async Task DeletePackageFromServer(string apiKey,
            string packageId,
            string packageVersion,
            ILogger logger,
            CancellationToken token)
        {
            // Review: Do these values need to be encoded in any way?
            var url = String.Join("/", packageId, packageVersion);
            using (var request = new HttpRequestMessage(HttpMethod.Delete, GetServiceEndpointUrl(url)))
            {
                if (!string.IsNullOrEmpty(apiKey))
                {
                    request.Headers.Add(ApiKeyHeader, apiKey);
                }
                var response = await  _httpSource.SendAsync(request, null, logger, token);
                response.EnsureSuccessStatusCode();
            }
        }

        /// <summary>
        /// Deletes a package from a FileSystem.
        /// </summary>
        /// <param name="packageId">The package Id.</param>
        /// <param name="packageVersion">The package Id.</param>
        /// <param name="logger">The logger</param>
        private void DeletePackageFromFileSystem(string packageId, string packageVersion, ILogger logger)
        {
            string root = _source.LocalPath;
            var resolver = new PackagePathResolver(_source.AbsolutePath, useSideBySidePaths: true);
            resolver.GetPackageFileName(new Packaging.Core.PackageIdentity(packageId, new NuGetVersion(packageVersion)));
            var packageIdentity = new PackageIdentity(packageId, new NuGetVersion(packageVersion));
            var packageFileName = resolver.GetPackageFileName(packageIdentity);

            var fullPath = Path.Combine(root, packageFileName);
            MakeFileWritable(fullPath);
            File.Delete(fullPath);
        }

        /// <summary>
        /// Remove the read-only flag.
        /// </summary>
        /// <param name="fullPath">The file path</param>
        private void MakeFileWritable(string fullPath)
        {
            FileAttributes attributes = File.GetAttributes(fullPath);
            if (attributes.HasFlag(FileAttributes.ReadOnly))
            {
                File.SetAttributes(fullPath, attributes & ~FileAttributes.ReadOnly);
            }
        }

        /// <summary>
        /// Calculates the URL to the package
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private Uri GetServiceEndpointUrl(string path)
        {
            var baseUri = EnsureTrailingSlash(PushEndpoint);
            Uri requestUri;
            if (String.IsNullOrEmpty(baseUri.AbsolutePath.TrimStart('/')))
            {
                // If there's no host portion specified, append the url to the client.
                requestUri = new Uri(baseUri, ServiceEndpoint + '/' + path);
            }
            else
            {
                requestUri = new Uri(baseUri, path);
            }
            return requestUri;
        }

        /// <summary>
        /// Ensure a trailing slash at the end
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private static Uri EnsureTrailingSlash(string value)
        {
            if (!value.EndsWith("/", StringComparison.OrdinalIgnoreCase))
            {
                value += "/";
            }

            return new Uri(value);
        }
    }
}
