// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using NuGet.Packaging.Core;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class PackagePathResolverTests
    {
        private const string InMemoryRootDirectory = ".";
        private static readonly PackageIdentity PackageIdentity = new PackageIdentity("PackageA", new NuGetVersion("1.0.0.0-BETA"));

        [Fact]
        public void Constructor_ThrowsForNullRootDirectory()
        {
            // Arrange & Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => new PackagePathResolver(
                rootDirectory: null,
                useSideBySidePaths: true));
            Assert.Equal("rootDirectory", exception.ParamName);
        }

        [Fact]
        public void Constructor_ThrowsForEmptyRootDirectory()
        {
            // Arrange & Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => new PackagePathResolver(
                rootDirectory: string.Empty,
                useSideBySidePaths: true));
            Assert.Equal("rootDirectory", exception.ParamName);
        }

        [Theory]
        [InlineData(true, "PackageA.1.0.0.0-BETA")]
        [InlineData(false, "PackageA")]
        public void GetPackageDirectoryName_ReturnsPackageDirectoryName(bool useSideBySidePaths, string expected)
        {
            // Arrange
            var target = new PackagePathResolver(
                rootDirectory: InMemoryRootDirectory,
                useSideBySidePaths: useSideBySidePaths);

            // Act
            var actual = target.GetPackageDirectoryName(PackageIdentity);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(true, "PackageA.1.0.0.0-BETA.nupkg")]
        [InlineData(false, "PackageA.nupkg")]
        public void GetPackageFileName_ReturnsPackageFileName(bool useSideBySidePaths, string expected)
        {
            // Arrange
            var target = new PackagePathResolver(
                rootDirectory: InMemoryRootDirectory,
                useSideBySidePaths: useSideBySidePaths);

            // Act
            var actual = target.GetPackageFileName(PackageIdentity);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(true, "PackageA.packagedownload.marker")]
        [InlineData(false, "PackageA.packagedownload.marker")]
        public void GetPackageDownloadMarkerFileName_ReturnsPackageDownloadMarkerFileName(
            bool useSideBySidePaths,
            string expected)
        {
            var target = new PackagePathResolver(InMemoryRootDirectory, useSideBySidePaths);

            var actual = target.GetPackageDownloadMarkerFileName(PackageIdentity);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(true, "PackageA.nuspec")]
        [InlineData(false, "PackageA.nuspec")]
        public void GetManifestFileName_ReturnsManifestFileName(bool useSideBySidePaths, string expected)
        {
            // Arrange
            var target = new PackagePathResolver(
                rootDirectory: InMemoryRootDirectory,
                useSideBySidePaths: useSideBySidePaths);

            // Act
            var actual = target.GetManifestFileName(PackageIdentity);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(true, "PackageA.1.0.0.0-BETA")]
        [InlineData(false, "PackageA")]
        public void GetInstallPath_ReturnsInstallPath(bool useSideBySidePaths, string expected)
        {
            // Arrange
            var target = new PackagePathResolver(
                rootDirectory: InMemoryRootDirectory,
                useSideBySidePaths: useSideBySidePaths);
            expected = Path.Combine(InMemoryRootDirectory, expected);

            // Act
            var actual = target.GetInstallPath(PackageIdentity);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void GetInstalledPath_ReturnsNullIfFileDoesNotExist(bool useSideBySidePaths)
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var target = new PackagePathResolver(testDirectory.Path, useSideBySidePaths);

                var filePath = target.GetInstalledPath(PackageIdentity);

                Assert.Null(filePath);
            }
        }

        [Theory]
        [InlineData(true, "PackageA.1.0.0.0-BETA.nupkg")]
        [InlineData(false, "PackageA.nupkg")]
        public void GetInstalledPath_ReturnsInstalledPath(bool useSideBySidePaths, string expectedFileName)
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var expectedFilePath = Path.Combine(testDirectory.Path, expectedFileName);

                File.WriteAllText(expectedFilePath, string.Empty);

                var target = new PackagePathResolver(testDirectory.Path, useSideBySidePaths);

                var actualFilePath = target.GetInstalledPath(PackageIdentity);

                Assert.Equal(testDirectory.Path, actualFilePath);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void GetInstalledPackageFilePath_ReturnsNullIfFileDoesNotExist(bool useSideBySidePaths)
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var target = new PackagePathResolver(testDirectory.Path, useSideBySidePaths);

                var filePath = target.GetInstalledPackageFilePath(PackageIdentity);

                Assert.Null(filePath);
            }
        }

        [Theory]
        [InlineData(true, "PackageA.1.0.0.0-BETA.nupkg")]
        [InlineData(false, "PackageA.nupkg")]
        public void GetInstalledPackageFilePath_ReturnsInstalledPackageFilePath(bool useSideBySidePaths, string expectedFileName)
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var expectedFilePath = Path.Combine(testDirectory.Path, expectedFileName);

                File.WriteAllText(expectedFilePath, string.Empty);

                var target = new PackagePathResolver(testDirectory.Path, useSideBySidePaths);

                var actualFilePath = target.GetInstalledPackageFilePath(PackageIdentity);

                Assert.Equal(expectedFilePath, actualFilePath);
            }
        }
    }
}