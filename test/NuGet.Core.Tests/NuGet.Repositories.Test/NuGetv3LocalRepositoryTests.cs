// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Repositories.Test
{
    public class NuGetv3LocalRepositoryTests
    {
        [Fact]
        public void NuGetv3LocalRepository_FindPackagesById_ReturnsEmptySequenceWithIdNotFound()
        {
            // Arrange
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var target = new NuGetv3LocalRepository(workingDir);

                // Act
                var packages = target.FindPackagesById("Foo");

                // Assert
                Assert.Empty(packages);
            }
        }

        [Fact]
        public async Task NuGetv3LocalRepository_FindPackagesById_UsesProvidedIdCase()
        {
            // Arrange
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var id = "Foo";
                var target = new NuGetv3LocalRepository(workingDir);
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    workingDir,
                    PackageSaveMode.Defaultv3,
                    new SimpleTestPackageContext("foo", "1.0.0"));

                // Act
                var packages = target.FindPackagesById(id);

                // Assert
                Assert.Equal(1, packages.Count());
                Assert.Equal(id, packages.ElementAt(0).Id);
                Assert.Equal("1.0.0", packages.ElementAt(0).Version.ToNormalizedString());
            }
        }
        
        [Fact]
        public async Task NuGetv3LocalRepository_FindPackagesById_LeavesVersionCaseFoundOnFileSystem()
        {
            // Arrange
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var id = "Foo";
                var target = new NuGetv3LocalRepository(workingDir);
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    workingDir,
                    PackageSaveMode.Defaultv3,
                    new SimpleTestPackageContext(id, "1.0.0"),
                    new SimpleTestPackageContext(id, "2.0.0-Beta"));

                // Act
                var packages = target.FindPackagesById(id);

                // Assert
                Assert.Equal(2, packages.Count());
                packages = packages.OrderBy(x => x.Version);
                Assert.Equal(id, packages.ElementAt(0).Id);
                Assert.Equal("1.0.0", packages.ElementAt(0).Version.ToNormalizedString());
                Assert.Equal(id, packages.ElementAt(1).Id);
                Assert.Equal("2.0.0-beta", packages.ElementAt(1).Version.ToNormalizedString());
            }
        }

        [Fact]
        public void NuGetv3LocalRepository_FindPackage_ReturnsNullWithIdNotFound()
        {
            // Arrange
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var target = new NuGetv3LocalRepository(workingDir);

                // Act
                var package = target.FindPackage("Foo", NuGetVersion.Parse("2.0.0-BETA"));

                // Assert
                Assert.Null(package);
            }
        }

        [Fact]
        public async Task NuGetv3LocalRepository_FindPackage_ReturnsNullWithVersionNotFound()
        {
            // Arrange
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var id = "Foo";
                var target = new NuGetv3LocalRepository(workingDir);
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    workingDir,
                    PackageSaveMode.Defaultv3,
                    new SimpleTestPackageContext(id, "1.0.0"),
                    new SimpleTestPackageContext(id, "2.0.0-Beta"));

                // Act
                var package = target.FindPackage(id, NuGetVersion.Parse("3.0.0-BETA"));

                // Assert
                Assert.Null(package);
            }
        }

        [Fact]
        public async Task NuGetv3LocalRepository_FindPackage_UsesProvidedVersionCase()
        {
            // Arrange
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var id = "Foo";
                var target = new NuGetv3LocalRepository(workingDir);
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    workingDir,
                    PackageSaveMode.Defaultv3,
                    new SimpleTestPackageContext(id, "1.0.0"),
                    new SimpleTestPackageContext(id, "2.0.0-Beta"));

                // Act
                var package = target.FindPackage(id, NuGetVersion.Parse("2.0.0-BETA"));

                // Assert
                Assert.NotNull(package);
                Assert.Equal(id, package.Id);
                Assert.Equal("2.0.0-BETA", package.Version.ToNormalizedString());
            }
        }
    }
}
