﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Commands.Test
{
    public class DependencyTypeConstraintTests
    {
        // Root project is favored over package in global folder
        [Fact]
        public async Task DependencyTypeConstraint_RootProjectIsUsedOverPackage()
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
                ""net45"": {
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

                var project1PackagePath = SimpleTestPackageUtility.CreateFullPackage(
                    packageSource.FullName,
                    "project1",
                    "1.0.0");

                await GlobalFolderUtility.AddPackageToGlobalFolderAsync(project1PackagePath, packagesDir);

                var logger = new TestLogger();
                var request = new RestoreRequest(spec1, sources, packagesDir.FullName, logger);

                request.LockFilePath = Path.Combine(project1.FullName, "project.lock.json");

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                var lockFile = result.LockFile;
                await result.CommitAsync(logger, CancellationToken.None);

                // Assert
                Assert.True(result.Success);
                Assert.Equal(0, lockFile.Libraries.Count);
            }
        }

        [Fact]
        public async Task DependencyTypeConstraint_PackagesDependOnProject()
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
                ""packageA"": ""1.0.0""
              },
              ""frameworks"": {
                ""net45"": {
                }
              }
            }";

            var packageBProjectJson = @"
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
                var packageBProject = new DirectoryInfo(Path.Combine(workingDir, "projects", "packageB"));
                packagesDir.Create();
                packageSource.Create();
                project1.Create();
                packageBProject.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                File.WriteAllText(Path.Combine(project1.FullName, "project.json"), project1Json);
                File.WriteAllText(Path.Combine(packageBProject.FullName, "project.json"), packageBProjectJson);
                File.WriteAllText(Path.Combine(workingDir, "global.json"), globalJson);

                var specPath1 = Path.Combine(project1.FullName, "project.json");
                var specPath2 = Path.Combine(packageBProject.FullName, "project.json");
                var spec1 = JsonPackageSpecReader.GetPackageSpec(project1Json, "project1", specPath1);
                var spec2 = JsonPackageSpecReader.GetPackageSpec(packageBProjectJson, "packageB", specPath2);

                var packageAPath = SimpleTestPackageUtility.CreateFullPackage(
                    packageSource.FullName,
                    "packageA",
                    "1.0.0",
                    new Packaging.Core.PackageDependency[]
                    {
                        new Packaging.Core.PackageDependency("packageB", VersionRange.Parse("1.0.0"))
                    });

                await GlobalFolderUtility.AddPackageToGlobalFolderAsync(packageAPath, packagesDir);

                var logger = new TestLogger();
                var request = new RestoreRequest(spec1, sources, packagesDir.FullName, logger);

                request.LockFilePath = Path.Combine(project1.FullName, "project.lock.json");

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                var lockFile = result.LockFile;
                await result.CommitAsync(logger, CancellationToken.None);

                // Assert
                Assert.True(result.Success);
                var packageBLib = lockFile.GetLibrary("packageB", NuGetVersion.Parse("1.0.0"));
                Assert.NotNull(packageBLib);
                Assert.Equal(LibraryType.Project, packageBLib.Type);
            }
        }

        // Default behavior takes external project for csproj
        [Fact]
        public async Task DependencyTypeConstraint_DefaultBehaviorWithNoTarget()
        {
            // Arrange
            var sources = new List<PackageSource>();

            var project1Json = @"
            {
              ""dependencies"": {
                ""packageA"": ""1.0.0""
              },
              ""frameworks"": {
                ""net45"": {
                }
              }
            }";

            var packageAProjectJson = @"
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

            var packageAExternalProjectJson = @"
            {
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
                var packageAProject = new DirectoryInfo(Path.Combine(workingDir, "projects", "packageA"));
                var packageAExternalProject = new DirectoryInfo(Path.Combine(workingDir, "external", "packageA"));
                packagesDir.Create();
                packageSource.Create();
                project1.Create();
                packageAProject.Create();
                packageAExternalProject.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                File.WriteAllText(Path.Combine(project1.FullName, "project.json"), project1Json);
                File.WriteAllText(Path.Combine(packageAProject.FullName, "project.json"), packageAProjectJson);
                File.WriteAllText(Path.Combine(packageAExternalProject.FullName, "project.json"),
                    packageAExternalProjectJson);
                File.WriteAllText(Path.Combine(workingDir, "global.json"), globalJson);

                var project1CSProjPath = Path.Combine(project1.FullName, "project1.csproj");
                File.WriteAllText(project1CSProjPath, string.Empty);
                File.WriteAllText(Path.Combine(packageAProject.FullName, "packageA.xproj"), string.Empty);
                var packageACSProjPath = Path.Combine(packageAExternalProject.FullName, "packageA.csproj");
                File.WriteAllText(packageACSProjPath, string.Empty);

                var specPath1 = Path.Combine(project1.FullName, "project.json");
                var specPath2 = Path.Combine(packageAProject.FullName, "project.json");
                var specPath3 = Path.Combine(packageAExternalProject.FullName, "project.json");
                var spec1 = JsonPackageSpecReader.GetPackageSpec(project1Json, "project1", specPath1);
                var spec2 = JsonPackageSpecReader.GetPackageSpec(packageAProjectJson, "packageA", specPath2);
                var spec3 = JsonPackageSpecReader.GetPackageSpec(packageAExternalProjectJson, "packageA", specPath3);

                var packageAPath = SimpleTestPackageUtility.CreateFullPackage(
                    packageSource.FullName,
                    "packageA",
                    "1.0.0");

                await GlobalFolderUtility.AddPackageToGlobalFolderAsync(packageAPath, packagesDir);

                var logger = new TestLogger();
                var request = new RestoreRequest(spec1, sources, packagesDir.FullName, logger);

                request.ExternalProjects.Add(
                    new ExternalProjectReference("project1", spec1, project1CSProjPath, new string[] { "packageA" }));
                request.ExternalProjects.Add(
                    new ExternalProjectReference("packageA", spec3, packageACSProjPath, Enumerable.Empty<string>()));

                request.LockFilePath = Path.Combine(project1.FullName, "project.lock.json");

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                var lockFile = result.LockFile;
                await result.CommitAsync(logger, CancellationToken.None);

                var packageALib = lockFile.GetLibrary("packageA", NuGetVersion.Parse("1.0.0"));

                var packageATarget = lockFile.GetTarget(
                        FrameworkConstants.CommonFrameworks.Net45,
                        runtimeIdentifier: null)
                    .Libraries
                    .Single(lib => lib.Name == "packageA");

                // Assert
                Assert.True(result.Success);

                Assert.Equal(LibraryType.Project, packageALib.Type);
                Assert.Equal(LibraryType.Project, packageATarget.Type);
            }
        }

        // Target takes package over project
        [Fact]
        public async Task DependencyTypeConstraint_TargetPackage()
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
                ""packageA"": {
                    ""version"": ""1.0.0"",
                    ""target"": ""package""
                }
              },
              ""frameworks"": {
                ""net45"": {
                }
              }
            }";

            var packageAProjectJson = @"
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
                var packageAProject = new DirectoryInfo(Path.Combine(workingDir, "projects", "packageA"));
                var packageAExternalProject = new DirectoryInfo(Path.Combine(workingDir, "external", "packageA"));
                packagesDir.Create();
                packageSource.Create();
                project1.Create();
                packageAProject.Create();
                packageAExternalProject.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                File.WriteAllText(Path.Combine(project1.FullName, "project.json"), project1Json);
                File.WriteAllText(Path.Combine(packageAProject.FullName, "project.json"), packageAProjectJson);
                File.WriteAllText(Path.Combine(workingDir, "global.json"), globalJson);

                var project1ProjPath = Path.Combine(project1.FullName, "project1.xproj");
                File.WriteAllText(project1ProjPath, string.Empty);
                File.WriteAllText(Path.Combine(packageAProject.FullName, "packageA.xproj"), string.Empty);

                var specPath1 = Path.Combine(project1.FullName, "project.json");
                var specPath2 = Path.Combine(packageAProject.FullName, "project.json");
                var spec1 = JsonPackageSpecReader.GetPackageSpec(project1Json, "project1", specPath1);
                var spec2 = JsonPackageSpecReader.GetPackageSpec(packageAProjectJson, "packageA", specPath2);

                var packageAPath = SimpleTestPackageUtility.CreateFullPackage(
                    packageSource.FullName,
                    "packageA",
                    "1.0.0");

                await GlobalFolderUtility.AddPackageToGlobalFolderAsync(packageAPath, packagesDir);

                var logger = new TestLogger();
                var request = new RestoreRequest(spec1, sources, packagesDir.FullName, logger);

                request.LockFilePath = Path.Combine(project1.FullName, "project.lock.json");

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                var lockFile = result.LockFile;
                await result.CommitAsync(logger, CancellationToken.None);

                var packageALib = lockFile.GetLibrary("packageA", NuGetVersion.Parse("1.0.0"));

                var packageATarget = lockFile.GetTarget(
                        FrameworkConstants.CommonFrameworks.Net45,
                        runtimeIdentifier: null)
                    .Libraries
                    .Single(lib => lib.Name == "packageA");

                // Assert
                Assert.True(result.Success);

                Assert.Equal(LibraryType.Package, packageALib.Type);
                Assert.Equal(LibraryType.Package, packageATarget.Type);
            }
        }

        // Target takes project over package
        [Fact]
        public async Task DependencyTypeConstraint_TargetProject()
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
                ""packageA"": {
                    ""version"": ""1.0.0"",
                    ""target"": ""project""
                }
              },
              ""frameworks"": {
                ""net45"": {
                }
              }
            }";

            var packageAProjectJson = @"
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
                var packageAProject = new DirectoryInfo(Path.Combine(workingDir, "projects", "packageA"));
                packagesDir.Create();
                packageSource.Create();
                project1.Create();
                packageAProject.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                File.WriteAllText(Path.Combine(project1.FullName, "project.json"), project1Json);
                File.WriteAllText(Path.Combine(packageAProject.FullName, "project.json"), packageAProjectJson);
                File.WriteAllText(Path.Combine(workingDir, "global.json"), globalJson);

                var specPath1 = Path.Combine(project1.FullName, "project.json");
                var specPath2 = Path.Combine(packageAProject.FullName, "project.json");
                var spec1 = JsonPackageSpecReader.GetPackageSpec(project1Json, "project1", specPath1);
                var spec2 = JsonPackageSpecReader.GetPackageSpec(packageAProjectJson, "packageA", specPath2);

                var packageAPath = SimpleTestPackageUtility.CreateFullPackage(
                    packageSource.FullName,
                    "packageA",
                    "1.0.0");

                await GlobalFolderUtility.AddPackageToGlobalFolderAsync(packageAPath, packagesDir);

                var logger = new TestLogger();
                var request = new RestoreRequest(spec1, sources, packagesDir.FullName, logger);

                request.LockFilePath = Path.Combine(project1.FullName, "project.lock.json");

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                var lockFile = result.LockFile;
                await result.CommitAsync(logger, CancellationToken.None);

                var packageALib = lockFile.GetLibrary("packageA", NuGetVersion.Parse("1.0.0"));

                var packageATarget = lockFile.GetTarget(
                        FrameworkConstants.CommonFrameworks.Net45,
                        runtimeIdentifier: null)
                    .Libraries
                    .Single(lib => lib.Name == "packageA");

                // Assert
                Assert.True(result.Success);

                Assert.Equal(LibraryType.Project, packageALib.Type);
                Assert.Equal(LibraryType.Project, packageATarget.Type);
            }
        }
    }
}
