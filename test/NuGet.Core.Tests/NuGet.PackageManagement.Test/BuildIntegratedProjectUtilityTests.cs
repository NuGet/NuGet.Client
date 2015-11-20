using System.IO;
using NuGet.ProjectManagement;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Test
{
    public class BuildIntegratedProjectUtilityTests
    {
        [Fact]
        public void GetEffectiveGlobalPackagesFolder_RelativePath()
        {
            // Arrange
            var configContents = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
<config>
<add key=""globalPackagesFolder"" value=""..\..\NuGetPackages"" />
</config>
</configuration>";

            using (var configFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                File.WriteAllText(Path.Combine(configFolder, "nuget.config"), configContents);

                var settings = new Configuration.Settings(configFolder);

                // Act
                var effectivePackagesFolderPath = BuildIntegratedProjectUtility.GetEffectiveGlobalPackagesFolder(
                    @"c:\level1\level2\level3\level4",
                    settings);

                // Assert
                Assert.Equal(@"c:\level1\level2\NuGetPackages", effectivePackagesFolderPath);
            }
        }
    }
}
