using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class NuGetPackageUtilsTests
    {
        [Fact]
        public async Task Test_ExtractPackage()
        {
            // Arrange
            var package = new PackageIdentity("packageA", new NuGetVersion("2.0.3"));
            var packageFileInfo = TestPackages.GetLegacyTestPackage();
            var packagesDirectory = TestFileSystemUtility.CreateRandomTestFolder();
            var versionFolderPathContext = new VersionFolderPathContext(
                package,
                packagesDirectory,
                Logging.NullLogger.Instance,
                fixNuspecIdCasing: false,
                extractNuspecOnly: false,
                normalizeFileNames: false);

            try
            {
                // Act
                using (var packageFileStream = packageFileInfo.OpenRead())
                {
                    await NuGetPackageUtils.InstallFromSourceAsync(
                        stream => packageFileStream.CopyToAsync(stream),
                        versionFolderPathContext,
                        CancellationToken.None);
                }

                // Assert
                var packageIdDirectory = Path.Combine(packagesDirectory, package.Id);
                var packageVersionDirectory = Path.Combine(packageIdDirectory, package.Version.ToNormalizedString());
                Assert.True(Directory.Exists(packageIdDirectory));
                Assert.True(Directory.Exists(packageVersionDirectory));
                Assert.True(File.Exists(Path.Combine(packageVersionDirectory, "packageA.2.0.3.nupkg")));
                Assert.True(File.Exists(Path.Combine(packageVersionDirectory, "packageA.nuspec")));
                Assert.True(File.Exists(Path.Combine(packageVersionDirectory, "packageA.2.0.3.nupkg.sha512")));

                Assert.True(File.Exists(Path.Combine(packageVersionDirectory, @"lib", "test.dll")));
            }
            finally
            {
                TestFileSystemUtility.DeleteRandomTestFolders(packagesDirectory);
            }
        }

        [Fact]
        public async Task Test_ExtractNuspecOnly()
        {
            // Arrange
            var package = new PackageIdentity("packageA", new NuGetVersion("2.0.3"));
            var packageFileInfo = TestPackages.GetLegacyTestPackage();
            var packagesDirectory = TestFileSystemUtility.CreateRandomTestFolder();
            var versionFolderPathContext = new VersionFolderPathContext(
                package,
                packagesDirectory,
                Logging.NullLogger.Instance,
                fixNuspecIdCasing: false,
                extractNuspecOnly: true,
                normalizeFileNames: false);

            try
            {
                // Act
                using (var packageFileStream = packageFileInfo.OpenRead())
                {
                    await NuGetPackageUtils.InstallFromSourceAsync(
                        stream => packageFileStream.CopyToAsync(stream),
                        versionFolderPathContext,
                        CancellationToken.None);
                }

                // Assert
                var packageIdDirectory = Path.Combine(packagesDirectory, package.Id);
                var packageVersionDirectory = Path.Combine(packageIdDirectory, package.Version.ToNormalizedString());
                Assert.True(Directory.Exists(packageIdDirectory));
                Assert.True(Directory.Exists(packageVersionDirectory));
                Assert.True(File.Exists(Path.Combine(packageVersionDirectory, "packageA.2.0.3.nupkg")));
                Assert.True(File.Exists(Path.Combine(packageVersionDirectory, "packageA.nuspec")));
                Assert.True(File.Exists(Path.Combine(packageVersionDirectory, "packageA.2.0.3.nupkg.sha512")));

                Assert.False(File.Exists(Path.Combine(packageVersionDirectory, @"lib", "test.dll")));
            }
            finally
            {
                TestFileSystemUtility.DeleteRandomTestFolders(packagesDirectory);
            }
        }

        [Fact]
        public async Task Test_ExtractNuspecOnly_NormalizeFileNames()
        {
            // Arrange
            var package = new PackageIdentity("packageA", new NuGetVersion("2.0.3"));
            var packageFileInfo = TestPackages.GetLegacyTestPackage();
            var packagesDirectory = TestFileSystemUtility.CreateRandomTestFolder();
            var versionFolderPathContext = new VersionFolderPathContext(
                package,
                packagesDirectory,
                Logging.NullLogger.Instance,
                fixNuspecIdCasing: false,
                extractNuspecOnly: true,
                normalizeFileNames: true);

            try
            {
                // Act
                using (var packageFileStream = packageFileInfo.OpenRead())
                {
                    await NuGetPackageUtils.InstallFromSourceAsync(
                        stream => packageFileStream.CopyToAsync(stream),
                        versionFolderPathContext,
                        CancellationToken.None);
                }

                // Assert
                var packageIdDirectory = Path.Combine(packagesDirectory, package.Id.ToLowerInvariant());
                var packageVersionDirectory = Path.Combine(packageIdDirectory, package.Version.ToNormalizedString());
                Assert.True(Directory.Exists(packageIdDirectory));
                Assert.True(Directory.Exists(packageVersionDirectory));
                Assert.True(File.Exists(Path.Combine(packageVersionDirectory, "packageA.2.0.3.nupkg")));
                Assert.True(File.Exists(Path.Combine(packageVersionDirectory, "packageA.nuspec")));
                Assert.True(File.Exists(Path.Combine(packageVersionDirectory, "packageA.2.0.3.nupkg.sha512")));

                Assert.False(File.Exists(Path.Combine(packageVersionDirectory, @"lib", "test.dll")));

                // The following check ensures that the file name is normalized
                var nuspecFile = Directory.EnumerateFiles(packageVersionDirectory, "*.nuspec").FirstOrDefault();
                Assert.True(nuspecFile.EndsWith("packagea.nuspec", StringComparison.Ordinal));
            }
            finally
            {
                TestFileSystemUtility.DeleteRandomTestFolders(packagesDirectory);
            }
        }
    }
}
