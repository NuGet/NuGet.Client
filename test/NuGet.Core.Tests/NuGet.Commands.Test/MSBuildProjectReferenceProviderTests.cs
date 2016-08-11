using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Commands.Test
{
    public class MSBuildProjectReferenceProviderTests
    {

        [Fact]
        public void MSBuildProjectReferenceProvider_EmptyFile()
        {
            // Arrange
            var lines = new List<string>();

            // Act
            var provider = new MSBuildProjectReferenceProvider(lines);

            // Assert
            Assert.Equal(0, provider.GetReferences("testpath").Count);
            Assert.Equal(0, provider.GetEntryPoints().Count);
        }

        [Fact]
        public void MSBuildProjectReferenceProvider_NoEdges()
        {
            // Arrange
            var lines = new List<string>();
            lines.Add("#:/tmp/project1.csproj");
            lines.Add("#:/tmp/project2.csproj");

            // Act
            var provider = new MSBuildProjectReferenceProvider(lines);

            // Assert
            Assert.Equal(1, provider.GetReferences("/tmp/project1.csproj").Count);
            Assert.Equal(1, provider.GetReferences("/tmp/project2.csproj").Count);
            Assert.Equal("/tmp/project1.csproj", provider.GetReferences("/tmp/project1.csproj").Single().MSBuildProjectPath);
            Assert.Null(provider.GetReferences("/tmp/project1.csproj").Single().PackageSpecPath);
            Assert.Equal(2, provider.GetEntryPoints().Count);
        }

        [Fact]
        public void MSBuildProjectReferenceProvider_FindProjectJson()
        {
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var project1Dir = Path.Combine(workingDir, "project1");
                Directory.CreateDirectory(project1Dir);
                var project1Json = Path.Combine(project1Dir, "project.json");
                var project1proj = Path.Combine(project1Dir, "project1.csproj");

                var project2Dir = Path.Combine(workingDir, "project2");
                Directory.CreateDirectory(project2Dir);
                var project2Json = Path.Combine(project2Dir, "project2.project.json");
                var project2proj = Path.Combine(project2Dir, "project2.csproj");

                File.WriteAllText(project1Json, ProjectJson);
                File.WriteAllText(project2Json, ProjectJson);
                File.WriteAllText(project1proj, string.Empty);
                File.WriteAllText(project2proj, string.Empty);

                var lines = new List<string>();
                lines.Add($"#:{project1proj}");
                lines.Add($"=:{project1proj}|{project2proj}");

                // Act
                var provider = new MSBuildProjectReferenceProvider(lines);

                var references = provider.GetReferences(project1proj);
                var root = references.Where(file => file.ExternalProjectReferences.Count > 0).Single();
                var child = references.Where(file => file.ExternalProjectReferences.Count == 0).Single();

                // Assert
                Assert.Equal(1, provider.GetEntryPoints().Count);
                Assert.Equal(project1Json, root.PackageSpec.FilePath);
                Assert.Equal(project2Json, child.PackageSpec.FilePath);
                Assert.Equal(project1proj, root.MSBuildProjectPath);
                Assert.Equal(project2proj, child.MSBuildProjectPath);
            }
        }

        private const string ProjectJson = @"
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
