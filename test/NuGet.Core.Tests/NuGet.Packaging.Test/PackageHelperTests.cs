using Xunit;

namespace NuGet.Packaging.Test
{
    public class PackageHelperTests
    {
        [Theory]
        [InlineData("packageA.nuspec", PackageSaveMode.Nuspec)]
        [InlineData("package.txt", PackageSaveMode.Nuspec)]
        [InlineData("lib/net40/a.dll", PackageSaveMode.Nupkg)]
        [InlineData(@"content\net45\a.js", PackageSaveMode.Nupkg)]
        [InlineData(@"content/[Content_Types].xml", PackageSaveMode.Nupkg)]
        void PackageHelper_IsPackageFile_True(string file, PackageSaveMode packageSaveMode)
        {
            // Arrange & Act
            var isPackageFile = PackageHelper.IsPackageFile(file, packageSaveMode);

            // Assert
            Assert.True(isPackageFile);
        }

        [Theory]
        [InlineData("packageA.nuspec", PackageSaveMode.Nupkg)]
        [InlineData(@"package\services\metadata\core-properties\blahblah.psmdcp", PackageSaveMode.Nupkg)]
        [InlineData(@"package/services/metadata/core-properties/blahblah.psmdcp", PackageSaveMode.Nupkg)]
        [InlineData(@"_rels\._rels", PackageSaveMode.Nupkg)]
        [InlineData("_rels/._rels", PackageSaveMode.Nupkg)]
        [InlineData(@"[Content_Types].xml", PackageSaveMode.Nupkg)]
        void PackageHelper_IsPackageFile_False(string file, PackageSaveMode packageSaveMode)
        {
            // Arrange & Act
            var isPackageFile = PackageHelper.IsPackageFile(file, packageSaveMode);

            // Assert
            Assert.False(isPackageFile);
        }
    }
}
