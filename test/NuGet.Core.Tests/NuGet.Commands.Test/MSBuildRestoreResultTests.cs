using System.IO;
using System.Linq;
using System.Xml.Linq;
using NuGet.Configuration;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Commands.Test
{
    public class MSBuildRestoreResultTests
    {
        [Fact]
        public void MSBuildRestoreResult_ReplaceWithUserProfileMacro()
        {
            // Arrange
            var randomProjectDirectory = TestFileSystemUtility.CreateRandomTestFolder();
            try
            {
                var projectName = "testproject";
                var globalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(NullSettings.Instance);

                if (!string.IsNullOrEmpty(globalPackagesFolder))
                {
                    // Only run the test if globalPackagesFolder can be determined
                    // Because, globalPackagesFolder would be null if %USERPROFILE% was null

                    var msBuildRestoreResult = new MSBuildRestoreResult(
                        projectName,
                        randomProjectDirectory,
                        globalPackagesFolder,
                        Enumerable.Empty<string>(),
                        new[] { "blah" });

                    var targetsName = $"{projectName}.nuget.targets";
                    var targetsPath = Path.Combine(randomProjectDirectory, targetsName);

                    // Assert
                    Assert.False(File.Exists(targetsPath));

                    // Act
                    msBuildRestoreResult.Commit(Logging.NullLogger.Instance);
                    
                    Assert.True(File.Exists(targetsPath));
                    var xml = XDocument.Load(targetsPath);
                    var ns = XNamespace.Get("http://schemas.microsoft.com/developer/msbuild/2003");
                    var elements = xml.Root.Descendants(ns + "NuGetPackageRoot");
                    Assert.Single(elements);

                    var element = elements.Single();

                    var expected = Path.Combine(@"$(UserProfile)", ".nuget", "packages") + Path.DirectorySeparatorChar;
                    Assert.Equal(expected, element.Value);
                }
            }
            finally
            {
                TestFileSystemUtility.DeleteRandomTestFolders(randomProjectDirectory);
            }
        }
    }
}
