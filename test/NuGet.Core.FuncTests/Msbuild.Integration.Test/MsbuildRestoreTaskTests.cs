﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace Msbuild.Integration.Test
{
    [Collection("Msbuild Integration Tests")]
    public class MsbuildRestoreTaskTests
    {
        private MsbuildIntegrationTestFixture _msbuildFixture;

        public MsbuildRestoreTaskTests(MsbuildIntegrationTestFixture fixture)
        {
            _msbuildFixture = fixture;
        }

        [PlatformFact(Platform.Windows)]
        public async Task MsbuildRestore_PackagesConfigDependencyAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var net461 = NuGetFramework.Parse("net461");

                var projectA = new SimpleTestProjectContext(
                    "a",
                    ProjectStyle.PackagesConfig,
                    pathContext.SolutionRoot);
                projectA.Frameworks.Add(new SimpleTestProjectFrameworkContext(net461));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };
                packageX.Files.Clear();
                packageX.AddFile("lib/net461/a.dll");

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                using (var writer = new StreamWriter(Path.Combine(Path.GetDirectoryName(projectA.ProjectPath), "packages.config")))
                {
                    writer.Write(
@"<packages>
  <package id=""x"" version=""1.0.0"" targetFramework=""net461"" />
</packages>");
                }

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    packageX);

                // Act
                var result = _msbuildFixture.RunMsBuild(pathContext.WorkingDirectory, $"/t:restore {pathContext.SolutionRoot} /p:RestorePackagesConfig=true", ignoreExitCode: true);


                // Assert
                Assert.True(result.ExitCode == 0, result.AllOutput);
                Assert.Contains("Added package 'x.1.0.0' to folder", result.AllOutput);
            }
        }

        [PlatformFact(Platform.Windows)]
        public async Task MsbuildRestore_PackagesConfigIsOptInAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var net461 = NuGetFramework.Parse("net461");

                var projectA = new SimpleTestProjectContext(
                    "a",
                    ProjectStyle.PackagesConfig,
                    pathContext.SolutionRoot);
                projectA.Frameworks.Add(new SimpleTestProjectFrameworkContext(net461));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };
                packageX.Files.Clear();
                packageX.AddFile("lib/net461/a.dll");

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                using (var writer = new StreamWriter(Path.Combine(Path.GetDirectoryName(projectA.ProjectPath), "packages.config")))
                {
                    writer.Write(
@"<packages>
  <package id=""x"" version=""1.0.0"" targetFramework=""net461"" />
</packages>");
                }

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    packageX);

                // Act
                var result = _msbuildFixture.RunMsBuild(pathContext.WorkingDirectory, $"/t:restore {pathContext.SolutionRoot}", ignoreExitCode: true);


                // Assert
                Assert.True(result.ExitCode == 0, result.AllOutput);
                Assert.Contains("Nothing to do. None of the projects specified contain packages to restore.", result.AllOutput);
            }
        }

        [PlatformFact(Platform.Windows)]
        public async Task MsbuildRestore_InsecurePackagesConfigDependencyAsync_Throws()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                NuGetFramework net461 = NuGetFramework.Parse("net461");

                var projectA = new SimpleTestProjectContext(
                    "a",
                    ProjectStyle.PackagesConfig,
                    pathContext.SolutionRoot);
                projectA.Frameworks.Add(new SimpleTestProjectFrameworkContext(net461));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };
                packageX.Files.Clear();
                packageX.AddFile("lib/net461/a.dll");

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                using (var writer = new StreamWriter(Path.Combine(Path.GetDirectoryName(projectA.ProjectPath), "packages.config")))
                {
                    writer.Write(
@"<!DOCTYPE package [
   <!ENTITY greeting ""Hello"">
   <!ENTITY name ""NuGet Client "">
   <!ENTITY sayhello ""&greeting; &name;"">
]>
<packages>
    <package id=""&sayhello;"" version=""1.1.0"" targetFramework=""net45"" />
    <package id=""x"" version=""1.0.0"" targetFramework=""net45"" />
</packages>");
                }

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    packageX);

                // Act
                CommandRunnerResult result = _msbuildFixture.RunMsBuild(pathContext.WorkingDirectory, $"/t:restore {pathContext.SolutionRoot} /p:RestorePackagesConfig=true", ignoreExitCode: true);

                // Assert
                Assert.Equal(1, result.ExitCode);
                Assert.Contains("Error parsing packages.config file", result.AllOutput);
            }
        }

        [PlatformFact(Platform.Windows)]
        public async Task MsbuildRestore_PackageReferenceDependencyAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var net461 = NuGetFramework.Parse("net461");

                var projectA = new SimpleTestProjectContext(
                    "a",
                    ProjectStyle.PackageReference,
                    pathContext.SolutionRoot);
                projectA.Frameworks.Add(new SimpleTestProjectFrameworkContext(net461));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };
                packageX.Files.Clear();
                projectA.AddPackageToAllFrameworks(packageX);
                packageX.AddFile("lib/net461/a.dll");

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                var configAPath = Path.Combine(Path.GetDirectoryName(projectA.ProjectPath), "NuGet.Config");
                var configText =
$@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""LocalSource"" value=""{pathContext.PackageSource}"" />
    </packageSources>
</configuration>";
                using (var writer = new StreamWriter(configAPath))
                {
                    writer.Write(configText);
                }

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    packageX);

                // Act
                var result = _msbuildFixture.RunMsBuild(pathContext.WorkingDirectory, $"/t:restore {pathContext.SolutionRoot}", ignoreExitCode: true);


                // Assert
                Assert.True(result.ExitCode == 0, result.AllOutput);
                var resolver = new VersionFolderPathResolver(pathContext.UserPackagesFolder);
                var nupkg = NupkgMetadataFileFormat.Read(resolver.GetNupkgMetadataPath(packageX.Id, NuGetVersion.Parse(packageX.Version)), NullLogger.Instance);
                Assert.Contains($"Installed x 1.0.0 from {pathContext.PackageSource} with content hash {nupkg.ContentHash}.", result.AllOutput);
                Assert.Contains(configAPath, result.AllOutput);
            }
        }

        [PlatformFact(Platform.Windows)]
        public async Task MsbuildRestore_PackageReferenceDependencyCsProjAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var net461 = NuGetFramework.Parse("net461");

                var projectA = new SimpleTestProjectContext(
                    "a",
                    ProjectStyle.PackageReference,
                    pathContext.SolutionRoot);
                projectA.Frameworks.Add(new SimpleTestProjectFrameworkContext(net461));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };
                packageX.Files.Clear();
                projectA.AddPackageToAllFrameworks(packageX);
                packageX.AddFile("lib/net461/a.dll");

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                var configAPath = Path.Combine(Path.GetDirectoryName(projectA.ProjectPath), "NuGet.Config");
                var configText =
$@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""LocalSource"" value=""{pathContext.PackageSource}"" />
    </packageSources>
</configuration>";
                using (var writer = new StreamWriter(configAPath))
                {
                    writer.Write(configText);
                }

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    packageX);

                // Act
                var result = _msbuildFixture.RunMsBuild(pathContext.WorkingDirectory, $"/t:restore {projectA.ProjectPath}", ignoreExitCode: true);


                // Assert
                Assert.True(result.ExitCode == 0, result.AllOutput);
                var resolver = new VersionFolderPathResolver(pathContext.UserPackagesFolder);
                var nupkg = NupkgMetadataFileFormat.Read(resolver.GetNupkgMetadataPath(packageX.Id, NuGetVersion.Parse(packageX.Version)), NullLogger.Instance);
                Assert.Contains($"Installed x 1.0.0 from {pathContext.PackageSource} with content hash {nupkg.ContentHash}.", result.AllOutput);
                Assert.Contains(configAPath, result.AllOutput);
            }
        }

        [PlatformFact(Platform.Windows)]
        public async Task MsbuildRestore_RequiresSolutionDirAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var net461 = NuGetFramework.Parse("net461");

                var projectA = new SimpleTestProjectContext(
                    "a",
                    ProjectStyle.PackagesConfig,
                    pathContext.SolutionRoot);
                projectA.Frameworks.Add(new SimpleTestProjectFrameworkContext(net461));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };
                packageX.Files.Clear();
                packageX.AddFile("lib/net461/a.dll");

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                using (var writer = new StreamWriter(Path.Combine(Path.GetDirectoryName(projectA.ProjectPath), "packages.config")))
                {
                    writer.Write(
@"<packages>
  <package id=""x"" version=""1.0.0"" targetFramework=""net461"" />
</packages>");
                }

                // SimpleTestPathContext adds a NuGet.Config with a repositoryPath,
                // so we go ahead and remove that config before running MSBuild.
                var configPath = Path.Combine(Path.GetDirectoryName(pathContext.SolutionRoot), "NuGet.Config");
                var doc = XDocument.Parse(File.ReadAllText(configPath));
                var root = doc.Element(XName.Get("configuration"));
                var config = root.Element(XName.Get("config"));
                foreach (var item in config.Elements(XName.Get("add")).Where(e => ((string)e.Attribute("key")).Equals("repositoryPath", StringComparison.OrdinalIgnoreCase)).ToArray())
                {
                    item.Remove();
                }
                File.Delete(configPath);
                File.WriteAllText(configPath, doc.ToString());

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    packageX);

                // Act
                var result = _msbuildFixture.RunMsBuild(pathContext.WorkingDirectory, $"/t:restore {projectA.ProjectPath} /p:RestorePackagesConfig=true", ignoreExitCode: true);


                // Assert
                Assert.True(result.ExitCode == 1, result.AllOutput);
                Assert.Contains("No solution found", result.AllOutput);
            }
        }

        [PlatformFact(Platform.Windows)]
        public async Task MsbuildRestore_SupportsPackageSaveModeAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var net461 = NuGetFramework.Parse("net461");

                var projectA = new SimpleTestProjectContext(
                    "a",
                    ProjectStyle.PackagesConfig,
                    pathContext.SolutionRoot);
                projectA.Frameworks.Add(new SimpleTestProjectFrameworkContext(net461));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };
                packageX.Files.Clear();
                packageX.AddFile("lib/net461/a.dll");

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                using (var writer = new StreamWriter(Path.Combine(Path.GetDirectoryName(projectA.ProjectPath), "packages.config")))
                {
                    writer.Write(
@"<packages>
  <package id=""x"" version=""1.0.0"" targetFramework=""net461"" />
</packages>");
                }

                var configPath = Path.Combine(Path.GetDirectoryName(pathContext.SolutionRoot), "NuGet.Config");
                var doc = XDocument.Parse(File.ReadAllText(configPath));
                var root = doc.Element(XName.Get("configuration"));
                var config = root.Element(XName.Get("config"));
                var setting = new XElement(XName.Get("add"));
                setting.Add(new XAttribute(XName.Get("key"), "PackageSaveMode"));
                setting.Add(new XAttribute(XName.Get("value"), "nuspec;nupkg"));
                config.Add(setting);
                File.Delete(configPath);
                File.WriteAllText(configPath, doc.ToString());

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    packageX);

                var pkgPath = Path.Combine(pathContext.SolutionRoot, "packages", "x.1.0.0");
                // Act
                var result = _msbuildFixture.RunMsBuild(pathContext.WorkingDirectory, $"/t:restore {pathContext.SolutionRoot} /p:RestorePackagesConfig=true", ignoreExitCode: true);


                // Assert
                Assert.True(result.ExitCode == 0, result.AllOutput);
                Assert.True(File.Exists(Path.Combine(pkgPath, "x.1.0.0.nupkg")));
                Assert.True(File.Exists(Path.Combine(pkgPath, "x.nuspec")));
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task MsbuildRestore_StaticGraphEvaluation_CleanupAssetsForUnsupportedProjectsAsync(bool cleanupAssetsForUnsupportedProjects)
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var net461 = NuGetFramework.Parse("net461");

                var projectA = new SimpleTestProjectContext(
                    "a",
                    ProjectStyle.PackageReference,
                    pathContext.SolutionRoot);
                projectA.Frameworks.Add(new SimpleTestProjectFrameworkContext(net461));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                packageX.Files.Clear();
                projectA.AddPackageToAllFrameworks(packageX);
                packageX.AddFile("lib/net461/a.dll");

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                var configAPath = Path.Combine(Path.GetDirectoryName(projectA.ProjectPath), "NuGet.Config");
                var configText =
$@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""LocalSource"" value=""{pathContext.PackageSource}"" />
    </packageSources>
</configuration>";
                using (var writer = new StreamWriter(configAPath))
                {
                    writer.Write(configText);
                }

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    packageX);

                // Restore the project with a PackageReference which generates assets
                var result = _msbuildFixture.RunMsBuild(pathContext.WorkingDirectory, $"/t:restore /p:RestoreUseStaticGraphEvaluation=true /p:RestoreCleanupAssetsForUnsupportedProjects={cleanupAssetsForUnsupportedProjects} {projectA.ProjectPath}", ignoreExitCode: true);

                Assert.True(result.ExitCode == 0, result.AllOutput);

                var assets = new[]
                {
                    projectA.AssetsFileOutputPath,
                    projectA.PropsOutput,
                    projectA.TargetsOutput,
                    projectA.CacheFileOutputPath,
                };

                foreach (var asset in assets)
                {
                    Assert.True(File.Exists(asset), result.AllOutput);
                }

                // Recreate the project with Unknown project style and no PackageReferences
                projectA = new SimpleTestProjectContext(
                    "a",
                    ProjectStyle.Unknown,
                    pathContext.SolutionRoot);
                projectA.Frameworks.Add(new SimpleTestProjectFrameworkContext(net461));

                projectA.Save();

                // Restore the project with a PackageReference which generates assets
                result = _msbuildFixture.RunMsBuild(pathContext.WorkingDirectory, $"/t:restore /p:RestoreUseStaticGraphEvaluation=true /p:RestoreCleanupAssetsForUnsupportedProjects={cleanupAssetsForUnsupportedProjects} {projectA.ProjectPath}", ignoreExitCode: true);

                // Assert
                Assert.True(result.ExitCode == 0, result.AllOutput);

                foreach (var asset in assets)
                {
                    if (cleanupAssetsForUnsupportedProjects)
                    {
                        Assert.False(File.Exists(asset), result.AllOutput);
                    }
                    else
                    {
                        Assert.True(File.Exists(asset), result.AllOutput);
                    }
                }
            }
        }

        [PlatformFact(Platform.Windows)]
        public async Task MsbuildRestore_WithLegacyPackageReferenceProject_BothStaticGraphAndRegularRestoreNoOp()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var net461 = NuGetFramework.Parse("net461");

                var project = SimpleTestProjectContext.CreateLegacyPackageReference(
                    "a",
                    pathContext.SolutionRoot,
                    net461);

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                packageX.Files.Clear();
                project.AddPackageToAllFrameworks(packageX);
                packageX.AddFile("lib/net461/a.dll");

                solution.Projects.Add(project);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    packageX);

                var projectOutputPaths = new[]
                {
                    project.AssetsFileOutputPath,
                    project.PropsOutput,
                    project.TargetsOutput,
                    project.CacheFileOutputPath,
                };

                var projectOutputTimestamps = new Dictionary<string, DateTime>();

                // Restore the project with a PackageReference which generates assets
                var result = _msbuildFixture.RunMsBuild(pathContext.WorkingDirectory, $"/t:restore {project.ProjectPath}", ignoreExitCode: true);
                result.Success.Should().BeTrue(because: result.AllOutput);

                foreach (var asset in projectOutputPaths)
                {
                    var fileInfo = new FileInfo(asset);
                    fileInfo.Exists.Should().BeTrue(because: result.AllOutput);
                    projectOutputTimestamps.Add(asset, fileInfo.LastWriteTimeUtc);
                }

                // Restore the project with a PackageReference which generates assets
                result = _msbuildFixture.RunMsBuild(pathContext.WorkingDirectory, $"/t:restore /p:RestoreUseStaticGraphEvaluation=true {project.ProjectPath}", ignoreExitCode: true);

                result.Success.Should().BeTrue(because: result.AllOutput);

                foreach (var asset in projectOutputPaths)
                {
                    var fileInfo = new FileInfo(asset);
                    fileInfo.Exists.Should().BeTrue(because: result.AllOutput);
                    fileInfo.LastWriteTimeUtc.Should().Be(projectOutputTimestamps[asset]);
                }

                result = _msbuildFixture.RunMsBuild(pathContext.WorkingDirectory, $"/t:restore {project.ProjectPath}", ignoreExitCode: true);
                result.Success.Should().BeTrue(result.AllOutput);
            }
        }

        [PlatformFact(Platform.Windows)]
        public async Task MsbuildRestore_WithStaticGraphAndRegularRestore_ErrorLoggedWhenOutputPathNotSpecified()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var net461 = NuGetFramework.Parse("net461");

                var project = new SimpleTestProjectContext("a", ProjectStyle.PackageReference, pathContext.SolutionRoot)
                {
                    ProjectExtensionsPath = string.Empty,
                    Properties =
                    {
                        // When these two properties are not defined, restore should fail with a clear error and not crash
                        ["MSBuildProjectExtensionsPath"] = string.Empty,
                        ["RestoreOutputPath"] = string.Empty
                    },
                    SetMSBuildProjectExtensionsPath = false,
                    SingleTargetFramework = true
                };

                project.Frameworks.Add(new SimpleTestProjectFrameworkContext(net461));

                var packageX = new SimpleTestPackageContext
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                packageX.Files.Clear();
                project.AddPackageToAllFrameworks(packageX);
                packageX.AddFile("lib/net461/a.dll");

                solution.Projects.Add(project);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    packageX);

                // Restore the project with a PackageReference which generates assets
                var result = _msbuildFixture.RunMsBuild(pathContext.WorkingDirectory, $"/t:restore /p:RestoreUseStaticGraphEvaluation=true {project.ProjectPath}", ignoreExitCode: true);

                result.Success.Should().BeFalse(because: result.AllOutput);

                result.AllOutput.Should().Contain($"error : Invalid restore input. Missing required property 'OutputPath' for project type 'PackageReference'. Input files: {project.ProjectPath}.");
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task MsbuildRestore_WithRelativeSource_ResolvesAgainstCurrentWorkingDirectory(bool isStaticGraphRestore)
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var net461 = NuGetFramework.Parse("net461");

                var project = SimpleTestProjectContext.CreateLegacyPackageReference(
                    "a",
                    pathContext.SolutionRoot,
                    net461);

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                packageX.Files.Clear();
                project.AddPackageToAllFrameworks(packageX);
                packageX.AddFile("lib/net461/a.dll");

                solution.Projects.Add(project);
                solution.Create(pathContext.SolutionRoot);
                var relativePath = "relativeSource";
                var relativeSource = Path.Combine(pathContext.WorkingDirectory, relativePath);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    relativeSource,
                    packageX);

                var projectOutputPaths = new[]
                {
                    project.AssetsFileOutputPath,
                    project.PropsOutput,
                    project.TargetsOutput,
                    project.CacheFileOutputPath,
                };

                // Restore the project with a PackageReference which generates assets
                var result = _msbuildFixture.RunMsBuild(pathContext.WorkingDirectory, $"/t:restore {project.ProjectPath} /p:RestoreSources=\"{relativePath}\"" +
                    (isStaticGraphRestore ? " /p:RestoreUseStaticGraphEvaluation=true" : string.Empty),
                    ignoreExitCode: true);
                result.Success.Should().BeTrue(because: result.AllOutput);

                foreach (var asset in projectOutputPaths)
                {
                    new FileInfo(asset).Exists.Should().BeTrue(because: result.AllOutput);
                }
            }
        }

        [PlatformTheory(Platform.Windows)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task MsbuildRestore_WithCPPCliVcxproj_RestoresSuccessfullyWithPackageReference(bool isStaticGraphRestore)
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set-up packages
                var packageX = new SimpleTestPackageContext("x", "1.0.0");
                packageX.AddFile("lib/net5.0/a.dll");
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    packageX);
                // Set up project
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
                var framework = NuGetFramework.Parse("net5.0-windows7.0");
                var projectA = SimpleTestProjectContext.CreateNETCore("projectName", pathContext.SolutionRoot, framework);
                projectA.Properties.Add("CLRSupport", "NetCore");
                //update path to vcxproj
                projectA.ProjectPath = Path.Combine(Path.GetDirectoryName(projectA.ProjectPath), projectA.ProjectName + ".vcxproj");
                projectA.AddPackageToAllFrameworks(packageX);
                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);
                // Act
                var result = _msbuildFixture.RunMsBuild(pathContext.WorkingDirectory,
                    $"/t:restore {pathContext.SolutionRoot}" + (isStaticGraphRestore ? " /p:RestoreUseStaticGraphEvaluation=true" : string.Empty));

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                File.Exists(projectA.AssetsFileOutputPath).Should().BeTrue(because: result.AllOutput);
                File.Exists(projectA.TargetsOutput).Should().BeTrue(because: result.AllOutput);
                File.Exists(projectA.PropsOutput).Should().BeTrue(because: result.AllOutput);

                var targetsSection = projectA.AssetsFile.Targets.First(e => string.IsNullOrEmpty(e.RuntimeIdentifier));
                targetsSection.Libraries.Should().Contain(e => e.Name.Equals("x"), because: string.Join(",", targetsSection.Libraries));
                var lockFileTargetLibrary = targetsSection.Libraries.First(e => e.Name.Equals("x"));
                lockFileTargetLibrary.CompileTimeAssemblies.Should().Contain("lib/net5.0/a.dll");
            }
        }

        [PlatformFact(Platform.Windows)]
        public async Task MsbuildRestore_WithCPPCliVcxproj_WithNativeDependency_Succeeds()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set-up packages
                var packageX = new SimpleTestPackageContext("x", "1.0.0");
                packageX.AddFile("build/native/x.targets");
                packageX.AddFile("lib/native/x.dll");
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    packageX);
                // Set up project
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
                var framework = NuGetFramework.Parse("net5.0-windows7.0");
                var projectA = SimpleTestProjectContext.CreateNETCore("projectName", pathContext.SolutionRoot, framework);
                projectA.Properties.Add("CLRSupport", "NetCore");
                //update path to vcxproj
                projectA.ProjectPath = Path.Combine(Path.GetDirectoryName(projectA.ProjectPath), projectA.ProjectName + ".vcxproj");
                projectA.AddPackageToAllFrameworks(packageX);
                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);
                // Act
                var result = _msbuildFixture.RunMsBuild(pathContext.WorkingDirectory, $"/t:restore {pathContext.SolutionRoot}");

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                File.Exists(projectA.AssetsFileOutputPath).Should().BeTrue(because: result.AllOutput);
                File.Exists(projectA.TargetsOutput).Should().BeTrue(because: result.AllOutput);
                File.Exists(projectA.PropsOutput).Should().BeTrue(because: result.AllOutput);

                var targetsSection = projectA.AssetsFile.Targets.First(e => string.IsNullOrEmpty(e.RuntimeIdentifier));
                targetsSection.Libraries.Should().Contain(e => e.Name.Equals("x"), because: string.Join(",", targetsSection.Libraries));
                var lockFileTargetLibrary = targetsSection.Libraries.First(e => e.Name.Equals("x"));
                lockFileTargetLibrary.CompileTimeAssemblies.Should().Contain("lib/native/x.dll");
                lockFileTargetLibrary.Build.Should().Contain("build/native/x.targets");
            }
        }

        [PlatformFact(Platform.Windows)]
        public async Task MsbuildRestore_WithCPPCliVcxproj_WithNativeAndManagedTransitiveDependency_Succeeds()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set-up packages
                // Managed 1.0.0 -> Managed.Child 1.0.0
                // Native 1.0.0 -> Native.Child 1.0.
                var packageNativeChild = new SimpleTestPackageContext("native.child", "1.0.0");
                packageNativeChild.AddFile("build/native/native.child.targets");
                packageNativeChild.AddFile("lib/native/native.child.dll");

                var packageNative = new SimpleTestPackageContext("native", "1.0.0");
                packageNative.AddFile("build/native/native.targets");
                packageNative.AddFile("lib/native/native.dll");


                packageNative.PerFrameworkDependencies.Add(FrameworkConstants.CommonFrameworks.Native, new List<SimpleTestPackageContext> { packageNativeChild });

                var packageManagedChild = new SimpleTestPackageContext("managed.child", "1.0.0");
                packageManagedChild.AddFile("build/net5.0/managed.child.targets");
                packageManagedChild.AddFile("lib/net5.0/managed.child.dll");

                var packageManaged = new SimpleTestPackageContext("managed", "1.0.0");
                packageManaged.AddFile("build/net5.0/managed.targets");
                packageManaged.AddFile("lib/net5.0/managed.dll");

                packageManaged.PerFrameworkDependencies.Add(FrameworkConstants.CommonFrameworks.Net50, new List<SimpleTestPackageContext> { packageManagedChild });

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    packageNative,
                    packageNativeChild,
                    packageManaged,
                    packageManagedChild);

                // Set up project
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
                var framework = NuGetFramework.Parse("net5.0-windows7.0");
                var projectA = SimpleTestProjectContext.CreateNETCore("projectName", pathContext.SolutionRoot, framework);
                projectA.Properties.Add("CLRSupport", "NetCore");
                //update path to vcxproj
                projectA.ProjectPath = Path.Combine(Path.GetDirectoryName(projectA.ProjectPath), projectA.ProjectName + ".vcxproj");
                projectA.AddPackageToAllFrameworks(packageNative);
                projectA.AddPackageToAllFrameworks(packageManaged);
                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);
                // Act
                var result = _msbuildFixture.RunMsBuild(pathContext.WorkingDirectory, $"/t:restore {pathContext.SolutionRoot}");

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                File.Exists(projectA.AssetsFileOutputPath).Should().BeTrue(because: result.AllOutput);
                File.Exists(projectA.TargetsOutput).Should().BeTrue(because: result.AllOutput);
                File.Exists(projectA.PropsOutput).Should().BeTrue(because: result.AllOutput);

                var targetsSection = projectA.AssetsFile.Targets.First(e => string.IsNullOrEmpty(e.RuntimeIdentifier));
                targetsSection.Libraries.Should().Contain(e => e.Name.Equals("native"), because: string.Join(",", targetsSection.Libraries));
                targetsSection.Libraries.Should().Contain(e => e.Name.Equals("native.child"), because: string.Join(",", targetsSection.Libraries));
                targetsSection.Libraries.Should().Contain(e => e.Name.Equals("managed"), because: string.Join(",", targetsSection.Libraries));
                targetsSection.Libraries.Should().Contain(e => e.Name.Equals("managed.child"), because: string.Join(",", targetsSection.Libraries));

                var nativeChild = targetsSection.Libraries.First(e => e.Name.Equals("native.child"));
                nativeChild.CompileTimeAssemblies.Should().Contain("lib/native/native.child.dll");
                nativeChild.Build.Should().Contain("build/native/native.child.targets");
            }
        }

        [PlatformFact(Platform.Windows)]
        public async Task MsbuildRestore_WithCPPCliVcxproj_WithAssetTargetFallback_Succeeds()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set-up packages
                var packageNative = new SimpleTestPackageContext("native", "1.0.0");
                packageNative.AddFile("build/native/native.targets");
                packageNative.AddFile("lib/native/native.dll");

                var packageManaged = new SimpleTestPackageContext("managed", "1.0.0");
                packageManaged.AddFile("build/net472/managed.targets");
                packageManaged.AddFile("lib/net472/managed.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    packageNative,
                    packageManaged);

                // Set up project
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
                var framework = NuGetFramework.Parse("net5.0-windows7.0");
                var projectA = SimpleTestProjectContext.CreateNETCore("projectName", pathContext.SolutionRoot, framework);
                projectA.Properties.Add("CLRSupport", "NetCore");
                projectA.Properties.Add("AssetTargetFallback", "net472");
                //update path to vcxproj
                projectA.ProjectPath = Path.Combine(Path.GetDirectoryName(projectA.ProjectPath), projectA.ProjectName + ".vcxproj");
                projectA.AddPackageToAllFrameworks(packageNative);
                projectA.AddPackageToAllFrameworks(packageManaged);
                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);
                // Act
                var result = _msbuildFixture.RunMsBuild(pathContext.WorkingDirectory, $"/t:restore {pathContext.SolutionRoot}");

                // Assert
                result.Success.Should().BeTrue(because: result.AllOutput);
                File.Exists(projectA.AssetsFileOutputPath).Should().BeTrue(because: result.AllOutput);
                File.Exists(projectA.TargetsOutput).Should().BeTrue(because: result.AllOutput);
                File.Exists(projectA.PropsOutput).Should().BeTrue(because: result.AllOutput);

                var targetsSection = projectA.AssetsFile.Targets.First(e => string.IsNullOrEmpty(e.RuntimeIdentifier));
                targetsSection.Libraries.Should().Contain(e => e.Name.Equals("native"), because: string.Join(",", targetsSection.Libraries));
                targetsSection.Libraries.Should().Contain(e => e.Name.Equals("managed"), because: string.Join(",", targetsSection.Libraries));

                var native = targetsSection.Libraries.First(e => e.Name.Equals("native"));
                native.CompileTimeAssemblies.Should().Contain("lib/native/native.dll");
                native.Build.Should().Contain("build/native/native.targets");

                var managed = targetsSection.Libraries.First(e => e.Name.Equals("managed"));
                managed.CompileTimeAssemblies.Should().Contain("lib/net472/managed.dll");
                managed.Build.Should().Contain("build/net472/managed.targets");
            }
        }

        [PlatformFact(Platform.Windows)]
        public async Task MsbuildRestore_PackageNamespaceFullPrefix_Succeed()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {

                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var net461 = NuGetFramework.Parse("net461");

                var projectA = new SimpleTestProjectContext(
                    "a",
                    ProjectStyle.PackagesConfig,
                    pathContext.SolutionRoot);
                projectA.Frameworks.Add(new SimpleTestProjectFrameworkContext(net461));
                var projectAPackages = Path.Combine(pathContext.SolutionRoot, "packages");

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                using (var writer = new StreamWriter(Path.Combine(Path.GetDirectoryName(projectA.ProjectPath), "packages.config")))
                {
                    writer.Write(
@"<packages>
  <package id=""测试更新包"" version=""1.0.0"" targetFramework=""net461"" />
  <package id=""Contoso.MVC.ASP"" version=""1.0.0"" targetFramework=""net461"" />
  <package id=""Contoso.Opensource.Buffers"" version=""1.0.0"" targetFramework=""net461"" />
</packages>");
                }

                var opensourceRepositoryPath = pathContext.PackageSource;
                Directory.CreateDirectory(opensourceRepositoryPath);

                var packageOpenSourceInternational = new SimpleTestPackageContext()
                {
                    Id = "测试更新包",
                    Version = "1.0.0"
                };
                packageOpenSourceInternational.Files.Clear();
                packageOpenSourceInternational.AddFile("lib/net461/a.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    opensourceRepositoryPath,
                    packageOpenSourceInternational);

                var packageOpenSourceContosoMvc = new SimpleTestPackageContext()
                {
                    Id = "Contoso.MVC.ASP",  // Package Id conflict with internally created package
                    Version = "1.0.0"
                };
                packageOpenSourceContosoMvc.Files.Clear();
                packageOpenSourceContosoMvc.AddFile("lib/net461/openA.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    opensourceRepositoryPath,
                    packageOpenSourceContosoMvc);

                var packageContosoBuffersOpenSource = new SimpleTestPackageContext()
                {
                    Id = "Contoso.Opensource.Buffers",
                    Version = "1.0.0"
                };
                packageContosoBuffersOpenSource.Files.Clear();
                packageContosoBuffersOpenSource.AddFile("lib/net461/openA.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    opensourceRepositoryPath,
                    packageContosoBuffersOpenSource);

                var sharedRepositoryPath = pathContext.UserPackagesFolder;
                Directory.CreateDirectory(sharedRepositoryPath);

                var packageContosoMvcReal = new SimpleTestPackageContext()
                {
                    Id = "Contoso.MVC.ASP",
                    Version = "1.0.0"
                };
                packageContosoMvcReal.Files.Clear();
                packageContosoMvcReal.AddFile("lib/net461/realA.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    sharedRepositoryPath,
                    packageContosoMvcReal);

                // SimpleTestPathContext adds a NuGet.Config with a repositoryPath,
                // so we go ahead and remove that config before running MSBuild.
                var configPath = Path.Combine(Path.GetDirectoryName(pathContext.SolutionRoot), "NuGet.Config");
                var configText =
$@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
    <!--To inherit the global NuGet package sources remove the <clear/> line below -->
    <clear />
    <add key=""PublicRepository"" value=""{opensourceRepositoryPath}"" />
    <add key=""SharedRepository"" value=""{sharedRepositoryPath}"" />
    </packageSources>
    <packageNamespaces>
        <packageSource key=""PublicRepository""> 
            <namespace id=""Contoso.Opensource.*"" />
        </packageSource>
        <packageSource key=""SharedRepository"">
            <namespace id=""Contoso.MVC.*"" /> 
        </packageSource>
    </packageNamespaces>
</configuration>";
                using (var writer = new StreamWriter(configPath))
                {
                    writer.Write(configText);
                }

                // Act
                var result = _msbuildFixture.RunMsBuild(pathContext.WorkingDirectory, $"/t:restore {pathContext.SolutionRoot} /p:RestorePackagesConfig=true", ignoreExitCode: true);

                // Assert
                Assert.True(result.ExitCode == 0);
                var contosoRestorePath = Path.Combine(projectAPackages, packageOpenSourceContosoMvc.ToString(), packageOpenSourceContosoMvc.ToString() + ".nupkg");
                using (var nupkgReader = new PackageArchiveReader(contosoRestorePath))
                {
                    var allFiles = nupkgReader.GetFiles().ToList();
                    // Assert correct Contoso package was restored.
                    Assert.Contains("lib/net461/realA.dll", allFiles);
                }
            }
        }

        [PlatformFact(Platform.Windows)]
        public async Task MsbuildRestore_PackageNamespaceFullPrefix_Fails()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {

                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var net461 = NuGetFramework.Parse("net461");

                var projectA = new SimpleTestProjectContext(
                    "a",
                    ProjectStyle.PackagesConfig,
                    pathContext.SolutionRoot);
                projectA.Frameworks.Add(new SimpleTestProjectFrameworkContext(net461));
                var projectAPackages = Path.Combine(pathContext.SolutionRoot, "packages");

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                using (var writer = new StreamWriter(Path.Combine(Path.GetDirectoryName(projectA.ProjectPath), "packages.config")))
                {
                    writer.Write(
@"<packages>
    <package id=""测试更新包"" version=""1.0.0"" targetFramework=""net461"" />
    <package id=""Contoso.MVC.ASP"" version=""1.0.0"" targetFramework=""net461"" />
    <package id=""Contoso.Opensource.Buffers"" version=""1.0.0"" targetFramework=""net461"" />
</packages>");
                }

                var opensourceRepositoryPath = pathContext.PackageSource;
                Directory.CreateDirectory(opensourceRepositoryPath);

                var packageOpenSourceInternational = new SimpleTestPackageContext()
                {
                    Id = "测试更新包",
                    Version = "1.0.0"
                };
                packageOpenSourceInternational.Files.Clear();
                packageOpenSourceInternational.AddFile("lib/net461/a.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    opensourceRepositoryPath,
                    packageOpenSourceInternational);

                var packageOpenSourceContosoMvc = new SimpleTestPackageContext()
                {
                    Id = "Contoso.MVC.ASP",  // Package Id conflict with internally created package
                    Version = "1.0.0"
                };
                packageOpenSourceContosoMvc.Files.Clear();
                packageOpenSourceContosoMvc.AddFile("lib/net461/openA.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    opensourceRepositoryPath,
                    packageOpenSourceContosoMvc);

                var packageContosoBuffersOpenSource = new SimpleTestPackageContext()
                {
                    Id = "Contoso.Opensource.Buffers",
                    Version = "1.0.0"
                };
                packageContosoBuffersOpenSource.Files.Clear();
                packageContosoBuffersOpenSource.AddFile("lib/net461/openA.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    opensourceRepositoryPath,
                    packageContosoBuffersOpenSource);

                var sharedRepositoryPath = pathContext.UserPackagesFolder;
                Directory.CreateDirectory(sharedRepositoryPath);

                // SimpleTestPathContext adds a NuGet.Config with a repositoryPath,
                // so we go ahead and remove that config before running MSBuild.
                var configPath = Path.Combine(Path.GetDirectoryName(pathContext.SolutionRoot), "NuGet.Config");
                var configText =
$@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
<packageSources>
<!--To inherit the global NuGet package sources remove the <clear/> line below -->
<clear />
<add key=""PublicRepository"" value=""{opensourceRepositoryPath}"" />
<add key=""SharedRepository"" value=""{sharedRepositoryPath}"" />
</packageSources>
<packageNamespaces>
    <packageSource key=""PublicRepository""> 
        <namespace id=""Contoso.Opensource.*"" />
    </packageSource>
    <packageSource key=""SharedRepository"">
        <namespace id=""Contoso.MVC.*"" /> 
    </packageSource>
</packageNamespaces>
</configuration>";
                using (var writer = new StreamWriter(configPath))
                {
                    writer.Write(configText);
                }

                // Act
                var result = _msbuildFixture.RunMsBuild(pathContext.WorkingDirectory, $"/t:restore {pathContext.SolutionRoot} /p:RestorePackagesConfig=true", ignoreExitCode: true);

                // Assert
                Assert.True(result.ExitCode == 1);
                var packageInternationalPath = Path.Combine(projectAPackages, packageOpenSourceInternational.ToString(), packageOpenSourceInternational.ToString() + ".nupkg");
                Assert.True(File.Exists(packageInternationalPath));
                var packageContosoBuffersPath = Path.Combine(projectAPackages, packageContosoBuffersOpenSource.ToString(), packageContosoBuffersOpenSource.ToString() + ".nupkg");
                Assert.True(File.Exists(packageContosoBuffersPath));
                // Assert Contoso.MVC.ASP is not restored.
                Assert.True(result.Output.Contains("Unable to find version '1.0.0' of package 'Contoso.MVC.ASP'."));
                var packageContosoMvcPath = Path.Combine(projectAPackages, packageOpenSourceContosoMvc.ToString(), packageOpenSourceContosoMvc.ToString() + ".nupkg");
                Assert.False(File.Exists(packageContosoMvcPath));
            }
        }

        [PlatformFact(Platform.Windows)]
        public async Task MsbuildRestore_PackageNamespacePartialPrefix_Succeed()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {

                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var net461 = NuGetFramework.Parse("net461");

                var projectA = new SimpleTestProjectContext(
                    "a",
                    ProjectStyle.PackagesConfig,
                    pathContext.SolutionRoot);
                projectA.Frameworks.Add(new SimpleTestProjectFrameworkContext(net461));
                var projectAPackages = Path.Combine(pathContext.SolutionRoot, "packages");

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                using (var writer = new StreamWriter(Path.Combine(Path.GetDirectoryName(projectA.ProjectPath), "packages.config")))
                {
                    writer.Write(
@"<packages>
  <package id=""测试更新包"" version=""1.0.0"" targetFramework=""net461"" />
  <package id=""Contoso.MVC.ASP"" version=""1.0.0"" targetFramework=""net461"" />
  <package id=""Contoso.Opensource.Buffers"" version=""1.0.0"" targetFramework=""net461"" />
</packages>");
                }

                var opensourceRepositoryPath = pathContext.PackageSource;
                Directory.CreateDirectory(opensourceRepositoryPath);

                var packageOpenSourceInternational = new SimpleTestPackageContext()
                {
                    Id = "测试更新包",
                    Version = "1.0.0"
                };
                packageOpenSourceInternational.Files.Clear();
                packageOpenSourceInternational.AddFile("lib/net461/a.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    opensourceRepositoryPath,
                    packageOpenSourceInternational);

                var packageOpenSourceContosoMvc = new SimpleTestPackageContext()
                {
                    Id = "Contoso.MVC.ASP",  // Package Id conflict with internally created package
                    Version = "1.0.0"
                };
                packageOpenSourceContosoMvc.Files.Clear();
                packageOpenSourceContosoMvc.AddFile("lib/net461/openA.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    opensourceRepositoryPath,
                    packageOpenSourceContosoMvc);

                var packageContosoBuffersOpenSource = new SimpleTestPackageContext()
                {
                    Id = "Contoso.Opensource.Buffers",
                    Version = "1.0.0"
                };
                packageContosoBuffersOpenSource.Files.Clear();
                packageContosoBuffersOpenSource.AddFile("lib/net461/openA.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    opensourceRepositoryPath,
                    packageContosoBuffersOpenSource);

                var sharedRepositoryPath = pathContext.UserPackagesFolder;
                Directory.CreateDirectory(sharedRepositoryPath);

                var packageContosoMvcReal = new SimpleTestPackageContext()
                {
                    Id = "Contoso.MVC.ASP",
                    Version = "1.0.0"
                };
                packageContosoMvcReal.Files.Clear();
                packageContosoMvcReal.AddFile("lib/net461/realA.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    sharedRepositoryPath,
                    packageContosoMvcReal);

                // SimpleTestPathContext adds a NuGet.Config with a repositoryPath,
                // so we go ahead and remove that config before running MSBuild.
                var configPath = Path.Combine(Path.GetDirectoryName(pathContext.SolutionRoot), "NuGet.Config");
                var configText =
$@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
    <!--To inherit the global NuGet package sources remove the <clear/> line below -->
    <clear />
    <add key=""PublicRepository"" value=""{opensourceRepositoryPath}"" />
    <add key=""SharedRepository"" value=""{sharedRepositoryPath}"" />
    </packageSources>
    <packageNamespaces>
        <packageSource key=""PublicRepository""> 
            <namespace id=""Contoso.O*"" />
        </packageSource>
        <packageSource key=""SharedRepository"">
            <namespace id=""Contoso.M*"" /> 
        </packageSource>
    </packageNamespaces>
</configuration>";
                using (var writer = new StreamWriter(configPath))
                {
                    writer.Write(configText);
                }

                // Act
                var result = _msbuildFixture.RunMsBuild(pathContext.WorkingDirectory, $"/t:restore {pathContext.SolutionRoot} /p:RestorePackagesConfig=true", ignoreExitCode: true);

                // Assert
                Assert.True(result.ExitCode == 0);
                var contosoRestorePath = Path.Combine(projectAPackages, packageOpenSourceContosoMvc.ToString(), packageOpenSourceContosoMvc.ToString() + ".nupkg");
                using (var nupkgReader = new PackageArchiveReader(contosoRestorePath))
                {
                    var allFiles = nupkgReader.GetFiles().ToList();
                    // Assert correct Contoso package was restored.
                    Assert.Contains("lib/net461/realA.dll", allFiles);
                }
            }
        }

        [PlatformFact(Platform.Windows)]
        public async Task MsbuildRestore_PackageNamespacePartialPrefix_Fails()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {

                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var net461 = NuGetFramework.Parse("net461");

                var projectA = new SimpleTestProjectContext(
                    "a",
                    ProjectStyle.PackagesConfig,
                    pathContext.SolutionRoot);
                projectA.Frameworks.Add(new SimpleTestProjectFrameworkContext(net461));
                var projectAPackages = Path.Combine(pathContext.SolutionRoot, "packages");

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                using (var writer = new StreamWriter(Path.Combine(Path.GetDirectoryName(projectA.ProjectPath), "packages.config")))
                {
                    writer.Write(
@"<packages>
    <package id=""测试更新包"" version=""1.0.0"" targetFramework=""net461"" />
    <package id=""Contoso.MVC.ASP"" version=""1.0.0"" targetFramework=""net461"" />
    <package id=""Contoso.Opensource.Buffers"" version=""1.0.0"" targetFramework=""net461"" />
</packages>");
                }

                var opensourceRepositoryPath = pathContext.PackageSource;
                Directory.CreateDirectory(opensourceRepositoryPath);

                var packageOpenSourceInternational = new SimpleTestPackageContext()
                {
                    Id = "测试更新包",
                    Version = "1.0.0"
                };
                packageOpenSourceInternational.Files.Clear();
                packageOpenSourceInternational.AddFile("lib/net461/a.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    opensourceRepositoryPath,
                    packageOpenSourceInternational);

                var packageOpenSourceContosoMvc = new SimpleTestPackageContext()
                {
                    Id = "Contoso.MVC.ASP",  // Package Id conflict with internally created package
                    Version = "1.0.0"
                };
                packageOpenSourceContosoMvc.Files.Clear();
                packageOpenSourceContosoMvc.AddFile("lib/net461/openA.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    opensourceRepositoryPath,
                    packageOpenSourceContosoMvc);

                var packageContosoBuffersOpenSource = new SimpleTestPackageContext()
                {
                    Id = "Contoso.Opensource.Buffers",
                    Version = "1.0.0"
                };
                packageContosoBuffersOpenSource.Files.Clear();
                packageContosoBuffersOpenSource.AddFile("lib/net461/openA.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    opensourceRepositoryPath,
                    packageContosoBuffersOpenSource);

                var sharedRepositoryPath = pathContext.UserPackagesFolder;
                Directory.CreateDirectory(sharedRepositoryPath);

                // SimpleTestPathContext adds a NuGet.Config with a repositoryPath,
                // so we go ahead and remove that config before running MSBuild.
                var configPath = Path.Combine(Path.GetDirectoryName(pathContext.SolutionRoot), "NuGet.Config");
                var configText =
$@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
<packageSources>
<!--To inherit the global NuGet package sources remove the <clear/> line below -->
<clear />
<add key=""PublicRepository"" value=""{opensourceRepositoryPath}"" />
<add key=""SharedRepository"" value=""{sharedRepositoryPath}"" />
</packageSources>
<packageNamespaces>
    <packageSource key=""PublicRepository""> 
        <namespace id=""Contoso.O*"" />
    </packageSource>
    <packageSource key=""SharedRepository"">
        <namespace id=""Contoso.M*"" /> 
    </packageSource>
</packageNamespaces>
</configuration>";
                using (var writer = new StreamWriter(configPath))
                {
                    writer.Write(configText);
                }

                // Act
                var result = _msbuildFixture.RunMsBuild(pathContext.WorkingDirectory, $"/t:restore {pathContext.SolutionRoot} /p:RestorePackagesConfig=true", ignoreExitCode: true);

                // Assert
                Assert.True(result.ExitCode == 1);
                var packageInternationalPath = Path.Combine(projectAPackages, packageOpenSourceInternational.ToString(), packageOpenSourceInternational.ToString() + ".nupkg");
                Assert.True(File.Exists(packageInternationalPath));
                var packageContosoBuffersPath = Path.Combine(projectAPackages, packageContosoBuffersOpenSource.ToString(), packageContosoBuffersOpenSource.ToString() + ".nupkg");
                Assert.True(File.Exists(packageContosoBuffersPath));
                // Assert Contoso.MVC.ASP is not restored.
                Assert.True(result.Output.Contains("Unable to find version '1.0.0' of package 'Contoso.MVC.ASP'."));
                var packageContosoMvcPath = Path.Combine(projectAPackages, packageOpenSourceContosoMvc.ToString(), packageOpenSourceContosoMvc.ToString() + ".nupkg");
                Assert.False(File.Exists(packageContosoMvcPath));
            }
        }

        [PlatformFact(Platform.Windows)]
        public async Task MsbuildRestore_PackageNamespaceLongerPrefixMatches_Succeed()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {

                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var net461 = NuGetFramework.Parse("net461");

                var projectA = new SimpleTestProjectContext(
                    "a",
                    ProjectStyle.PackagesConfig,
                    pathContext.SolutionRoot);
                projectA.Frameworks.Add(new SimpleTestProjectFrameworkContext(net461));
                var projectAPackages = Path.Combine(pathContext.SolutionRoot, "packages");

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                using (var writer = new StreamWriter(Path.Combine(Path.GetDirectoryName(projectA.ProjectPath), "packages.config")))
                {
                    writer.Write(
@"<packages>
  <package id=""测试更新包"" version=""1.0.0"" targetFramework=""net461"" />
  <package id=""Contoso.MVC.ASP"" version=""1.0.0"" targetFramework=""net461"" />
  <package id=""Contoso.Opensource.Buffers"" version=""1.0.0"" targetFramework=""net461"" />
</packages>");
                }

                var opensourceRepositoryPath = pathContext.PackageSource;
                Directory.CreateDirectory(opensourceRepositoryPath);

                var packageOpenSourceInternational = new SimpleTestPackageContext()
                {
                    Id = "测试更新包",
                    Version = "1.0.0"
                };
                packageOpenSourceInternational.Files.Clear();
                packageOpenSourceInternational.AddFile("lib/net461/a.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    opensourceRepositoryPath,
                    packageOpenSourceInternational);

                var packageOpenSourceContosoMvc = new SimpleTestPackageContext()
                {
                    Id = "Contoso.MVC.ASP",  // Package Id conflict with internally created package
                    Version = "1.0.0"
                };
                packageOpenSourceContosoMvc.Files.Clear();
                packageOpenSourceContosoMvc.AddFile("lib/net461/openA.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    opensourceRepositoryPath,
                    packageOpenSourceContosoMvc);

                var packageContosoBuffersOpenSource = new SimpleTestPackageContext()
                {
                    Id = "Contoso.Opensource.Buffers",
                    Version = "1.0.0"
                };
                packageContosoBuffersOpenSource.Files.Clear();
                packageContosoBuffersOpenSource.AddFile("lib/net461/openA.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    opensourceRepositoryPath,
                    packageContosoBuffersOpenSource);

                var sharedRepositoryPath = pathContext.UserPackagesFolder;
                Directory.CreateDirectory(sharedRepositoryPath);

                var packageContosoMvcReal = new SimpleTestPackageContext()
                {
                    Id = "Contoso.MVC.ASP",
                    Version = "1.0.0"
                };
                packageContosoMvcReal.Files.Clear();
                packageContosoMvcReal.AddFile("lib/net461/realA.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    sharedRepositoryPath,
                    packageContosoMvcReal);

                // SimpleTestPathContext adds a NuGet.Config with a repositoryPath,
                // so we go ahead and remove that config before running MSBuild.
                var configPath = Path.Combine(Path.GetDirectoryName(pathContext.SolutionRoot), "NuGet.Config");
                var configText =
$@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
    <!--To inherit the global NuGet package sources remove the <clear/> line below -->
    <clear />
    <add key=""PublicRepository"" value=""{opensourceRepositoryPath}"" />
    <add key=""SharedRepository"" value=""{sharedRepositoryPath}"" />
    </packageSources>
    <packageNamespaces>
        <packageSource key=""PublicRepository""> 
            <namespace id=""Contoso.Opensource.*"" />
            <namespace id=""Contoso.MVC.*"" /> 
        </packageSource>
        <packageSource key=""SharedRepository"">
            <namespace id=""Contoso.MVC.ASP"" />
        </packageSource>
    </packageNamespaces>
</configuration>";
                using (var writer = new StreamWriter(configPath))
                {
                    writer.Write(configText);
                }

                // Act
                var result = _msbuildFixture.RunMsBuild(pathContext.WorkingDirectory, $"/t:restore {pathContext.SolutionRoot} /p:RestorePackagesConfig=true", ignoreExitCode: true);

                // Assert
                Assert.True(result.ExitCode == 0);
                var contosoRestorePath = Path.Combine(projectAPackages, packageOpenSourceContosoMvc.ToString(), packageOpenSourceContosoMvc.ToString() + ".nupkg");
                using (var nupkgReader = new PackageArchiveReader(contosoRestorePath))
                {
                    var allFiles = nupkgReader.GetFiles().ToList();
                    // Assert correct Contoso package was restored.
                    Assert.Contains("lib/net461/realA.dll", allFiles);
                }
            }
        }
    }
}
