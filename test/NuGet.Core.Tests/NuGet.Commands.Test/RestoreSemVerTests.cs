using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Logging;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Commands.Test
{
    public class RestoreSemVerTests
    {
        // Restore 1.0.0-alpha.1.2.3
        [Fact]
        public async Task RestoreSemVer_RestorePackageWithMultipleReleaseLabels()
        {
            // Arrange
            var sources = new List<PackageSource>();

            var project1Json = @"
            {
              ""version"": ""1.0.0"",
              ""description"": """",
              ""authors"": [ ""author"" ],
              ""tags"": [ """" ],
              ""projectUrl"": """",
              ""licenseUrl"": """",
              ""frameworks"": {
                ""netstandard1.5"": {
                    ""dependencies"": {
                        ""packageA"": ""1.0.0-alpha.1.2.3""
                    }
                }
              }
            }";

            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var packagesDir = new DirectoryInfo(Path.Combine(workingDir, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(workingDir, "packageSource"));
                var project1 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project1"));
                packagesDir.Create();
                packageSource.Create();
                project1.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                File.WriteAllText(Path.Combine(project1.FullName, "project.json"), project1Json);

                var specPath1 = Path.Combine(project1.FullName, "project.json");
                var spec1 = JsonPackageSpecReader.GetPackageSpec(project1Json, "project1", specPath1);

                var logger = new TestLogger();
                var request = new RestoreRequest(spec1, sources, packagesDir.FullName, logger);

                request.LockFilePath = Path.Combine(project1.FullName, "project.lock.json");

                var packageAContext = new SimpleTestPackageContext("packageA")
                {
                    Version = "1.0.0-alpha.1.2.3"
                };

                packageAContext.AddFile("lib/netstandard1.5/a.dll");

                SimpleTestPackageUtility.CreateFullPackage(packageSource.FullName, packageAContext);

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                var lockFile = result.LockFile;
                await result.CommitAsync(logger, CancellationToken.None);

                var targetLib = lockFile.GetTarget(NuGetFramework.Parse("netstandard1.5"), null)
                    .Libraries
                    .Single(library => library.Name == "packageA");

                var compile = targetLib.CompileTimeAssemblies.Single();

                // Assert
                Assert.True(result.Success);
                Assert.Equal("lib/netstandard1.5/a.dll", compile.Path);
                Assert.Equal("1.0.0-alpha.1.2.3", targetLib.Version.ToNormalizedString());
            }
        }
    }
}
