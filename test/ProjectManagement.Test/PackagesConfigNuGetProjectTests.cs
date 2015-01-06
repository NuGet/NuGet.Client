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
            Assert.Equal(installedPackagesList.Count, 1);
            Assert.Equal(installedPackagesList[0].TargetFramework, targetFramework);
        }
    }
}
