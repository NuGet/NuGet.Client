using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.PackageManagement;
using NuGet.ProjectManagement.Projects;
using Test.Utility;
using Xunit;

namespace NuGet.Test
{
    public class BuildIntegratedRestoreUtilityTests
    {
        [Fact]
        public async Task BuildIntegratedRestoreUtility_BasicRestoreTest()
        {
            // Arrange
            string projectName = "testproj";

            var rootFolder = TestFilesystemUtility.CreateRandomTestFolder();
            var projectFolder = new DirectoryInfo(Path.Combine(rootFolder, projectName));
            projectFolder.Create();
            var projectConfig = new FileInfo(Path.Combine(projectFolder.FullName, "project.json"));

            CreateConfigJson(projectConfig.FullName);

            List<string> sources = new List<string>()
            {
                "https://www.nuget.org/api/v2/"
            };

            var projectTargetFramework = NuGetFramework.Parse("netcore50");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var project = new BuildIntegratedNuGetProject(projectConfig.FullName, msBuildNuGetProjectSystem);

            // Act
            var result = await BuildIntegratedRestoreUtility.Restore(project, new TestNuGetProjectContext(), sources, CancellationToken.None);

            // Assert
            Assert.True(File.Exists(Path.Combine(projectFolder.FullName, "project.lock.json")));
            Assert.True(result.Success);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(rootFolder);
        }

        private static void CreateConfigJson(string path)
        {
            using (var writer = new StreamWriter(path))
            {
                writer.Write(BasicConfig.ToString());
            }
        }

        private static JObject BasicConfig
        {
            get
            {
                JObject json = new JObject();

                JObject frameworks = new JObject();
                frameworks["netcore50"] = new JObject();

                json["frameworks"] = frameworks;

                json.Add("runtimes", JObject.Parse("{ \"uap10-x86\": { }, \"uap10-x86-aot\": { } }"));

                return json;
            }
        }
    }
}
