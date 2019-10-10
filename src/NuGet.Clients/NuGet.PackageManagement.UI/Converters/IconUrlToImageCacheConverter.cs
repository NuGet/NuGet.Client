// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Cache;
using System.Runtime.Caching;
using System.Text;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using NuGet.Packaging;

namespace NuGet.PackageManagement.UI
{
    internal class IconUrlToImageCacheConverter : IValueConverter
    {
        private const int DecodePixelWidth = 32;

        // same URIs can reuse the bitmapImage that we've already used.
        private static readonly ObjectCache _bitmapImageCache = MemoryCache.Default;

        private static readonly WebExceptionStatus[] FatalErrors = new[]
        {
            WebExceptionStatus.ConnectFailure,
            WebExceptionStatus.RequestCanceled,
            WebExceptionStatus.ConnectionClosed,
            WebExceptionStatus.Timeout,
            WebExceptionStatus.UnknownError
        };

        private static readonly RequestCachePolicy RequestCacheIfAvailable = new RequestCachePolicy(RequestCacheLevel.CacheIfAvailable);

        private static readonly ErrorFloodGate _errorFloodGate = new ErrorFloodGate();

        public StringBuilder Messages { get; set; }

        private void LogMessage(string message)
        {
            Messages?.AppendFormat("IconUrlToImageCacheConverter: {0}{1}", message, Environment.NewLine);
        }

        // We bind to a BitmapImage instead of a Uri so that we can control the decode size, since we are displaying 32x32 images, while many of the images are 128x128 or larger.
        // This leads to a memory savings.
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            LogMessage($"Convert Params: {value.ToString()}, {targetType.ToString()}, {parameter.ToString()}, {culture.ToString()}");
            var iconUrl = value as Uri;
            var defaultPackageIcon = parameter as BitmapSource;
            if (iconUrl == null)
            {
                LogMessage($"Convert iconUrl is null");
                return null;
            }

            var cachedBitmapImage = _bitmapImageCache.Get(iconUrl.ToString()) as BitmapSource;
            if (cachedBitmapImage != null)
            {
                LogMessage($"Convert Cache Hit: {cachedBitmapImage.ToString()}");
                return cachedBitmapImage;
            }

            // Some people run on networks with internal NuGet feeds, but no access to the package images on the internet.
            // This is meant to detect that kind of case, and stop spamming the network, so the app remains responsive.
            if (_errorFloodGate.IsOpen)
            {
                LogMessage($"Convert errorFloodGate open");
                return defaultPackageIcon;
            }

            var iconBitmapImage = new BitmapImage();
            iconBitmapImage.BeginInit();

            var markIdx = iconUrl.OriginalString.LastIndexOf("#");

            BitmapSource imageResult = null;

            if (iconUrl.IsAbsoluteUri && iconUrl.IsFile && markIdx >= 0)
            {
                LogMessage("Convert EmbeddedIcon");
                try
                {
                    using (var par = new PackageArchiveReader(Uri.UnescapeDataString(iconUrl.LocalPath)))
                    {
                        var iconEntry = Uri.UnescapeDataString(iconUrl.Fragment).Substring(1);
                        var zipEntry = par.GetEntry(iconEntry);
                        iconBitmapImage.StreamSource = zipEntry.Open();
                        imageResult = FinishImageProcessing(iconBitmapImage, iconUrl, defaultPackageIcon);
                        LogMessage($"Converd EmbeddedIcon PAR: {par}");
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"Convert EmbeddedIcon Exception {ex.Message}");
                    AddToCache(iconUrl, defaultPackageIcon);
                    imageResult = defaultPackageIcon;
                }
            }
            else
            {
                LogMessage("Convert NormalIcon");
                iconBitmapImage.UriSource = iconUrl;
                imageResult = FinishImageProcessing(iconBitmapImage, iconUrl, defaultPackageIcon);
            }

            LogMessage($"Convert result: {imageResult}");
            return imageResult;
        }

        public BitmapSource FinishImageProcessing(BitmapImage iconBitmapImage, Uri iconUrl, BitmapSource defaultPackageIcon)
        {
            LogMessage("Finish: Entering");
            // Default cache policy: Per MSDN, satisfies a request for a resource either by using the cached copy of the resource or by sending a request
            // for the resource to the server. The action taken is determined by the current cache policy and the age of the content in the cache.
            // This is the cache level that should be used by most applications.
            iconBitmapImage.UriCachePolicy = RequestCacheIfAvailable;

            // Instead of scaling larger images and keeping larger image in memory, this makes it so we scale it down, and throw away the bigger image.
            // Only need to set this on one dimension, to preserve aspect ratio
            iconBitmapImage.DecodePixelWidth = DecodePixelWidth;

            iconBitmapImage.DecodeFailed += IconBitmapImage_DownloadOrDecodeFailed;
            iconBitmapImage.DownloadFailed += IconBitmapImage_DownloadOrDecodeFailed;
            iconBitmapImage.DownloadCompleted += IconBitmapImage_DownloadCompleted;

            BitmapSource image = null;
            try
            {
                iconBitmapImage.EndInit();
                LogMessage("Finish: After EndInit");
            }
            // if the URL is a file: URI (which actually happened!), we'll get an exception.
            // if the URL is a file: URI which is in an existing directory, but the file doesn't exist, we'll fail silently.
            catch (Exception ex)
            {
                iconBitmapImage = null;
                LogMessage("Finish: Image set to null");
                LogMessage($"Finish: exception: {ex.Message}");
            }
            finally
            {
                // store this bitmapImage in the bitmap image cache, so that other occurances can reuse the BitmapImage
                LogMessage($"Finish: Pre final image {iconBitmapImage}");
                var cachedBitmapImage = iconBitmapImage ?? defaultPackageIcon;
                AddToCache(iconUrl, cachedBitmapImage);
                LogMessage($"Finish: Added to cache {iconUrl.ToString()} {cachedBitmapImage.ToString()}");

                _errorFloodGate.ReportAttempt();

                image = cachedBitmapImage;
            }

            return image;
        }

        private static void AddToCache(Uri iconUrl, BitmapSource iconBitmapImage)
        {
            var policy = new CacheItemPolicy
            {
                SlidingExpiration = TimeSpan.FromMinutes(10),
                RemovedCallback = CacheEntryRemoved
            };
            _bitmapImageCache.Set(iconUrl.ToString(), iconBitmapImage, policy);
        }

        private static void CacheEntryRemoved(CacheEntryRemovedArguments arguments)
        {

        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        private void IconBitmapImage_DownloadCompleted(object sender, EventArgs e)
        {
            LogMessage("Raising IconBitmapImage_DownloadCompleted");
            var bitmapImage = sender as BitmapImage;
            if (!bitmapImage.IsFrozen)
            {
                bitmapImage.Freeze();
            }
        }

        private void IconBitmapImage_DownloadOrDecodeFailed(object sender, System.Windows.Media.ExceptionEventArgs e)
        {
            LogMessage("Raising IconBitmapImage_DownloadOrDecodeFailed");
            LogMessage($"IconBitmapImage_DownloadOrDecodeFailed: {e.ErrorException.Message}");
            var bitmapImage = sender as BitmapImage;

            var uri = bitmapImage.UriSource;

            string cacheKey = uri != null ? uri.ToString() : string.Empty;
            // Fix the bitmap image cache to have default package icon, if some other failure didn't already do that.            
            var cachedBitmapImage = _bitmapImageCache.Get(cacheKey) as BitmapSource;
            if (cachedBitmapImage != Images.DefaultPackageIcon)
            {
                AddToCache(bitmapImage.UriSource, Images.DefaultPackageIcon);

                var webex = e.ErrorException as WebException;
                if (webex != null && FatalErrors.Any(c => webex.Status == c))
                {
                    _errorFloodGate.ReportError();
                }
            }
        }
    }
}
