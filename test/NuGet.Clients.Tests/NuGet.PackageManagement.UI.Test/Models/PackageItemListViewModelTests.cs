// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.VisualStudio.Sdk.TestFramework;
using NuGet.PackageManagement.VisualStudio.Test;
using NuGet.Packaging;
using NuGet.Test.Utility;
using Test.Utility.Threading;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.PackageManagement.UI.Test
{
    [Collection(MockedVS.Collection)]
    public class PackageItemListViewModelTests : MockedVSCollectionTests, IClassFixture<LocalPackageSearchMetadataFixture>, IClassFixture<DispatcherThreadFixture>
    {
        private readonly LocalPackageSearchMetadataFixture _testData;
        private readonly PackageItemListViewModel _testInstance;
        private readonly ITestOutputHelper _output;

        public PackageItemListViewModelTests(
            GlobalServiceProvider globalServiceProvider,
            ITestOutputHelper output,
            LocalPackageSearchMetadataFixture testData)
            : base(globalServiceProvider)
        {
            globalServiceProvider.Reset();

            _testData = testData;
            _testInstance = new PackageItemListViewModel()
            {
                PackageReader = _testData.TestData.PackageReader,
            };
            _output = output;
        }

        [Fact]
        public void LocalSources_PackageReader_NotNull()
        {
            Assert.NotNull(_testInstance.PackageReader);

            Func<PackageReaderBase> func = _testInstance.PackageReader;

            PackageReaderBase reader = func();
            Assert.IsType(typeof(PackageArchiveReader), reader);
        }

        [Fact]
        public async Task IconUrl_WithMalformedUrlScheme_ReturnsDefaultInitallyAndFinally()
        {
            var iconUrl = new Uri("httphttphttp://fake.test/image.png");

            var packageItemListViewModel = new PackageItemListViewModel()
            {
                IconUrl = iconUrl
            };

            // initial result should be fetching and defaultpackageicon
            BitmapSource initialResult = packageItemListViewModel.IconBitmap;
            Assert.Equal(IconBitmapStatus.Fetching, packageItemListViewModel.BitmapStatus);
            Assert.Same(initialResult, Images.DefaultPackageIcon);

            BitmapSource result = await GetFinalIconBitmapAsync(packageItemListViewModel);
            VerifyImageResult(result, packageItemListViewModel.BitmapStatus);
            Assert.Equal(IconBitmapStatus.DefaultIconDueToWebExceptionBadNetwork, packageItemListViewModel.BitmapStatus);
        }

        [Fact]
        public async Task IconUrl_WhenFileNotFound_ReturnsDefault()
        {
            var iconUrl = new Uri(@"C:\path\to\image.png");

            var packageItemListViewModel = new PackageItemListViewModel()
            {
                IconUrl = iconUrl
            };

            BitmapSource result = await GetFinalIconBitmapAsync(packageItemListViewModel);

            VerifyImageResult(result, packageItemListViewModel.BitmapStatus);
            Assert.Equal(IconBitmapStatus.DefaultIconDueToWebExceptionBadNetwork, packageItemListViewModel.BitmapStatus);
        }

        [Fact]
        public async Task IconUrl_RelativeUri_ReturnsDefault()
        {
            // relative URIs are not supported in viewmodel.
            var iconUrl = new Uri("resources/testpackageicon.png", UriKind.Relative);
            var packageItemListViewModel = new PackageItemListViewModel()
            {
                IconUrl = iconUrl
            };

            BitmapSource result = await GetFinalIconBitmapAsync(packageItemListViewModel);

            VerifyImageResult(result, packageItemListViewModel.BitmapStatus);
            Assert.Equal(IconBitmapStatus.DefaultIconDueToRelativeUri, packageItemListViewModel.BitmapStatus);
        }

        [Fact]
        public async Task IconUrl_WithLocalPathAndColorProfile_LoadsImage()
        {
            // Prepare
            using (var testDir = TestDirectory.Create())
            {
                byte[] bytes;
                Assembly testAssembly = typeof(PackageItemListViewModelTests).Assembly;

                using (Stream sourceStream = testAssembly.GetManifestResourceStream("NuGet.PackageManagement.UI.Test.Resources.grayicc.png"))
                using (var memoryStream = new MemoryStream())
                {
                    sourceStream.CopyTo(memoryStream);
                    bytes = memoryStream.ToArray();
                }

                string grayiccImagePath = Path.Combine(testDir, "grayicc.png");
                File.WriteAllBytes(grayiccImagePath, bytes);

                var packageItemListViewModel = new PackageItemListViewModel()
                {
                    IconUrl = new Uri(grayiccImagePath, UriKind.Absolute)
                };

                // Act
                BitmapSource result = await GetFinalIconBitmapAsync(packageItemListViewModel);

                // Assert
                VerifyImageResult(result, packageItemListViewModel.BitmapStatus);
                Assert.Equal(IconBitmapStatus.DownloadedIcon, packageItemListViewModel.BitmapStatus);
            }
        }

        [Fact]
        public async Task IconUrl_WithValidImageUrl_FailsDownloadsImage_ReturnsDefault()
        {
            var iconUrl = new Uri("http://fake.test/image.png");

            var packageItemListViewModel = new PackageItemListViewModel()
            {
                IconUrl = iconUrl
            };

            BitmapSource result = await GetFinalIconBitmapAsync(packageItemListViewModel);

            VerifyImageResult(result, packageItemListViewModel.BitmapStatus);
            Assert.Equal(IconBitmapStatus.DefaultIconDueToWebExceptionOther, packageItemListViewModel.BitmapStatus);
        }

        [LocalOnlyTheory]
        [InlineData("icon.png", "icon.png", "icon.png", "")]
        [InlineData("folder/icon.png", "folder\\icon.png", "folder/icon.png", "folder")]
        [InlineData("folder\\icon.png", "folder\\icon.png", "folder\\icon.png", "folder")]
        public async Task IconUrl_EmbeddedIcon_HappyPath_LoadsImage(
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
                    iconFileTargetElement: fileTargetElement,
                    isRealImage: true);

                // prepare test
                var builder = new UriBuilder(new Uri(zipPath, UriKind.Absolute))
                {
                    Fragment = iconElement
                };

                var packageItemListViewModel = new PackageItemListViewModel()
                {
                    IconUrl = builder.Uri,
                    PackageReader = new Func<PackageReaderBase>(() => new PackageArchiveReader(zipPath))
                };

                _output.WriteLine($"ZipPath {zipPath}");
                _output.WriteLine($"File Exists {File.Exists(zipPath)}");
                _output.WriteLine($"Url {builder.Uri}");

                // Act
                BitmapSource result = await GetFinalIconBitmapAsync(packageItemListViewModel);

                // Assert
                _output.WriteLine($"result {result}");
                Assert.Equal(IconBitmapStatus.EmbeddedIcon, packageItemListViewModel.BitmapStatus);
                VerifyImageResult(result, packageItemListViewModel.BitmapStatus);
            }
        }

        [Fact]
        public async Task IconUrl_FileUri_LoadsImage()
        {
            // Prepare
            using (var testDir = TestDirectory.Create())
            {
                var imagePath = Path.Combine(testDir, "image.png");
                CreateNoisePngImage(path: imagePath);

                var packageItemListViewModel = new PackageItemListViewModel()
                {
                    IconUrl = new Uri(imagePath, UriKind.Absolute)
                };

                // Act
                BitmapSource result = await GetFinalIconBitmapAsync(packageItemListViewModel);

                // Assert
                VerifyImageResult(result, packageItemListViewModel.BitmapStatus);
                Assert.Equal(IconBitmapStatus.DownloadedIcon, packageItemListViewModel.BitmapStatus);
            }
        }

        [Theory]
        [InlineData("/")]
        [InlineData(@"\")]
        public async Task IconUrl_EmbeddedIcon_RelativeParentPath_ReturnsDefault(string separator)
        {
            using (var testDir = TestDirectory.Create())
            {
                // Create decoy nuget package
                var zipPath = Path.Combine(testDir.Path, "file.nupkg");
                CreateDummyPackage(zipPath);

                // prepare test
                var builder = new UriBuilder(new Uri(zipPath, UriKind.Absolute))
                {
                    Fragment = $"..{separator}icon.png"
                };

                var packageItemListViewModel = new PackageItemListViewModel()
                {
                    IconUrl = builder.Uri,
                    PackageReader = new Func<PackageReaderBase>(() => new PackageArchiveReader(zipPath))
                };

                // Act
                BitmapSource result = await GetFinalIconBitmapAsync(packageItemListViewModel);

                // Assert
                VerifyImageResult(result, packageItemListViewModel.BitmapStatus);
                Assert.Equal(IconBitmapStatus.DefaultIconDueToDecodingError, packageItemListViewModel.BitmapStatus);
            }
        }

        [Theory]
        [MemberData(nameof(EmbeddedTestData))]
        public void IsEmbeddedIconUri_Tests(Uri testUri, bool expectedResult)
        {
            var result = PackageItemListViewModel.IsEmbeddedIconUri(testUri);
            Assert.Equal(expectedResult, result);
        }

        public static IEnumerable<object[]> EmbeddedTestData()
        {
            Uri baseUri = new Uri(@"C:\path\to\package");
            var builder1 = new UriBuilder(baseUri)
            {
                Fragment = "    " // UriBuilder trims the string
            };
            var builder2 = new UriBuilder(baseUri)
            {
                Fragment = "icon.png"
            };
            var builder3 = new UriBuilder(baseUri)
            {
                Fragment = @"..\icon.png"
            };
            var builder4 = new UriBuilder(baseUri)
            {
                Fragment = string.Empty // implies that there's a Tag, but no value
            };
            var builder5 = new UriBuilder(baseUri)
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
                new object[]{ new Uri("https://sample.test/"), false },
                new object[]{ baseUri, false },
                new object[]{ new Uri("https://another.test/#"), false },
                new object[]{ new Uri("https://complimentary.test/#anchor"), false },
                new object[]{ new Uri("https://complimentary.test/?param"), false },
                new object[]{ new Uri("relative/path", UriKind.Relative), false },
            };
        }

        /// <summary>
        /// Tests the final bitmap returned by the view model, by waiting for the BitmapStatus to be "complete".
        /// </summary>
        private static async Task<BitmapSource> GetFinalIconBitmapAsync(PackageItemListViewModel packageItemListViewModel)
        {
            BitmapSource result = packageItemListViewModel.IconBitmap;
            int millisecondsToWait = 3000;
            while (!IconBitmapStatusUtility.GetIsCompleted(packageItemListViewModel.BitmapStatus) && millisecondsToWait >= 0)
            {
                await Task.Delay(250);
                millisecondsToWait -= 250;
            }

            result = packageItemListViewModel.IconBitmap;
            return result;
        }

        [Theory]
        [InlineData("icon.jpg", "icon.jpg", "icon.jpg", "")]
        [InlineData("icon2.jpg", "icon2.jpg", "icon2.jpg", "")]
        public async Task IconUrl_EmbeddedIcon_NotAnIcon_ReturnsDefault(
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
                    iconFileTargetElement: fileTargetElement,
                    isRealImage: false);

                // prepare test
                UriBuilder builder = new UriBuilder(new Uri(zipPath, UriKind.Absolute))
                {
                    Fragment = iconElement
                };

                var packageItemListViewModel = new PackageItemListViewModel()
                {
                    IconUrl = builder.Uri,
                    PackageReader = new Func<PackageReaderBase>(() => new PackageArchiveReader(zipPath))
                };

                _output.WriteLine($"ZipPath {zipPath}");
                _output.WriteLine($"File Exists {File.Exists(zipPath)}");
                _output.WriteLine($"Url {builder.Uri}");

                // Act
                BitmapSource result = await GetFinalIconBitmapAsync(packageItemListViewModel);

                VerifyImageResult(result, packageItemListViewModel.BitmapStatus);

                _output.WriteLine($"result {result}");
                string resultFormat = result != null ? result.Format.ToString() : "";
                _output.WriteLine($"Pixel format: {resultFormat}");

                // Assert
                Assert.Equal(IconBitmapStatus.DefaultIconDueToDecodingError, packageItemListViewModel.BitmapStatus);
            }
        }

        private static void VerifyImageResult(object result, IconBitmapStatus bitmapStatus)
        {
            Assert.NotNull(result);
            Assert.True(result is BitmapImage || result is CachedBitmap);
            var image = result as BitmapSource;
            Assert.NotNull(image);
            Assert.Equal(PackageItemListViewModel.DecodePixelWidth, image.PixelWidth);
            Assert.Equal(PackageItemListViewModel.DecodePixelWidth, image.PixelHeight);
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
            string iconFileTargetElement = "",
            bool isRealImage = true)
        {
            var dir = Path.GetDirectoryName(zipPath);
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

            if (isRealImage)
            {
                CreateNoisePngImage(iconPath);
            }
            else
            {
                File.WriteAllText(iconPath, "I am an image");
            }

            // Create nuget package
            using (var nuspecStream = new MemoryStream())
            using (FileStream nupkgStream = File.Create(zipPath))
            using (var writer = new StreamWriter(nuspecStream))
            {
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

            using (var fs = File.OpenWrite(path))
            {
                encoder.Save(fs);
            }
        }
    }
}
