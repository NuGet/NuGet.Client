// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NuGet.Packaging;
using NuGet.Test.Utility;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.PackageManagement.UI.Test
{
    public class IconUrlToImageCacheConverterTests
    {
        private static readonly ImageSource DefaultPackageIcon;
        private readonly ITestOutputHelper output;

        static IconUrlToImageCacheConverterTests()
        {
            DefaultPackageIcon = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgr24, null, new byte[3] { 0, 0, 0 }, 3);
            DefaultPackageIcon.Freeze();
        }

        public IconUrlToImageCacheConverterTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void Convert_WithMalformedUrlScheme_ReturnsDefault()
        {
            var iconUrl = new Uri("ttp://fake.com/image.png");

            var converter = new IconUrlToImageCacheConverter();

            var image = converter.Convert(
                values: new object[] { iconUrl, DependencyProperty.UnsetValue },
                targetType: null,
                parameter: DefaultPackageIcon,
                culture: null);

            Assert.Same(DefaultPackageIcon, image);
        }

        [Fact]
        public void Convert_WhenFileNotFound_ReturnsDefault()
        {
            var iconUrl = new Uri(@"C:\path\to\image.png");

            var converter = new IconUrlToImageCacheConverter();

            var image = converter.Convert(
                values: new object[] { iconUrl, DependencyProperty.UnsetValue },
                targetType: null,
                parameter: DefaultPackageIcon,
                culture: null);

            Assert.Same(DefaultPackageIcon, image);
        }

        [Fact]
        public void Convert_WithLocalPath_LoadsImage()
        {
            var iconUrl = new Uri(@"resources/packageicon.png", UriKind.Relative);

            var converter = new IconUrlToImageCacheConverter();

            var image = converter.Convert(
                values: new object[] { iconUrl, DependencyProperty.UnsetValue },
                targetType: null,
                parameter: DefaultPackageIcon,
                culture: null) as BitmapImage;

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
                values: new object[] { iconUrl, DependencyProperty.UnsetValue },
                targetType: null,
                parameter: DefaultPackageIcon,
                culture: null) as BitmapImage;

            Assert.NotNull(image);
            Assert.NotSame(DefaultPackageIcon, image);
            Assert.Equal(iconUrl, image.UriSource);
        }

        [Theory(Skip = "Runs only on Windows Desktop with WPF support")]
        [InlineData("icon.png", "icon.png", "icon.png", "")]
        [InlineData("folder/icon.png", "folder\\icon.png", "folder/icon.png", "folder")]
        [InlineData("folder\\icon.png", "folder\\icon.png", "folder\\icon.png", "folder")]
        public void Convert_EmbeddedIcon_HappyPath_LoadsImage(
            string iconElement,
            string iconFileLocation,
            string fileSourceElement,
            string fileTargetElement)
        {
            using (var testDir = TestDirectory.Create())
            {
                // Create decoy nuget package
                var zipPath = Path.Combine(testDir.Path, "file.nupkg");
                CreateDummyPackage(
                    zipPath: zipPath,
                    iconName: iconElement,
                    iconFile: iconFileLocation,
                    iconFileSourceElement: fileSourceElement,
                    iconFileTargetElement: fileTargetElement);

                // prepare test
                var converter = new IconUrlToImageCacheConverter();

                UriBuilder builder = new UriBuilder(new Uri(zipPath, UriKind.Absolute))
                {
                    Fragment = iconElement
                };

                output.WriteLine($"ZipPath {zipPath}");
                output.WriteLine($"File Exists {File.Exists(zipPath)}");
                output.WriteLine($"Url {builder.Uri.ToString()}");

                // Act
                var result = converter.Convert(
                    values: new object[]
                    {
                        builder.Uri,
                        new Func<PackageReaderBase>(() => new Packaging.PackageArchiveReader(zipPath))
                    },
                    targetType: null,
                    parameter: DefaultPackageIcon,
                    culture: null);

                var image = result as BitmapImage;

                output.WriteLine($"result {result.ToString()}");

                // Assert
                Assert.NotNull(result);
                Assert.NotSame(DefaultPackageIcon, result);
                Assert.Equal(32, image.PixelWidth);
            }
        }

        [Fact]
        public void Convert_FileUri_LoadsImage()
        {
            // Prepare
            var converter = new IconUrlToImageCacheConverter();

            using (var testDir = TestDirectory.Create())
            {
                var imagePath = Path.Combine(testDir, "image.png");
                CreateNoisePngImage(path: imagePath);

                var uri = new Uri(imagePath, UriKind.Absolute);

                // Act
                var result = converter.Convert(
                    values: new object[] { uri, DependencyProperty.UnsetValue },
                    targetType: null,
                    parameter: DefaultPackageIcon,
                    culture: null);

                var image = result as BitmapImage;
                
                // Assert
                Assert.NotNull(result);
                Assert.NotSame(DefaultPackageIcon, result);
                Assert.Equal(32, image.PixelWidth);
            }
        }

        [Theory]
        [InlineData(@"/")]
        [InlineData(@"\")]
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
                    values: new object[] { builder.Uri, new Func<PackageReaderBase>(() => new PackageArchiveReader(zipPath)) },
                    targetType: null,
                    parameter: DefaultPackageIcon,
                    culture: null);

                // Assert
                Assert.NotNull(result);
                Assert.Same(DefaultPackageIcon, result);
            }
        }

        [Theory]
        [MemberData(nameof(TestData))]
        public void IsEmbeddedIconUri_Tests(Uri testUri, bool expectedResult)
        {
            var result = IconUrlToImageCacheConverter.IsEmbeddedIconUri(testUri);
            Assert.Equal(expectedResult, result);
        }

        public static IEnumerable<object[]> TestData()
        {
            Uri baseUri = new Uri(@"C:\path\to\package");
            UriBuilder builder1 = new UriBuilder(baseUri)
            {
                Fragment = "    " // UriBuilder trims the string
            };
            UriBuilder builder2 = new UriBuilder(baseUri)
            {
                Fragment = "icon.png"
            };
            UriBuilder builder3 = new UriBuilder(baseUri)
            {
                Fragment = @"..\icon.png"
            };
            UriBuilder builder4 = new UriBuilder(baseUri)
            {
                Fragment = string.Empty // implies that there's a Tag, but no value
            };
            UriBuilder builder5 = new UriBuilder(baseUri)
            {
                Query = "aParam"
            };

            return new List<object[]>
            {
                new object[]{ builder1.Uri, false },
                new object[]{ builder2.Uri, true },
                new object[]{ builder3.Uri, true },
                new object[]{ builder4.Uri, false },
                new object[]{ builder5.Uri, false },
                new object[]{ new Uri("https://sample.uri/"), false },
                new object[]{ baseUri, false },
                new object[]{ new Uri("https://another.uri/#"), false },
                new object[]{ new Uri("https://complimentary.uri/#anchor"), false },
                new object[]{ new Uri("https://complimentary.uri/?param"), false },
                new object[]{ new Uri("relative/path", UriKind.Relative), false },
            };
        }


        /// <summary>
        /// Creates a NuGet package with .nupkg extension and with a PNG image named "icon.png"
        /// </summary>
        /// <param name="path">Path to NuGet package</param>
        /// <param name="iconName">Icon filename with .png extension</param>
        private static void CreateDummyPackage(
            string zipPath,
            string iconName = "icon.png",
            string iconFile = "icon.png",
            string iconFileSourceElement = "icon.png",
            string iconFileTargetElement = "")
        {
            var dir =  Path.GetDirectoryName(zipPath);
            var holdDir = "pkg";
            var folderPath = Path.Combine(dir, holdDir);

            // base dir
            Directory.CreateDirectory(folderPath);

            // create nuspec
            var nuspec = NuspecBuilder.Create()
                .WithIcon(iconName)
                .WithFile(iconFileSourceElement, iconFileTargetElement);

            // create png image
            var iconPath = Path.Combine(folderPath, iconFile);
            var iconDir = Path.GetDirectoryName(iconPath);
            Directory.CreateDirectory(iconDir);
            CreateNoisePngImage(iconPath);

            // Create nuget package
            using (var nuspecStream = new MemoryStream())
            using (FileStream nupkgStream = File.Create(zipPath))
            {
                var writer = new StreamWriter(nuspecStream);
                nuspec.Write(writer);
                writer.Flush();
                nuspecStream.Position = 0;
                var pkgBuilder = new PackageBuilder(stream: nuspecStream, basePath: folderPath);
                pkgBuilder.Save(nupkgStream);
            }
        }

        /// <summary>
        /// Creates a PNG image with random pixels
        /// </summary>
        /// <param name="path">Filename in which the image is created</param>
        /// <param name="w">Image width in pixels</param>
        /// <param name="h">Image height in pixels</param>
        /// <param name="dpiX">Horizontal Dots (pixels) Per Inch in the image</param>
        /// <param name="dpiY">Vertical Dots (pixels) Per Inch in the image</param>
        private static void CreateNoisePngImage(string path, int w = 128, int h = 128, int dpiX = 96, int dpiY = 96)
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

            BitmapEncoder encoder = new PngBitmapEncoder();

            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using(var fs = File.OpenWrite(path))
            {
                encoder.Save(fs);
            }
        }
    }
}
