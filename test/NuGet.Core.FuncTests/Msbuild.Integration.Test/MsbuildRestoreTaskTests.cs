// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Test.Utility;
using NuGet.ProjectModel;
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
                CommandRunnerResult result = _msbuildFixture.RunMsBuild(pathContext.WorkingDirectory, $"/t:restore {pathContext.SolutionRoot} /p:RestorePackagesConfig=true", ignoreExitCode:true);

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
                Assert.Contains("Installing x 1.0.0", result.AllOutput);
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
                Assert.Contains("Installing x 1.0.0", result.AllOutput);
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
    }
}
