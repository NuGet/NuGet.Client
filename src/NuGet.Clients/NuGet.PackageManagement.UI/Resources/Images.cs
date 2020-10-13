// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Windows;
using System.Windows.Media.Imaging;

namespace NuGet.PackageManagement.UI
{
    internal static class Images
    {
        public static readonly BitmapImage DefaultPackageIcon;

        static Images()
        {
            // in VS, look up the icon via pack://application url
            if (Application.Current != null)
            {
                DefaultPackageIcon = new BitmapImage();
                DefaultPackageIcon.BeginInit();

                // If the DLL name changes, this URI would need to change to match.
                DefaultPackageIcon.UriSource = new Uri("pack://application:,,,/NuGet.PackageManagement.UI;component/Resources/packageicon.png");

                // Instead of scaling larger images and keeping larger image in memory, this makes it so we scale it down, and throw away the bigger image.
                // Only need to set this on one dimension, to preserve aspect ratio
                DefaultPackageIcon.DecodePixelWidth = 32;

                DefaultPackageIcon.EndInit();
                DefaultPackageIcon.Freeze();
            }
            else // for tests, don't actually load the icon.
            {
                DefaultPackageIcon = null;
            }
        }
    }
}
