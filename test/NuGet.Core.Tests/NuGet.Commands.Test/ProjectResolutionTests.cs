using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.LibraryModel;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Commands.Test
{
    public class ProjectResolutionTests
    {
        // Project -> Project -> Project with multiple global.json files
        [Fact]
        public async Task ProjectResolution_MultipleGlobalJsonFiles()
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
              ""dependencies"": {
                ""project2"": ""1.0.0""
              },
              ""frameworks"": {
                ""net45"": {
                }
              }
            }";

            var project2Json = @"
            {
              ""version"": ""1.0.0"",
              ""description"": """",
              ""authors"": [ ""author"" ],
              ""tags"": [ """" ],
              ""projectUrl"": """",
              ""licenseUrl"": """",
              ""dependencies"": {
                ""project3"": ""1.0.0""
              },
              ""frameworks"": {
                ""net45"": {
                }
              }
            }";

            var project3Json = @"
            {
              ""version"": ""1.0.0"",
              ""description"": """",
              ""authors"": [ ""author"" ],
              ""tags"": [ """" ],
              ""projectUrl"": """",
              ""licenseUrl"": """",
              ""dependencies"": {
              },
              ""frameworks"": {
                ""net45"": {
                }
              }
            }";

            // section1 can resolve section2
            var global1Json = @"
            {
                ""projects"": [
                    ""../section2/projects""
                ]
            }";

            // section2 can resolve section3
            var global2Json = @"
            {
                ""projects"": [
                    ""../section3/projects""
                ]
            }";

            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var packagesDir = new DirectoryInfo(Path.Combine(workingDir, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(workingDir, "packageSource"));
                var project1 = new DirectoryInfo(Path.Combine(workingDir, "section1", "projects", "project1"));
                var project2 = new DirectoryInfo(Path.Combine(workingDir, "section2", "projects", "project2"));
                var project3 = new DirectoryInfo(Path.Combine(workingDir, "section3", "projects", "project3"));
                packagesDir.Create();
                packageSource.Create();
                project1.Create();
                project2.Create();
                project3.Create();

                File.WriteAllText(Path.Combine(project1.FullName, "project.json"), project1Json);
                File.WriteAllText(Path.Combine(project2.FullName, "project.json"), project2Json);
                File.WriteAllText(Path.Combine(project3.FullName, "project.json"), project3Json);
                File.WriteAllText(Path.Combine(workingDir, "section1", "global.json"), global1Json);
                File.WriteAllText(Path.Combine(workingDir, "section2", "global.json"), global2Json);

                var specPath1 = Path.Combine(project1.FullName, "project.json");
                var specPath2 = Path.Combine(project2.FullName, "project.json");
                var specPath3 = Path.Combine(project3.FullName, "project.json");
                var spec1 = JsonPackageSpecReader.GetPackageSpec(project1Json, "project1", specPath1);
                var spec2 = JsonPackageSpecReader.GetPackageSpec(project2Json, "project2", specPath2);
                var spec3 = JsonPackageSpecReader.GetPackageSpec(project3Json, "project3", specPath3);

                var request = new RestoreRequest(spec1, sources, packagesDir.FullName);

                request.LockFilePath = Path.Combine(project1.FullName, "project.lock.json");

                // Act
                var logger = new TestLogger();
                var command = new RestoreCommand(logger, request);
                var result = await command.ExecuteAsync();
                var lockFile = result.LockFile;
                result.Commit(logger);

                var project3Lib = lockFile.GetLibrary("project3", NuGetVersion.Parse("1.0.0"));

                // Assert
                Assert.True(result.Success);
                Assert.Equal(LibraryTypes.Project, project3Lib.Type);
            }
        }

        // Verify target files are not written out for non-msbuild/xproj
        [Fact]
        public async Task ProjectResolution_TargetFilesSkippedForProjectJsonOnlyProject()
        {
            // Arrange
            var sources = new List<PackageSource>();

            var project1Json = @"
            {
              ""version"": ""1.0.0-*"",
              ""description"": """",
              ""authors"": [ ""author"" ],
              ""tags"": [ """" ],
              ""projectUrl"": """",
              ""licenseUrl"": """",
              ""dependencies"": {
                ""packageA"": ""1.0.0""
              },
              ""frameworks"": {
                ""net45"": {
                }
              }
            }";

            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var packagesDir = new DirectoryInfo(Path.Combine(workingDir, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(workingDir, "packageSource"));
                var project1 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project1"));
                var project2 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project2"));
                packagesDir.Create();
                packageSource.Create();
                project1.Create();
                project2.Create();

                File.WriteAllText(Path.Combine(project1.FullName, "project.json"), project1Json);

                var project1ProjPath = Path.Combine(project1.FullName, "project1.xproj");
                File.WriteAllText(project1ProjPath, string.Empty);

                var specPath1 = Path.Combine(project1.FullName, "project.json");
                var spec1 = JsonPackageSpecReader.GetPackageSpec(project1Json, "project1", specPath1);

                sources.Add(new PackageSource(packageSource.FullName));

                // PackageA -> PackageB
                var packageAPath = SimpleTestPackageUtility.CreateFullPackage(
                    packageSource.FullName,
                    "packageA",
                    "1.0.0");

                var request = new RestoreRequest(spec1, sources, packagesDir.FullName);

                request.LockFilePath = Path.Combine(project1.FullName, "project.lock.json");

                // Act
                var logger = new TestLogger();
                var command = new RestoreCommand(logger, request);
                var result = await command.ExecuteAsync();
                var lockFile = result.LockFile;
                result.Commit(logger);

                // Assert
                Assert.True(result.Success);
                Assert.Equal(0, result.MSBuild.Targets.Count());
                Assert.Equal(0, result.MSBuild.Props.Count());
                Assert.True(result.MSBuild.Success);
            }
        }

        // Verify target files are not written out for non-msbuild/xproj
        [Fact]
        public async Task ProjectResolution_TargetFilesSkippedForXProjProjects()
        {
            // Arrange
            var sources = new List<PackageSource>();

            var project1Json = @"
            {
              ""version"": ""1.0.0-*"",
              ""description"": """",
              ""authors"": [ ""author"" ],
              ""tags"": [ """" ],
              ""projectUrl"": """",
              ""licenseUrl"": """",
              ""dependencies"": {
                ""packageA"": ""1.0.0""
              },
              ""frameworks"": {
                ""net45"": {
                }
              }
            }";

            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var packagesDir = new DirectoryInfo(Path.Combine(workingDir, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(workingDir, "packageSource"));
                var project1 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project1"));
                var project2 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project2"));
                packagesDir.Create();
                packageSource.Create();
                project1.Create();
                project2.Create();

                File.WriteAllText(Path.Combine(project1.FullName, "project.json"), project1Json);

                var project1ProjPath = Path.Combine(project1.FullName, "project1.xproj");
                File.WriteAllText(project1ProjPath, string.Empty);

                var specPath1 = Path.Combine(project1.FullName, "project.json");
                var spec1 = JsonPackageSpecReader.GetPackageSpec(project1Json, "project1", specPath1);

                sources.Add(new PackageSource(packageSource.FullName));

                await TestPackages.GeneratePackageAsync(
                    packageSource.FullName,
                    "packageA",
                    "1.0.0",
                    DateTime.UtcNow,
                    "build/net45/packageA.targets",
                    "build/net45/packageA.props");

                var request = new RestoreRequest(spec1, sources, packagesDir.FullName);

                request.LockFilePath = Path.Combine(project1.FullName, "project.lock.json");

                request.ExternalProjects.Add(
                    new ExternalProjectReference("project1", spec1, project1ProjPath, new string[] { }));

                // Act
                var logger = new TestLogger();
                var command = new RestoreCommand(logger, request);
                var result = await command.ExecuteAsync();
                var lockFile = result.LockFile;
                result.Commit(logger);

                // Assert
                Assert.True(result.Success);
                Assert.Equal(0, result.MSBuild.Targets.Count());
                Assert.Equal(0, result.MSBuild.Props.Count());
                Assert.True(result.MSBuild.Success);
            }
        }

        // CSProj does not resolve sibling folder
        [Fact]
        public async Task ProjectResolution_MSBuildProjectDoesNotResolveByDirectory()
        {
            // Arrange
            var sources = new List<PackageSource>();

            var project1Json = @"
            {
              ""version"": ""1.0.0-*"",
              ""description"": """",
              ""authors"": [ ""author"" ],
              ""tags"": [ """" ],
              ""projectUrl"": """",
              ""licenseUrl"": """",
              ""dependencies"": {
                ""project2"": ""1.0.0-*""
              },
              ""frameworks"": {
                ""net45"": {
                }
              }
            }";

            var project2Json = @"
            {
              ""version"": ""1.0.0-*"",
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

            var globalJson = @"
            {
                ""projects"": [
                    ""projects""
                ]
            }";

            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var packagesDir = new DirectoryInfo(Path.Combine(workingDir, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(workingDir, "packageSource"));
                var project1 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project1"));
                var project2 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project2"));
                packagesDir.Create();
                packageSource.Create();
                project1.Create();
                project2.Create();

                File.WriteAllText(Path.Combine(project1.FullName, "project.json"), project1Json);
                File.WriteAllText(Path.Combine(project2.FullName, "project.json"), project2Json);
                File.WriteAllText(Path.Combine(workingDir, "global.json"), globalJson);

                var project1CSProjPath = Path.Combine(project1.FullName, "project1.csproj");
                File.WriteAllText(project1CSProjPath, string.Empty);

                var specPath1 = Path.Combine(project1.FullName, "project.json");
                var specPath2 = Path.Combine(project2.FullName, "project.json");
                var spec1 = JsonPackageSpecReader.GetPackageSpec(project1Json, "project1", specPath1);
                var spec2 = JsonPackageSpecReader.GetPackageSpec(project2Json, "project2", specPath2);

                var request = new RestoreRequest(spec1, sources, packagesDir.FullName);

                request.LockFilePath = Path.Combine(project1.FullName, "project.lock.json");

                request.ExternalProjects.Add(
                    new ExternalProjectReference("project1", spec1, project1CSProjPath, new string[] { }));

                // Act
                var logger = new TestLogger();
                var command = new RestoreCommand(logger, request);
                var result = await command.ExecuteAsync();
                var lockFile = result.LockFile;
                result.Commit(logger);

                // Assert
                Assert.False(result.Success);
                Assert.Equal(1, result.GetAllUnresolved().Count);
                Assert.Equal("project2", result.GetAllUnresolved().Single().Name);
            }
        }

        // Project -> Project resolved without global.json
        [Fact]
        public async Task ProjectResolution_ResolveProjectSiblingDir()
        {
            // Arrange
            var sources = new List<PackageSource>();

            var project1Json = @"
            {
              ""version"": ""1.0.0-*"",
              ""description"": """",
              ""authors"": [ ""author"" ],
              ""tags"": [ """" ],
              ""projectUrl"": """",
              ""licenseUrl"": """",
              ""dependencies"": {
                ""project2"": ""1.0.0-*""
              },
              ""frameworks"": {
                ""net45"": {
                }
              }
            }";

            var project2Json = @"
            {
              ""version"": ""1.0.0-*"",
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

            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var packagesDir = new DirectoryInfo(Path.Combine(workingDir, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(workingDir, "packageSource"));
                var project1 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project1"));
                var project2 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project2"));
                packagesDir.Create();
                packageSource.Create();
                project1.Create();
                project2.Create();

                File.WriteAllText(Path.Combine(project1.FullName, "project.json"), project1Json);
                File.WriteAllText(Path.Combine(project2.FullName, "project.json"), project2Json);

                var specPath1 = Path.Combine(project1.FullName, "project.json");
                var specPath2 = Path.Combine(project2.FullName, "project.json");
                var spec1 = JsonPackageSpecReader.GetPackageSpec(project1Json, "project1", specPath1);
                var spec2 = JsonPackageSpecReader.GetPackageSpec(project2Json, "project2", specPath2);

                var request = new RestoreRequest(spec1, sources, packagesDir.FullName);

                request.LockFilePath = Path.Combine(project1.FullName, "project.lock.json");

                // Act
                var logger = new TestLogger();
                var command = new RestoreCommand(logger, request);
                var result = await command.ExecuteAsync();
                var lockFile = result.LockFile;
                result.Commit(logger);

                var project2Lib = lockFile.GetLibrary("project2", NuGetVersion.Parse("1.0.0"));

                // Assert
                Assert.True(result.Success);
                Assert.Equal(0, result.GetAllUnresolved().Count);
                Assert.Equal(LibraryTypes.Project, project2Lib.Type);
            }
        }

        // Project -> Project, folder exists but no project.json
        [Fact]
        public async Task ProjectResolution_ResolveProjectSiblingDirNoProjectJson()
        {
            // Arrange
            var sources = new List<PackageSource>();

            var project1Json = @"
            {
              ""version"": ""1.0.0-*"",
              ""description"": """",
              ""authors"": [ ""author"" ],
              ""tags"": [ """" ],
              ""projectUrl"": """",
              ""licenseUrl"": """",
              ""dependencies"": {
                ""project2"": ""1.0.0-*""
              },
              ""frameworks"": {
                ""net45"": {
                }
              }
            }";

            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var packagesDir = new DirectoryInfo(Path.Combine(workingDir, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(workingDir, "packageSource"));
                var project1 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project1"));
                var project2 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project2"));
                packagesDir.Create();
                packageSource.Create();
                project1.Create();
                project2.Create();

                File.WriteAllText(Path.Combine(project1.FullName, "project.json"), project1Json);
                File.WriteAllText(Path.Combine(project2.FullName, "packages.config"), string.Empty);

                var specPath1 = Path.Combine(project1.FullName, "project.json");
                var spec1 = JsonPackageSpecReader.GetPackageSpec(project1Json, "project1", specPath1);

                var request = new RestoreRequest(spec1, sources, packagesDir.FullName);

                request.LockFilePath = Path.Combine(project1.FullName, "project.lock.json");

                // Act
                var logger = new TestLogger();
                var command = new RestoreCommand(logger, request);
                var result = await command.ExecuteAsync();
                var lockFile = result.LockFile;
                result.Commit(logger);

                var project2Lib = lockFile.GetLibrary("project2", NuGetVersion.Parse("1.0.0"));

                // Assert
                Assert.False(result.Success);
                Assert.Equal(1, result.GetAllUnresolved().Count);
                Assert.Equal("project2", result.GetAllUnresolved().Single().Name);
            }
        }

        // Verify that if multiple project2 folders exist, the project2/project.json folder is used.
        [Fact]
        public async Task ProjectResolution_ResolveProjectWithGlobalJsonIgnoreNonProject()
        {
            // Arrange
            var sources = new List<PackageSource>();

            var project1Json = @"
            {
              ""version"": ""1.0.0-*"",
              ""description"": """",
              ""authors"": [ ""author"" ],
              ""tags"": [ """" ],
              ""projectUrl"": """",
              ""licenseUrl"": """",
              ""dependencies"": {
                ""project2"": ""1.0.0-*""
              },
              ""frameworks"": {
                ""net45"": {
                }
              }
            }";

            var project2Json = @"
            {
              ""version"": ""1.0.0-*"",
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

            var globalJson = @"
            {
                ""projects"": [
                    ""otherProjects""
                ]
            }";

            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var packagesDir = new DirectoryInfo(Path.Combine(workingDir, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(workingDir, "packageSource"));
                var project1 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project1"));
                var project2Empty = new DirectoryInfo(Path.Combine(workingDir, "projects", "project2"));
                var project2 = new DirectoryInfo(Path.Combine(workingDir, "otherProjects", "project2"));
                packagesDir.Create();
                packageSource.Create();
                project1.Create();
                project2.Create();
                project2Empty.Create();

                File.WriteAllText(Path.Combine(project1.FullName, "project.json"), project1Json);
                File.WriteAllText(Path.Combine(project2.FullName, "project.json"), project2Json);
                File.WriteAllText(Path.Combine(project2Empty.FullName, "packages.config"), string.Empty);
                File.WriteAllText(Path.Combine(workingDir, "global.json"), globalJson);

                var specPath1 = Path.Combine(project1.FullName, "project.json");
                var specPath2 = Path.Combine(project2.FullName, "project.json");
                var spec1 = JsonPackageSpecReader.GetPackageSpec(project1Json, "project1", specPath1);
                var spec2 = JsonPackageSpecReader.GetPackageSpec(project2Json, "project2", specPath2);

                var request = new RestoreRequest(spec1, sources, packagesDir.FullName);

                request.LockFilePath = Path.Combine(project1.FullName, "project.lock.json");

                // Act
                var logger = new TestLogger();
                var command = new RestoreCommand(logger, request);
                var result = await command.ExecuteAsync();
                var lockFile = result.LockFile;
                result.Commit(logger);

                var project2Lib = lockFile.GetLibrary("project2", NuGetVersion.Parse("1.0.0"));

                // Assert
                Assert.True(result.Success);
                Assert.Equal(0, result.GetAllUnresolved().Count);
                Assert.Equal(LibraryTypes.Project, project2Lib.Type);
            }
        }

        // Project -> Project resolves with global.json
        [Fact]
        public async Task ProjectResolution_ResolveProjectWithGlobalJson()
        {
            // Arrange
            var sources = new List<PackageSource>();

            var project1Json = @"
            {
              ""version"": ""1.0.0-*"",
              ""description"": """",
              ""authors"": [ ""author"" ],
              ""tags"": [ """" ],
              ""projectUrl"": """",
              ""licenseUrl"": """",
              ""dependencies"": {
                ""project2"": ""1.0.0-*""
              },
              ""frameworks"": {
                ""net45"": {
                }
              }
            }";

            var project2Json = @"
            {
              ""version"": ""1.0.0-*"",
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

            var globalJson = @"
            {
                ""projects"": [
                    ""otherProjects""
                ]
            }";

            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var packagesDir = new DirectoryInfo(Path.Combine(workingDir, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(workingDir, "packageSource"));
                var project1 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project1"));
                var project2 = new DirectoryInfo(Path.Combine(workingDir, "otherProjects", "project2"));
                packagesDir.Create();
                packageSource.Create();
                project1.Create();
                project2.Create();

                File.WriteAllText(Path.Combine(project1.FullName, "project.json"), project1Json);
                File.WriteAllText(Path.Combine(project2.FullName, "project.json"), project2Json);
                File.WriteAllText(Path.Combine(workingDir, "global.json"), globalJson);

                var specPath1 = Path.Combine(project1.FullName, "project.json");
                var specPath2 = Path.Combine(project2.FullName, "project.json");
                var spec1 = JsonPackageSpecReader.GetPackageSpec(project1Json, "project1", specPath1);
                var spec2 = JsonPackageSpecReader.GetPackageSpec(project2Json, "project2", specPath2);

                var request = new RestoreRequest(spec1, sources, packagesDir.FullName);

                request.LockFilePath = Path.Combine(project1.FullName, "project.lock.json");

                // Act
                var logger = new TestLogger();
                var command = new RestoreCommand(logger, request);
                var result = await command.ExecuteAsync();
                var lockFile = result.LockFile;
                result.Commit(logger);

                var project2Lib = lockFile.GetLibrary("project2", NuGetVersion.Parse("1.0.0"));

                // Assert
                Assert.True(result.Success);
                Assert.Equal(0, result.GetAllUnresolved().Count);
                Assert.Equal(LibraryTypes.Project, project2Lib.Type);
            }
        }
    }
}
