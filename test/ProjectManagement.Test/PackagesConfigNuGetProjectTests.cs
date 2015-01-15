using NuGet.Frameworks;
using NuGet.PackagingCore;
using NuGet.ProjectManagement;
using NuGet.Versioning;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Test.Utility;
using Xunit;

namespace ProjectManagement.Test
{
    public class PackagesConfigNuGetProjectTests
    {
        [Fact]
        public void TestInstallPackage()
        {
            // Arrange
            var randomTestFolder = TestFilesystemUtility.CreateRandomTestFolder();
            var packagesConfigFileName = "packages.config";
            var targetFramework = NuGetFramework.Parse("net45");
            var metadata = new Dictionary<string, object>()
            {
                { NuGetProjectMetadataKeys.TargetFramework, targetFramework},
            };
            var packagesConfigNuGetProject = new PackagesConfigNuGetProject(Path.Combine(randomTestFolder, packagesConfigFileName), metadata);
            var packageIdentity = new PackageIdentity("A", new NuGetVersion("1.0.0"));

            // Act
            packagesConfigNuGetProject.InstallPackage(packageIdentity, Stream.Null, new TestNuGetProjectContext());

            // Assert
            var installedPackagesList = packagesConfigNuGetProject.GetInstalledPackages().ToList();
            Assert.Equal(1, installedPackagesList.Count);
            Assert.Equal(packageIdentity, installedPackagesList[0].PackageIdentity);
            Assert.Equal(targetFramework, installedPackagesList[0].TargetFramework);
        }

        [Fact]
        public void TestUninstallLastPackage()
        {
            // Arrange
            var randomTestFolder = TestFilesystemUtility.CreateRandomTestFolder();
            var packagesConfigFileName = "packages.config";
            var targetFramework = NuGetFramework.Parse("net45");
            var metadata = new Dictionary<string, object>()
            {
                { NuGetProjectMetadataKeys.TargetFramework, targetFramework},
            };
            var packagesConfigNuGetProject = new PackagesConfigNuGetProject(Path.Combine(randomTestFolder, packagesConfigFileName), metadata);
            var packageIdentity = new PackageIdentity("A", new NuGetVersion("1.0.0"));
            var testNuGetProjectContext = new TestNuGetProjectContext();

            // Act
            packagesConfigNuGetProject.InstallPackage(packageIdentity, Stream.Null, testNuGetProjectContext);

            // Assert
            var installedPackagesList = packagesConfigNuGetProject.GetInstalledPackages().ToList();
            Assert.Equal(1, installedPackagesList.Count);
            Assert.Equal(packageIdentity, installedPackagesList[0].PackageIdentity);
            Assert.Equal(targetFramework, installedPackagesList[0].TargetFramework);

            // Main Act
            packagesConfigNuGetProject.UninstallPackage(packageIdentity, testNuGetProjectContext);

            // Main Assert
            installedPackagesList = packagesConfigNuGetProject.GetInstalledPackages().ToList();
            Assert.Equal(0, installedPackagesList.Count);
        }

        [Fact]
        public void TestInstallSecondPackage()
        {
            // Arrange
            var randomTestFolder = TestFilesystemUtility.CreateRandomTestFolder();
            var packagesConfigFileName = "packages.config";
            var targetFramework = NuGetFramework.Parse("net45");
            var metadata = new Dictionary<string, object>()
            {
                { NuGetProjectMetadataKeys.TargetFramework, targetFramework},
            };
            var packagesConfigNuGetProject = new PackagesConfigNuGetProject(Path.Combine(randomTestFolder, packagesConfigFileName), metadata);

            var packageA = new PackageIdentity("A", new NuGetVersion("1.0.0"));
            var packageB = new PackageIdentity("B", new NuGetVersion("1.0.0"));
            var testNuGetProjectContext = new TestNuGetProjectContext();

            // Act
            packagesConfigNuGetProject.InstallPackage(packageA, Stream.Null, testNuGetProjectContext);

            // Assert
            var installedPackagesList = packagesConfigNuGetProject.GetInstalledPackages().ToList();
            Assert.Equal(1, installedPackagesList.Count);
            Assert.Equal(packageA, installedPackagesList[0].PackageIdentity);
            Assert.Equal(targetFramework, installedPackagesList[0].TargetFramework);

            // Main Act
            packagesConfigNuGetProject.InstallPackage(packageB, Stream.Null, testNuGetProjectContext);
            // Assert
            installedPackagesList = packagesConfigNuGetProject.GetInstalledPackages().ToList();
            Assert.Equal(2, installedPackagesList.Count);
            Assert.Equal(packageA, installedPackagesList[0].PackageIdentity);
            Assert.Equal(packageB, installedPackagesList[1].PackageIdentity);
            Assert.Equal(targetFramework, installedPackagesList[0].TargetFramework);
            Assert.Equal(targetFramework, installedPackagesList[1].TargetFramework);
        }

        [Fact]
        public void TestUninstallPenultimatePackage()
        {
            // Arrange
            var randomTestFolder = TestFilesystemUtility.CreateRandomTestFolder();
            var packagesConfigFileName = "packages.config";
            var targetFramework = NuGetFramework.Parse("net45");
            var metadata = new Dictionary<string, object>()
            {
                { NuGetProjectMetadataKeys.TargetFramework, targetFramework},
            };
            var packagesConfigNuGetProject = new PackagesConfigNuGetProject(Path.Combine(randomTestFolder, packagesConfigFileName), metadata);

            var packageA = new PackageIdentity("A", new NuGetVersion("1.0.0"));
            var packageB = new PackageIdentity("B", new NuGetVersion("1.0.0"));
            var testNuGetProjectContext = new TestNuGetProjectContext();

            // Act
            packagesConfigNuGetProject.InstallPackage(packageA, Stream.Null, testNuGetProjectContext);

            // Assert
            var installedPackagesList = packagesConfigNuGetProject.GetInstalledPackages().ToList();
            Assert.Equal(1, installedPackagesList.Count);
            Assert.Equal(packageA, installedPackagesList[0].PackageIdentity);
            Assert.Equal(targetFramework, installedPackagesList[0].TargetFramework);

            // Act
            packagesConfigNuGetProject.InstallPackage(packageB, Stream.Null, testNuGetProjectContext);
            // Assert
            installedPackagesList = packagesConfigNuGetProject.GetInstalledPackages().ToList();
            Assert.Equal(2, installedPackagesList.Count);
            Assert.Equal(packageA, installedPackagesList[0].PackageIdentity);
            Assert.Equal(packageB, installedPackagesList[1].PackageIdentity);
            Assert.Equal(targetFramework, installedPackagesList[0].TargetFramework);
            Assert.Equal(targetFramework, installedPackagesList[1].TargetFramework);

            // Main Act
            packagesConfigNuGetProject.UninstallPackage(packageA, testNuGetProjectContext);

            // Main Assert
            installedPackagesList = packagesConfigNuGetProject.GetInstalledPackages().ToList();
            Assert.Equal(1, installedPackagesList.Count);
            Assert.Equal(packageB, installedPackagesList[0].PackageIdentity);
            Assert.Equal(targetFramework, installedPackagesList[0].TargetFramework);
        }

        [Fact]
        public void TestInstallHigherVersionPackage()
        {
            // Arrange
            var randomTestFolder = TestFilesystemUtility.CreateRandomTestFolder();
            var packagesConfigFileName = "packages.config";
            var targetFramework = NuGetFramework.Parse("net45");
            var metadata = new Dictionary<string, object>()
            {
                { NuGetProjectMetadataKeys.TargetFramework, targetFramework},
            };
            var packagesConfigNuGetProject = new PackagesConfigNuGetProject(Path.Combine(randomTestFolder, packagesConfigFileName), metadata);
            var packageA1 = new PackageIdentity("A", new NuGetVersion("1.0.0"));
            var packageA2 = new PackageIdentity("A", new NuGetVersion("2.0.0"));

            // Act
            packagesConfigNuGetProject.InstallPackage(packageA1, Stream.Null, new TestNuGetProjectContext());

            // Assert
            var installedPackagesList = packagesConfigNuGetProject.GetInstalledPackages().ToList();
            Assert.Equal(1, installedPackagesList.Count);
            Assert.Equal(packageA1, installedPackagesList[0].PackageIdentity);
            Assert.Equal(targetFramework, installedPackagesList[0].TargetFramework);

            // Main Act
            packagesConfigNuGetProject.InstallPackage(packageA2, Stream.Null, new TestNuGetProjectContext());

            // Assert
            installedPackagesList = packagesConfigNuGetProject.GetInstalledPackages().ToList();
            Assert.Equal(1, installedPackagesList.Count);
            Assert.Equal(packageA2, installedPackagesList[0].PackageIdentity);
            Assert.Equal(targetFramework, installedPackagesList[0].TargetFramework);
        }
    }
}
