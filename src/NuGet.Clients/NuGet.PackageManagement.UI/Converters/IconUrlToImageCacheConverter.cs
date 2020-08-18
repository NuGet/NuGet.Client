// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Cache;
using System.Runtime.Caching;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using NuGet.Common;
using NuGet.Packaging;

namespace NuGet.PackageManagement.UI
{
    internal class IconUrlToImageCacheConverter : IMultiValueConverter
    {
        private const int DecodePixelWidth = 32;

        // same URIs can reuse the bitmapImage that we've already used.
        private static readonly ObjectCache BitmapImageCache = MemoryCache.Default;

        private static readonly WebExceptionStatus[] FatalErrors = new[]
        {
            WebExceptionStatus.ConnectFailure,
            WebExceptionStatus.RequestCanceled,
            WebExceptionStatus.ConnectionClosed,
            WebExceptionStatus.Timeout,
            WebExceptionStatus.UnknownError
        };

        private static readonly RequestCachePolicy RequestCacheIfAvailable = new RequestCachePolicy(RequestCacheLevel.CacheIfAvailable);

        private static readonly ErrorFloodGate ErrorFloodGate = new ErrorFloodGate();

        /// <summary>
        /// Converts IconUrl from PackageItemListViewModel to an image represented by a BitmapSource
        /// </summary>
        /// <param name="values">An array of two elements containing the IconUri and a generator function of PackageReaderBase objects</param>
        /// <param name="targetType">unused</param>
        /// <param name="parameter">A BitmapImage that will be used as the default package icon</param>
        /// <param name="culture">unused</param>
        /// <returns>A BitmapSource with the image</returns>
        /// <remarks>
        /// We bind to a BitmapImage instead of a Uri so that we can control the decode size, since we are displaying 32x32 images, while many of the images are 128x128 or larger.
        /// This leads to a memory savings.
        /// </remarks>
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length == 0)
            {
                return null;
            }

            var iconUrl = values[0] as Uri;
            var defaultPackageIcon = parameter as BitmapSource;
            if (iconUrl == null)
            {
                return null;
            }

            var cachedBitmapImage = BitmapImageCache.Get(iconUrl.ToString()) as BitmapSource;
            if (cachedBitmapImage != null)
            {
                return cachedBitmapImage;
            }

            // Some people run on networks with internal NuGet feeds, but no access to the package images on the internet.
            // This is meant to detect that kind of case, and stop spamming the network, so the app remains responsive.
            if (ErrorFloodGate.IsOpen)
            {
                return defaultPackageIcon;
            }

            var iconBitmapImage = new BitmapImage();
            iconBitmapImage.BeginInit();

            BitmapSource imageResult;

            if (IsEmbeddedIconUri(iconUrl))
            {
                // Check if we have enough info to read the icon from the package
                if (values.Length == 2 && values[1] is Func<PackageReaderBase> lazyReader)
                {
                    try
                    {
                        PackageReaderBase reader = lazyReader(); // Always returns a new reader. That avoids using an already disposed one
                        if (reader is PackageArchiveReader par) // This reader is closed in BitmapImage events
                        {
                            var iconEntryNormalized = PathUtility.StripLeadingDirectorySeparators(
                                Uri.UnescapeDataString(iconUrl.Fragment)
                                    .Substring(1)); // Substring skips the '#' in the URI fragment
                            iconBitmapImage.StreamSource = par.GetEntry(iconEntryNormalized).Open();

                            iconBitmapImage.DecodeFailed += (sender, args) =>
                            {
                                reader.Dispose();
                                IconBitmapImage_DownloadOrDecodeFailed(sender, args);
                                AddToCache(iconUrl, defaultPackageIcon);
                            };
                            iconBitmapImage.DownloadFailed += (sender, args) =>
                            {
                                reader.Dispose();
                                IconBitmapImage_DownloadOrDecodeFailed(sender, args);
                                AddToCache(iconUrl, defaultPackageIcon);
                            };
                            iconBitmapImage.DownloadCompleted += (sender, args) =>
                            {
                                reader.Dispose();
                                IconBitmapImage_DownloadCompleted(sender, args);
                            };

                            imageResult = FinishImageProcessing(iconBitmapImage, iconUrl, defaultPackageIcon);
                        }
                        else // we cannot use the reader object
                        {
                            reader?.Dispose();
                            AddToCache(iconUrl, defaultPackageIcon);
                            imageResult = defaultPackageIcon;
                        }
                    }
                    catch (Exception)
                    {
                        AddToCache(iconUrl, defaultPackageIcon);
                        imageResult = defaultPackageIcon;
                    }
                }
                else // Identified an embedded icon URI, but we are unable to process it
                {
                    AddToCache(iconUrl, defaultPackageIcon);
                    imageResult = defaultPackageIcon;
                }
            }
            else
            {
                iconBitmapImage.UriSource = iconUrl;

                iconBitmapImage.DecodeFailed += IconBitmapImage_DownloadOrDecodeFailed;
                iconBitmapImage.DownloadFailed += IconBitmapImage_DownloadOrDecodeFailed;
                iconBitmapImage.DownloadCompleted += IconBitmapImage_DownloadCompleted;

                imageResult = FinishImageProcessing(iconBitmapImage, iconUrl, defaultPackageIcon);
            }

            return imageResult;
        }

        public BitmapSource FinishImageProcessing(BitmapImage iconBitmapImage, Uri iconUrl, BitmapSource defaultPackageIcon)
        {
            // Default cache policy: Per MSDN, satisfies a request for a resource either by using the cached copy of the resource or by sending a request
            // for the resource to the server. The action taken is determined by the current cache policy and the age of the content in the cache.
            // This is the cache level that should be used by most applications.
            iconBitmapImage.UriCachePolicy = RequestCacheIfAvailable;

            // Instead of scaling larger images and keeping larger image in memory, this makes it so we scale it down, and throw away the bigger image.
            // Only need to set this on one dimension, to preserve aspect ratio
            iconBitmapImage.DecodePixelWidth = DecodePixelWidth;

            BitmapSource image = null;
            try
            {
                iconBitmapImage.EndInit();
            }
            // if the URL is a file: URI (which actually happened!), we'll get an exception.
            // if the URL is a file: URI which is in an existing directory, but the file doesn't exist, we'll fail silently.
            catch (Exception)
            {
                iconBitmapImage = null;
            }
            finally
            {
                // store this bitmapImage in the bitmap image cache, so that other occurances can reuse the BitmapImage
                var cachedBitmapImage = iconBitmapImage ?? defaultPackageIcon;
                AddToCache(iconUrl, cachedBitmapImage);
                ErrorFloodGate.ReportAttempt();

                image = cachedBitmapImage;
            }

            return image;
        }

        private static void AddToCache(Uri iconUrl, BitmapSource iconBitmapImage)
        {
            string cacheKey = iconUrl == null ? string.Empty : iconUrl.ToString();
            AddToCache(cacheKey, iconBitmapImage);
        }

        private static void AddToCache(string cacheKey, BitmapSource iconBitmapImage)
        {
            var policy = new CacheItemPolicy
            {
                SlidingExpiration = TimeSpan.FromMinutes(10),
                RemovedCallback = CacheEntryRemoved
            };
            BitmapImageCache.Set(cacheKey, iconBitmapImage, policy);
        }

        private static void CacheEntryRemoved(CacheEntryRemovedArguments arguments)
        {

        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        private void IconBitmapImage_DownloadCompleted(object sender, EventArgs e)
        {
            var bitmapImage = sender as BitmapImage;
            if (!bitmapImage.IsFrozen)
            {
                bitmapImage.Freeze();
            }
        }

        private void IconBitmapImage_DownloadOrDecodeFailed(object sender, System.Windows.Media.ExceptionEventArgs e)
        {
            var bitmapImage = sender as BitmapImage;

            var uri = bitmapImage.UriSource;

            string cacheKey = uri == null ? string.Empty : uri.ToString();
            // Fix the bitmap image cache to have default package icon, if some other failure didn't already do that.            
            var cachedBitmapImage = BitmapImageCache.Get(cacheKey) as BitmapSource;
            if (cachedBitmapImage != Images.DefaultPackageIcon)
            {
                AddToCache(cacheKey, Images.DefaultPackageIcon);

                var webex = e.ErrorException as WebException;
                if (webex != null && FatalErrors.Any(c => webex.Status == c))
                {
                    ErrorFloodGate.ReportError();
                }
            }
        }

        /// <summary>
        /// NuGet Embedded Icon Uri verification
        /// </summary>
        /// <param name="iconUrl">An URI to test</param>
        /// <returns><c>true</c> if <c>iconUrl</c> is an URI to an embedded icon in a NuGet package</returns>
        public static bool IsEmbeddedIconUri(Uri iconUrl)
        {
            return iconUrl != null
                && iconUrl.IsAbsoluteUri
                && iconUrl.IsFile
                && !string.IsNullOrEmpty(iconUrl.Fragment)
                && iconUrl.Fragment.Length > 1;
        }
    }
}
