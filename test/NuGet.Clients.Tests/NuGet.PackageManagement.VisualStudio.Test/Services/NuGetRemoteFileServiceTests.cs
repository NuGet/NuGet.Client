// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using Microsoft.ServiceHub.Framework;
using Microsoft.ServiceHub.Framework.Services;
using Moq;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.PackageManagement.VisualStudio.Test
{
    using ExceptionUtility = global::Test.Utility.ExceptionUtility;

    public class NuGetRemoteFileServiceTests
    {
        [Fact]
        public void Constructor_WhenServiceBrokerIsNull_Throws()
        {
            Exception exception = Assert.ThrowsAny<Exception>(
                () => new NuGetRemoteFileService(
                    default(ServiceActivationOptions),
                    serviceBroker: null,
                    new AuthorizationServiceClient(Mock.Of<IAuthorizationService>())));

            ExceptionUtility.AssertMicrosoftAssumesException(exception);
        }

        [Fact]
        public void Constructor_WhenAuthorizationServiceClientIsNull_Throws()
        {
            Exception exception = Assert.ThrowsAny<Exception>(
                () => new NuGetRemoteFileService(
                    default(ServiceActivationOptions),
                    Mock.Of<IServiceBroker>(),
                    authorizationServiceClient: null));

            ExceptionUtility.AssertMicrosoftAssumesException(exception);
        }

        [Fact]
        public async void AddIconToCache_WhenAdded_IsFound()
        {
            using (TestDirectory testDirectory = TestDirectory.Create())
            {
                string fileContents = "testFileContents";
                string filePath = Path.Combine(testDirectory.Path, "testFile.txt");
                File.WriteAllText(filePath, fileContents);
                var remoteFileService = new NuGetRemoteFileService(
                        default(ServiceActivationOptions),
                        Mock.Of<IServiceBroker>(),
                        new AuthorizationServiceClient(Mock.Of<IAuthorizationService>()));
                var packageIdentity = new PackageIdentity("AddIconToCache_WhenAdded_IsFound", NuGetVersion.Parse("1.0.0"));
                NuGetRemoteFileService.AddIconToCache(packageIdentity, new Uri(filePath));

                Stream iconStream = await remoteFileService.GetPackageIconAsync(packageIdentity, CancellationToken.None);

                Assert.NotNull(iconStream);
                Assert.Equal(fileContents.Length, iconStream.Length);
            }
        }

        [Fact]
        public async void AddIconToCache_WhenAddedTwice_UsesSecond()
        {
            using (TestDirectory testDirectory = TestDirectory.Create())
            {
                string fileContents = "testFileContents";
                string filePath = Path.Combine(testDirectory.Path, "testFile.txt");
                File.WriteAllText(filePath, fileContents);

                string fileContents2 = fileContents + fileContents;
                string filePath2 = Path.Combine(testDirectory.Path, "testFile2.txt");
                File.WriteAllText(filePath2, fileContents2);

                var remoteFileService = new NuGetRemoteFileService(
                        default(ServiceActivationOptions),
                        Mock.Of<IServiceBroker>(),
                        new AuthorizationServiceClient(Mock.Of<IAuthorizationService>()));
                var packageIdentity = new PackageIdentity("AddIconToCache_WhenAddedTwice_UsesSecond", NuGetVersion.Parse("1.0.0"));
                NuGetRemoteFileService.AddIconToCache(packageIdentity, new Uri(filePath));
                NuGetRemoteFileService.AddIconToCache(packageIdentity, new Uri(filePath2));

                Stream iconStream = await remoteFileService.GetPackageIconAsync(packageIdentity, CancellationToken.None);

                Assert.NotNull(iconStream);
                Assert.Equal(fileContents2.Length, iconStream.Length);
            }
        }

        [Fact]
        public async void AddIconToCache_WhenMissing_IsNotFound()
        {
            var remoteFileService = new NuGetRemoteFileService(
                    default(ServiceActivationOptions),
                    Mock.Of<IServiceBroker>(),
                    new AuthorizationServiceClient(Mock.Of<IAuthorizationService>()));
            var packageIdentity = new PackageIdentity("AddIconToCache_WhenMissing_IsNotFound", NuGetVersion.Parse("1.0.0"));
            Stream iconStream = await remoteFileService.GetPackageIconAsync(packageIdentity, CancellationToken.None);

            Assert.Null(iconStream);
        }

        [Theory]
        [InlineData("icon.png", "icon.png", "icon.png", "")]
        public async void AddLicenseToCache_WhenAdded_IsFound(
            string iconElement,
            string iconFileLocation,
            string fileSourceElement,
            string fileTargetElement)
        {
            using (TestDirectory testDir = TestDirectory.Create())
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
                var builder = new UriBuilder(new Uri(zipPath, UriKind.Absolute))
                {
                    Fragment = iconElement
                };

                var remoteFileService = new NuGetRemoteFileService(
                        default(ServiceActivationOptions),
                        Mock.Of<IServiceBroker>(),
                        new AuthorizationServiceClient(Mock.Of<IAuthorizationService>()));
                var packageIdentity = new PackageIdentity("AddLicenseToCache_WhenAdded_IsFound", NuGetVersion.Parse("1.0.0"));
                NuGetRemoteFileService.AddLicenseToCache(packageIdentity, builder.Uri);

                Stream licenseStream = await remoteFileService.GetEmbeddedLicenseAsync(packageIdentity, CancellationToken.None);

                Assert.NotNull(licenseStream);
                Assert.Equal(StreamContents.Length, licenseStream.Length);
            }
        }

        [Theory]
        [InlineData("icon.png", "icon.png", "icon.png", "")]
        public async void AddLicenseToCache_WhenAddedTwice_UsesSecond(
            string iconElement,
            string iconFileLocation,
            string fileSourceElement,
            string fileTargetElement)
        {
            using (TestDirectory testDir = TestDirectory.Create())
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
                var builder = new UriBuilder(new Uri(zipPath, UriKind.Absolute))
                {
                    Fragment = iconElement
                };

                var remoteFileService = new NuGetRemoteFileService(
                        default(ServiceActivationOptions),
                        Mock.Of<IServiceBroker>(),
                        new AuthorizationServiceClient(Mock.Of<IAuthorizationService>()));
                var packageIdentity = new PackageIdentity("AddLicenseToCache_WhenAdded_IsFound", NuGetVersion.Parse("1.0.0"));
                NuGetRemoteFileService.AddLicenseToCache(packageIdentity, new Uri(builder.Uri.ToString() + "more"));
                NuGetRemoteFileService.AddLicenseToCache(packageIdentity, builder.Uri);

                Stream licenseStream = await remoteFileService.GetEmbeddedLicenseAsync(packageIdentity, CancellationToken.None);

                Assert.NotNull(licenseStream);
                Assert.Equal(StreamContents.Length, licenseStream.Length);
            }
        }

        [Fact]
        public async void AddLicenseToCache_WhenMissing_IsNotFound()
        {
            var remoteFileService = new NuGetRemoteFileService(
                    default(ServiceActivationOptions),
                    Mock.Of<IServiceBroker>(),
                    new AuthorizationServiceClient(Mock.Of<IAuthorizationService>()));
            var packageIdentity = new PackageIdentity("AddLicenseToCache_WhenMissing_IsNotFound", NuGetVersion.Parse("1.0.0"));
            Stream licenseStream = await remoteFileService.GetEmbeddedLicenseAsync(packageIdentity, CancellationToken.None);

            Assert.Null(licenseStream);
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

            File.WriteAllText(iconPath, StreamContents);

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

        private static string StreamContents = "I am an image";
    }
}
