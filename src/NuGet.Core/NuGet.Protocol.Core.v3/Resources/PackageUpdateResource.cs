using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Logging;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using System.Collections.Generic;
using System.Linq;
using NuGet.Protocol.Core.v3;
using System.Globalization;
using NuGet.Common;

namespace NuGet.Protocol.Core.Types
{
    /// <summary>
    /// Contains logics to push or delete packages in Http server or file system
    /// </summary>
    public class PackageUpdateResource : INuGetResource
    {
        private const string ServiceEndpoint = "/api/v2/package";
        private const string ApiKeyHeader = "X-NuGet-ApiKey";

        private HttpSource _httpSource;
        private string _source;

        public PackageUpdateResource(string source,
            HttpSource httpSource)
        {
            _source = source;
            _httpSource = httpSource;
        }

        public async Task Push(string packagePath,
            int timeoutInSecond,
            Func<string, string> getApiKey,
            ILogger log)
        {
            using (var tokenSource = new CancellationTokenSource())
            {
                if (timeoutInSecond > 0)
                {
                    tokenSource.CancelAfter(TimeSpan.FromSeconds(timeoutInSecond));
                }

                var apiKey = getApiKey(_source);

                await PushPackage(packagePath, _source, apiKey, log, tokenSource.Token);

                if (!IsFileSource())
                {
                    await PushSymbols(packagePath, apiKey, log, tokenSource.Token);
                }
            }
        }

        public async Task Delete(string packageId,
            string packageVersion,
            Func<string, string> getApiKey,
            Func<string, bool> confirm, 
            ILogger log)
        {
            var sourceDisplayName = GetSourceDisplayName(_source);
            var apiKey = getApiKey(_source);
            if (String.IsNullOrEmpty(apiKey))
            {
                log.LogWarning(string.Format(CultureInfo.CurrentCulture,
                    Strings.NoApiKeyFound, 
                    sourceDisplayName));
            }

            if (confirm(string.Format(CultureInfo.CurrentCulture, Strings.DeleteCommandConfirm, packageId, packageVersion, sourceDisplayName)))
            {
                log.LogWarning(string.Format(
                    Strings.DeleteCommandDeletingPackage,
                    packageId,
                    packageVersion,
                    sourceDisplayName
                    ));
                await DeletePackage(_source, apiKey, packageId, packageVersion, log, CancellationToken.None);
                log.LogInformation(string.Format(CultureInfo.CurrentCulture,
                    Strings.DeleteCommandDeletedPackage, 
                    packageId, 
                    packageVersion));
            }
            else
            {
                log.LogInformation(Strings.DeleteCommandCanceled);
            }
        }

        private async Task PushSymbols(string packagePath, 
            string apiKey, 
            ILogger log, 
            CancellationToken token)
        {
            // Get the symbol package for this package
            var symbolPackagePath = GetSymbolsPath(packagePath);

            // Push the symbols package if it exists
            if (File.Exists(symbolPackagePath))
            {
                // See if the api key exists
                if (String.IsNullOrEmpty(apiKey))
                {
                    log.LogWarning(string.Format(CultureInfo.CurrentCulture,
                        Strings.Warning_SymbolServerNotConfigured,
                        Path.GetFileName(symbolPackagePath),
                        Strings.DefaultSymbolServer));
                }
                var source = NuGetConstants.DefaultSymbolServerUrl;
                await PushPackage(symbolPackagePath, source, apiKey, log, token);
            }
        }

        /// <summary>
        /// Get the symbols package from the original package. Removes the .nupkg and adds .symbols.nupkg
        /// </summary>
        private static string GetSymbolsPath(string packagePath)
        {
            string symbolPath = Path.GetFileNameWithoutExtension(packagePath) + NuGetConstants.SymbolsExtension;
            string packageDir = Path.GetDirectoryName(packagePath);
            return Path.Combine(packageDir, symbolPath);
        }

        private async Task PushPackage(string packagePath, 
            string source, 
            string apiKey, 
            ILogger log, 
            CancellationToken token)
        {
            if (string.IsNullOrEmpty(apiKey) && !IsFileSource())
            {
                log.LogWarning(string.Format(CultureInfo.CurrentCulture,
                    Strings.NoApiKeyFound,
                    GetSourceDisplayName(source)));
            }

            var packagesToPush = GetPackagesToPush(packagePath);

            EnsurePackageFileExists(packagePath, packagesToPush);
            foreach (string packageToPush in packagesToPush)
            {
                await PushPackageCore(source, apiKey, packageToPush, log, token);
            }
        }

        private async Task PushPackageCore(string source,
            string apiKey,
            string packageToPush,
            ILogger log,
            CancellationToken token)
        {
            var sourceUri = new Uri(source);
            var sourceName = GetSourceDisplayName(source);

            log.LogInformation(string.Format(CultureInfo.CurrentCulture,
                Strings.PushCommandPushingPackage,
                Path.GetFileName(packageToPush),
                sourceName));

            if (IsFileSource())
            {
                PushPackageToFileSystem(sourceUri, packageToPush);
            }
            else
            {
                var length = new FileInfo(packageToPush).Length;
                await PushPackageToServer(source, apiKey, packageToPush, length, log, token);
            }

            log.LogInformation(Strings.PushCommandPackagePushed);
        }

        private static string GetSourceDisplayName(string source)
        {
            if (String.IsNullOrEmpty(source) || source.Equals(NuGetConstants.DefaultGalleryServerUrl, StringComparison.OrdinalIgnoreCase))
            {
                return Strings.LiveFeed + " (" + NuGetConstants.DefaultGalleryServerUrl + ")";
            }
            if (source.Equals(NuGetConstants.DefaultSymbolServerUrl, StringComparison.OrdinalIgnoreCase))
            {
                return Strings.DefaultSymbolServer + " (" + NuGetConstants.DefaultSymbolServerUrl + ")";
            }
            return "'" + source + "'";
        }

        private static IEnumerable<string> GetPackagesToPush(string packagePath)
        {
            // Ensure packagePath ends with *.nupkg
            packagePath = EnsurePackageExtension(packagePath);
            return PathResolver.PerformWildcardSearch(Directory.GetCurrentDirectory(), packagePath);
        }

        private static string EnsurePackageExtension(string packagePath)
        {
            if (packagePath.IndexOf('*') == -1)
            {
                // If there's no wildcard in the path to begin with, assume that it's an absolute path.
                return packagePath;
            }
            // If the path does not contain wildcards, we need to add *.nupkg to it.
            if (!packagePath.EndsWith(NuGetConstants.PackageExtension, StringComparison.OrdinalIgnoreCase))
            {
                if (packagePath.EndsWith("**", StringComparison.OrdinalIgnoreCase))
                {
                    packagePath = packagePath + Path.DirectorySeparatorChar + '*';
                }
                else if (!packagePath.EndsWith("*", StringComparison.OrdinalIgnoreCase))
                {
                    packagePath = packagePath + '*';
                }
                packagePath = packagePath + NuGetConstants.PackageExtension;
            }
            return packagePath;
        }

        private static void EnsurePackageFileExists(string packagePath, IEnumerable<string> packagesToPush)
        {
            if (!packagesToPush.Any())
            {
                throw new ArgumentException(String.Format(CultureInfo.CurrentCulture,
                    Strings.UnableToFindFile,
                    packagePath));
            }
        }

        // Indicates whether the specified source is a file source, such as: \\a\b, c:\temp, etc.
        private bool IsFileSource()
        {
            //we leverage the detection already done at resource provider level. 
            //that for file system, the "httpSource" is null. 
            return _httpSource == null;
        }

        // Pushes a package to the Http server.
        private async Task PushPackageToServer(string source,
            string apiKey,
            string pathToPackage,
            long packageSize,
            ILogger logger,
            CancellationToken token)
        {
            var response = await _httpSource.SendAsync(
                () => CreateRequest(source, pathToPackage, apiKey),
                token);

            using (response)
            {
                response.EnsureSuccessStatusCode();
            }
        }

        private HttpRequestMessage CreateRequest(string source,
            string pathToPackage,
            string apiKey)
        {
            var fileStream = new FileStream(pathToPackage, FileMode.Open, FileAccess.Read, FileShare.Read);
            var request = new HttpRequestMessage(HttpMethod.Put, GetServiceEndpointUrl(source, string.Empty));
            var content = new MultipartFormDataContent();

            var packageContent = new StreamContent(fileStream);
            packageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
            //"package" and "package.nupkg" are random names for content deserializing
            //not tied to actual package name.  
            content.Add(packageContent, "package", "package.nupkg");
            request.Content = content;

            if (!string.IsNullOrEmpty(apiKey))
            {
                request.Headers.Add(ApiKeyHeader, apiKey);
            }
            return request;
        }

        private void PushPackageToFileSystem(Uri sourceUri, string pathToPackage)
        {
            string root = sourceUri.LocalPath;
            var reader = new PackageArchiveReader(pathToPackage);
            var packageIdentity = reader.GetIdentity();

            //TODD: support V3 repo style if detect it is
            var pathResolver = new PackagePathResolver(root, useSideBySidePaths: true);
            var packageFileName = pathResolver.GetPackageFileName(packageIdentity);

            var fullPath = Path.Combine(root, packageFileName);
            File.Copy(pathToPackage, fullPath, overwrite: true);
        }

        // Deletes a package from a Http server or file system
        private async Task DeletePackage(string source,
            string apiKey,
            string packageId,
            string packageVersion,
            ILogger logger,
            CancellationToken token)
        {
            var sourceUri = GetServiceEndpointUrl(source, string.Empty);
            if (IsFileSource())
            {
                DeletePackageFromFileSystem(source, packageId, packageVersion, logger);
            }
            else
            {
                await DeletePackageFromServer(source, apiKey, packageId, packageVersion, logger, token);
            }
        }

        // Deletes a package from a Http server
        private async Task DeletePackageFromServer(string source,
            string apiKey,
            string packageId,
            string packageVersion,
            ILogger logger,
            CancellationToken token)
        {
            var response = await _httpSource.SendAsync(
                ()=> {
                    // Review: Do these values need to be encoded in any way?
                    var url = String.Join("/", packageId, packageVersion);
                    var request = new HttpRequestMessage(HttpMethod.Delete, GetServiceEndpointUrl(source, url));
                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        request.Headers.Add(ApiKeyHeader, apiKey);
                    }
                    return request;
                }, 
                token);

            using (response)
            {
                response.EnsureSuccessStatusCode();
            }
        }

        // Deletes a package from a FileSystem.
        private void DeletePackageFromFileSystem(string source, string packageId, string packageVersion, ILogger logger)
        {
            var sourceuri = new Uri(source);
            var root = sourceuri.LocalPath;
            var resolver = new PackagePathResolver(sourceuri.AbsolutePath, useSideBySidePaths: true);
            resolver.GetPackageFileName(new Packaging.Core.PackageIdentity(packageId, new NuGetVersion(packageVersion)));
            var packageIdentity = new PackageIdentity(packageId, new NuGetVersion(packageVersion));
            var packageFileName = resolver.GetPackageFileName(packageIdentity);

            var fullPath = Path.Combine(root, packageFileName);
            MakeFileWritable(fullPath);
            File.Delete(fullPath);
        }

        // Remove the read-only flag.
        private void MakeFileWritable(string fullPath)
        {
            var attributes = File.GetAttributes(fullPath);
            if (attributes.HasFlag(FileAttributes.ReadOnly))
            {
                File.SetAttributes(fullPath, attributes & ~FileAttributes.ReadOnly);
            }
        }

        // Calculates the URL to the package to.
        private Uri GetServiceEndpointUrl(string source, string path)
        {
            var baseUri = EnsureTrailingSlash(source);
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

        // Ensure a trailing slash at the end
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
