// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Newtonsoft.Json;
using NuGet.Packaging.PackageExtraction;
using NuGet.Packaging.Signing;

namespace NuGet.Protocol.Core.Types
{
    /// <summary>
    /// Contains logics to push or delete packages in Http server or file system
    /// </summary>
    public class PackageUpdateResource : INuGetResource
    {
        private const string ServiceEndpoint = "/api/v2/package";
        private const string ApiKeyHeader = "X-NuGet-ApiKey";
        private const string InvalidApiKey = "invalidapikey";

        /// <summary>
        /// Create temporary verification api key endpoint: "create-verification-key/[package id]/[package version]"
        /// </summary>
        private const string TempApiKeyServiceEndpoint = "create-verification-key/{0}/{1}";

        private HttpSource _httpSource;
        private string _source;
        private bool _disableBuffering;
        public ISettings Settings { get; set; }

        public PackageUpdateResource(string source,
            HttpSource httpSource)
        {
            _source = source;
            _httpSource = httpSource;
        }

        public Uri SourceUri
        {
            get { return UriUtility.CreateSourceUri(_source); }
        }

        public async Task Push(
            string packagePath,
            string symbolSource, // empty to not push symbols
            int timeoutInSecond,
            bool disableBuffering,
            Func<string, string> getApiKey,
            Func<string, string> getSymbolApiKey,
            bool noServiceEndpoint,
            SymbolPackageUpdateResourceV3 symbolPackageUpdateResource,
            ILogger log)
        {
            // TODO: Figure out how to hook this up with the HTTP request
            _disableBuffering = disableBuffering;

            using (var tokenSource = new CancellationTokenSource())
            {
                var requestTimeout = TimeSpan.FromSeconds(timeoutInSecond);
                tokenSource.CancelAfter(requestTimeout);
                var apiKey = getApiKey(_source);

                // if only a snupkg is being pushed, then don't try looking for nupkgs.
                if(!packagePath.EndsWith(NuGetConstants.SnupkgExtension, StringComparison.OrdinalIgnoreCase))
                {
                    await PushPackage(packagePath, _source, apiKey, noServiceEndpoint, requestTimeout, log, tokenSource.Token, isSnupkgPush: false);
                }

                // symbolSource is only set when:
                // - The user specified it on the command line
                // - The endpoint for main package supports pushing snupkgs
                if (!string.IsNullOrEmpty(symbolSource))
                {
                    var symbolApiKey = getSymbolApiKey(symbolSource);

                    await PushSymbols(packagePath, symbolSource, symbolApiKey,
                        noServiceEndpoint, symbolPackageUpdateResource,
                        requestTimeout, log, tokenSource.Token);
                }
            }
        }

        public async Task Push(
            string packagePath,
            string symbolSource, // empty to not push symbols
            int timeoutInSecond,
            bool disableBuffering,
            Func<string, string> getApiKey,
            Func<string, string> getSymbolApiKey,
            bool noServiceEndpoint,
            ILogger log)
        {
            await Push(
                packagePath,
                symbolSource,
                timeoutInSecond,
                disableBuffering,
                getApiKey,
                getSymbolApiKey,
                noServiceEndpoint,
                symbolPackageUpdateResource: null,
                log: log);
        }

        public async Task Delete(string packageId,
            string packageVersion,
            Func<string, string> getApiKey,
            Func<string, bool> confirm,
            bool noServiceEndpoint,
            ILogger log)
        {
            var sourceDisplayName = GetSourceDisplayName(_source);
            var apiKey = getApiKey(_source);
            if (string.IsNullOrEmpty(apiKey) && !IsFileSource())
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
                await DeletePackage(_source, apiKey, packageId, packageVersion, noServiceEndpoint, log, CancellationToken.None);
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
            string source,
            string apiKey,
            bool noServiceEndpoint,
            SymbolPackageUpdateResourceV3 symbolPackageUpdateResource,
            TimeSpan requestTimeout,
            ILogger log,
            CancellationToken token)
        {
            var isSymbolEndpointSnupkgCapable = symbolPackageUpdateResource != null;
            // Get the symbol package for this package
            var symbolPackagePath = GetSymbolsPath(packagePath, isSymbolEndpointSnupkgCapable);

            // Push the symbols package if it exists
            if (File.Exists(symbolPackagePath) || symbolPackagePath.IndexOf('*') != -1)
            {
                var sourceUri = UriUtility.CreateSourceUri(source);

                // See if the api key exists
                if (string.IsNullOrEmpty(apiKey) && !sourceUri.IsFile)
                {
                    log.LogWarning(string.Format(CultureInfo.CurrentCulture,
                        Strings.Warning_SymbolServerNotConfigured,
                        Path.GetFileName(symbolPackagePath),
                        Strings.DefaultSymbolServer));
                }

                await PushPackage(symbolPackagePath, source, apiKey, noServiceEndpoint, requestTimeout, log, token, isSnupkgPush: isSymbolEndpointSnupkgCapable);
            }
        }

        /// <summary>
        /// Get the symbols package from the original package. Removes the .nupkg and adds .symbols.nupkg
        /// </summary>
        private static string GetSymbolsPath(string packagePath, bool isSnupkg)
        {
            var symbolPath = Path.GetFileNameWithoutExtension(packagePath) + (isSnupkg ? NuGetConstants.SnupkgExtension : NuGetConstants.SymbolsExtension);
            var packageDir = Path.GetDirectoryName(packagePath);
            return Path.Combine(packageDir, symbolPath);
        }

        private async Task PushPackage(string packagePath,
            string source,
            string apiKey,
            bool noServiceEndpoint,
            TimeSpan requestTimeout,
            ILogger log,
            CancellationToken token,
            bool isSnupkgPush)
        {
            var sourceUri = UriUtility.CreateSourceUri(source);

            if (string.IsNullOrEmpty(apiKey) && !sourceUri.IsFile)
            {
                log.LogWarning(string.Format(CultureInfo.CurrentCulture,
                    Strings.NoApiKeyFound,
                    GetSourceDisplayName(source)));
            }

            var packagesToPush = LocalFolderUtility.ResolvePackageFromPath(packagePath, isSnupkgPush);

            LocalFolderUtility.EnsurePackageFileExists(packagePath, packagesToPush);

            foreach (var packageToPush in packagesToPush)
            {
                await PushPackageCore(source, apiKey, packageToPush, noServiceEndpoint, requestTimeout, log, token);
            }
        }

        private async Task PushPackageCore(string source,
            string apiKey,
            string packageToPush,
            bool noServiceEndpoint,
            TimeSpan requestTimeout,
            ILogger log,
            CancellationToken token)
        {
            var sourceUri = UriUtility.CreateSourceUri(source);
            var sourceName = GetSourceDisplayName(source);

            log.LogInformation(string.Format(CultureInfo.CurrentCulture,
                Strings.PushCommandPushingPackage,
                Path.GetFileName(packageToPush),
                sourceName));

            if (sourceUri.IsFile)
            {
                await PushPackageToFileSystem(sourceUri, packageToPush, log, token);
            }
            else
            {
                var length = new FileInfo(packageToPush).Length;
                await PushPackageToServer(source, apiKey, packageToPush, length, noServiceEndpoint, requestTimeout, log, token);
            }

            log.LogInformation(Strings.PushCommandPackagePushed);
        }

        private static string GetSourceDisplayName(string source)
        {
            if (string.IsNullOrEmpty(source) || source.Equals(NuGetConstants.DefaultGalleryServerUrl, StringComparison.OrdinalIgnoreCase))
            {
                return Strings.LiveFeed + " (" + NuGetConstants.DefaultGalleryServerUrl + ")";
            }
            if (source.Equals(NuGetConstants.DefaultSymbolServerUrl, StringComparison.OrdinalIgnoreCase))
            {
                return Strings.DefaultSymbolServer + " (" + NuGetConstants.DefaultSymbolServerUrl + ")";
            }
            return "'" + source + "'";
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
            bool noServiceEndpoint,
            TimeSpan requestTimeout,
            ILogger logger,
            CancellationToken token)
        {
            var serviceEndpointUrl = GetServiceEndpointUrl(source, string.Empty, noServiceEndpoint);
            var useTempApiKey = IsSourceNuGetSymbolServer(source);

            if (useTempApiKey)
            {
                var maxTries = 3;

                using (var packageReader = new PackageArchiveReader(pathToPackage))
                {
                    var packageIdentity = packageReader.GetIdentity();
                    var success = false;
                    var retry = 0;

                    while (retry < maxTries && !success)
                    {
                        try
                        {
                            retry++;
                            success = true;
                            // If user push to https://nuget.smbsrc.net/, use temp api key.
                            var tmpApiKey = await GetSecureApiKey(packageIdentity, apiKey, noServiceEndpoint, requestTimeout, logger, token);

                            await _httpSource.ProcessResponseAsync(
                                new HttpSourceRequest(() => CreateRequest(serviceEndpointUrl, pathToPackage, tmpApiKey, logger))
                                {
                                    RequestTimeout = requestTimeout,
                                    MaxTries = 1
                                },
                                response =>
                                {
                                    response.EnsureSuccessStatusCode();

                                    return Task.FromResult(0);
                                },
                                logger,
                                token);
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception e)
                        {
                            if (retry == maxTries)
                            {
                                throw;
                            }

                            success = false;

                            logger.LogInformation(string.Format(
                                CultureInfo.CurrentCulture,
                                Strings.Log_RetryingHttp,
                                HttpMethod.Put,
                                source)
                                + Environment.NewLine
                                + ExceptionUtilities.DisplayMessage(e));
                        }
                    }
                }
            }
            else
            {
                await _httpSource.ProcessResponseAsync(
                    new HttpSourceRequest(() => CreateRequest(serviceEndpointUrl, pathToPackage, apiKey, logger))
                    {
                        RequestTimeout = requestTimeout
                    },
                    response =>
                    {
                        response.EnsureSuccessStatusCode();

                        return Task.FromResult(0);
                    },
                    logger,
                    token);
            }
        }

        private HttpRequestMessage CreateRequest(
            Uri serviceEndpointUrl,
            string pathToPackage,
            string apiKey,
            ILogger log)
        {
            var fileStream = new FileStream(pathToPackage, FileMode.Open, FileAccess.Read, FileShare.Read);
            var hasApiKey = !string.IsNullOrEmpty(apiKey);
            var request = HttpRequestMessageFactory.Create(
                HttpMethod.Put,
                serviceEndpointUrl,
                new HttpRequestMessageConfiguration(
                    logger: log,
                    promptOn403: !hasApiKey)); // Receiving an HTTP 403 when providing an API key typically indicates
                                               // an invalid API key, so prompting for credentials does not help.
            var content = new MultipartFormDataContent();

            var packageContent = new StreamContent(fileStream);
            packageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
            //"package" and "package.nupkg" are random names for content deserializing
            //not tied to actual package name.
            content.Add(packageContent, "package", "package.nupkg");
            request.Content = content;

            // Send the data in chunks so that it can be canceled if auth fails.
            // Otherwise the whole package needs to be sent to the server before the PUT fails.
            request.Headers.TransferEncodingChunked = true;

            if (hasApiKey)
            {
                request.Headers.Add(ProtocolConstants.ApiKeyHeader, apiKey);
            }

            return request;
        }

        private async Task PushPackageToFileSystem(Uri sourceUri,
            string pathToPackage,
            ILogger log,
            CancellationToken token)
        {
            var root = sourceUri.LocalPath;
            PackageIdentity packageIdentity = null;
            using (var reader = new PackageArchiveReader(pathToPackage))
            {
                packageIdentity = reader.GetIdentity();
            }

            if (IsV2LocalRepository(root))
            {
                var pathResolver = new PackagePathResolver(root, useSideBySidePaths: true);
                var packageFileName = pathResolver.GetPackageFileName(packageIdentity);

                var fullPath = Path.Combine(root, packageFileName);
                File.Copy(pathToPackage, fullPath, overwrite: true);
            }
            else
            {
                var packageExtractionContext = new PackageExtractionContext(
                    PackageSaveMode.Defaultv3,
                    PackageExtractionBehavior.XmlDocFileSaveMode,
                    ClientPolicyContext.GetClientPolicy(Settings, log),
                    log);

                var context = new OfflineFeedAddContext(pathToPackage,
                    root,
                    log,
                    throwIfSourcePackageIsInvalid: true,
                    throwIfPackageExistsAndInvalid: false,
                    throwIfPackageExists: false,
                    extractionContext: packageExtractionContext);
                
                await OfflineFeedUtility.AddPackageToSource(context, token);
            }
        }

        // Deletes a package from a Http server or file system
        private async Task DeletePackage(string source,
            string apiKey,
            string packageId,
            string packageVersion,
            bool noServiceEndpoint,
            ILogger logger,
            CancellationToken token)
        {
            var sourceUri = GetServiceEndpointUrl(source, string.Empty, noServiceEndpoint);
            if (sourceUri.IsFile)
            {
                DeletePackageFromFileSystem(source, packageId, packageVersion, logger);
            }
            else
            {
                await DeletePackageFromServer(source, apiKey, packageId, packageVersion, noServiceEndpoint, logger, token);
            }
        }

        // Deletes a package from a Http server
        private async Task DeletePackageFromServer(string source,
            string apiKey,
            string packageId,
            string packageVersion,
            bool noServiceEndpoint,
            ILogger logger,
            CancellationToken token)
        {
            var url = string.Join("/", packageId, packageVersion);
            var serviceEndpointUrl = GetServiceEndpointUrl(source, url, noServiceEndpoint);

            await _httpSource.ProcessResponseAsync(
                new HttpSourceRequest(
                    () =>
                    {
                        // Review: Do these values need to be encoded in any way?
                        var hasApiKey = !string.IsNullOrEmpty(apiKey);
                        var request = HttpRequestMessageFactory.Create(
                            HttpMethod.Delete,
                            serviceEndpointUrl,
                            new HttpRequestMessageConfiguration(
                                logger: logger,
                                promptOn403: !hasApiKey)); // Receiving an HTTP 403 when providing an API key typically
                                                           // indicates an invalid API key, so prompting for credentials
                                                           // does not help.

                        if (hasApiKey)
                        {
                            request.Headers.Add(ProtocolConstants.ApiKeyHeader, apiKey);
                        }

                        return request;
                    }),
                response =>
                {
                    response.EnsureSuccessStatusCode();

                    return Task.FromResult(0);
                },
                logger,
                token);
        }

        // Deletes a package from a FileSystem.
        private void DeletePackageFromFileSystem(string source, string packageId, string packageVersion, ILogger logger)
        {
            var sourceuri = UriUtility.CreateSourceUri(source);
            var root = sourceuri.LocalPath;
            var resolver = new PackagePathResolver(sourceuri.AbsolutePath, useSideBySidePaths: true);
            resolver.GetPackageFileName(new Packaging.Core.PackageIdentity(packageId, new NuGetVersion(packageVersion)));
            var packageIdentity = new PackageIdentity(packageId, new NuGetVersion(packageVersion));
            if (IsV2LocalRepository(root))
            {
                var packageFileName = resolver.GetPackageFileName(packageIdentity);
                var nupkgFilePath = Path.Combine(root, packageFileName);
                if (!File.Exists(nupkgFilePath))
                {
                    throw new ArgumentException(Strings.DeletePackage_NotFound);
                }
                ForceDeleteFile(nupkgFilePath);
            }
            else
            {
                var packageDirectory = OfflineFeedUtility.GetPackageDirectory(packageIdentity, root);
                if (!Directory.Exists(packageDirectory))
                {
                    throw new ArgumentException(Strings.DeletePackage_NotFound);
                }
                ForceDeleteDirectory(packageDirectory);
            }
        }

        // Remove the read-only flag and delete
        private void ForceDeleteFile(string fullPath)
        {
            var attributes = File.GetAttributes(fullPath);
            if (attributes.HasFlag(FileAttributes.ReadOnly))
            {
                File.SetAttributes(fullPath, attributes & ~FileAttributes.ReadOnly);
            }
            File.Delete(fullPath);
        }

        //Remove read-only flags from all files under a folder and delete
        public static void ForceDeleteDirectory(string path)
        {
            var directory = new DirectoryInfo(path) { Attributes = FileAttributes.Normal };

            foreach (var info in directory.GetFileSystemInfos("*", SearchOption.AllDirectories))
            {
                info.Attributes = FileAttributes.Normal;
            }

            directory.Delete(true);
        }

        // Calculates the URL to the package to.
        private Uri GetServiceEndpointUrl(string source, string path, bool noServiceEndpoint)
        {
            var baseUri = EnsureTrailingSlash(source);
            Uri requestUri;
            if (string.IsNullOrEmpty(baseUri.AbsolutePath.TrimStart('/')) && !noServiceEndpoint)
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

            return UriUtility.CreateSourceUri(value);
        }

        private bool IsV2LocalRepository(string root)
        {
            if (!Directory.Exists(root) ||
                Directory.GetFiles(root, "*.nupkg").Any())
            {
                // If the repository does not exist or if there are .nupkg in the path, this is a v2-style repository.
                return true;
            }

            foreach (var idDirectory in Directory.GetDirectories(root))
            {
                if (Directory.GetFiles(idDirectory, "*.nupkg").Any() ||
                    Directory.GetFiles(idDirectory, "*.nuspec").Any())
                {
                    // ~/Foo/Foo.1.0.0.nupkg (LocalPackageRepository with PackageSaveModes.Nupkg) or
                    // ~/Foo/Foo.1.0.0.nuspec (LocalPackageRepository with PackageSaveMode.Nuspec)
                    return true;
                }
                var idDirectoryName = Path.GetFileName(idDirectory);
                foreach (var versionDirectoryPath in Directory.GetDirectories(idDirectory))
                {
                    if (Directory.GetFiles(versionDirectoryPath, idDirectoryName + NuGetConstants.ManifestExtension).Any())
                    {
                        // If we have files in the format {packageId}/{version}/{packageId}.nuspec, assume it's an expanded package repository.
                        return false;
                    }
                }
            }

            return true;
        }

        // Get a temp API key from nuget.org for pushing to https://nuget.smbsrc.net/
        private async Task<string> GetSecureApiKey(
            PackageIdentity packageIdentity,
            string apiKey,
            bool noServiceEndpoint,
            TimeSpan requestTimeout,
            ILogger logger,
            CancellationToken token)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                return apiKey;
            }
            var serviceEndpointUrl = GetServiceEndpointUrl(NuGetConstants.DefaultGalleryServerUrl,
                string.Format(TempApiKeyServiceEndpoint, packageIdentity.Id, packageIdentity.Version), noServiceEndpoint);

            try
            {
                var result = await _httpSource.GetJObjectAsync(
                    new HttpSourceRequest(
                        () =>
                        {
                            var request = HttpRequestMessageFactory.Create(
                                HttpMethod.Post,
                                serviceEndpointUrl,
                                new HttpRequestMessageConfiguration(
                                    logger: logger,
                                    promptOn403: false));
                            request.Headers.Add(ApiKeyHeader, apiKey);
                            return request;
                        })
                    {
                        RequestTimeout = requestTimeout,
                        MaxTries = 1
                    },
                   logger,
                   token);

                return result.Value<string>("Key")?? InvalidApiKey;
            }
            catch(HttpRequestException ex)
            {
                if (ex.Message.Contains("Response status code does not indicate success: 403 (Forbidden)."))
                {
                    return InvalidApiKey;
                }

                throw;
            }
        }

        private bool IsSourceNuGetSymbolServer(string source)
        {
            var sourceUri = UriUtility.CreateSourceUri(source);

            return sourceUri.Host.Equals(NuGetConstants.NuGetSymbolHostName, StringComparison.OrdinalIgnoreCase);
        }
    }
}