// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Lucene.Net.Util;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.PackageManagement.UI.Test
{
    public class IconUrlToImageCacheConverterTests
    {
        private static readonly ImageSource DefaultPackageIcon;

        static IconUrlToImageCacheConverterTests()
        {
            DefaultPackageIcon = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgr24, null, new byte[3] { 0, 0, 0 }, 3);
            DefaultPackageIcon.Freeze();
        }

        [Fact]
        public void Convert_WithMalformedUrlScheme_ReturnsDefault()
        {
            var iconUrl = new Uri("ttp://fake.com/image.png");

            var converter = new IconUrlToImageCacheConverter();

            var image = converter.Convert(
                iconUrl,
                typeof(ImageSource),
                DefaultPackageIcon,
                Thread.CurrentThread.CurrentCulture);

            Assert.Same(DefaultPackageIcon, image);
        }

        [Fact]
        public void Convert_WhenFileNotFound_ReturnsDefault()
        {
            var iconUrl = new Uri(@"C:\path\to\image.png");

            var converter = new IconUrlToImageCacheConverter();

            var image = converter.Convert(
                iconUrl,
                typeof(ImageSource),
                DefaultPackageIcon,
                Thread.CurrentThread.CurrentCulture);

            Assert.Same(DefaultPackageIcon, image);
        }

        [Fact]
        public void Convert_WithLocalPath_LoadsImage()
        {
            var iconUrl = new Uri(@"resources/packageicon.png", UriKind.Relative);

            var converter = new IconUrlToImageCacheConverter();

            var image = converter.Convert(
                iconUrl,
                typeof(ImageSource),
                DefaultPackageIcon,
                Thread.CurrentThread.CurrentCulture) as BitmapImage;

            Assert.NotNull(image);
            Assert.NotSame(DefaultPackageIcon, image);
            Assert.Equal(iconUrl, image.UriSource);
        }

        [Fact(Skip="Fails on CI. Tracking issue: https://github.com/NuGet/Home/issues/2474")]
        public void Convert_WithValidImageUrl_DownloadsImage()
        {
            var iconUrl = new Uri("http://fake.com/image.png");

            var converter = new IconUrlToImageCacheConverter();

            var image = converter.Convert(
                iconUrl,
                typeof(ImageSource),
                DefaultPackageIcon,
                Thread.CurrentThread.CurrentCulture) as BitmapImage;

            Assert.NotNull(image);
            Assert.NotSame(DefaultPackageIcon, image);
            Assert.Equal(iconUrl, image.UriSource);
        }

        [Fact]
        public void Convert_EmbeddedIcon_LoadsImage()
        {
            // Create PNG image with noise
            var fmt = PixelFormats.Bgr32;
            int w = 128, h = 128;
            int dpiX = 96, dpiY = 96;

            // a row of pixels
            int stride = (w * fmt.BitsPerPixel );
            var data = new byte[ stride * h ];

            // Random pixels
            Random rnd = new Random();
            rnd.NextBytes(data);

            BitmapSource bitmap = BitmapSource.Create(w, h,
                dpiX, dpiY,
                fmt,
                null, data, stride);

            BitmapEncoder enconder = new PngBitmapEncoder();

            using (var testDir = TestDirectory.Create())
            {
                enconder.Frames.Add(BitmapFrame.Create(bitmap));

                var zipPath = Path.Combine(testDir.Path, "file.nupkg");
                var iconName = "icon.png";

                // zip
                using (var zipStream = new FileStream(zipPath, FileMode.Open))
                {
                    using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create))
                    {
                        var entry = zip.CreateEntry(iconName);
                        using (var fs = entry.Open())
                        {
                            enconder.Save(fs);
                        }
                    }
                }

                // prepare test
                var converter = new IconUrlToImageCacheConverter();
                var uri = new Uri(string.Format("{0}!{1}", zipPath, iconName), UriKind.Absolute);

                var result = converter.Convert(
                    uri,
                    typeof(ImageSource),
                    DefaultPackageIcon,
                    Thread.CurrentThread.CurrentCulture) as BitmapImage;

                Assert.Equal(w, result.PixelWidth);
            }
        }
    }
}
