using System;
using System.IO;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class PackagePathResolverTests
    {
        private const string InMemoryRootDirectory = ".";
        private static readonly PackageIdentity PackageIdentity = new PackageIdentity("PackageA", new NuGetVersion("1.0.0.0-BETA"));

        [Fact]
        public void PackagePathResolver_RejectsNullRootDirectory()
        {
            // Arrange & Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => new PackagePathResolver(
                rootDirectory: null,
                useSideBySidePaths: true));
            Assert.Equal("rootDirectory", exception.ParamName);
        }

        [Fact]
        public void PackagePathResolver_RejectsEmptyRootDirectory()
        {
            // Arrange & Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => new PackagePathResolver(
                rootDirectory: string.Empty,
                useSideBySidePaths: true));
            Assert.Equal("rootDirectory", exception.ParamName);
        }

        [Theory]
        [InlineData(true, "packagea.1.0.0.0-beta")]
        [InlineData(false, "packagea")]
        public void PackagePathResolver_GetPackageDirectoryName(bool useSideBySidePaths, string expected)
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
        [InlineData(true, "packagea.1.0.0.0-beta.nupkg")]
        [InlineData(false, "packagea.nupkg")]
        public void PackagePathResolver_GetPackageFileName(bool useSideBySidePaths, string expected)
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
        [InlineData(true, "packagea.nuspec")]
        [InlineData(false, "packagea.nuspec")]
        public void PackagePathResolver_GetManifestFileName(bool useSideBySidePaths, string expected)
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
        [InlineData(true, "packagea.1.0.0.0-beta")]
        [InlineData(false, "packagea")]
        public void PackagePathResolver_GetInstallPath(bool useSideBySidePaths, string expected)
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
    }
}
