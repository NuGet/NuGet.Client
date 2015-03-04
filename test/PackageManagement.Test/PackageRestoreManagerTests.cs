using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.PackageManagement;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Test.Utility;
using Xunit;
using System.Threading;

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
        public async Task TestGetMissingPackagesForSolution()
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
            var token = CancellationToken.None;

            using (var packageStream = packageFileInfo.OpenRead())
            {
                // Act
                await projectA.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
                await projectB.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
            }

            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSettings = NullSettings.Instance;
            var packageRestoreManager = new PackageRestoreManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            
            // Act            
            var packageReferencesFromSolution = (await packageRestoreManager.GetPackageReferencesFromSolution(token)).ToList();
            var missingPackagesFromSolution = (await packageRestoreManager.GetMissingPackagesInSolution(token)).ToList();

            Assert.Equal(2, packageReferencesFromSolution.Count);
            Assert.Equal(0, missingPackagesFromSolution.Count);

            // Delete packages folder
            Directory.Delete(Path.Combine(testSolutionManager.SolutionDirectory, "packages"), recursive: true);

            packageReferencesFromSolution = (await packageRestoreManager.GetPackageReferencesFromSolution(token)).ToList();
            missingPackagesFromSolution = (await packageRestoreManager.GetMissingPackagesInSolution(token)).ToList();
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
            var token = CancellationToken.None;

            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);

            await nuGetPackageManager.InstallPackageAsync(projectA, packageIdentity,
                resolutionContext, new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(), null, token);
            await nuGetPackageManager.InstallPackageAsync(projectB, packageIdentity,
                resolutionContext, new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(), null, token);

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
            await packageRestoreManager.RestoreMissingPackagesInSolutionAsync(CancellationToken.None);

            Assert.Equal(1, restoredPackages.Count);
            Assert.True(nuGetPackageManager.PackageExistsInPackagesFolder((packageIdentity)));
        }

        [Fact]
        public async Task TestCheckForMissingPackages()
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
            var token = CancellationToken.None;

            using (var packageStream = packageFileInfo.OpenRead())
            {
                // Act
                await projectA.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
                await projectB.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
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
            await packageRestoreManager.RaisePackagesMissingEventForSolution(token);

            // Assert
            Assert.Equal(1, packagesMissingEventCount);
            Assert.False(packagesMissing);

            // Delete packages folder
            Directory.Delete(Path.Combine(testSolutionManager.SolutionDirectory, "packages"), recursive: true);

            // Act
            await packageRestoreManager.RaisePackagesMissingEventForSolution(token);

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
            var token = CancellationToken.None;

            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);

            await nuGetPackageManager.InstallPackageAsync(projectA, packageIdentity,
                resolutionContext, new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(), null, token);
            await nuGetPackageManager.InstallPackageAsync(projectB, packageIdentity,
                resolutionContext, new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(), null, token);

            var packageRestoreManager = new PackageRestoreManager(sourceRepositoryProvider, testSettings, testSolutionManager);

            Assert.True(nuGetPackageManager.PackageExistsInPackagesFolder(packageIdentity));

            // Delete packages folder
            Directory.Delete(Path.Combine(testSolutionManager.SolutionDirectory, "packages"), recursive: true);

            Assert.False(nuGetPackageManager.PackageExistsInPackagesFolder((packageIdentity)));

            // Act
            await packageRestoreManager.RestoreMissingPackagesInSolutionAsync(CancellationToken.None);

            Assert.True(nuGetPackageManager.PackageExistsInPackagesFolder((packageIdentity)));
        }
    }
}
