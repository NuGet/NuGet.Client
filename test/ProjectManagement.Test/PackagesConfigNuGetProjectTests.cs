using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Versioning;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Test.Utility;
using Xunit;

namespace ProjectManagement.Test
{
    public class PackagesConfigNuGetProjectTests
    {
        [Fact]
        public async Task TestInstallPackage()
        {
            // Arrange
            var randomTestFolder = TestFilesystemUtility.CreateRandomTestFolder();
            var packagesConfigFileName = "packages.config";
            var targetFramework = NuGetFramework.Parse("net45");
            var metadata = new Dictionary<string, object>()
            {
                { NuGetProjectMetadataKeys.TargetFramework, targetFramework},
            };
            var packagesConfigFullPath = Path.Combine(randomTestFolder, packagesConfigFileName);
            var packagesConfigNuGetProject = new PackagesConfigNuGetProject(packagesConfigFullPath, metadata);
            var packageIdentity = new PackageIdentity("A", new NuGetVersion("1.0.0"));
            var token = CancellationToken.None;
            MakeFileReadOnly(packagesConfigFullPath);

            // Act
            await packagesConfigNuGetProject.InstallPackageAsync(packageIdentity, Stream.Null, new TestNuGetProjectContext(), token);
            MakeFileReadOnly(packagesConfigFullPath);

            // Assert
            var installedPackagesList = (await packagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, installedPackagesList.Count);
            Assert.Equal(packageIdentity, installedPackagesList[0].PackageIdentity);
            Assert.Equal(targetFramework, installedPackagesList[0].TargetFramework);
        }

        [Fact]
        public async Task TestInstallPackageUnsupportedFx()
        {
            // Arrange
            var randomTestFolder = TestFilesystemUtility.CreateRandomTestFolder();
            var packagesConfigFileName = "packages.config";
            var targetFramework = NuGetFramework.UnsupportedFramework;
            var metadata = new Dictionary<string, object>()
            {
                { NuGetProjectMetadataKeys.TargetFramework, targetFramework},
            };
            var packagesConfigFullPath = Path.Combine(randomTestFolder, packagesConfigFileName);
            var packagesConfigNuGetProject = new PackagesConfigNuGetProject(packagesConfigFullPath, metadata);
            var packageIdentity = new PackageIdentity("A", new NuGetVersion("1.0.0"));
            var token = CancellationToken.None;
            MakeFileReadOnly(packagesConfigFullPath);

            // Act
            await packagesConfigNuGetProject.InstallPackageAsync(packageIdentity, Stream.Null, new TestNuGetProjectContext(), token);
            MakeFileReadOnly(packagesConfigFullPath);

            // Assert
            var installedPackagesList = (await packagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, installedPackagesList.Count);
            Assert.Equal(packageIdentity, installedPackagesList[0].PackageIdentity);
            Assert.True(installedPackagesList[0].TargetFramework.IsUnsupported);
        }

        [Fact]
        public async Task TestUninstallLastPackage()
        {
            // Arrange
            var randomTestFolder = TestFilesystemUtility.CreateRandomTestFolder();
            var packagesConfigFileName = "packages.config";
            var targetFramework = NuGetFramework.Parse("net45");
            var metadata = new Dictionary<string, object>()
            {
                { NuGetProjectMetadataKeys.TargetFramework, targetFramework},
            };
            var packagesConfigFullPath = Path.Combine(randomTestFolder, packagesConfigFileName);
            var packagesConfigNuGetProject = new PackagesConfigNuGetProject(packagesConfigFullPath, metadata);
            var packageIdentity = new PackageIdentity("A", new NuGetVersion("1.0.0"));
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var token = CancellationToken.None;
            MakeFileReadOnly(packagesConfigFullPath);

            // Act
            await packagesConfigNuGetProject.InstallPackageAsync(packageIdentity, Stream.Null, testNuGetProjectContext, token);
            MakeFileReadOnly(packagesConfigFullPath);

            // Assert
            var installedPackagesList = (await packagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, installedPackagesList.Count);
            Assert.Equal(packageIdentity, installedPackagesList[0].PackageIdentity);
            Assert.Equal(targetFramework, installedPackagesList[0].TargetFramework);

            // Main Act
            await packagesConfigNuGetProject.UninstallPackageAsync(packageIdentity, testNuGetProjectContext, token);

            // Main Assert
            installedPackagesList = (await packagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, installedPackagesList.Count);
        }

        [Fact]
        public async Task TestInstallSecondPackage()
        {
            // Arrange
            var randomTestFolder = TestFilesystemUtility.CreateRandomTestFolder();
            var packagesConfigFileName = "packages.config";
            var targetFramework = NuGetFramework.Parse("net45");
            var metadata = new Dictionary<string, object>()
            {
                { NuGetProjectMetadataKeys.TargetFramework, targetFramework},
            };
            var packagesConfigFullPath = Path.Combine(randomTestFolder, packagesConfigFileName);
            var packagesConfigNuGetProject = new PackagesConfigNuGetProject(packagesConfigFullPath, metadata);

            var packageA = new PackageIdentity("A", new NuGetVersion("1.0.0"));
            var packageB = new PackageIdentity("B", new NuGetVersion("1.0.0"));
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var token = CancellationToken.None;
            MakeFileReadOnly(packagesConfigFullPath);

            // Act
            await packagesConfigNuGetProject.InstallPackageAsync(packageA, Stream.Null, testNuGetProjectContext, token);
            MakeFileReadOnly(packagesConfigFullPath);

            // Assert
            var installedPackagesList = (await packagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, installedPackagesList.Count);
            Assert.Equal(packageA, installedPackagesList[0].PackageIdentity);
            Assert.Equal(targetFramework, installedPackagesList[0].TargetFramework);

            // Main Act
            await packagesConfigNuGetProject.InstallPackageAsync(packageB, Stream.Null, testNuGetProjectContext, token);
            // Assert
            installedPackagesList = (await packagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(2, installedPackagesList.Count);
            Assert.Equal(packageA, installedPackagesList[0].PackageIdentity);
            Assert.Equal(packageB, installedPackagesList[1].PackageIdentity);
            Assert.Equal(targetFramework, installedPackagesList[0].TargetFramework);
            Assert.Equal(targetFramework, installedPackagesList[1].TargetFramework);
        }

        [Fact]
        public async Task TestUninstallPenultimatePackage()
        {
            // Arrange
            var randomTestFolder = TestFilesystemUtility.CreateRandomTestFolder();
            var packagesConfigFileName = "packages.config";
            var targetFramework = NuGetFramework.Parse("net45");
            var metadata = new Dictionary<string, object>()
            {
                { NuGetProjectMetadataKeys.TargetFramework, targetFramework},
            };
            var packagesConfigFullPath = Path.Combine(randomTestFolder, packagesConfigFileName);
            var packagesConfigNuGetProject = new PackagesConfigNuGetProject(packagesConfigFullPath, metadata);

            var packageA = new PackageIdentity("A", new NuGetVersion("1.0.0"));
            var packageB = new PackageIdentity("B", new NuGetVersion("1.0.0"));
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var token = CancellationToken.None;
            MakeFileReadOnly(packagesConfigFullPath);

            // Act
            await packagesConfigNuGetProject.InstallPackageAsync(packageA, Stream.Null, testNuGetProjectContext, token);
            MakeFileReadOnly(packagesConfigFullPath);

            // Assert
            var installedPackagesList = (await packagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, installedPackagesList.Count);
            Assert.Equal(packageA, installedPackagesList[0].PackageIdentity);
            Assert.Equal(targetFramework, installedPackagesList[0].TargetFramework);

            // Act
            await packagesConfigNuGetProject.InstallPackageAsync(packageB, Stream.Null, testNuGetProjectContext, token);
            // Assert
            installedPackagesList = (await packagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(2, installedPackagesList.Count);
            Assert.Equal(packageA, installedPackagesList[0].PackageIdentity);
            Assert.Equal(packageB, installedPackagesList[1].PackageIdentity);
            Assert.Equal(targetFramework, installedPackagesList[0].TargetFramework);
            Assert.Equal(targetFramework, installedPackagesList[1].TargetFramework);

            // Main Act
            await packagesConfigNuGetProject.UninstallPackageAsync(packageA, testNuGetProjectContext, token);

            // Main Assert
            installedPackagesList = (await packagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, installedPackagesList.Count);
            Assert.Equal(packageB, installedPackagesList[0].PackageIdentity);
            Assert.Equal(targetFramework, installedPackagesList[0].TargetFramework);
        }

        [Fact]
        public async Task TestInstallHigherVersionPackage()
        {
            // Arrange
            var randomTestFolder = TestFilesystemUtility.CreateRandomTestFolder();
            var packagesConfigFileName = "packages.config";
            var targetFramework = NuGetFramework.Parse("net45");
            var metadata = new Dictionary<string, object>()
            {
                { NuGetProjectMetadataKeys.TargetFramework, targetFramework},
            };
            var packagesConfigFullPath = Path.Combine(randomTestFolder, packagesConfigFileName);
            var packagesConfigNuGetProject = new PackagesConfigNuGetProject(packagesConfigFullPath, metadata);
            var packageA1 = new PackageIdentity("A", new NuGetVersion("1.0.0"));
            var packageA2 = new PackageIdentity("A", new NuGetVersion("2.0.0"));
            var token = CancellationToken.None;
            MakeFileReadOnly(packagesConfigFullPath);

            // Act
            await packagesConfigNuGetProject.InstallPackageAsync(packageA1, Stream.Null, new TestNuGetProjectContext(), token);
            MakeFileReadOnly(packagesConfigFullPath);

            // Assert
            var installedPackagesList = (await packagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, installedPackagesList.Count);
            Assert.Equal(packageA1, installedPackagesList[0].PackageIdentity);
            Assert.Equal(targetFramework, installedPackagesList[0].TargetFramework);

            // Main Act
            await packagesConfigNuGetProject.InstallPackageAsync(packageA2, Stream.Null, new TestNuGetProjectContext(), token);

            // Assert
            installedPackagesList = (await packagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, installedPackagesList.Count);
            Assert.Equal(packageA2, installedPackagesList[0].PackageIdentity);
            Assert.Equal(targetFramework, installedPackagesList[0].TargetFramework);
        }

        private static void MakeFileReadOnly(string fullPath)
        {
            if(File.Exists(fullPath))
            {
                File.SetAttributes(fullPath, File.GetAttributes(fullPath) | FileAttributes.ReadOnly);
            }
        }
    }
}
