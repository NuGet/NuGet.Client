using System.IO;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Commands.Test
{
    public class RequestFactoryTests
    {
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
                context.CacheContext = new SourceCacheContext();
                context.Log = new TestLogger();

                // Act
                var supports = await provider.Supports(workingDir);
                var requests = await provider.CreateRequests(workingDir, context);

                // Assert
                Assert.Equal(true, supports);
                Assert.Equal(3, requests.Count);
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
    }
}
