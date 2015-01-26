using NuGet.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using Test.Utility;
using Xunit;

namespace NuGet.Test
{
    public class SourceRepositoryProviderTests
    {
        [Fact]
        public void TestSourceRepoPackageSourcesChanged()
        {
            // Arrange
            var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var localPackageSource = new PackageSource(localAppDataPath);
            var oldPackageSources = new List<PackageSource>() { localPackageSource };
            var packageSourceProvider = new TestPackageSourceProvider(oldPackageSources);
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateSourceRepositoryProvider(packageSourceProvider);

            // Act
            var oldEffectivePackageSources = sourceRepositoryProvider.GetRepositories().ToList();

            // Assert
            Assert.Equal(1, oldEffectivePackageSources.Count);
            Assert.Equal(localAppDataPath, oldEffectivePackageSources[0].PackageSource.Source);

            // Main Act
            var newPackageSources = new List<PackageSource>() { TestSourceRepositoryUtility.V3PackageSource, localPackageSource };
            packageSourceProvider.SavePackageSources(newPackageSources);

            var newEffectivePackageSources = sourceRepositoryProvider.GetRepositories().ToList();

            // Main Assert
            Assert.Equal(2, newEffectivePackageSources.Count);
            Assert.Equal(TestSourceRepositoryUtility.V3PackageSource.Source, newEffectivePackageSources[0].PackageSource.Source);
            Assert.Equal(localAppDataPath, newEffectivePackageSources[1].PackageSource.Source);
        }

        [Fact]
        public void TestSourceRepoPackageSourcesChanged2()
        {
            // Arrange
            var settingsPath = TestPackageSourceSettings.CreateAndGetSettingFilePath();
            var settings = new Settings(settingsPath);
            var packageSourceProvider = new PackageSourceProvider(settings);
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateSourceRepositoryProvider(packageSourceProvider);

            // Act
            var oldEffectivePackageSources = sourceRepositoryProvider.GetRepositories().ToList();

            // Assert
            Assert.Equal(1, oldEffectivePackageSources.Count);
            Assert.Equal(TestSourceRepositoryUtility.V2PackageSource.Source, oldEffectivePackageSources[0].PackageSource.Source);

            // Main Act
            var newPackageSources = new List<PackageSource>() { TestSourceRepositoryUtility.V3PackageSource,
                TestSourceRepositoryUtility.V2PackageSource };
            packageSourceProvider.SavePackageSources(newPackageSources);

            var newEffectivePackageSources = sourceRepositoryProvider.GetRepositories().ToList();

            // Main Assert
            Assert.Equal(2, newEffectivePackageSources.Count);
            Assert.Equal(TestSourceRepositoryUtility.V3PackageSource.Source, newEffectivePackageSources[0].PackageSource.Source);
            Assert.Equal(TestSourceRepositoryUtility.V2PackageSource.Source, newEffectivePackageSources[1].PackageSource.Source);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(settingsPath);
        }
    }
}
