// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.PackageExtraction;
using NuGet.Packaging.Signing;
using NuGet.Versioning;

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
            IList<string> packagePaths,
            string symbolSource, // empty to not push symbols
            int timeoutInSecond,
            bool disableBuffering,
            Func<string, string> getApiKey,
            Func<string, string> getSymbolApiKey,
            bool noServiceEndpoint,
            bool skipDuplicate,
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

                foreach (var packagePath in packagePaths)
                {
                    bool explicitSnupkgPush = true;
                    if (!packagePath.EndsWith(NuGetConstants.SnupkgExtension, StringComparison.OrdinalIgnoreCase))
                    {
                        await PushPackage(packagePath, _source, apiKey, noServiceEndpoint, skipDuplicate,
                            requestTimeout, log, tokenSource.Token);

                        //Since this was not a snupkg push (probably .nupkg), when we try pushing symbols later, don't error if there are no snupkg files found.
                        explicitSnupkgPush = false;
                    }

                    // symbolSource is only set when:
                    // - The user specified it on the command line
                    // - The endpoint for main package supports pushing snupkgs
                    if (!string.IsNullOrEmpty(symbolSource))
                    {
                        var symbolApiKey = getSymbolApiKey(symbolSource);

                        await PushSymbols(packagePath, symbolSource, symbolApiKey,
                            noServiceEndpoint, skipDuplicate, symbolPackageUpdateResource,
                            requestTimeout, log, explicitSnupkgPush, tokenSource.Token);
                    }
                }
            }
        }

        [Obsolete("Use Push method which takes multiple package paths.")]
        public Task Push(
            string packagePath,
            string symbolSource, // empty to not push symbols
            int timeoutInSecond,
            bool disableBuffering,
            Func<string, string> getApiKey,
            Func<string, string> getSymbolApiKey,
            bool noServiceEndpoint,
            bool skipDuplicate,
            SymbolPackageUpdateResourceV3 symbolPackageUpdateResource,
            ILogger log)
        {
            return Push(new[] { packagePath }, symbolSource, timeoutInSecond, disableBuffering, getApiKey,
                getSymbolApiKey, noServiceEndpoint, skipDuplicate, symbolPackageUpdateResource, log);
        }

        [Obsolete("Consolidating to one PackageUpdateResource.Push method which has all parameters defined.")]
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
                skipDuplicate: false,
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
            bool skipDuplicate,
            SymbolPackageUpdateResourceV3 symbolPackageUpdateResource,
            TimeSpan requestTimeout,
            ILogger log,
            bool explicitSymbolsPush,
            CancellationToken token)
        {

            var isSymbolEndpointSnupkgCapable = symbolPackageUpdateResource != null;
            // Get the symbol package for this package
            var symbolPackagePath = GetSymbolsPath(packagePath, isSymbolEndpointSnupkgCapable);

            var symbolsToPush = LocalFolderUtility.ResolvePackageFromPath(symbolPackagePath, isSnupkg: isSymbolEndpointSnupkgCapable);
            bool symbolsPathResolved = symbolsToPush != null && symbolsToPush.Any();

            //No files were resolved.
            if (!symbolsPathResolved)
            {
                if (explicitSymbolsPush)
                {
                    throw new ArgumentException(string.Format(CultureInfo.CurrentCulture,
                        Strings.UnableToFindFile,
                        packagePath));
                }
            }
            else
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

                await PushAll(source, apiKey, noServiceEndpoint, skipDuplicate, requestTimeout, log, packagesToPush: symbolsToPush, token);
            }
        }

        private async Task PushPackage(string packagePath,
            string source,
            string apiKey,
            bool noServiceEndpoint,
            bool skipDuplicate,
            TimeSpan requestTimeout,
            ILogger log,
            CancellationToken token)
        {
            var nupkgsToPush = LocalFolderUtility.ResolvePackageFromPath(packagePath, isSnupkg: false);

            if (!(nupkgsToPush != null && nupkgsToPush.Any()))
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture,
                    Strings.UnableToFindFile,
                    packagePath));
            }

            var sourceUri = UriUtility.CreateSourceUri(source);

            if (string.IsNullOrEmpty(apiKey) && !sourceUri.IsFile)
            {
                log.LogWarning(string.Format(CultureInfo.CurrentCulture,
                    Strings.NoApiKeyFound,
                    GetSourceDisplayName(source)));
            }

            await PushAll(source, apiKey, noServiceEndpoint, skipDuplicate, requestTimeout, log, packagesToPush: nupkgsToPush, token);
        }

        private async Task PushAll(string source, string apiKey, bool noServiceEndpoint, bool skipDuplicate, TimeSpan requestTimeout, ILogger log, IEnumerable<string> packagesToPush, CancellationToken token)
        {
            foreach (var packageToPush in packagesToPush)
            {
                await PushPackageCore(source, apiKey, packageToPush, noServiceEndpoint, skipDuplicate, requestTimeout, log, token);
            }
        }

        private async Task PushPackageCore(string source,
            string apiKey,
            string packageToPush,
            bool noServiceEndpoint,
            bool skipDuplicate,
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

            bool showPushCommandPackagePushed = true;

            if (sourceUri.IsFile)
            {
                await PushPackageToFileSystem(sourceUri, packageToPush, skipDuplicate, log, token);
            }
            else
            {
                var length = new FileInfo(packageToPush).Length;
                showPushCommandPackagePushed = await PushPackageToServerAsync(source, apiKey, packageToPush, noServiceEndpoint, skipDuplicate
                                                    , requestTimeout, log, token);

            }

            if (showPushCommandPackagePushed)
            {
                log.LogInformation(Strings.PushCommandPackagePushed);
            }
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

        /// <summary>
        /// Get the symbols package from the original package. Removes the .nupkg and adds .snupkg or .symbols.nupkg.
        /// </summary>
        private static string GetSymbolsPath(string packagePath, bool isSnupkg)
        {
            var symbolPath = Path.GetFileNameWithoutExtension(packagePath) + (isSnupkg ? NuGetConstants.SnupkgExtension : NuGetConstants.SymbolsExtension);
            var packageDir = Path.GetDirectoryName(packagePath);
            return Path.Combine(packageDir, symbolPath);
        }

        /// <summary>
        /// Pushes a package to the Http server.
        /// </summary>
        /// <returns>Indicator of whether to show PushCommandPackagePushed message.</returns>
        private async Task<bool> PushPackageToServerAsync(string source,
            string apiKey,
            string pathToPackage,
            bool noServiceEndpoint,
            bool skipDuplicate,
            TimeSpan requestTimeout,
            ILogger logger,
            CancellationToken token)
        {
            var serviceEndpointUrl = GetServiceEndpointUrl(source, string.Empty, noServiceEndpoint);
            var useTempApiKey = IsSourceNuGetSymbolServer(source);
            var codeNotToThrow = ConvertSkipDuplicateParamToHttpStatusCode(skipDuplicate);
            var showPushCommandPackagePushed = true;

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
                            var tmpApiKey = await GetSecureApiKeyAsync(packageIdentity, apiKey, noServiceEndpoint, requestTimeout, logger, token);

                            await _httpSource.ProcessResponseAsync(
                                new HttpSourceRequest(() => CreateRequest(serviceEndpointUrl, pathToPackage, tmpApiKey, logger))
                                {
                                    RequestTimeout = requestTimeout,
                                    MaxTries = 1
                                },
                                response =>
                                {
                                    var responseStatusCode = EnsureSuccessStatusCode(response, codeNotToThrow, logger);

                                    var logOccurred = DetectAndLogSkippedErrorOccurrence(responseStatusCode, source, pathToPackage, response.ReasonPhrase, logger);
                                    showPushCommandPackagePushed = !logOccurred;

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
                        var responseStatusCode = EnsureSuccessStatusCode(response, codeNotToThrow, logger);
                        var logOccurred = DetectAndLogSkippedErrorOccurrence(responseStatusCode, source, pathToPackage, response.ReasonPhrase, logger);
                        showPushCommandPackagePushed = !logOccurred;

                        return Task.FromResult(0);
                    },
                    logger,
                    token);
            }

            return showPushCommandPackagePushed;
        }

        /// <summary>
        /// Ensures a Success HTTP Status Code is returned unless a specified exclusion occurred. If CodeNotToThrow is provided and the response contains
        /// this code, do not EnsureSuccess and instead return the exception code gracefully.
        /// </summary>
        /// <param name="response"></param>
        /// <param name="codeNotToThrow"></param>
        /// <param name="logger"></param>
        /// <returns>Response StatusCode</returns>
        private static HttpStatusCode? EnsureSuccessStatusCode(HttpResponseMessage response, HttpStatusCode? codeNotToThrow, ILogger logger)
        {
            //If this status code is to be excluded.
            if (codeNotToThrow != null && codeNotToThrow == response.StatusCode)
            {
                return response.StatusCode;
            }
            else
            {
                AdvertiseAvailableOptionToIgnore(response.StatusCode, logger);
            }

            //No exception to the rule specified.
            response.EnsureSuccessStatusCode();
            return null;
        }


        /// <summary>
        /// Gently log any specified Skipped status code without throwing.
        /// </summary>
        /// <param name="skippedErrorStatusCode">If provided, it indicates that this StatusCode occurred but was flagged as to be Skipped.</param>
        /// <param name="logger"></param>
        /// <returns>Indication of whether the log occurred.</returns>
        private static bool DetectAndLogSkippedErrorOccurrence(HttpStatusCode? skippedErrorStatusCode, string source, string packageIdentity, string reasonMessage, ILogger logger)
        {
            bool skippedErrorOccurred = false;

            if (skippedErrorStatusCode != null)
            {
                string messageToLog = null;
                string messageToLogVerbose = null;

                switch (skippedErrorStatusCode.Value)
                {
                    case HttpStatusCode.Conflict:

                        messageToLog = string.Format(
                                   CultureInfo.CurrentCulture,
                                   Strings.AddPackage_PackageAlreadyExists,
                                   packageIdentity,
                                   source);
                        messageToLogVerbose = reasonMessage;
                        skippedErrorOccurred = true;
                        break;
                    case HttpStatusCode.BadRequest:
                        messageToLog = Strings.NupkgPath_Invalid;
                        skippedErrorOccurred = true;
                        break;
                    default: break; //Not a skippable response code.
                }
                if (messageToLog != null)
                {
                    logger?.LogMinimal(messageToLog);
                }
                if (messageToLogVerbose != null)
                {
                    logger?.LogVerbose(messageToLogVerbose);
                }
            }

            return skippedErrorOccurred;
        }

        /// <summary>
        /// If we provide such option, output a help message that explains that the error that occurred can be ignored by using it.
        /// </summary>
        /// <param name="errorCodeThatOccurred">Error to check for a relevant option to advertise to the user. </param>
        /// <param name="logger"></param>
        private static void AdvertiseAvailableOptionToIgnore(HttpStatusCode errorCodeThatOccurred, ILogger logger)
        {
            string advertiseDescription = null;

            switch (errorCodeThatOccurred)
            {
                case HttpStatusCode.Conflict:

#if IS_DESKTOP
                    advertiseDescription = Strings.PushCommandSkipDuplicateAdvertiseNuGetExe;
#else
                    advertiseDescription = Strings.PushCommandSkipDuplicateAdvertiseDotnetExe;
#endif
                    break;

                default: break; //Not a supported response code.
            }

            if (advertiseDescription != null)
            {
                logger?.LogInformation(advertiseDescription);
            }
        }

        private HttpStatusCode? ConvertSkipDuplicateParamToHttpStatusCode(bool skipDuplicate)
        {
            if (skipDuplicate)
            {
                return HttpStatusCode.Conflict;
            }

            return null;
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
            bool skipDuplicate,
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
                var pathResolver = new PackagePathResolver(sourceUri.AbsolutePath, useSideBySidePaths: true);
                var packageFileName = pathResolver.GetPackageFileName(packageIdentity);

                var fullPath = Path.Combine(root, packageFileName);
                File.Copy(pathToPackage, fullPath, overwrite: true);

                //Indicate that SkipDuplicate is currently not supported in this scenario.
                if (skipDuplicate)
                {
                    log?.LogWarning(Strings.PushCommandSkipDuplicateNotImplemented);
                }
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
                    throwIfPackageExistsAndInvalid: !skipDuplicate,
                    throwIfPackageExists: !skipDuplicate,
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
                DeletePackageFromFileSystem(source, packageId, packageVersion);
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
        private void DeletePackageFromFileSystem(string source, string packageId, string packageVersion)
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
        private async Task<string> GetSecureApiKeyAsync(
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

                return result.Value<string>("Key") ?? InvalidApiKey;
            }
            catch (HttpRequestException ex)
            {
#if NETCOREAPP
                if (ex.Message.Contains("Response status code does not indicate success: 403 (Forbidden).", StringComparison.OrdinalIgnoreCase))
#else
                if (ex.Message.Contains("Response status code does not indicate success: 403 (Forbidden)."))
#endif
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
