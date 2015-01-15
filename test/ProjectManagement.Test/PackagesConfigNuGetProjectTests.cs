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

            // Act
            packagesConfigNuGetProject.InstallPackage(new PackageIdentity("A", new NuGetVersion("1.0.0")), Stream.Null, new TestNuGetProjectContext());

            // Assert
            var installedPackagesList = packagesConfigNuGetProject.GetInstalledPackages().ToList();
            Assert.Equal(1, installedPackagesList.Count);
            Assert.Equal(targetFramework, installedPackagesList[0].TargetFramework);
        }

        [Fact]
        public void TestUninstallPackage()
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
            Assert.Equal(targetFramework, installedPackagesList[0].TargetFramework);

            // Main Act
            packagesConfigNuGetProject.UninstallPackage(packageIdentity, testNuGetProjectContext);

            // Main Assert
            installedPackagesList = packagesConfigNuGetProject.GetInstalledPackages().ToList();
            Assert.Equal(0, installedPackagesList.Count);
        }
    }
}
