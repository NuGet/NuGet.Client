// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
        public void Convert_EmbeddedIcon_HappyPath_LoadsImage()
        {
            using (var testDir = TestDirectory.Create())
            {
                // Create decoy nuget package
                var zipPath = Path.Combine(testDir.Path, "file.nupkg");
                CreateDummyPackage(zipPath);

                // prepare test
                var converter = new IconUrlToImageCacheConverter();
                UriBuilder builder = new UriBuilder(new Uri(zipPath, UriKind.Absolute))
                {
                    Fragment = "icon.png"
                };
                Console.WriteLine(builder.Uri.ToString());

                // Act
                var result = converter.Convert(
                    builder.Uri,
                    typeof(ImageSource),
                    DefaultPackageIcon,
                    Thread.CurrentThread.CurrentCulture) as BitmapImage;

                // Assert
                Assert.NotNull(result);
                Assert.NotSame(DefaultPackageIcon, result);
                Assert.Equal(32, result.PixelWidth);
            }
        }

        [InlineData(@"/")]
        [InlineData(@"\")]
        [Theory]
        public void Convert_EmbeddedIcon_RelativeParentPath_ReturnsDefault(string separator)
        {
            using (var testDir = TestDirectory.Create())
            {
                // Create decoy nuget package
                var zipPath = Path.Combine(testDir.Path, "file.nupkg");
                CreateDummyPackage(zipPath);

                // prepare test
                var converter = new IconUrlToImageCacheConverter();
                UriBuilder builder = new UriBuilder(new Uri(zipPath, UriKind.Absolute))
                {
                    Fragment = $@"..{separator}icon.png"
                };

                // Act
                var result = converter.Convert(
                    builder.Uri,
                    typeof(ImageSource),
                    DefaultPackageIcon,
                    Thread.CurrentThread.CurrentCulture) as BitmapImage;

                // Assert
                Assert.NotNull(result);
                Assert.Same(DefaultPackageIcon, result);
            }
        }

        /// <summary>
        /// Creates a dummy zip file with .nupkg extension and with a PNG image named "icon.png"
        /// </summary>
        /// <param name="path">Final path to the dummy .nupkg</param>
        /// <param name="iconName">Icon filename with .png extension</param>
        private void CreateDummyPackage(string zipPath, string iconName = "icon.png")
        {
            var dir =  Path.GetDirectoryName(zipPath);
            var holdDir = Path.GetRandomFileName();
            var folderPath = Path.Combine(dir, holdDir);

            Directory.CreateDirectory(folderPath);

            var iconPath = Path.Combine(folderPath, iconName);
            CreateNoisePngImage(iconPath);

            // Create decoy nuget package
            ZipFile.CreateFromDirectory(folderPath, zipPath);
        }

        /// <summary>
        /// Creates a PNG image with random pixels
        /// </summary>
        /// <param name="path">Filename in which the image is created</param>
        /// <param name="w">Image width in pixels</param>
        /// <param name="h">Image height in pixels</param>
        /// <param name="dpiX">Horizontal Dots (pixels) Per Inch in the image</param>
        /// /// <param name="dpiX">Vertical Dots (pixels) Per Inch in the image</param>
        private void CreateNoisePngImage(string path, int w = 128, int h = 128, int dpiX = 96, int dpiY = 96)
        {
            // Create PNG image with noise
            var fmt = PixelFormats.Bgr32;

            // a row of pixels
            int stride = (w * fmt.BitsPerPixel);
            var data = new byte[stride * h];

            // Random pixels
            Random rnd = new Random();
            rnd.NextBytes(data);

            BitmapSource bitmap = BitmapSource.Create(w, h,
                dpiX, dpiY,
                fmt,
                null, data, stride);

            BitmapEncoder enconder = new PngBitmapEncoder();

            enconder.Frames.Add(BitmapFrame.Create(bitmap));

            using(var fs = File.OpenWrite(path))
            {
                enconder.Save(fs);
            }
        }
    }
}
