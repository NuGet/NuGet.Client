using System.IO;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class VersionFolderPathResolverTests
    {
        [Fact]
        public void VersionFolderPathResolver_GetInstallPath()
        {
            // Arrange
            var tc = new TestContext();

            // Act
            var actual = tc.Target.GetInstallPath(tc.Id, tc.Version);

            // Assert
            Assert.Equal(Path.Combine(tc.PackagesPath, "nuget.packaging", "3.4.3-beta"), actual);
        }

        [Fact]
        public void VersionFolderPathResolver_GetPackageFilePath()
        {
            // Arrange
            var tc = new TestContext();

            // Act
            var actual = tc.Target.GetPackageFilePath(tc.Id, tc.Version);

            // Assert
            Assert.Equal(
                Path.Combine(
                    tc.PackagesPath,
                    "nuget.packaging",
                    "3.4.3-beta",
                    "nuget.packaging.3.4.3-beta.nupkg"),
                actual);
        }

        [Fact]
        public void VersionFolderPathResolver_GetManifestFilePath()
        {
            // Arrange
            var tc = new TestContext();

            // Act
            var actual = tc.Target.GetManifestFilePath(tc.Id, tc.Version);

            // Assert
            Assert.Equal(
                Path.Combine(
                    tc.PackagesPath, "nuget.packaging", "3.4.3-beta", "nuget.packaging.nuspec"),
                actual);
        }

        [Fact]
        public void VersionFolderPathResolver_GetHashPath()
        {
            // Arrange
            var tc = new TestContext();

            // Act
            var actual = tc.Target.GetHashPath(tc.Id, tc.Version);

            // Assert
            Assert.Equal(
                Path.Combine(
                    tc.PackagesPath,
                    "nuget.packaging",
                    "3.4.3-beta",
                    "nuget.packaging.3.4.3-beta.nupkg.sha512"),
                actual);
        }

        [Fact]
        public void VersionFolderPathResolver_GetPackageDirectory()
        {
            // Arrange
            var tc = new TestContext();

            // Act
            var actual = tc.Target.GetPackageDirectory(tc.Id, tc.Version);

            // Assert
            Assert.Equal(Path.Combine("nuget.packaging", "3.4.3-beta"), actual);
        }

        [Fact]
        public void VersionFolderPathResolver_GetPackageFileName()
        {
            // Arrange
            var tc = new TestContext();

            // Act
            var actual = tc.Target.GetPackageFileName(tc.Id, tc.Version);

            // Assert
            Assert.Equal("nuget.packaging.3.4.3-beta.nupkg", actual);
        }

        [Fact]
        public void VersionFolderPathResolver_GetManifestFileName()
        {
            // Arrange
            var tc = new TestContext();

            // Act
            var actual = tc.Target.GetManifestFileName(tc.Id, tc.Version);

            // Assert
            Assert.Equal("nuget.packaging.nuspec", actual);
        }

        [Fact]
        public void VersionFolderPathResolver_GetVersionListPath()
        {
            // Arrange
            var tc = new TestContext();

            // Act
            var actual = tc.Target.GetVersionListPath(tc.Id);

            // Assert
            Assert.Equal(
                Path.Combine(
                    tc.PackagesPath,
                    "nuget.packaging"),
                actual);
        }

        [Fact]
        public void VersionFolderPathResolver_GetVersionListDirectory()
        {
            // Arrange
            var tc = new TestContext();

            // Act
            var actual = tc.Target.GetVersionListDirectory(tc.Id);

            // Assert
            Assert.Equal("nuget.packaging", actual);
        }

        private class TestContext
        {
            public TestContext()
            {
                // data
                PackagesPath = "prefix";
                Id = "NuGet.Packaging";
                Version = new NuGetVersion("3.04.3-Beta");
            }

            public string Id { get; private set; }
            public string PackagesPath { get; set; }
            public NuGetVersion Version { get; set; }
            public VersionFolderPathResolver Target
            {
                get { return new VersionFolderPathResolver(PackagesPath); }
            }
        }
    }
}
