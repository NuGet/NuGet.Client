using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Commands.Test
{
    public class Project2ProjectTests
    {
        [Fact]
        public async Task Project2ProjectInLockFile_VerifyProjectReferenceWithoutVersion()
        {
            // Arrange
            var sources = new List<PackageSource>();

            var project1Json = @"
            {
              ""version"": ""2.0.0-*"",
              ""description"": ""Proj1 Class Library"",
              ""authors"": [ ""author"" ],
              ""tags"": [ """" ],
              ""projectUrl"": """",
              ""licenseUrl"": """",
              ""dependencies"": {
                ""project2"": { ""target"": ""project"" }
              },
              ""frameworks"": {
                ""net45"": {
                }
              }
            }";

            var project2Json = @"
            {
              ""version"": ""2.0.0-*"",
              ""description"": ""Proj2 Class Library"",
              ""authors"": [ ""author"" ],
              ""tags"": [ """" ],
              ""projectUrl"": """",
              ""licenseUrl"": """",
              ""frameworks"": {
                ""net45"": {
                }
              }
            }";

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var project1 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project1"));
                var project2 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project2"));
                project1.Create();
                project2.Create();

                File.WriteAllText(Path.Combine(project1.FullName, "project.json"), project1Json);
                File.WriteAllText(Path.Combine(project2.FullName, "project.json"), project2Json);

                var specPath1 = Path.Combine(project1.FullName, "project.json");
                var specPath2 = Path.Combine(project2.FullName, "project.json");
                var spec1 = JsonPackageSpecReader.GetPackageSpec(project1Json, "project1", specPath1);
                var spec2 = JsonPackageSpecReader.GetPackageSpec(project2Json, "project2", specPath2);

                var logger = new TestLogger();
                var request = new RestoreRequest(spec1, sources, packagesDir, logger);

                request.LockFilePath = Path.Combine(project1.FullName, "project.lock.json");
                var format = new LockFileFormat();

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                result.Commit(logger);

                var lockFile = format.Read(request.LockFilePath, logger);

                var project2Reference = lockFile.ProjectFileDependencyGroups.First().Dependencies.Single();

                // Assert
                Assert.True(result.Success);

                Assert.Equal("project2", project2Reference);
            }
        }

        [Fact]
        public async Task Project2ProjectInLockFile_VerifySnapshotVersionsXProj()
        {
            // Arrange
            var sources = new List<PackageSource>();

            var project1Json = @"
            {
              ""version"": ""2.0.0-*"",
              ""description"": ""Proj1 Class Library"",
              ""authors"": [ ""author"" ],
              ""tags"": [ """" ],
              ""projectUrl"": """",
              ""licenseUrl"": """",
              ""dependencies"": {
                ""project2"": ""2.0.0-*""
              },
              ""frameworks"": {
                ""net45"": {
                }
              }
            }";

            var project2Json = @"
            {
              ""version"": ""2.0.0-*"",
              ""description"": ""Proj2 Class Library"",
              ""authors"": [ ""author"" ],
              ""tags"": [ """" ],
              ""projectUrl"": """",
              ""licenseUrl"": """",
              ""frameworks"": {
                ""net45"": {
                }
              }
            }";

            var globalJson = @"
            {
                ""projects"": [
                    ""projects""
                ]
            }";

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var project1 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project1"));
                var project2 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project2"));
                project1.Create();
                project2.Create();

                File.WriteAllText(Path.Combine(project1.FullName, "project.json"), project1Json);
                File.WriteAllText(Path.Combine(project2.FullName, "project.json"), project2Json);
                File.WriteAllText(Path.Combine(workingDir, "global.json"), globalJson);

                File.WriteAllText(Path.Combine(project1.FullName, "project1.csproj"), string.Empty);
                File.WriteAllText(Path.Combine(project2.FullName, "project2.xproj"), string.Empty);

                var specPath1 = Path.Combine(project1.FullName, "project.json");
                var specPath2 = Path.Combine(project2.FullName, "project.json");
                var spec1 = JsonPackageSpecReader.GetPackageSpec(project1Json, "project1", specPath1);
                var spec2 = JsonPackageSpecReader.GetPackageSpec(project2Json, "project2", specPath2);

                var logger = new TestLogger();
                var request = new RestoreRequest(spec1, sources, packagesDir, logger);

                request.LockFilePath = Path.Combine(project1.FullName, "project.lock.json");
                var format = new LockFileFormat();

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                result.Commit(logger);

                var lockFile = format.Read(request.LockFilePath, logger);

                var project2Lib = lockFile.GetLibrary("project2", NuGetVersion.Parse("2.0.0"));

                var project2Target = lockFile.GetTarget(FrameworkConstants.CommonFrameworks.Net45, runtimeIdentifier: null)
                    .Libraries
                    .Single(lib => lib.Name == "project2");

                // Assert
                Assert.True(result.Success);

                Assert.Equal("2.0.0", project2Target.Version.ToString());
                Assert.Equal("2.0.0", project2Lib.Version.ToString());
            }
        }

        [Fact]
        public async Task Project2ProjectInLockFile_VerifySnapshotVersions()
        {
            // Arrange
            var sources = new List<PackageSource>();

            var projectJson = @"
            {
                ""version"": ""2.0.0"",
                ""dependencies"": {
                },
                ""frameworks"": {
                    ""net45"": {}
                }
            }";

            var project2Json = @"
            {
              ""version"": ""2.0.0-*"",
              ""description"": ""Proj2 Class Library"",
              ""authors"": [ ""author"" ],
              ""tags"": [ """" ],
              ""projectUrl"": """",
              ""licenseUrl"": """",
              ""dependencies"": {
                ""project3"": ""2.0.0-*""
              },
              ""frameworks"": {
                ""net45"": {
                }
              }
            }";

            var project3Json = @"
            {
              ""version"": ""2.0.0-*"",
              ""description"": ""Proj3 Class Library"",
              ""authors"": [ ""author"" ],
              ""tags"": [ """" ],
              ""projectUrl"": """",
              ""licenseUrl"": """",
              ""frameworks"": {
                ""net45"": {
                }
              }
            }";

            var globalJson = @"
            {
                ""projects"": [
                    ""projects""
                ]
            }";

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var project1 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project1"));
                var project2 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project2"));
                var project3 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project3"));
                project1.Create();
                project2.Create();
                project3.Create();

                File.WriteAllText(Path.Combine(project1.FullName, "project.json"), projectJson);
                File.WriteAllText(Path.Combine(project2.FullName, "project.json"), project2Json);
                File.WriteAllText(Path.Combine(project3.FullName, "project.json"), project3Json);
                File.WriteAllText(Path.Combine(workingDir, "global.json"), globalJson);

                File.WriteAllText(Path.Combine(project1.FullName, "project1.csproj"), string.Empty);
                File.WriteAllText(Path.Combine(project2.FullName, "project2.xproj"), string.Empty);
                File.WriteAllText(Path.Combine(project2.FullName, "project3.xproj"), string.Empty);

                var specPath1 = Path.Combine(project1.FullName, "project.json");
                var specPath2 = Path.Combine(project2.FullName, "project.json");
                var specPath3 = Path.Combine(project3.FullName, "project.json");
                var spec1 = JsonPackageSpecReader.GetPackageSpec(projectJson, "project1", specPath1);
                var spec2 = JsonPackageSpecReader.GetPackageSpec(project2Json, "project2", specPath2);
                var spec3 = JsonPackageSpecReader.GetPackageSpec(project3Json, "project3", specPath3);

                var logger = new TestLogger();
                var request = new RestoreRequest(spec1, sources, packagesDir, logger);
                request.ExternalProjects.Add(new ExternalProjectReference(
                    "project1",
                    spec1,
                    Path.Combine(project1.FullName, "project1.csproj"),
                    new string[] { "project2" }));

                request.ExternalProjects.Add(new ExternalProjectReference(
                    "project2",
                    spec2,
                    Path.Combine(project2.FullName, "project2.xproj"),
                    new string[] { "project3" }));

                request.ExternalProjects.Add(new ExternalProjectReference(
                    "project3",
                    spec3,
                    Path.Combine(project2.FullName, "project3.xproj"),
                    new string[] { }));


                request.LockFilePath = Path.Combine(project1.FullName, "project.lock.json");
                var format = new LockFileFormat();

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                result.Commit(logger);

                var lockFile = format.Read(request.LockFilePath, logger);

                var project2Lib = lockFile.GetLibrary("project2", NuGetVersion.Parse("2.0.0"));
                var project3Lib = lockFile.GetLibrary("project3", NuGetVersion.Parse("2.0.0"));

                var project2Target = lockFile.GetTarget(FrameworkConstants.CommonFrameworks.Net45, runtimeIdentifier: null)
                    .Libraries
                    .Single(lib => lib.Name == "project2");

                var project3Target = lockFile.GetTarget(FrameworkConstants.CommonFrameworks.Net45, runtimeIdentifier: null)
                    .Libraries
                    .Single(lib => lib.Name == "project3");

                // Assert
                Assert.True(result.Success);

                Assert.Equal("2.0.0", project2Target.Version.ToString());
                Assert.Equal("2.0.0", project3Target.Version.ToString());
                Assert.Equal("2.0.0", project2Lib.Version.ToString());
                Assert.Equal("2.0.0", project3Lib.Version.ToString());

                Assert.Equal("2.0.0", project2Target.Dependencies.Single().VersionRange.ToLegacyShortString());
            }
        }

        [Fact]
        public async Task Project2ProjectInLockFile_PackageReferenceConflict()
        {
            // Arrange
            var sources = new List<PackageSource>()
            {
                new PackageSource("https://api.nuget.org/v3/index.json")
            };

            var project1Json = @"
            {
                ""version"": ""1.0.0"",
                ""dependencies"": {
                },
                ""frameworks"": {
                    ""net45"": {}
                }
            }";

            var project2Json = @"
            {
                ""version"": ""1.0.0"",
                ""dependencies"": {
                    ""Microsoft.VisualBasic"": ""10.0.0""
                },
                ""frameworks"": {
                    ""net45"": {}
                }
            }";

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var project1 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project1"));
                var project2 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project2"));
                project1.Create();
                project2.Create();

                File.WriteAllText(Path.Combine(project1.FullName, "project.json"), project1Json);
                File.WriteAllText(Path.Combine(project2.FullName, "project.json"), project2Json);

                File.WriteAllText(Path.Combine(project1.FullName, "project1.csproj"), string.Empty);
                File.WriteAllText(Path.Combine(project2.FullName, "project2.csproj"), string.Empty);

                var specPath1 = Path.Combine(project1.FullName, "project.json");
                var specPath2 = Path.Combine(project2.FullName, "project.json");
                var spec1 = JsonPackageSpecReader.GetPackageSpec(project1Json, "project1", specPath1);
                var spec2 = JsonPackageSpecReader.GetPackageSpec(project2Json, "project2", specPath2);

                var logger = new TestLogger();
                var request = new RestoreRequest(spec1, sources, packagesDir, logger);
                request.ExternalProjects.Add(new ExternalProjectReference(
                    "project1",
                    spec1,
                    Path.Combine(project1.FullName, "project1.csproj"),
                    new string[] { "project2" }));

                request.ExternalProjects.Add(new ExternalProjectReference(
                    "project2",
                    spec2,
                    Path.Combine(project2.FullName, "project2.csproj"),
                    new string[] { }));

                request.LockFilePath = Path.Combine(project1.FullName, "project.lock.json");
                var format = new LockFileFormat();

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                result.Commit(logger);

                // Assert
                Assert.True(result.Success);
            }
        }

        [Fact]
        public async Task Project2ProjectInLockFile_CSProjToXProj()
        {
            // Arrange
            var sources = new List<PackageSource>();

            var projectJson = @"
            {
                ""version"": ""1.0.0"",
                ""dependencies"": {
                },
                ""frameworks"": {
                    ""net45"": {}
                }
            }";

            var project2Json = @"
            {
              ""version"": ""1.0.0-*"",
              ""description"": ""Proj2 Class Library"",
              ""authors"": [ ""author"" ],
              ""tags"": [ """" ],
              ""projectUrl"": """",
              ""licenseUrl"": """",

              ""frameworks"": {
                ""net45"": {
                }
              }
            }";

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var project1 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project1"));
                var project2 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project2"));
                project1.Create();
                project2.Create();

                File.WriteAllText(Path.Combine(project1.FullName, "project.json"), projectJson);
                File.WriteAllText(Path.Combine(project2.FullName, "project.json"), project2Json);

                File.WriteAllText(Path.Combine(project1.FullName, "project1.csproj"), string.Empty);
                File.WriteAllText(Path.Combine(project2.FullName, "project2.xproj"), string.Empty);

                var specPath1 = Path.Combine(project1.FullName, "project.json");
                var specPath2 = Path.Combine(project2.FullName, "project.json");
                var spec1 = JsonPackageSpecReader.GetPackageSpec(projectJson, "project1", specPath1);
                var spec2 = JsonPackageSpecReader.GetPackageSpec(project2Json, "project2", specPath2);

                var logger = new TestLogger();
                var request = new RestoreRequest(spec1, sources, packagesDir, logger);
                request.ExternalProjects.Add(new ExternalProjectReference(
                    "project1",
                    spec1,
                    Path.Combine(project1.FullName, "project1.xproj"),
                    new string[] { "project2" }));

                request.ExternalProjects.Add(new ExternalProjectReference(
                    "project2",
                    spec2,
                    Path.Combine(project2.FullName, "project2.xproj"),
                    new string[] { }));

                request.LockFilePath = Path.Combine(project1.FullName, "project.lock.json");
                var format = new LockFileFormat();

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                result.Commit(logger);

                var lockFile = format.Read(request.LockFilePath, logger);

                var project2Lib = lockFile.GetLibrary("project2", NuGetVersion.Parse("1.0.0"));

                var project2Target = lockFile.GetTarget(FrameworkConstants.CommonFrameworks.Net45, runtimeIdentifier: null)
                    .Libraries
                    .Where(lib => lib.Name == "project2")
                    .Single();

                // Assert
                Assert.True(result.Success);
                Assert.Equal(1, lockFile.Libraries.Count);

                Assert.Equal("project", project2Lib.Type);
                Assert.Equal("../project2/project.json", project2Lib.Path);
                Assert.Equal("../project2/project2.xproj", project2Lib.MSBuildProject);

                Assert.Equal(".NETFramework,Version=v4.5", project2Target.Framework);
                Assert.Equal(1, project2Target.CompileTimeAssemblies.Count);
                Assert.Equal("net45/project2.dll", project2Target.CompileTimeAssemblies.Single().Path);
            }
        }

        [Fact]
        public async Task Project2ProjectInLockFile_VerifyP2PWithNonProjectJsonReference()
        {
            // Arrange
            var sources = new List<PackageSource>();

            var projectJson = @"
            {
                ""version"": ""1.0.0"",
                ""dependencies"": {
                },
                ""frameworks"": {
                    ""net45"": {}
                }
            }";

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var project1 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project1"));
                var project2 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project2"));
                var project3 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project3"));
                project1.Create();
                project2.Create();
                project3.Create();

                File.WriteAllText(Path.Combine(project1.FullName, "project.json"), projectJson);

                File.WriteAllText(Path.Combine(project1.FullName, "project1.csproj"), string.Empty);
                File.WriteAllText(Path.Combine(project2.FullName, "project2.csproj"), string.Empty);
                File.WriteAllText(Path.Combine(project3.FullName, "project3.csproj"), string.Empty);

                var specPath1 = Path.Combine(project1.FullName, "project.json");
                var spec1 = JsonPackageSpecReader.GetPackageSpec(projectJson, "project1", specPath1);

                var logger = new TestLogger();
                var request = new RestoreRequest(spec1, sources, packagesDir, logger);
                request.ExternalProjects.Add(new ExternalProjectReference(
                    "project1",
                    spec1,
                    Path.Combine(project1.FullName, "project1.xproj"),
                    new string[] { "project2" }));

                request.ExternalProjects.Add(new ExternalProjectReference(
                    "project2",
                    null,
                    Path.Combine(project2.FullName, "project2.csproj"),
                    new string[] { "project3" }));

                request.ExternalProjects.Add(new ExternalProjectReference(
                    "project3",
                    null,
                    Path.Combine(project3.FullName, "project3.csproj"),
                    new string[] { }));

                request.LockFilePath = Path.Combine(project1.FullName, "project.lock.json");
                var format = new LockFileFormat();

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                result.Commit(logger);

                var lockFile = format.Read(request.LockFilePath, logger);

                var project1Lib = lockFile.GetLibrary("project1", NuGetVersion.Parse("1.0.0"));
                var project2Lib = lockFile.GetLibrary("project2", NuGetVersion.Parse("1.0.0"));
                var project3Lib = lockFile.GetLibrary("project3", NuGetVersion.Parse("1.0.0"));

                var project2Target = lockFile.GetTarget(FrameworkConstants.CommonFrameworks.Net45, runtimeIdentifier: null)
                    .Libraries
                    .Where(lib => lib.Name == "project2")
                    .Single();

                var project3Target = lockFile.GetTarget(FrameworkConstants.CommonFrameworks.Net45, runtimeIdentifier: null)
                    .Libraries
                    .Where(lib => lib.Name == "project3")
                    .Single();

                // Assert
                Assert.True(result.Success);
                Assert.Equal(2, lockFile.Libraries.Count);

                Assert.Equal("project", project2Lib.Type);
                Assert.Equal("project", project3Lib.Type);

                Assert.Null(project2Lib.Path);
                Assert.Null(project3Lib.Path);

                Assert.Equal("../project2/project2.csproj", project2Lib.MSBuildProject);
                Assert.Equal("../project3/project3.csproj", project3Lib.MSBuildProject);

                Assert.Equal(1, project2Target.Dependencies.Count);
                Assert.Equal("project3", project2Target.Dependencies.Single().Id);
                Assert.Equal("1.0.0", project2Target.Dependencies.Single().VersionRange.ToLegacyShortString());

                Assert.Equal(0, project3Target.Dependencies.Count);

                Assert.Null(project2Target.Framework);
                Assert.Null(project3Target.Framework);
            }
        }

        [Fact]
        public async Task Project2ProjectInLockFile_VerifyProjectsUnderProjectFileDependencyGroups_External()
        {
            // Arrange
            var sources = new List<PackageSource>();

            var projectJson = @"
            {
                ""version"": ""1.0.0"",
                ""dependencies"": {
                },
                ""frameworks"": {
                    ""net45"": {}
                }
            }";

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var project1 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project1"));
                var project2 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project2"));
                var project3 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project3"));
                project1.Create();
                project2.Create();
                project3.Create();

                File.WriteAllText(Path.Combine(project1.FullName, "project.json"), projectJson);
                File.WriteAllText(Path.Combine(project2.FullName, "project.json"), projectJson);
                File.WriteAllText(Path.Combine(project3.FullName, "project.json"), projectJson);

                File.WriteAllText(Path.Combine(project1.FullName, "project1.csproj"), string.Empty);
                File.WriteAllText(Path.Combine(project2.FullName, "project2.csproj"), string.Empty);
                File.WriteAllText(Path.Combine(project3.FullName, "project3.csproj"), string.Empty);

                var specPath1 = Path.Combine(project1.FullName, "project.json");
                var specPath2 = Path.Combine(project2.FullName, "project.json");
                var specPath3 = Path.Combine(project3.FullName, "project.json");
                var spec1 = JsonPackageSpecReader.GetPackageSpec(projectJson, "project1", specPath1);
                var spec2 = JsonPackageSpecReader.GetPackageSpec(projectJson, "project2", specPath2);
                var spec3 = JsonPackageSpecReader.GetPackageSpec(projectJson, "project3", specPath3);

                var logger = new TestLogger();
                var request = new RestoreRequest(spec1, sources, packagesDir, logger);
                request.ExternalProjects.Add(new ExternalProjectReference(
                    "project1",
                    spec1,
                    Path.Combine(project1.FullName, "project1.xproj"),
                    new string[] { "project2" }));

                request.ExternalProjects.Add(new ExternalProjectReference(
                    "project2",
                    spec2,
                    Path.Combine(project2.FullName, "project2.csproj"),
                    new string[] { "project3" }));

                request.ExternalProjects.Add(new ExternalProjectReference(
                    "project3",
                    spec3,
                    Path.Combine(project3.FullName, "project3.csproj"),
                    new string[] { }));

                request.LockFilePath = Path.Combine(project1.FullName, "project.lock.json");
                var format = new LockFileFormat();

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                result.Commit(logger);

                var lockFile = format.Read(request.LockFilePath, logger);

                var project1Lib = lockFile.GetLibrary("project1", NuGetVersion.Parse("1.0.0"));
                var project2Lib = lockFile.GetLibrary("project2", NuGetVersion.Parse("1.0.0"));
                var project3Lib = lockFile.GetLibrary("project3", NuGetVersion.Parse("1.0.0"));

                var project2Target = lockFile.GetTarget(FrameworkConstants.CommonFrameworks.Net45, runtimeIdentifier: null)
                    .Libraries
                    .Where(lib => lib.Name == "project2")
                    .Single();

                var project3Target = lockFile.GetTarget(FrameworkConstants.CommonFrameworks.Net45, runtimeIdentifier: null)
                    .Libraries
                    .Where(lib => lib.Name == "project3")
                    .Single();

                // Assert
                Assert.True(result.Success);
                Assert.Equal(2, lockFile.Libraries.Count);

                Assert.Equal("project", project2Lib.Type);
                Assert.Equal("project", project3Lib.Type);

                Assert.Equal("../project2/project.json", project2Lib.Path);
                Assert.Equal("../project3/project.json", project3Lib.Path);

                Assert.Equal("../project2/project2.csproj", project2Lib.MSBuildProject);
                Assert.Equal("../project3/project3.csproj", project3Lib.MSBuildProject);

                Assert.Equal(1, project2Target.Dependencies.Count);
                Assert.Equal("project3", project2Target.Dependencies.Single().Id);
                Assert.Equal("1.0.0", project2Target.Dependencies.Single().VersionRange.ToLegacyShortString());

                Assert.Equal(0, project3Target.Dependencies.Count);

                Assert.Equal(".NETFramework,Version=v4.5", project2Target.Framework);
                Assert.Equal(".NETFramework,Version=v4.5", project3Target.Framework);
            }
        }

        [Fact]
        public async Task Project2ProjectInLockFile_VerifyProjectsReferencesInLibAndTargets()
        {
            // Arrange
            var sources = new List<PackageSource>();

            var project1Json = @"
            {
                ""version"": ""1.0.0"",
                ""dependencies"": {
                    ""project2"": ""1.0.0-*"",
                },
                ""frameworks"": {
                    ""net45"": {}
                }
            }";

            var project2Json = @"
            {
                ""version"": ""1.0.0"",
                ""dependencies"": {
                    ""project3"": ""1.0.0-*"",
                },
                ""frameworks"": {
                    ""net45"": {}
                }
            }";

            var project3Json = @"
            {
                ""version"": ""1.0.0"",
                ""dependencies"": {
                },
                ""frameworks"": {
                    ""net45"": {}
                }
            }";

            var globalJson = @"
            {
                ""projects"": [
                    ""projects""
                ]
            }";

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var project1 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project1"));
                var project2 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project2"));
                var project3 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project3"));
                project1.Create();
                project2.Create();
                project3.Create();

                File.WriteAllText(Path.Combine(project1.FullName, "project.json"), project1Json);
                File.WriteAllText(Path.Combine(project2.FullName, "project.json"), project2Json);
                File.WriteAllText(Path.Combine(project3.FullName, "project.json"), project3Json);
                File.WriteAllText(Path.Combine(workingDir, "global.json"), globalJson);

                File.WriteAllText(Path.Combine(project1.FullName, "project1.xproj"), string.Empty);
                File.WriteAllText(Path.Combine(project2.FullName, "project2.xproj"), string.Empty);
                File.WriteAllText(Path.Combine(project3.FullName, "project3.xproj"), string.Empty);

                var specPath1 = Path.Combine(project1.FullName, "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(project1Json, "project1", specPath1);

                var specPath2 = Path.Combine(project2.FullName, "project.json");
                var specPath3 = Path.Combine(project3.FullName, "project.json");

                var logger = new TestLogger();
                var request = new RestoreRequest(spec, sources, packagesDir, logger);

                request.LockFilePath = Path.Combine(project1.FullName, "project.lock.json");
                var format = new LockFileFormat();

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                result.Commit(logger);

                var lockFile = format.Read(request.LockFilePath, logger);

                var project1Lib = lockFile.GetLibrary("project1", NuGetVersion.Parse("1.0.0"));
                var project2Lib = lockFile.GetLibrary("project2", NuGetVersion.Parse("1.0.0"));
                var project3Lib = lockFile.GetLibrary("project3", NuGetVersion.Parse("1.0.0"));

                var project2Target = lockFile.GetTarget(FrameworkConstants.CommonFrameworks.Net45, runtimeIdentifier: null)
                    .Libraries
                    .Where(lib => lib.Name == "project2")
                    .Single();

                var project3Target = lockFile.GetTarget(FrameworkConstants.CommonFrameworks.Net45, runtimeIdentifier: null)
                    .Libraries
                    .Where(lib => lib.Name == "project3")
                    .Single();

                // Assert
                Assert.True(result.Success);
                Assert.Equal(2, lockFile.Libraries.Count);

                Assert.Equal("project", project2Lib.Type);
                Assert.Equal("project", project3Lib.Type);

                Assert.Equal("../project2/project.json", project2Lib.Path);
                Assert.Equal("../project3/project.json", project3Lib.Path);

                Assert.Equal("../project2/project2.xproj", project2Lib.MSBuildProject);
                Assert.Equal("../project3/project3.xproj", project3Lib.MSBuildProject);

                Assert.Equal(1, project2Target.CompileTimeAssemblies.Count);
                Assert.Equal("net45/project2.dll", project2Target.CompileTimeAssemblies.Single().Path);

                Assert.Equal(1, project3Target.CompileTimeAssemblies.Count);
                Assert.Equal("net45/project3.dll", project3Target.CompileTimeAssemblies.Single().Path);

                Assert.Equal(1, project2Target.Dependencies.Count);
                Assert.Equal("project3", project2Target.Dependencies.Single().Id);
                Assert.Equal("1.0.0", project2Target.Dependencies.Single().VersionRange.ToLegacyShortString());

                Assert.Equal(0, project3Target.Dependencies.Count);

                Assert.Equal(".NETFramework,Version=v4.5", project2Target.Framework);
                Assert.Equal(".NETFramework,Version=v4.5", project3Target.Framework);
            }
        }

        [Fact]
        public async Task Project2ProjectInLockFile_VerifyProjectsNoAddedForV1()
        {
            // Arrange
            var sources = new List<PackageSource>();

            var project1Json = @"
            {
                ""version"": ""1.0.0"",
                ""dependencies"": {
                    ""project2"": ""1.0.0-*"",
                },
                ""frameworks"": {
                    ""net45"": {}
                }
            }";

            var project2Json = @"
            {
                ""version"": ""1.0.0"",
                ""dependencies"": {
                    ""project3"": ""1.0.0-*"",
                },
                ""frameworks"": {
                    ""net45"": {}
                }
            }";

            var project3Json = @"
            {
                ""version"": ""1.0.0"",
                ""dependencies"": {
                },
                ""frameworks"": {
                    ""net45"": {}
                }
            }";

            var globalJson = @"
            {
                ""projects"": [
                    ""projects""
                ]
            }";

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var project1 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project1"));
                var project2 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project2"));
                var project3 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project3"));
                project1.Create();
                project2.Create();
                project3.Create();

                File.WriteAllText(Path.Combine(project1.FullName, "project.json"), project1Json);
                File.WriteAllText(Path.Combine(project2.FullName, "project.json"), project2Json);
                File.WriteAllText(Path.Combine(project3.FullName, "project.json"), project3Json);
                File.WriteAllText(Path.Combine(workingDir, "global.json"), globalJson);

                File.WriteAllText(Path.Combine(project1.FullName, "project1.xproj"), string.Empty);
                File.WriteAllText(Path.Combine(project2.FullName, "project2.xproj"), string.Empty);
                File.WriteAllText(Path.Combine(project3.FullName, "project3.xproj"), string.Empty);

                var specPath1 = Path.Combine(project1.FullName, "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(project1Json, "project1", specPath1);

                var specPath2 = Path.Combine(project2.FullName, "project.json");
                var specPath3 = Path.Combine(project3.FullName, "project.json");

                var logger = new TestLogger();
                var request = new RestoreRequest(spec, sources, packagesDir, logger);

                request.LockFilePath = Path.Combine(project1.FullName, "project.lock.json");
                request.LockFileVersion = 1;
                var format = new LockFileFormat();

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                result.Commit(logger);

                var lockFile = format.Read(request.LockFilePath, logger);

                // Assert
                Assert.True(result.Success);
                Assert.Equal(0, lockFile.Libraries.Count);
                Assert.Equal(0, lockFile.Targets[0].Libraries.Count);
            }
        }
    }
}
