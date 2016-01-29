using Xunit;

namespace NuGet.Packaging.Test
{
    public class PackageHelperTests
    {
        [Theory]
        [InlineData("packageA.nuspec", PackageSaveMode.Nuspec)]
        [InlineData("package.txt", PackageSaveMode.Files)]
        [InlineData("lib/net40/a.dll", PackageSaveMode.Files)]
        [InlineData(@"content\net45\a.js", PackageSaveMode.Files)]
        [InlineData(@"content/[Content_Types].xml", PackageSaveMode.Files)]
        void PackageHelper_IsPackageFile_True(string file, PackageSaveMode packageSaveMode)
        {
            // Arrange & Act
            var isPackageFile = PackageHelper.IsPackageFile(file, packageSaveMode);

            // Assert
            Assert.True(isPackageFile);
        }

        [Theory]
        [InlineData("packageA.nuspec", PackageSaveMode.Files)]
        [InlineData(@"package\services\metadata\core-properties\blahblah.psmdcp", PackageSaveMode.Files)]
        [InlineData(@"package/services/metadata/core-properties/blahblah.psmdcp", PackageSaveMode.Files)]
        [InlineData(@"_rels\._rels", PackageSaveMode.Files)]
        [InlineData("_rels/._rels", PackageSaveMode.Files)]
        [InlineData(@"[Content_Types].xml", PackageSaveMode.Files)]
        void PackageHelper_IsPackageFile_False(string file, PackageSaveMode packageSaveMode)
        {
            // Arrange & Act
            var isPackageFile = PackageHelper.IsPackageFile(file, packageSaveMode);

            // Assert
            Assert.False(isPackageFile);
        }
    }
}
