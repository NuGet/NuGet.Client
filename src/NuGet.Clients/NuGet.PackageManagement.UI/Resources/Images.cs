// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NuGet.PackageManagement.UI
{
    internal static class Images
    {
        public static readonly BitmapSource DefaultPackageIcon;

        static Images()
        {
            // in VS, look up the icon via pack://application url
            if (Application.Current != null)
            {
                var image = new BitmapImage();
                image.BeginInit();

                // If the DLL name changes, this URI would need to change to match.
                image.UriSource = new Uri("pack://application:,,,/NuGet.PackageManagement.UI;component/Resources/packageicon.png");

                // Instead of scaling larger images and keeping larger image in memory, this makes it so we scale it down, and throw away the bigger image.
                // Only need to set this on one dimension, to preserve aspect ratio
                image.DecodePixelWidth = PackageItemListViewModel.DecodePixelWidth;

                image.EndInit();
                image.Freeze();
                DefaultPackageIcon = image;
            }
            else // for tests, don't actually load the icon, just use a 1x1 image.
            {
                BitmapSource image = BitmapSource.Create(
                    pixelWidth: 1,
                    pixelHeight: 1,
                    dpiX: 96.0,
                    dpiY: 96.0,
                    PixelFormats.Bgr32,
                    palette: null,
                    pixels: new byte[] { 0, 0, 255, 0 },
                    stride: 32);
                image.Freeze();
                DefaultPackageIcon = image;
            }
        }
    }
}
