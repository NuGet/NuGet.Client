using System.IO;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Commands.Test
{
    public class RequestFactoryTests
    {
        [Fact]
        public void RequestFactory_FindConfigInProjectFolder()
        {
            // Verifies that we include any config file found in the project folder
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var innerConfigFile = Path.Combine(workingDir, "sub", Settings.DefaultSettingsFileName);
                var outerConfigFile = Path.Combine(workingDir, Settings.DefaultSettingsFileName);

                var projectDirectory = Path.GetDirectoryName(innerConfigFile);
                Directory.CreateDirectory(projectDirectory);

                File.WriteAllText(innerConfigFile, InnerConfig);
                File.WriteAllText(outerConfigFile, OuterConfig);

                var restoreArgs = new RestoreArgs();

                // Act
                var settings = restoreArgs.GetSettings(projectDirectory);
                var innerValue = settings.GetValue("SectionName", "inner-key");
                var outerValue = settings.GetValue("SectionName", "outer-key");

                // Assert
                Assert.Equal("inner-value", innerValue);
                Assert.Equal("outer-value", outerValue);
            }
        }

        [Fact]
        public void RequestFactory_RestorePackagesArgRelativeToCwd()
        {
            // If a packages argument is provided, GetEffectiveGlobalPackagesFolder() should ignore
            // the provided root path and any configuration information and resolve relative to the
            // current working directory.

            // Arrange
            var globalPackagesFolder = "MyPackages";
            var restoreArgs = new RestoreArgs()
            {
                GlobalPackagesFolder = globalPackagesFolder
            };

            // Act
            var resolvedGlobalPackagesFolder = restoreArgs.GetEffectiveGlobalPackagesFolder("C:\\Dummy", null);

            // Assert
            var expectedResolvedGlobalPackagesFolder = Path.GetFullPath(globalPackagesFolder);
            Assert.Equal(expectedResolvedGlobalPackagesFolder, resolvedGlobalPackagesFolder);
        }

        [Fact]
        public async Task RequestFactory_FindProjectJsonFilesInDirectory()
        {
            // Arrange
            var cache = new RestoreCommandProvidersCache();
            var provider = new ProjectJsonRestoreRequestProvider(cache);

            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var p1 = Path.Combine(workingDir, "project.json");
                var p2 = Path.Combine(workingDir, "sub", "project.json");
                var p3 = Path.Combine(workingDir, "myproj.project.json");

                Directory.CreateDirectory(Path.GetDirectoryName(p1));
                Directory.CreateDirectory(Path.GetDirectoryName(p2));

                File.WriteAllText(p1, EmptyProjectJson);
                File.WriteAllText(p2, EmptyProjectJson);
                File.WriteAllText(p3, EmptyProjectJson);

                var context = new RestoreArgs();
                using (var cacheContext = new SourceCacheContext())
                {
                    context.CacheContext = cacheContext;
                    context.Log = new TestLogger();

                    // Act
                    var supports = await provider.Supports(workingDir);
                    var requests = await provider.CreateRequests(workingDir, context);

                    // Assert
                    Assert.Equal(true, supports);
                    Assert.Equal(3, requests.Count);
                }
            }
        }

        private static string EmptyProjectJson = @"
            {
              ""version"": ""1.0.0"",
              ""description"": """",
              ""authors"": [ ""author"" ],
              ""tags"": [ """" ],
              ""projectUrl"": """",
              ""licenseUrl"": """",
              ""frameworks"": {
                ""net45"": {
                }
              }
            }";

        private static string InnerConfig =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
              <configuration>
                <SectionName>
                  <add key=""inner-key"" value=""inner-value"" />
                </SectionName>
              </configuration>";

        private static string OuterConfig =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
              <configuration>
                <SectionName>
                  <add key=""outer-key"" value=""outer-value"" />
                </SectionName>
              </configuration>";
    }
}
