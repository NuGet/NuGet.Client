using Xunit;

namespace NuGet.Packaging.Test
{
    public class PackageHelperTests
    {
        [Theory]
        [InlineData("packageA.nuspec", PackageSaveModes.Nuspec)]
        [InlineData("package.txt", PackageSaveModes.Nuspec)]
        [InlineData("lib/net40/a.dll", PackageSaveModes.Nupkg)]
        [InlineData(@"content\net45\a.js", PackageSaveModes.Nupkg)]
        [InlineData(@"content/[Content_Types].xml", PackageSaveModes.Nupkg)]
        void PackageHelper_IsPackageFile_True(string file, PackageSaveModes packageSaveMode)
        {
            // Arrange & Act
            var isPackageFile = PackageHelper.IsPackageFile(file, packageSaveMode);

            // Assert
            Assert.True(isPackageFile);
        }

        [Theory]
        [InlineData("packageA.nuspec", PackageSaveModes.Nupkg)]
        [InlineData(@"package\services\metadata\core-properties\blahblah.psmdcp", PackageSaveModes.Nupkg)]
        [InlineData(@"package/services/metadata/core-properties/blahblah.psmdcp", PackageSaveModes.Nupkg)]
        [InlineData(@"_rels\._rels", PackageSaveModes.Nupkg)]
        [InlineData("_rels/._rels", PackageSaveModes.Nupkg)]
        [InlineData(@"[Content_Types].xml", PackageSaveModes.Nupkg)]
        void PackageHelper_IsPackageFile_False(string file, PackageSaveModes packageSaveMode)
        {
            // Arrange & Act
            var isPackageFile = PackageHelper.IsPackageFile(file, packageSaveMode);

            // Assert
            Assert.False(isPackageFile);
        }
    }
}
