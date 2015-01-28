using NuGet.Configuration;
using NuGet.PackagingCore;
using NuGet.PackageManagement;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Test.Utility;
using Xunit;

namespace NuGet.Test
{
    public class PackageRestoreManagerTests
    {
        private List<PackageIdentity> Packages = new List<PackageIdentity>()
        {
            new PackageIdentity("jQuery", new NuGetVersion("1.4.4")),
            new PackageIdentity("jQuery", new NuGetVersion("1.6.4")),
            new PackageIdentity("jQuery.Validation", new NuGetVersion("1.13.1")),
            new PackageIdentity("jQuery.UI.Combined", new NuGetVersion("1.11.2")),
        };

        [Fact]
        public void TestGetMissingPackagesForSolution()
        {
            // Arrange
            var testSolutionManager = new TestSolutionManager();
            var projectA = testSolutionManager.AddNewMSBuildProject();
            var projectB = testSolutionManager.AddNewMSBuildProject();

            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomPackageSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var packageFileInfo = TestPackages.GetLegacyTestPackage(randomPackageSourcePath,
                packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            var testNuGetProjectContext = new TestNuGetProjectContext();

            using (var packageStream = packageFileInfo.OpenRead())
            {
                // Act
                projectA.InstallPackage(packageIdentity, packageStream, testNuGetProjectContext);
                projectB.InstallPackage(packageIdentity, packageStream, testNuGetProjectContext);
            }

            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSettings = NullSettings.Instance;
            var packageRestoreManager = new PackageRestoreManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            
            // Act            
            var packageReferencesFromSolution = packageRestoreManager.GetPackageReferencesFromSolution().ToList();
            var missingPackagesFromSolution = packageRestoreManager.GetMissingPackagesInSolution().ToList();

            Assert.Equal(2, packageReferencesFromSolution.Count);
            Assert.Equal(0, missingPackagesFromSolution.Count);

            // Delete packages folder
            Directory.Delete(Path.Combine(testSolutionManager.SolutionDirectory, "packages"), recursive: true);

            packageReferencesFromSolution = packageRestoreManager.GetPackageReferencesFromSolution().ToList();
            missingPackagesFromSolution = packageRestoreManager.GetMissingPackagesInSolution().ToList();
            Assert.Equal(2, packageReferencesFromSolution.Count);
            Assert.Equal(1, missingPackagesFromSolution.Count);
        }

        [Fact]
        public async Task TestPackageRestoredEvent()
        {
            // Arrange
            var testSolutionManager = new TestSolutionManager();
            var projectA = testSolutionManager.AddNewMSBuildProject();
            var projectB = testSolutionManager.AddNewMSBuildProject();

            var packageIdentity = Packages[0];
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSettings = NullSettings.Instance;
            var resolutionContext = new ResolutionContext();

            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);

            await nuGetPackageManager.InstallPackageAsync(projectA, packageIdentity,
                resolutionContext, new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First());
            await nuGetPackageManager.InstallPackageAsync(projectB, packageIdentity,
                resolutionContext, new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First());

            var packageRestoreManager = new PackageRestoreManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var restoredPackages = new List<PackageIdentity>();
            packageRestoreManager.PackageRestoredEvent += delegate(object sender, PackageRestoredEventArgs args)
            {
                if(args.Restored)
                {
                    restoredPackages.Add(args.Package);
                }
            };


            Assert.True(nuGetPackageManager.PackageExistsInPackagesFolder(packageIdentity));

            // Delete packages folder
            Directory.Delete(Path.Combine(testSolutionManager.SolutionDirectory, "packages"), recursive: true);

            Assert.False(nuGetPackageManager.PackageExistsInPackagesFolder((packageIdentity)));

            // Act
            await packageRestoreManager.RestoreMissingPackagesInSolution();

            Assert.Equal(1, restoredPackages.Count);
            Assert.True(nuGetPackageManager.PackageExistsInPackagesFolder((packageIdentity)));
        }

        [Fact]
        public void TestCheckForMissingPackages()
        {
            // Arrange
            var testSolutionManager = new TestSolutionManager();
            var projectA = testSolutionManager.AddNewMSBuildProject();
            var projectB = testSolutionManager.AddNewMSBuildProject();

            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomPackageSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var packageFileInfo = TestPackages.GetLegacyTestPackage(randomPackageSourcePath,
                packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            var testNuGetProjectContext = new TestNuGetProjectContext();

            using (var packageStream = packageFileInfo.OpenRead())
            {
                // Act
                projectA.InstallPackage(packageIdentity, packageStream, testNuGetProjectContext);
                projectB.InstallPackage(packageIdentity, packageStream, testNuGetProjectContext);
            }

            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSettings = NullSettings.Instance;
            var packageRestoreManager = new PackageRestoreManager(sourceRepositoryProvider, testSettings, testSolutionManager);

            int packagesMissingEventCount = 0;
            bool packagesMissing = false;
            packageRestoreManager.PackagesMissingStatusChanged += delegate(object sender, PackagesMissingStatusEventArgs args)
            {
                packagesMissingEventCount++;
                packagesMissing = args.PackagesMissing;
            };

            // Act
            packageRestoreManager.RaisePackagesMissingEventForSolution();

            // Assert
            Assert.Equal(1, packagesMissingEventCount);
            Assert.False(packagesMissing);

            // Delete packages folder
            Directory.Delete(Path.Combine(testSolutionManager.SolutionDirectory, "packages"), recursive: true);

            // Act
            packageRestoreManager.RaisePackagesMissingEventForSolution();

            // Assert
            Assert.Equal(2, packagesMissingEventCount);
            Assert.True(packagesMissing);
        }

        [Fact]
        public async Task TestRestoreMissingPackages()
        {
            // Arrange
            var testSolutionManager = new TestSolutionManager();
            var projectA = testSolutionManager.AddNewMSBuildProject();
            var projectB = testSolutionManager.AddNewMSBuildProject();

            var packageIdentity = Packages[0];
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSettings = NullSettings.Instance;
            var resolutionContext = new ResolutionContext();

            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);

            await nuGetPackageManager.InstallPackageAsync(projectA, packageIdentity,
                resolutionContext, new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First());
            await nuGetPackageManager.InstallPackageAsync(projectB, packageIdentity,
                resolutionContext, new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First());

            var packageRestoreManager = new PackageRestoreManager(sourceRepositoryProvider, testSettings, testSolutionManager);

            Assert.True(nuGetPackageManager.PackageExistsInPackagesFolder(packageIdentity));

            // Delete packages folder
            Directory.Delete(Path.Combine(testSolutionManager.SolutionDirectory, "packages"), recursive: true);

            Assert.False(nuGetPackageManager.PackageExistsInPackagesFolder((packageIdentity)));

            // Act
            await packageRestoreManager.RestoreMissingPackagesInSolution();

            Assert.True(nuGetPackageManager.PackageExistsInPackagesFolder((packageIdentity)));
        }
    }
}
