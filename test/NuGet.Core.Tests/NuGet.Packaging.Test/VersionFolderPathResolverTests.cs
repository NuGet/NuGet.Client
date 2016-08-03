// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class VersionFolderPathResolverTests
    {
        [Theory]
        [InlineData("nuget.packaging", "3.4.3-beta", true)]
        [InlineData("NuGet.Packaging", "3.4.3-Beta", false)]
        public void VersionFolderPathResolver_GetInstallPath(string id, string version, bool isLowercase)
        {
            // Arrange
            var tc = new TestContext { IsLowercase = isLowercase };

            // Act
            var actual = tc.Target.GetInstallPath(tc.Id, tc.Version);

            // Assert
            Assert.Equal(Path.Combine(tc.PackagesPath, id, version), actual);
        }

        [Theory]
        [InlineData("nuget.packaging", "3.4.3-beta", "nuget.packaging.3.4.3-beta.nupkg", true)]
        [InlineData("NuGet.Packaging", "3.4.3-Beta", "NuGet.Packaging.3.4.3-Beta.nupkg", false)]
        public void VersionFolderPathResolver_GetPackageFilePath(string id, string version, string file, bool isLowercase)
        {
            // Arrange
            var tc = new TestContext { IsLowercase = isLowercase };

            // Act
            var actual = tc.Target.GetPackageFilePath(tc.Id, tc.Version);

            // Assert
            Assert.Equal(
                Path.Combine(tc.PackagesPath, id, version, file),
                actual);
        }

        [Theory]
        [InlineData("nuget.packaging", "3.4.3-beta", "nuget.packaging.nuspec", true)]
        [InlineData("NuGet.Packaging", "3.4.3-Beta", "NuGet.Packaging.nuspec", false)]
        public void VersionFolderPathResolver_GetManifestFilePath(string id, string version, string file, bool isLowercase)
        {
            // Arrange
            var tc = new TestContext { IsLowercase = isLowercase };

            // Act
            var actual = tc.Target.GetManifestFilePath(tc.Id, tc.Version);

            // Assert
            Assert.Equal(
                Path.Combine(tc.PackagesPath, id, version, file),
                actual);
        }

        [Theory]
        [InlineData("nuget.packaging", "3.4.3-beta", "nuget.packaging.3.4.3-beta.nupkg.sha512", true)]
        [InlineData("NuGet.Packaging", "3.4.3-Beta", "NuGet.Packaging.3.4.3-Beta.nupkg.sha512", false)]
        public void VersionFolderPathResolver_GetHashPath(string id, string version, string file, bool isLowercase)
        {
            // Arrange
            var tc = new TestContext { IsLowercase = isLowercase };

            // Act
            var actual = tc.Target.GetHashPath(tc.Id, tc.Version);

            // Assert
            Assert.Equal(
                Path.Combine(tc.PackagesPath, id, version, file),
                actual);
        }

        [Theory]
        [InlineData("nuget.packaging", "3.4.3-beta", true)]
        [InlineData("NuGet.Packaging", "3.4.3-Beta", false)]
        public void VersionFolderPathResolver_GetPackageDirectory(string id, string version, bool isLowercase)
        {
            // Arrange
            var tc = new TestContext { IsLowercase = isLowercase };

            // Act
            var actual = tc.Target.GetPackageDirectory(tc.Id, tc.Version);

            // Assert
            Assert.Equal(Path.Combine(id, version), actual);
        }

        [Theory]
        [InlineData("nuget.packaging.3.4.3-beta.nupkg", true)]
        [InlineData("NuGet.Packaging.3.4.3-Beta.nupkg", false)]
        public void VersionFolderPathResolver_GetPackageFileName(string file, bool isLowercase)
        {
            // Arrange
            var tc = new TestContext { IsLowercase = isLowercase };

            // Act
            var actual = tc.Target.GetPackageFileName(tc.Id, tc.Version);

            // Assert
            Assert.Equal(file, actual);
        }

        [Theory]
        [InlineData("nuget.packaging.nuspec", true)]
        [InlineData("NuGet.Packaging.nuspec", false)]
        public void VersionFolderPathResolver_GetManifestFileName(string file, bool isLowercase)
        {
            // Arrange
            var tc = new TestContext { IsLowercase = isLowercase };

            // Act
            var actual = tc.Target.GetManifestFileName(tc.Id, tc.Version);

            // Assert
            Assert.Equal(file, actual);
        }

        [Theory]
        [InlineData("nuget.packaging", true)]
        [InlineData("NuGet.Packaging", false)]
        public void VersionFolderPathResolver_GetVersionListPath(string directory, bool isLowercase)
        {
            // Arrange
            var tc = new TestContext { IsLowercase = isLowercase };

            // Act
            var actual = tc.Target.GetVersionListPath(tc.Id);

            // Assert
            Assert.Equal(
                Path.Combine(tc.PackagesPath, directory),
                actual);
        }

        [Theory]
        [InlineData("nuget.packaging", true)]
        [InlineData("NuGet.Packaging", false)]
        public void VersionFolderPathResolver_GetVersionListDirectory(string directory, bool isLowercase)
        {
            // Arrange
            var tc = new TestContext { IsLowercase = isLowercase };

            // Act
            var actual = tc.Target.GetVersionListDirectory(tc.Id);

            // Assert
            Assert.Equal(directory, actual);
        }

        private class TestContext
        {
            public TestContext()
            {
                // data
                PackagesPath = "prefix";
                Id = "NuGet.Packaging";
                Version = new NuGetVersion("3.04.3-Beta");
                IsLowercase = true;
            }

            public string Id { get; private set; }
            public string PackagesPath { get; set; }
            public NuGetVersion Version { get; set; }
            public bool IsLowercase { get; set; }
            public VersionFolderPathResolver Target
            {
                get { return new VersionFolderPathResolver(PackagesPath, IsLowercase); }
            }
        }
    }
}
