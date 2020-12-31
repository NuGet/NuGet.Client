// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.Caching;
using System.Threading;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.ServiceHub.Framework;
using Microsoft.ServiceHub.Framework.Services;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.VisualStudio
{
    public sealed class NuGetRemoteFileService : INuGetRemoteFileService
    {
        private ServiceActivationOptions? _options;
        private IServiceBroker _serviceBroker;
        private AuthorizationServiceClient? _authorizationServiceClient;
        private bool _disposedValue;

        // TODO: right settings for cache and policy?
        internal readonly static MemoryCache IdentityToUriCache = new MemoryCache("PackageSearchMetadata",
            new NameValueCollection
            {
                { "cacheMemoryLimitMegabytes", "4" },
                { "physicalMemoryLimitPercentage", "0" },
                { "pollingInterval", "00:02:00" }
            });
        private static readonly CacheItemPolicy CacheItemPolicy = new CacheItemPolicy
        {
            SlidingExpiration = ObjectCache.NoSlidingExpiration,
            AbsoluteExpiration = ObjectCache.InfiniteAbsoluteExpiration,
        };

        public static void AddIconToCache(PackageIdentity packageIdentity, Uri uri)
        {
            if (uri != null)
            {
                IdentityToUriCache.Add("icon:" + packageIdentity.ToString(), uri, CacheItemPolicy);
            }
        }

        public static void AddLicenseToCache(PackageIdentity packageIdentity, Uri embeddedLicenseUri)
        {
            Assumes.NotNull(embeddedLicenseUri);
            IdentityToUriCache.Add("license:" + packageIdentity.ToString(), embeddedLicenseUri, CacheItemPolicy);
        }

        public NuGetRemoteFileService(
            ServiceActivationOptions options,
            IServiceBroker serviceBroker,
            AuthorizationServiceClient authorizationServiceClient)
        {
            _options = options;
            _serviceBroker = serviceBroker;
            _authorizationServiceClient = authorizationServiceClient;

            Assumes.NotNull(_serviceBroker);
            Assumes.NotNull(_authorizationServiceClient);
        }

        public NuGetRemoteFileService(IServiceBroker serviceBroker)
        {
            _serviceBroker = serviceBroker;
            Assumes.NotNull(_serviceBroker);
        }

        public async ValueTask<Stream?> GetPackageIconAsync(PackageIdentity packageIdentity, CancellationToken cancellationToken)
        {
            Assumes.NotNull(packageIdentity);
            string key = "icon:" + packageIdentity.ToString();
            Uri? uri = IdentityToUriCache.Get(key) as Uri;

            if (uri == null)
            {
                return null;
            }

            Stream? stream;
            if (IsEmbeddedUri(uri))
            {
                stream = await GetEmbeddedFileAsync(uri, cancellationToken);
            }
            else
            {
                stream = await GetStream(uri);
            }

            return stream;
        }

        public async ValueTask<Stream?> GetEmbeddedLicenseAsync(PackageIdentity packageIdentity, CancellationToken cancellationToken)
        {
            Assumes.NotNull(packageIdentity);
            string key = "license:" + packageIdentity.ToString();
            Uri? uri = IdentityToUriCache.Get(key) as Uri;
            if (uri == null)
            {
                return null;
            }

            Stream? stream = await GetEmbeddedFileAsync(uri, cancellationToken);
            return stream;
        }

        private async ValueTask<Stream?> GetEmbeddedFileAsync(Uri uri, CancellationToken cancellationToken)
        {
            string packagePath = uri.LocalPath;
            if (File.Exists(packagePath))
            {
                string fileRelativePath = PathUtility.StripLeadingDirectorySeparators(
                Uri.UnescapeDataString(uri.Fragment)
                    .Substring(1)); // Substring skips the '#' in the URI fragment

                string dirPath = Path.GetDirectoryName(packagePath);
                string extractedIconPath = Path.Combine(dirPath, fileRelativePath);

                // use GetFullPath to normalize "..", so that zip slip attack cannot allow a user to walk up the file directory
                if (Path.GetFullPath(extractedIconPath).StartsWith(dirPath, StringComparison.OrdinalIgnoreCase) && File.Exists(extractedIconPath))
                {
                    Stream fileStream = new FileStream(extractedIconPath, FileMode.Open);
                    return fileStream;
                }
                else
                {
                    try
                    {
                        using (PackageArchiveReader reader = new PackageArchiveReader(packagePath))
                        using (Stream parStream = await reader.GetStreamAsync(fileRelativePath, cancellationToken))
                        {
                            var memoryStream = new MemoryStream();
                            await parStream.CopyToAsync(memoryStream);
                            return memoryStream;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                        return null;
                    }
                }
            }
            else
            {
                return null;
            }
        }

        private async Task<Stream?> GetStream(Uri uri)
        {
            // BitmapImage can download on its own from URIs, but in order
            // to support downloading on a worker thread, we need to download the image
            // data and put into a memorystream. Then have the BitmapImage decode the
            // image from the memorystream.

            byte[]? imageData = null;
            MemoryStream? memoryStream = null;

            if (uri.IsFile)
            {
                if (File.Exists(uri.LocalPath))
                {
                    memoryStream = new MemoryStream(File.ReadAllBytes(uri.LocalPath));
                    return memoryStream;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                using (var httpClient = new HttpClient())
                {
                    try
                    {
                        imageData = await httpClient.GetByteArrayAsync(uri);

#pragma warning disable CA2000 // Dispose objects before losing scope - stream needs to be disposed by caller.
                        memoryStream = new MemoryStream(imageData, writable: false);
#pragma warning restore CA2000 // Dispose objects before losing scope
                    }
                    catch (HttpRequestException)
                    {
                        return null;
                    }
                    catch (TaskCanceledException)
                    {
                        return null;
                    }
                    catch (ArgumentException)
                    {
                        return null;
                    }
                }

                return memoryStream;
            }
        }

        /// <summary>
        /// NuGet Embedded Uri verification
        /// </summary>
        /// <param name="uri">An URI to test</param>
        /// <returns><c>true</c> if <c>uri</c> is an URI to an embedded file in a NuGet package</returns>
        public static bool IsEmbeddedUri(Uri uri)
        {
            return uri != null
                && uri.IsAbsoluteUri
                && uri.IsFile
                && !string.IsNullOrEmpty(uri.Fragment)
                && uri.Fragment.Length > 1;
        }

        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _authorizationServiceClient?.Dispose();
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
