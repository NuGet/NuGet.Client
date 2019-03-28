// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class RestoreNetCoreTest
    {
        [Fact]
        public async Task RestoreNetCore_AddExternalTargetVerifyTargetUsedAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var pkgX = new SimpleTestPackageContext("x", "1.0.0");
                var pkgY = new SimpleTestPackageContext("y", "1.0.0");

                // Add y to the project
                projectA.AddPackageToAllFrameworks(pkgY);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreatePackagesAsync(pathContext.PackageSource, pkgX, pkgY);

                // Inject dependency x
                var doc = XDocument.Load(projectA.ProjectPath);
                var ns = doc.Root.GetDefaultNamespace().NamespaceName;
                doc.Root.AddFirst(
                    new XElement(XName.Get("Target", ns),
                    new XAttribute(XName.Get("Name"), "RunMe"),
                    new XAttribute(XName.Get("BeforeTargets"), "CollectPackageReferences"),
                        new XElement(XName.Get("ItemGroup", ns),
                            new XElement(XName.Get("PackageReference", ns),
                                new XAttribute(XName.Get("Include"), "x"),
                                new XAttribute(XName.Get("Version"), "1.0.0")))));

                doc.Save(projectA.ProjectPath);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                r.Success.Should().BeTrue();
                projectA.AssetsFile.GetLibrary("x", NuGetVersion.Parse("1.0.0")).Should().NotBeNull();
                projectA.AssetsFile.GetLibrary("y", NuGetVersion.Parse("1.0.0")).Should().NotBeNull();
            }
        }

        [PlatformFact(Platform.Windows)]
        public void RestoreNetCore_IfProjectsWitAndWithoutRestoreTargetsExistVerifyValidProjectsRestore()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var projectB = SimpleTestProjectContext.CreateNETCore(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);
                solution.Create(pathContext.SolutionRoot);

                // Remove all contents from B to make it invalid for restore.
                File.Delete(projectB.ProjectPath);
                File.WriteAllText(projectB.ProjectPath, "<Project ToolsVersion=\"15.0\"></Project>");

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                r.Success.Should().BeTrue();
                File.Exists(projectA.AssetsFileOutputPath).Should().BeTrue();
                File.Exists(projectB.AssetsFileOutputPath).Should().BeFalse();
                r.AllOutput.Should().Contain("NU1503");
                r.AllOutput.Should().Contain("The project file may be invalid or missing targets required for restore.");
            }
        }

        [PlatformFact(Platform.Windows)]
        public void RestoreNetCore_IfAllProjectsAreWithoutRestoreTargetsVerifySuccess()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                // Remove all contents from A to make it invalid for restore.
                File.Delete(projectA.ProjectPath);
                File.WriteAllText(projectA.ProjectPath, "<Project ToolsVersion=\"15.0\"></Project>");

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                r.Success.Should().BeTrue();
                File.Exists(projectA.AssetsFileOutputPath).Should().BeFalse();
                r.AllOutput.Should().Contain("NU1503");
                r.AllOutput.Should().Contain("The project file may be invalid or missing targets required for restore.");
            }
        }

        /// <summary>
        /// Create 3 projects, each with their own nuget.config file and source.
        /// When restoring without a solution settings should be found from the project folder.
        /// Solution settings are verified in RestoreProjectJson_RestoreFromSlnUsesNuGetFolderSettings
        /// </summary>
        [Fact]
        public async Task RestoreNetCore_VerifyPerProjectConfigSourcesAreUsedForChildProjectsWithoutSolutionAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
                var projects = new Dictionary<string, SimpleTestProjectContext>();
                var sources = new List<string>();

                foreach (var letter in new[] { "A", "B", "C", "D" })
                {
                    // Project
                    var project = SimpleTestProjectContext.CreateNETCore(
                        $"project{letter}",
                        pathContext.SolutionRoot,
                        NuGetFramework.Parse("net45"));

                    projects.Add(letter, project);
                    solution.Projects.Add(project);

                    // Package
                    var package = new SimpleTestPackageContext()
                    {
                        Id = $"package{letter}",
                        Version = "1.0.0"
                    };

                    // Do not flow the reference up
                    package.PrivateAssets = "all";

                    project.AddPackageToAllFrameworks(package);
                    project.Properties.Clear();

                    // Source
                    var source = Path.Combine(pathContext.WorkingDirectory, $"source{letter}");
                    await SimpleTestPackageUtility.CreatePackagesAsync(source, package);
                    sources.Add(source);

                    // Create a nuget.config for the project specific source.
                    var projectDir = Path.GetDirectoryName(project.ProjectPath);
                    Directory.CreateDirectory(projectDir);
                    var configPath = Path.Combine(projectDir, "NuGet.Config");

                    var doc = new XDocument();
                    var configuration = new XElement(XName.Get("configuration"));
                    doc.Add(configuration);

                    var config = new XElement(XName.Get("config"));
                    configuration.Add(config);

                    var packageSources = new XElement(XName.Get("packageSources"));
                    configuration.Add(packageSources);

                    var sourceEntry = new XElement(XName.Get("add"));
                    sourceEntry.Add(new XAttribute(XName.Get("key"), "projectSource"));
                    sourceEntry.Add(new XAttribute(XName.Get("value"), source));
                    packageSources.Add(sourceEntry);

                    File.WriteAllText(configPath, doc.ToString());
                }

                // Create root project
                var projectRoot = SimpleTestProjectContext.CreateNETCore(
                    "projectRoot",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                // Link the root project to all other projects
                foreach (var child in projects.Values)
                {
                    projectRoot.AddProjectToAllFrameworks(child);
                }

                projectRoot.Save();
                solution.Projects.Add(projectRoot);

                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.Restore(pathContext, projectRoot.ProjectPath, expectedExitCode: 0, additionalArgs: "-Recursive");

                // Assert
                Assert.True(projects.Count > 0);

                foreach (var letter in projects.Keys)
                {
                    Assert.True(projects[letter].AssetsFile.Libraries.Select(e => e.Name).Contains($"package{letter}"));
                }
            }
        }

        /// <summary>
        /// Verify the project level config can override a solution level config's sources.
        /// </summary>
        [Fact]
        public async Task RestoreNetCore_VerifyProjectConfigCanOverrideSolutionConfigAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                // Project
                var project = SimpleTestProjectContext.CreateNETCore(
                    $"projectA",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                solution.Projects.Add(project);

                // Package
                var packageGood = new SimpleTestPackageContext()
                {
                    Id = $"packageA",
                    Version = "1.0.0"
                };

                var packageGoodDep = new SimpleTestPackageContext()
                {
                    Id = $"packageB",
                    Version = "1.0.0"
                };

                packageGood.Dependencies.Add(packageGoodDep);

                var packageBad = new SimpleTestPackageContext()
                {
                    Id = $"packageA",
                    Version = "1.0.0"
                };

                project.AddPackageToAllFrameworks(packageBad);
                project.Properties.Clear();

                // Source
                var source = Path.Combine(pathContext.WorkingDirectory, "sourceA");

                // The override source contains an extra dependency
                await SimpleTestPackageUtility.CreatePackagesAsync(source, packageGood, packageGoodDep);

                // The solution level source does not contain B
                await SimpleTestPackageUtility.CreatePackagesAsync(pathContext.PackageSource, packageBad);

                // Create a nuget.config for the project specific source.
                var projectDir = Path.GetDirectoryName(project.ProjectPath);
                Directory.CreateDirectory(projectDir);
                var configPath = Path.Combine(projectDir, "NuGet.Config");

                var doc = new XDocument();
                var configuration = new XElement(XName.Get("configuration"));
                doc.Add(configuration);

                var config = new XElement(XName.Get("config"));
                configuration.Add(config);

                var packageSources = new XElement(XName.Get("packageSources"));
                configuration.Add(packageSources);
                packageSources.Add(new XElement(XName.Get("clear")));

                var sourceEntry = new XElement(XName.Get("add"));
                sourceEntry.Add(new XAttribute(XName.Get("key"), "projectSource"));
                sourceEntry.Add(new XAttribute(XName.Get("value"), source));
                packageSources.Add(sourceEntry);

                File.WriteAllText(configPath, doc.ToString());

                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.Restore(pathContext, project.ProjectPath);

                // Assert
                Assert.True(project.AssetsFile.Libraries.Select(e => e.Name).Contains("packageB"));
            }
        }

        [Fact]
        public async Task RestoreNetCore_VerifyProjectConfigChangeTriggersARestoreAsync()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                //Act
                var r1 = Util.RestoreSolution(pathContext);

                //Assert.
                Assert.Equal(0, r1.Item1);
                Assert.Contains("Writing cache file", r1.Item2);

                // Act
                var r2 = Util.RestoreSolution(pathContext);

                //Assert.
                Assert.Equal(0, r2.Item1);
                Assert.DoesNotContain("Writing cache file", r2.Item2);

                // create a config file
                var projectDir = Path.GetDirectoryName(projectA.ProjectPath);

                var configPath = Path.Combine(projectDir, "NuGet.Config");

                var doc = new XDocument();
                var configuration = new XElement(XName.Get("configuration"));
                doc.Add(configuration);

                var config = new XElement(XName.Get("config"));
                configuration.Add(config);

                var packageSources = new XElement(XName.Get("packageSources"));
                configuration.Add(packageSources);

                var sourceEntry = new XElement(XName.Get("add"));
                sourceEntry.Add(new XAttribute(XName.Get("key"), "projectSource"));
                sourceEntry.Add(new XAttribute(XName.Get("value"), "https://www.nuget.org/api/v2"));
                packageSources.Add(sourceEntry);

                var localSource = new XElement(XName.Get("add"));
                localSource.Add(new XAttribute(XName.Get("key"), "localSource"));
                localSource.Add(new XAttribute(XName.Get("value"), pathContext.PackageSource));
                packageSources.Add(localSource);


                File.WriteAllText(configPath, doc.ToString());

                // Act
                var r3 = Util.RestoreSolution(pathContext, 0, "-configFile", "NuGet.Config");


                //Assert.
                Assert.Equal(0, r3.Item1);
                Assert.Contains("Writing cache file", r3.Item2);
            }
        }


        [Fact]
        public async Task RestoreNetCore_VerifyFallbackFoldersChangeTriggersARestoreAsync()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                //Act
                var r1 = Util.RestoreSolution(pathContext);

                //Assert.
                Assert.Equal(0, r1.Item1);
                Assert.Contains("Writing cache file", r1.Item2);

                // Act
                var r2 = Util.RestoreSolution(pathContext);

                //Assert.
                Assert.Equal(0, r2.Item1);
                Assert.DoesNotContain("Writing cache file", r2.Item2);

                // create a config file
                var projectDir = Path.GetDirectoryName(projectA.ProjectPath);

                var configPath = Path.Combine(projectDir, "NuGet.Config");

                var doc = new XDocument();
                var configuration = new XElement(XName.Get("configuration"));
                doc.Add(configuration);

                var config = new XElement(XName.Get("config"));
                configuration.Add(config);

                var packageSources = new XElement(XName.Get("fallbackFolders"));
                configuration.Add(packageSources);

                var sourceEntry = new XElement(XName.Get("add"));
                sourceEntry.Add(new XAttribute(XName.Get("key"), "folder"));
                sourceEntry.Add(new XAttribute(XName.Get("value"), "blaa"));
                packageSources.Add(sourceEntry);

                var sources = new XElement(XName.Get("packageSources"));
                configuration.Add(sources);
                var localSource = new XElement(XName.Get("add"));
                localSource.Add(new XAttribute(XName.Get("key"), "localSource"));
                localSource.Add(new XAttribute(XName.Get("value"), pathContext.PackageSource));
                sources.Add(localSource);

                File.WriteAllText(configPath, doc.ToString());

                // Act
                var r3 = Util.RestoreSolution(pathContext, 0, "-configFile", "NuGet.Config");


                //Assert.
                Assert.Equal(0, r3.Item1);
                Assert.Contains("Writing cache file", r3.Item2);
            }
        }

        [Fact]
        public async Task RestoreNetCore_VerifyGlobalPackagesPathChangeTriggersARestoreAsync()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                //Act
                var r1 = Util.RestoreSolution(pathContext);

                //Assert.
                Assert.Equal(0, r1.Item1);
                Assert.Contains("Writing cache file", r1.Item2);

                // Act
                var r2 = Util.RestoreSolution(pathContext);

                //Assert.
                Assert.Equal(0, r2.Item1);
                Assert.DoesNotContain("Writing cache file", r2.Item2);

                // create a config file
                var projectDir = Path.GetDirectoryName(projectA.ProjectPath);

                var configPath = Path.Combine(projectDir, "NuGet.Config");

                var doc = new XDocument();
                var configuration = new XElement(XName.Get("configuration"));
                doc.Add(configuration);

                var config = new XElement(XName.Get("config"));
                configuration.Add(config);

                var sourceEntry = new XElement(XName.Get("add"));
                sourceEntry.Add(new XAttribute(XName.Get("key"), "globalPackagesPath"));
                sourceEntry.Add(new XAttribute(XName.Get("value"), "blaa"));
                configuration.Add(sourceEntry);

                var packageSources = new XElement(XName.Get("packageSources"));
                configuration.Add(packageSources);
                var localSource = new XElement(XName.Get("add"));
                localSource.Add(new XAttribute(XName.Get("key"), "localSource"));
                localSource.Add(new XAttribute(XName.Get("value"), pathContext.PackageSource));
                packageSources.Add(localSource);

                File.WriteAllText(configPath, doc.ToString());

                // Act
                var r3 = Util.RestoreSolution(pathContext, 0, "-configFile", "NuGet.Config");


                //Assert.
                Assert.Equal(0, r3.Item1);
                Assert.Contains("Writing cache file", r3.Item2);
            }
        }

        [Fact]
        public async Task RestoreNetCore_VerifyPackageReference_WithoutRestoreProjectStyleAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                projectA.AddPackageToAllFrameworks(packageX);
                projectA.Properties.Clear();

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Act
                var r = Util.RestoreSolution(pathContext);

                var propsXML = XDocument.Load(projectA.PropsOutput);
                var styleNode = propsXML.Root.Elements().First().Elements(XName.Get("NuGetProjectStyle", "http://schemas.microsoft.com/developer/msbuild/2003")).FirstOrDefault();

                // Assert
                var assetsFile = projectA.AssetsFile;
                Assert.NotNull(assetsFile);
                Assert.Equal(ProjectStyle.PackageReference, assetsFile.PackageSpec.RestoreMetadata.ProjectStyle);
                Assert.Equal("PackageReference", styleNode.Value);
            }
        }

        [Fact]
        public async Task RestoreNetCore_SetProjectStyleWithProperty_PackageReferenceAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                // Add a project.json file which will be ignored
                var projectJson = JObject.Parse(@"{
                                                    'dependencies': {
                                                    },
                                                    'frameworks': {
                                                        'net45': {
                                                            'x': '1.0.0'
                                                    }
                                                  }
                                               }");

                projectA.AddPackageToAllFrameworks(packageX);
                projectA.Properties.Clear();
                projectA.Properties.Add("RestoreProjectStyle", "PackageReference");

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                File.WriteAllText(Path.Combine(Path.GetDirectoryName(projectA.ProjectPath), "project.json"), projectJson.ToString());

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Act
                var r = Util.RestoreSolution(pathContext);

                var propsXML = XDocument.Load(projectA.PropsOutput);
                var styleNode = propsXML.Root.Elements().First().Elements(XName.Get("NuGetProjectStyle", "http://schemas.microsoft.com/developer/msbuild/2003")).FirstOrDefault();

                // Assert
                var assetsFile = projectA.AssetsFile;
                Assert.NotNull(assetsFile);
                Assert.Equal(ProjectStyle.PackageReference, assetsFile.PackageSpec.RestoreMetadata.ProjectStyle);
                Assert.Equal("PackageReference", styleNode.Value);
            }
        }

        [Fact]
        public async Task RestoreNetCore_SetProjectStyleWithProperty_ProjectJsonAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                // Create a .NETCore project, but add a project.json file to it.
                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var projectJson = JObject.Parse(@"{
                                                    'dependencies': {
                                                        'x': '1.0.0'
                                                    },
                                                    'frameworks': {
                                                        'net45': { }                                                            
                                                    }
                                                  }");

                // Force this project to ProjectJson
                projectA.Properties.Clear();
                projectA.Properties.Add("RestoreProjectStyle", "ProjectJson");
                projectA.Type = ProjectStyle.ProjectJson;

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                File.WriteAllText(Path.Combine(Path.GetDirectoryName(projectA.ProjectPath), "project.json"), projectJson.ToString());

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                packageX.AddFile("build/net45/x.targets");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                var projectXML = XDocument.Load(projectA.ProjectPath);
                projectXML.Root.AddFirst(new XElement(XName.Get("Target", "http://schemas.microsoft.com/developer/msbuild/2003"), new XAttribute(XName.Get("Name"), "_SplitProjectReferencesByFileExistence")));
                projectXML.Save(projectA.ProjectPath);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                var assetsFile = projectA.AssetsFile;
                Assert.NotNull(assetsFile);
                Assert.Equal(ProjectStyle.ProjectJson, assetsFile.PackageSpec.RestoreMetadata.ProjectStyle);
            }
        }

        [Fact]
        public void RestoreNetCore_ProjectToProject_Recursive()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("NETCoreApp1.0"));

                var projectB = SimpleTestProjectContext.CreateNETCore(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("NETStandard1.5"));

                var projectC = SimpleTestProjectContext.CreateNETCore(
                    "c",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("NETStandard1.5"));

                // A -> B
                projectA.AddProjectToAllFrameworks(projectB);

                // B -> C
                projectB.AddProjectToAllFrameworks(projectC);

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);
                solution.Projects.Add(projectC);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var nugetexe = Util.GetNuGetExePath();

                var args = new string[] {
                    "restore",
                    projectA.ProjectPath,
                    "-Verbosity",
                    "detailed",
                    "-Recursive"
                };

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    pathContext.WorkingDirectory.Path,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);

                // Assert
                Assert.True(File.Exists(projectA.AssetsFileOutputPath));
                Assert.True(File.Exists(projectB.AssetsFileOutputPath));
                Assert.True(File.Exists(projectC.AssetsFileOutputPath));
            }
        }

        [Fact]
        public void RestoreNetCore_ProjectToProject_RecursiveIgnoresNonRestorable()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("NETCoreApp1.0"));

                var projectB = SimpleTestProjectContext.CreateNonNuGet(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("NETStandard1.5"));

                var projectC = SimpleTestProjectContext.CreateNETCore(
                    "c",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("NETStandard1.5"));

                // A -> B
                projectA.AddProjectToAllFrameworks(projectB);

                // B -> C
                projectB.AddProjectToAllFrameworks(projectC);

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);
                solution.Projects.Add(projectC);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var nugetexe = Util.GetNuGetExePath();

                var args = new string[] {
                    "restore",
                    projectA.ProjectPath,
                    "-Verbosity",
                    "detailed",
                    "-Recursive"
                };

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    pathContext.WorkingDirectory.Path,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);

                // Assert
                Assert.True(File.Exists(projectA.AssetsFileOutputPath));
                Assert.False(File.Exists(projectB.AssetsFileOutputPath));
                Assert.True(File.Exists(projectC.AssetsFileOutputPath));
            }
        }

        [Fact]
        public async Task RestoreNetCore_RestoreWithRIDAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                projectA.Properties.Add("RuntimeIdentifiers", "win7-x86");

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                Assert.True(File.Exists(projectA.AssetsFileOutputPath), r.Item2);
                Assert.True(File.Exists(projectA.TargetsOutput), r.Item2);
                Assert.True(File.Exists(projectA.PropsOutput), r.Item2);

                var assetsFile = projectA.AssetsFile;
                Assert.Equal(2, assetsFile.Targets.Count);
                Assert.Equal(NuGetFramework.Parse("net45"), assetsFile.Targets.Single(t => string.IsNullOrEmpty(t.RuntimeIdentifier)).TargetFramework);
                Assert.Equal(NuGetFramework.Parse("net45"), assetsFile.Targets.Single(t => !string.IsNullOrEmpty(t.RuntimeIdentifier)).TargetFramework);
                Assert.Equal("win7-x86", assetsFile.Targets.Single(t => !string.IsNullOrEmpty(t.RuntimeIdentifier)).RuntimeIdentifier);
            }
        }

        [Fact]
        public async Task RestoreNetCore_RestoreWithRID_ValidateRID_FailureAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                projectA.Properties.Add("RuntimeIdentifiers", "win7-x86");

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                packageX.AddFile("ref/net45/x.dll");
                packageX.AddFile("lib/win8/x.dll");

                projectA.AddPackageToAllFrameworks(packageX);
                projectA.Properties.Add("ValidateRuntimeIdentifierCompatibility", "true");

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 1);
                var output = r.Item2 + " " + r.Item3;

                // Assert
                Assert.True(r.Item1 == 1);
                Assert.Contains("no run-time assembly compatible", output);
            }
        }

        [Fact]
        public async Task RestoreNetCore_RestoreWithRID_ValidateRID_IgnoreFailureAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                projectA.Properties.Add("RuntimeIdentifiers", "win7-x86");

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                packageX.AddFile("ref/net45/x.dll");
                packageX.AddFile("lib/win8/x.dll");

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                Assert.True(r.Item1 == 0);
                Assert.DoesNotContain("no run-time assembly compatible", r.Item3);
            }
        }

        [Fact]
        public async Task RestoreNetCore_RestoreWithRID_ValidateRID_FailureForProjectJsonAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectJson = JObject.Parse(@"{
                                                    'dependencies': {
                                                        'x': '1.0.0'
                                                    },
                                                    'frameworks': {
                                                        'net45': {
                                                    }
                                                  },
                                                  'runtimes': { 'win7-x86': {} }
                                               }");

                var projectA = SimpleTestProjectContext.CreateUAP(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"),
                    projectJson);

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                packageX.AddFile("ref/net45/x.dll");
                packageX.AddFile("lib/win8/x.dll");

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 1);
                var output = r.Item2 + " " + r.Item3;

                // Assert
                Assert.True(r.Item1 == 1);
                Assert.Contains("no run-time assembly compatible", output);
            }
        }

        [Fact]
        public async Task RestoreNetCore_RestoreWithRIDSingleAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                projectA.Properties.Add("RuntimeIdentifier", "win7-x86");

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                Assert.True(File.Exists(projectA.AssetsFileOutputPath), r.Item2);
                Assert.True(File.Exists(projectA.TargetsOutput), r.Item2);
                Assert.True(File.Exists(projectA.PropsOutput), r.Item2);

                var assetsFile = projectA.AssetsFile;
                Assert.Equal(2, assetsFile.Targets.Count);
                Assert.Equal(NuGetFramework.Parse("net45"), assetsFile.Targets.Single(t => string.IsNullOrEmpty(t.RuntimeIdentifier)).TargetFramework);
                Assert.Equal(NuGetFramework.Parse("net45"), assetsFile.Targets.Single(t => !string.IsNullOrEmpty(t.RuntimeIdentifier)).TargetFramework);
                Assert.Equal("win7-x86", assetsFile.Targets.Single(t => !string.IsNullOrEmpty(t.RuntimeIdentifier)).RuntimeIdentifier);
            }
        }

        [Fact]
        public async Task RestoreNetCore_RestoreWithRIDDuplicatesAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                projectA.Properties.Add("RuntimeIdentifier", "win7-x86");
                projectA.Properties.Add("RuntimeIdentifiers", "win7-x86;win7-x86;;");

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                Assert.True(File.Exists(projectA.AssetsFileOutputPath), r.Item2);
                Assert.True(File.Exists(projectA.TargetsOutput), r.Item2);
                Assert.True(File.Exists(projectA.PropsOutput), r.Item2);

                var assetsFile = projectA.AssetsFile;
                Assert.Equal(2, assetsFile.Targets.Count);
                Assert.Equal(NuGetFramework.Parse("net45"), assetsFile.Targets.Single(t => string.IsNullOrEmpty(t.RuntimeIdentifier)).TargetFramework);
                Assert.Equal(NuGetFramework.Parse("net45"), assetsFile.Targets.Single(t => !string.IsNullOrEmpty(t.RuntimeIdentifier)).TargetFramework);
                Assert.Equal("win7-x86", assetsFile.Targets.Single(t => !string.IsNullOrEmpty(t.RuntimeIdentifier)).RuntimeIdentifier);
            }
        }

        [Fact]
        public async Task RestoreNetCore_RestoreWithSupportsAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var guid = Guid.NewGuid().ToString();
                projectA.Properties.Add("RuntimeSupports", guid);

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Act
                var r = Util.RestoreSolution(pathContext);
                var output = r.Item2 + r.Item3;

                // Assert
                Assert.True(File.Exists(projectA.AssetsFileOutputPath), output);
                Assert.Contains($"Compatibility Profile: {guid}", output);
            }
        }

        [Fact]
        public async Task RestoreNetCore_RestoreWithMultipleRIDsAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                projectA.Properties.Add("RuntimeIdentifiers", "win7-x86;win7-x64");

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                Assert.True(File.Exists(projectA.AssetsFileOutputPath), r.Item2);
                Assert.True(File.Exists(projectA.TargetsOutput), r.Item2);
                Assert.True(File.Exists(projectA.PropsOutput), r.Item2);

                var assetsFile = projectA.AssetsFile;
                Assert.Equal(3, assetsFile.Targets.Count);
                Assert.Equal("win7-x64", assetsFile.Targets.Single(t => t.RuntimeIdentifier == "win7-x64").RuntimeIdentifier);
                Assert.Equal("win7-x86", assetsFile.Targets.Single(t => t.RuntimeIdentifier == "win7-x86").RuntimeIdentifier);
            }
        }

        [Fact]
        public async Task RestoreNetCore_MultipleProjects_SameToolDifferentVersionsWithMultipleHitsAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Create this many different tool versions and projects
                var testCount = 10;

                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                var avoidVersion = $"{testCount + 100}.0.0";

                var packageZ = new SimpleTestPackageContext()
                {
                    Id = "z",
                    Version = avoidVersion
                };

                var projects = new List<SimpleTestProjectContext>();

                for (var i = 0; i < testCount; i++)
                {
                    var project = SimpleTestProjectContext.CreateNETCore(
                        $"proj{i}",
                        pathContext.SolutionRoot,
                        NuGetFramework.Parse("net45"));

                    project.AddPackageToAllFrameworks(packageX);

                    var packageZSub = new SimpleTestPackageContext()
                    {
                        Id = "z",
                        Version = $"{i + 1}.0.0"
                    };

                    project.DotnetCLIToolReferences.Add(packageZSub);

                    await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                        pathContext.PackageSource,
                        PackageSaveMode.Defaultv3,
                        packageZSub);

                    solution.Projects.Add(project);
                    solution.Create(pathContext.SolutionRoot);
                }

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX,
                    packageZ);

                var path = Path.Combine(pathContext.UserPackagesFolder, ".tools", "z", avoidVersion, "netcoreapp1.0", "project.assets.json");
                var zPath = Path.Combine(pathContext.UserPackagesFolder, ".tools", "z");

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                // Version should not be used
                Assert.False(File.Exists(path), r.Item2);

                // Each project should have its own tool verion
                Assert.Equal(testCount, Directory.GetDirectories(zPath).Length);
            }
        }

        [Fact]
        public async Task RestoreNetCore_MultipleProjects_SameToolDifferentVersionsWithMultipleHits_NoOpAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Create this many different tool versions and projects
                var testCount = 10;

                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                var avoidVersion = $"{testCount + 100}.0.0";

                var packageZ = new SimpleTestPackageContext()
                {
                    Id = "z",
                    Version = avoidVersion
                };

                var projects = new List<SimpleTestProjectContext>();

                for (var i = 0; i < testCount; i++)
                {
                    var project = SimpleTestProjectContext.CreateNETCore(
                        $"proj{i}",
                        pathContext.SolutionRoot,
                        NuGetFramework.Parse("net45"));

                    project.AddPackageToAllFrameworks(packageX);

                    var packageZSub = new SimpleTestPackageContext()
                    {
                        Id = "z",
                        Version = $"{i + 1}.0.0"
                    };

                    project.DotnetCLIToolReferences.Add(packageZSub);

                    await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                        pathContext.PackageSource,
                        PackageSaveMode.Defaultv3,
                        packageZSub);

                    solution.Projects.Add(project);
                    solution.Create(pathContext.SolutionRoot);
                }

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX,
                    packageZ);

                var path = Path.Combine(pathContext.UserPackagesFolder, ".tools", "z", avoidVersion, "netcoreapp1.0", "project.assets.json");
                var cacheFile = Path.Combine(pathContext.UserPackagesFolder, ".tools", "z", avoidVersion, "netcoreapp1.0", "z.nuget.cache");
                var zPath = Path.Combine(pathContext.UserPackagesFolder, ".tools", "z");

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                // Version should not be used
                Assert.False(File.Exists(path), r.Item2);
                Assert.False(File.Exists(cacheFile), r.Item2);

                // Each project should have its own tool verion
                Assert.Equal(testCount, Directory.GetDirectories(zPath).Length);

                // Act
                var r2 = Util.RestoreSolution(pathContext);

                // Assert
                // Version should not be used
                Assert.False(File.Exists(path), r2.Item2);
                Assert.False(File.Exists(cacheFile), r2.Item2);
                Assert.DoesNotContain("NU1603", r2.Item2);
                for (var i = 1; i <= testCount; i++)
                {
                    Assert.Contains($"The restore inputs for 'z-netcoreapp1.0-[{i}.0.0, )' have not changed. No further actions are required to complete the restore.", r2.Item2);
                }
                // Each project should have its own tool verion
                Assert.Equal(testCount, Directory.GetDirectories(zPath).Length);
            }
        }

        [Fact]
        public async Task RestoreNetCore_NoOp_AddingNewPackageRestoresAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                var packageZ = new SimpleTestPackageContext()
                {
                    Id = "z",
                    Version = "20.0.0"
                };

                var projects = new List<SimpleTestProjectContext>();

                var project = SimpleTestProjectContext.CreateNETCore(
                    $"proj",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                project.AddPackageToAllFrameworks(packageX);
                solution.Projects.Add(project);
                solution.Create(pathContext.SolutionRoot);


                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX,
                    packageZ);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                Assert.Equal(0,r.Item1);
                Assert.Contains("Writing cache file", r.Item2);

                //re-arrange again
                project.AddPackageToAllFrameworks(packageZ);
                project.Save();
                
                //assert
                Assert.Contains("Writing cache file", r.Item2);
                Assert.Equal(0, r.Item1);


            }
        }

        [Fact]
        public async Task RestoreNetCore_OriginalTargetFrameworkArePreservedAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                var projects = new List<SimpleTestProjectContext>();

                var project = SimpleTestProjectContext.CreateNETCore(
                    "proj",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netstandard1.3"),
                    NuGetFramework.Parse("net4"));

                project.OriginalFrameworkStrings = new List<string> { "netstandard1.3", "net4" };

                project.AddPackageToAllFrameworks(packageX);
                solution.Projects.Add(project);
                solution.Create(pathContext.SolutionRoot);


                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Act
                var r = Util.RestoreSolution(pathContext);
                Assert.Equal(0, r.Item1);
                Assert.True(File.Exists(project.PropsOutput), r.Item2);
                var propsXML = XDocument.Parse(File.ReadAllText(project.PropsOutput));

                var propsItemGroups = propsXML.Root.Elements().Where(e => e.Name.LocalName == "ItemGroup").ToList();

                Assert.Equal("'$(TargetFramework)' == 'net4' AND '$(ExcludeRestorePackageImports)' != 'true'", propsItemGroups[0].Attribute(XName.Get("Condition")).Value.Trim());
                Assert.Equal("'$(TargetFramework)' == 'netstandard1.3' AND '$(ExcludeRestorePackageImports)' != 'true'", propsItemGroups[1].Attribute(XName.Get("Condition")).Value.Trim());
            }
        }

        [Fact]
        public async Task RestoreNetCore_NoOp_AddingANewProjectRestoresOnlyThatProjectAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                var packageZ = new SimpleTestPackageContext()
                {
                    Id = "z",
                    Version = "20.0.0"
                };

                var projects = new List<SimpleTestProjectContext>();

                var project = SimpleTestProjectContext.CreateNETCore(
                    $"proj",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                project.AddPackageToAllFrameworks(packageX);
                solution.Projects.Add(project);
                solution.Create(pathContext.SolutionRoot);


                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX,
                    packageZ);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                Assert.Equal(0, r.Item1);
                Assert.Contains("Writing cache file", r.Item2);
                
                // build project
                var project2 = SimpleTestProjectContext.CreateNETCore(
                    $"proj2",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                project2.AddPackageToAllFrameworks(packageZ);
                solution.Projects.Add(project2);
                solution.Save();
                project2.Save();

                // Act
                var r2 = Util.RestoreSolution(pathContext);

                // Assert
                Assert.Equal(0, r2.Item1);
                Assert.Contains("Writing cache file", r2.Item2);
                Assert.Contains("No further actions are required to complete", r2.Item2);

            }
        }

        [Fact]
        public async Task RestoreNetCore_NoOp_WarningsAndErrorsDontAffectHashAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                var projects = new List<SimpleTestProjectContext>();

                var project = SimpleTestProjectContext.CreateNETCore(
                    $"proj",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                project.AddPackageToAllFrameworks(packageX);
                // Setup - set warnings As Errors
                project.WarningsAsErrors = true;

                solution.Projects.Add(project);
                solution.Create(pathContext.SolutionRoot);


                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                Assert.Equal(0, r.Item1);
                Assert.Contains("Writing cache file", r.Item2);

                //Setup - remove the warnings and errors
                project.WarningsAsErrors = false;
                project.Save();

                // Act
                var r2 = Util.RestoreSolution(pathContext);

                // Assert
                Assert.Equal(0, r2.Item1);
                Assert.Contains("No further actions are required to complete", r2.Item2);
            }
        }

        [Fact]
        public async Task RestoreNetCore_MultipleProjects_SameToolDifferentVersionsAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                var packageZ = new SimpleTestPackageContext()
                {
                    Id = "z",
                    Version = "20.0.0"
                };

                var projects = new List<SimpleTestProjectContext>();

                for (var i = 0; i < 10; i++)
                {
                    var project = SimpleTestProjectContext.CreateNETCore(
                        $"proj{i}",
                        pathContext.SolutionRoot,
                        NuGetFramework.Parse("net45"));

                    project.AddPackageToAllFrameworks(packageX);
                    project.DotnetCLIToolReferences.Add(new SimpleTestPackageContext()
                    {
                        Id = "z",
                        Version = $"{i}.0.0"
                    });

                    solution.Projects.Add(project);
                    solution.Create(pathContext.SolutionRoot);
                }

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX,
                    packageZ);

                var path = Path.Combine(pathContext.UserPackagesFolder, ".tools", "z", "20.0.0", "netcoreapp1.0", "project.assets.json");
                var zPath = Path.Combine(pathContext.UserPackagesFolder, ".tools", "z");

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                Assert.True(File.Exists(path), r.Item2);
                Assert.Equal(1, Directory.GetDirectories(zPath).Length);
            }
        }

        [Fact]
        public async Task RestoreNetCore_MultipleProjects_SameToolDifferentVersions_NoOp_FailsAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                var packageZ = new SimpleTestPackageContext()
                {
                    Id = "z",
                    Version = "20.0.0"
                };

                var projects = new List<SimpleTestProjectContext>();

                for (var i = 0; i < 10; i++)
                {
                    var project = SimpleTestProjectContext.CreateNETCore(
                        $"proj{i}",
                        pathContext.SolutionRoot,
                        NuGetFramework.Parse("net45"));

                    project.AddPackageToAllFrameworks(packageX);
                    project.DotnetCLIToolReferences.Add(new SimpleTestPackageContext()
                    {
                        Id = "z",
                        Version = $"{i}.0.0"
                    });

                    solution.Projects.Add(project);
                    solution.Create(pathContext.SolutionRoot);
                }

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX,
                    packageZ);

                var path = Path.Combine(pathContext.UserPackagesFolder, ".tools", "z", "20.0.0", "netcoreapp1.0", "project.assets.json");
                var cachePath = Path.Combine(pathContext.UserPackagesFolder, ".tools", "z", "20.0.0", "netcoreapp1.0", "z.nuget.cache");
                var zPath = Path.Combine(pathContext.UserPackagesFolder, ".tools", "z");

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                Assert.True(File.Exists(path), r.Item2);
                Assert.Equal(1, Directory.GetDirectories(zPath).Length);
                Assert.True(File.Exists(cachePath));
                Assert.True(File.Exists(path));

                // Act
                var r2 = Util.RestoreSolution(pathContext);
                // Assert
                Assert.True(File.Exists(path), r2.Item2);
                Assert.True(File.Exists(cachePath), r2.Item2);
                Assert.Equal(1, Directory.GetDirectories(zPath).Length);
                // This is expected because all the projects keep overwriting the cache file for the tool.

                Assert.Contains(@"have changed. Continuing restore.", r2.Item2);
                var count = Regex.Matches(r2.Item2, (@"have changed. Continuing restore.")).Count;
                Assert.True(count == 9 || count == 10, $"{ count } needs to be 9 or 10 in \n: { r2.Item2 }");
            }
        }

        [Fact]
        public async Task RestoreNetCore_MultipleProjects_SameToolAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                var packageZ = new SimpleTestPackageContext()
                {
                    Id = "z",
                    Version = "1.0.0"
                };

                var projects = new List<SimpleTestProjectContext>();

                for (var i = 0; i < 10; i++)
                {
                    var project = SimpleTestProjectContext.CreateNETCore(
                        $"proj{i}",
                        pathContext.SolutionRoot,
                        NuGetFramework.Parse("net45"));

                    project.AddPackageToAllFrameworks(packageX);
                    project.DotnetCLIToolReferences.Add(new SimpleTestPackageContext()
                    {
                        Id = "z",
                        Version = "1.0.0"
                    });

                    solution.Projects.Add(project);
                    solution.Create(pathContext.SolutionRoot);
                }

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX,
                    packageZ);

                var path = Path.Combine(pathContext.UserPackagesFolder, ".tools", "z", "1.0.0", "netcoreapp1.0", "project.assets.json");

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                Assert.True(File.Exists(path), r.Item2);
            }
        }

        [Fact]
        public async Task RestoreNetCore_MultipleProjects_SameTool_DifferentVersionRanges_DoesNotNoOpAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                var packageZ = new SimpleTestPackageContext()
                {
                    Id = "z",
                    Version = "2.0.0"
                };

                var projects = new List<SimpleTestProjectContext>();


                var project = SimpleTestProjectContext.CreateNETCore(
                    $"proj1",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                project.AddPackageToAllFrameworks(packageX);
                project.DotnetCLIToolReferences.Add(new SimpleTestPackageContext()
                {
                    Id = "z",
                    Version = "2.0.0"
                });

                var project2 = SimpleTestProjectContext.CreateNETCore(
                    $"proj2",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                project2.AddPackageToAllFrameworks(packageX);
                project2.DotnetCLIToolReferences.Add(new SimpleTestPackageContext()
                {
                    Id = "z",
                    Version = "1.5.*"
                });

                solution.Projects.Add(project2);
                solution.Projects.Add(project);

                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX,
                    packageZ);

                var assetsPath = Path.Combine(pathContext.UserPackagesFolder, ".tools", "z", "2.0.0", "netcoreapp1.0", "project.assets.json");
                var cachePath = Path.Combine(pathContext.UserPackagesFolder, ".tools", "z", "2.0.0", "netcoreapp1.0", "z.nuget.cache");

                // Act
                var r = Util.RestoreSolution(pathContext);
                // Assert
                Assert.True(File.Exists(assetsPath));
                Assert.True(File.Exists(cachePath));

                // Act
                var r2 = Util.RestoreSolution(pathContext);
                // Assert
                Assert.True(File.Exists(assetsPath));
                Assert.True(File.Exists(cachePath));
                // This is expected, because despite the fact that both projects resolve to the same tool, the version range they request is different so they will keep overwriting each other
                // Basically, it is impossible for both tools to no-op.
                Assert.Contains($"Writing tool assets file to disk", r2.Item2);
                r = Util.RestoreSolution(pathContext);

            }
        }

        [Fact]
        public async Task RestoreNetCore_MultipleProjects_SameTool_OverlappingVersionRanges_DoesNoOpAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                var packageZ = new SimpleTestPackageContext()
                {
                    Id = "z",
                    Version = "2.0.0"
                };

                var projects = new List<SimpleTestProjectContext>();


                var project = SimpleTestProjectContext.CreateNETCore(
                    $"proj1",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                project.AddPackageToAllFrameworks(packageX);
                project.DotnetCLIToolReferences.Add(new SimpleTestPackageContext()
                {
                    Id = "z",
                    Version = "2.0.0"
                });

                var project2 = SimpleTestProjectContext.CreateNETCore(
                    $"proj2",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                project2.AddPackageToAllFrameworks(packageX);
                project2.DotnetCLIToolReferences.Add(new SimpleTestPackageContext()
                {
                    Id = "z",
                    Version = "2.0.*"
                });

                solution.Projects.Add(project2);
                solution.Projects.Add(project);

                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX,
                    packageZ);

                var assetsPath = Path.Combine(pathContext.UserPackagesFolder, ".tools", "z", "2.0.0", "netcoreapp1.0", "project.assets.json");
                var cachePath = Path.Combine(pathContext.UserPackagesFolder, ".tools", "z", "2.0.0", "netcoreapp1.0", "z.nuget.cache");

                // Act
                var r = Util.RestoreSolution(pathContext);
                // Assert
                Assert.True(File.Exists(assetsPath));
                Assert.True(File.Exists(cachePath));

                // Act
                var r2 = Util.RestoreSolution(pathContext);
                // Assert
                Assert.True(File.Exists(assetsPath));
                Assert.True(File.Exists(cachePath));

                // This is expected, because despite the fact that both projects resolve to the same tool, the version range they request is different so they will keep overwriting each other
                // Basically, it is impossible for both tools to no-op.
                Assert.Contains($"Writing tool assets file to disk", r2.Item2);
            }
        }

        [Fact]
        public async Task RestoreNetCore_MultipleProjects_SameTool_OverlappingVersionRanges_OnlyOneMatchesPackage_DoesNoOpAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                var packageZ = new SimpleTestPackageContext()
                {
                    Id = "z",
                    Version = "2.5.0"
                };


                var packageZ20 = new SimpleTestPackageContext()
                {
                    Id = "z",
                    Version = "2.0.0"
                };


                var projects = new List<SimpleTestProjectContext>();


                var project = SimpleTestProjectContext.CreateNETCore(
                    $"proj1",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                project.AddPackageToAllFrameworks(packageX);
                project.DotnetCLIToolReferences.Add(new SimpleTestPackageContext()
                {
                    Id = "z",
                    Version = "2.0.0"
                });

                var project2 = SimpleTestProjectContext.CreateNETCore(
                    $"proj2",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                project2.AddPackageToAllFrameworks(packageX);
                project2.DotnetCLIToolReferences.Add(new SimpleTestPackageContext()
                {
                    Id = "z",
                    Version = "2.0.*"
                });

                solution.Projects.Add(project2);
                solution.Projects.Add(project);

                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX,
                    packageZ);

                var assetsPath = Path.Combine(pathContext.UserPackagesFolder, ".tools", "z", "2.5.0", "netcoreapp1.0", "project.assets.json");
                var cachePath = Path.Combine(pathContext.UserPackagesFolder, ".tools", "z", "2.5.0", "netcoreapp1.0", "z.nuget.cache");

                // Act
                var r = Util.RestoreSolution(pathContext);
                // Assert
                Assert.True(File.Exists(assetsPath));
                Assert.True(File.Exists(cachePath));


                // Setup Again. Add the new package....should not be picked up though
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                   pathContext.PackageSource,
                   PackageSaveMode.Defaultv3,
                   packageZ20);

                var assetsPath20 = Path.Combine(pathContext.UserPackagesFolder, ".tools", "z", "2.0.0", "netcoreapp1.0", "project.assets.json");
                var cachePath20 = Path.Combine(pathContext.UserPackagesFolder, ".tools", "z", "2.0.0", "netcoreapp1.0", "z.nuget.cache");

                // Act
                var r2 = Util.RestoreSolution(pathContext);

                // Assert
                Assert.True(File.Exists(assetsPath));
                Assert.True(File.Exists(cachePath));

                Assert.True(File.Exists(assetsPath20));
                Assert.True(File.Exists(cachePath20));

            }
        }


        [Fact]
        public async Task RestoreNetCore_MultipleProjects_SameTool_NoOpAsync() 
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                var packageZ = new SimpleTestPackageContext()
                {
                    Id = "z",
                    Version = "1.0.0"
                };

                var projects = new List<SimpleTestProjectContext>();

                for (var i = 0; i < 10; i++)
                {
                    var project = SimpleTestProjectContext.CreateNETCore(
                        $"proj{i}",
                        pathContext.SolutionRoot,
                        NuGetFramework.Parse("net45"));

                    project.AddPackageToAllFrameworks(packageX);
                    project.DotnetCLIToolReferences.Add(new SimpleTestPackageContext()
                    {
                        Id = "z",
                        Version = "1.0.0"
                    });

                    solution.Projects.Add(project);
                    solution.Create(pathContext.SolutionRoot);
                }

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX,
                    packageZ);

                var assetsPath = Path.Combine(pathContext.UserPackagesFolder, ".tools", "z", "1.0.0", "netcoreapp1.0", "project.assets.json");
                var cachePath = Path.Combine(pathContext.UserPackagesFolder, ".tools", "z", "1.0.0", "netcoreapp1.0", "z.nuget.cache");

                // Act
                var r = Util.RestoreSolution(pathContext);
                // Assert
                Assert.True(File.Exists(assetsPath));
                Assert.True(File.Exists(cachePath));

                // Act
                var r2 = Util.RestoreSolution(pathContext);
                // Assert
                Assert.True(File.Exists(assetsPath));
                Assert.True(File.Exists(cachePath));
                Assert.Contains($"The restore inputs for 'z-netcoreapp1.0-[1.0.0, )' have not changed. No further actions are required to complete the restore", r2.Item2);

                r = Util.RestoreSolution(pathContext);

            }
        }

        [Fact]
        public async Task RestoreNetCore_SingleToolRestoreAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                var packageZ = new SimpleTestPackageContext()
                {
                    Id = "z",
                    Version = "1.0.0"
                };

                var packageY = new SimpleTestPackageContext()
                {
                    Id = "y",
                    Version = "1.0.0"
                };

                packageZ.Dependencies.Add(packageY);

                projectA.AddPackageToAllFrameworks(packageX);

                projectA.DotnetCLIToolReferences.Add(new SimpleTestPackageContext()
                {
                    Id = "z",
                    Version = "1.0.0"
                });

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX,
                    packageZ,
                    packageY);

                var path = Path.Combine(pathContext.UserPackagesFolder, ".tools", "z", "1.0.0", "netcoreapp1.0", "project.assets.json");

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                Assert.True(File.Exists(path), r.Item2);
            }
        }

        [Fact]
        public async Task RestoreNetCore_SingleToolRestore_NoopAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                var packageZ = new SimpleTestPackageContext()
                {
                    Id = "z",
                    Version = "1.0.0"
                };

                var packageY = new SimpleTestPackageContext()
                {
                    Id = "y",
                    Version = "1.0.0"
                };

                packageZ.Dependencies.Add(packageY);

                projectA.AddPackageToAllFrameworks(packageX);

                projectA.DotnetCLIToolReferences.Add(new SimpleTestPackageContext()
                {
                    Id = "z",
                    Version = "1.0.0"
                });

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX,
                    packageZ,
                    packageY);

                var assetsPath = Path.Combine(pathContext.UserPackagesFolder, ".tools", "z", "1.0.0", "netcoreapp1.0", "project.assets.json");
                var cachePath = Path.Combine(pathContext.UserPackagesFolder, ".tools", "z", "1.0.0", "netcoreapp1.0", "z.nuget.cache");

                // Act
                var r = Util.RestoreSolution(pathContext);
                // Assert
                Assert.True(File.Exists(assetsPath));
                Assert.True(File.Exists(cachePath));

                // Act
                var r2 = Util.RestoreSolution(pathContext);
                // Assert
                Assert.True(File.Exists(assetsPath));
                Assert.True(File.Exists(cachePath));
                Assert.Contains($"The restore inputs for 'z-netcoreapp1.0-[1.0.0, )' have not changed. No further actions are required to complete the restore.", r2.Item2);

                r = Util.RestoreSolution(pathContext);
            }
        }

        // Just utlizing the infrastracture that we have here, rather than trying to create my own directory structure to test this :)
        [Theory]
        [InlineData("[1.0.0]", "1.0.0")]
        [InlineData("[5.0.0]", "5.0.0")]
        [InlineData("[1.5.0]", null)]
        [InlineData("1.1.*", "2.0.0")]
        public async Task ToolPathResolver_FindsBestMatchingToolVersionAsync(string requestedVersion, string expectedVersion)
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);


                for (var i = 0; i < 10; i++)
                {
                    var project = SimpleTestProjectContext.CreateNETCore(
                        $"proj{i}",
                        pathContext.SolutionRoot,
                        NuGetFramework.Parse("net45"));

                    var packageZ = new SimpleTestPackageContext()
                    {
                        Id = "z",
                        Version = $"{i}.0.0"
                    };

                    project.DotnetCLIToolReferences.Add(new SimpleTestPackageContext()
                    {
                        Id = "z",
                        Version = $"{i}.0.0"
                    });

                    await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                        pathContext.PackageSource,
                        PackageSaveMode.Defaultv3,
                        packageZ);
                    solution.Projects.Add(project);
                }

                solution.Create(pathContext.SolutionRoot);

                var r = Util.RestoreSolution(pathContext);


                // Arrange
                var target = new ToolPathResolver(pathContext.UserPackagesFolder, isLowercase: true);

                var expected = expectedVersion != null ?
                    Path.Combine(
                    pathContext.UserPackagesFolder,
                    ".tools",
                    "z",
                    expectedVersion,
                    "netcoreapp1.0")
                    : null;
                // Act
                var actual = target.GetBestToolDirectoryPath("z", VersionRange.Parse(requestedVersion), NuGetFramework.Parse("netcoreapp1.0"));

                // Assert
                Assert.True(StringComparer.Ordinal.Equals(expected, actual), $"{expected} : {actual}");
            }
        }

        [Fact]
        public async Task RestoreNetCore_RestoreToolInChildProjectWithRecursive_NoOpAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var projectB = SimpleTestProjectContext.CreateNETCore(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                // A -> B
                projectA.AddProjectToAllFrameworks(projectB);

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                projectB.DotnetCLIToolReferences.Add(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                var assetsPath = Path.Combine(pathContext.UserPackagesFolder, ".tools", "x", "1.0.0", "netcoreapp1.0", "project.assets.json");
                var cachePath = Path.Combine(pathContext.UserPackagesFolder, ".tools", "x", "1.0.0", "netcoreapp1.0", "x.nuget.cache");

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 0, additionalArgs: "-Recursive");
                // Assert
                Assert.True(File.Exists(assetsPath));
                Assert.True(File.Exists(cachePath));

                // Act
                var r2 = Util.RestoreSolution(pathContext, expectedExitCode: 0, additionalArgs: "-Recursive");
                // Assert
                Assert.True(File.Exists(assetsPath));
                Assert.True(File.Exists(cachePath));
                Assert.Contains($"The restore inputs for 'x-netcoreapp1.0-[1.0.0, )' have not changed. No further actions are required to complete the restore.", r2.Item2);
                Assert.Contains($"The restore inputs for 'a' have not changed. No further actions are required to complete the restore.", r2.Item2);
                Assert.Contains($"The restore inputs for 'b' have not changed. No further actions are required to complete the restore.", r2.Item2);

                r = Util.RestoreSolution(pathContext);
            }
        }

        [Fact]
        public async Task RestoreNetCore_RestoreToolInChildProjectWithRecursiveAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var projectB = SimpleTestProjectContext.CreateNETCore(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                // A -> B
                projectA.AddProjectToAllFrameworks(projectB);

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                projectB.DotnetCLIToolReferences.Add(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                var path = Path.Combine(pathContext.UserPackagesFolder, ".tools", "x", "1.0.0", "netcoreapp1.0", "project.assets.json");

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 0, additionalArgs: "-Recursive");

                // Assert
                Assert.True(File.Exists(path), r.Item2);
            }
        }

        [Fact]
        public async Task RestoreNetCore_SkipRestoreToolInChildProjectForNonRecursiveAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var projectB = SimpleTestProjectContext.CreateNETCore(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                // A -> B
                projectA.AddProjectToAllFrameworks(projectB);

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                projectB.DotnetCLIToolReferences.Add(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                var path = Path.Combine(pathContext.UserPackagesFolder, ".tools", "x", "1.0.0", "netcoreapp1.0", "project.assets.json");

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                Assert.False(File.Exists(path), r.Item2);
            }
        }
        [Fact]
        public async Task RestoreNetCore_ToolRestoreWithNoVersionAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                var packageZ = new SimpleTestPackageContext()
                {
                    Id = "z",
                    Version = "1.0.0"
                };

                var packageY = new SimpleTestPackageContext()
                {
                    Id = "y",
                    Version = "1.0.0"
                };

                packageZ.Dependencies.Add(packageY);

                projectA.AddPackageToAllFrameworks(packageX);

                projectA.DotnetCLIToolReferences.Add(new SimpleTestPackageContext()
                {
                    Id = "z",
                    Version = ""
                });

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX,
                    packageZ,
                    packageY);

                var path = Path.Combine(pathContext.UserPackagesFolder, ".tools", "z", "1.0.0", "netcoreapp1.0", "project.assets.json");

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                Assert.Contains("WARNING: NU1604", r.AllOutput);
            }
        }

        [Fact]
        public async Task RestoreNetCore_VerifyBuildCrossTargeting_VerifyImportOrderAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                var packageZ = new SimpleTestPackageContext()
                {
                    Id = "z",
                    Version = "1.0.0"
                };

                var packageS = new SimpleTestPackageContext()
                {
                    Id = "s",
                    Version = "1.0.0"
                };

                packageX.AddFile("buildCrossTargeting/x.targets");
                packageZ.AddFile("buildCrossTargeting/z.targets");
                packageS.AddFile("buildCrossTargeting/s.targets");

                packageX.AddFile("buildCrossTargeting/x.props");
                packageZ.AddFile("buildCrossTargeting/z.props");
                packageS.AddFile("buildCrossTargeting/s.props");

                packageX.AddFile("build/x.targets");
                packageZ.AddFile("build/z.targets");
                packageS.AddFile("build/s.targets");

                packageX.AddFile("build/x.props");
                packageZ.AddFile("build/z.props");
                packageS.AddFile("build/s.props");

                packageX.AddFile("lib/net45/test.dll");
                packageZ.AddFile("lib/net45/test.dll");
                packageS.AddFile("lib/net45/test.dll");

                // To avoid sorting on case accidently
                // A -> X -> Z -> S
                packageX.Dependencies.Add(packageZ);
                packageZ.Dependencies.Add(packageS);

                projectA.AddPackageToAllFrameworks(packageX);
                projectA.AddPackageToAllFrameworks(packageZ);
                projectA.AddPackageToAllFrameworks(packageS);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                Assert.True(File.Exists(projectA.TargetsOutput), r.Item2);

                var targetsXML = XDocument.Parse(File.ReadAllText(projectA.TargetsOutput));
                var targetItemGroups = targetsXML.Root.Elements().Where(e => e.Name.LocalName == "ImportGroup").ToList();

                var propsXML = XDocument.Parse(File.ReadAllText(projectA.PropsOutput));
                var propsItemGroups = propsXML.Root.Elements().Where(e => e.Name.LocalName == "ImportGroup").ToList();

                // CrossTargeting should be first
                Assert.Equal(2, targetItemGroups.Count);
                Assert.Equal("'$(TargetFramework)' == '' AND '$(ExcludeRestorePackageImports)' != 'true'", targetItemGroups[0].Attribute(XName.Get("Condition")).Value.Trim());
                Assert.Equal("'$(TargetFramework)' == 'net45' AND '$(ExcludeRestorePackageImports)' != 'true'", targetItemGroups[1].Attribute(XName.Get("Condition")).Value.Trim());

                Assert.Equal(3, targetItemGroups[0].Elements().Count());
                Assert.EndsWith("s.targets", targetItemGroups[0].Elements().ToList()[0].Attribute(XName.Get("Project")).Value);
                Assert.EndsWith("z.targets", targetItemGroups[0].Elements().ToList()[1].Attribute(XName.Get("Project")).Value);
                Assert.EndsWith("x.targets", targetItemGroups[0].Elements().ToList()[2].Attribute(XName.Get("Project")).Value);

                Assert.Equal(3, targetItemGroups[1].Elements().Count());
                Assert.EndsWith("s.targets", targetItemGroups[1].Elements().ToList()[0].Attribute(XName.Get("Project")).Value);
                Assert.EndsWith("z.targets", targetItemGroups[1].Elements().ToList()[1].Attribute(XName.Get("Project")).Value);
                Assert.EndsWith("x.targets", targetItemGroups[1].Elements().ToList()[2].Attribute(XName.Get("Project")).Value);

                Assert.Equal(2, propsItemGroups.Count);
                Assert.Equal("'$(TargetFramework)' == '' AND '$(ExcludeRestorePackageImports)' != 'true'", propsItemGroups[0].Attribute(XName.Get("Condition")).Value.Trim());
                Assert.Equal("'$(TargetFramework)' == 'net45' AND '$(ExcludeRestorePackageImports)' != 'true'", propsItemGroups[1].Attribute(XName.Get("Condition")).Value.Trim());

                Assert.Equal(3, propsItemGroups[0].Elements().Count());
                Assert.EndsWith("s.props", propsItemGroups[0].Elements().ToList()[0].Attribute(XName.Get("Project")).Value);
                Assert.EndsWith("z.props", propsItemGroups[0].Elements().ToList()[1].Attribute(XName.Get("Project")).Value);
                Assert.EndsWith("x.props", propsItemGroups[0].Elements().ToList()[2].Attribute(XName.Get("Project")).Value);

                Assert.Equal(3, propsItemGroups[1].Elements().Count());
                Assert.EndsWith("s.props", propsItemGroups[1].Elements().ToList()[0].Attribute(XName.Get("Project")).Value);
                Assert.EndsWith("z.props", propsItemGroups[1].Elements().ToList()[1].Attribute(XName.Get("Project")).Value);
                Assert.EndsWith("x.props", propsItemGroups[1].Elements().ToList()[2].Attribute(XName.Get("Project")).Value);
            }
        }

        [Fact]
        public async Task RestoreNetCore_VerifyBuildCrossTargeting_VerifyImportIsAddedAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                packageX.AddFile("buildCrossTargeting/x.targets");
                packageX.AddFile("lib/net45/test.dll");

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                Assert.True(File.Exists(projectA.TargetsOutput), r.Item2);

                var targetsXML = XDocument.Parse(File.ReadAllText(projectA.TargetsOutput));
                var targetItemGroups = targetsXML.Root.Elements().Where(e => e.Name.LocalName == "ImportGroup").ToList();

                Assert.Equal(1, targetItemGroups.Count);
                Assert.Equal("'$(TargetFramework)' == '' AND '$(ExcludeRestorePackageImports)' != 'true'", targetItemGroups[0].Attribute(XName.Get("Condition")).Value.Trim());
                Assert.Equal(1, targetItemGroups[0].Elements().Count());
                Assert.EndsWith("x.targets", targetItemGroups[0].Elements().ToList()[0].Attribute(XName.Get("Project")).Value);
            }
        }

        [Fact]
        public async Task RestoreNetCore_VerifyBuildCrossTargeting_VerifyNoDuplicateImportsAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"),
                    NuGetFramework.Parse("net46"));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                packageX.AddFile("buildCrossTargeting/x.targets");
                packageX.AddFile("lib/net45/test.dll");

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                Assert.True(File.Exists(projectA.TargetsOutput), r.Item2);

                var targetsXML = XDocument.Parse(File.ReadAllText(projectA.TargetsOutput));
                var targetItemGroups = targetsXML.Root.Elements().Where(e => e.Name.LocalName == "ImportGroup").ToList();

                Assert.Equal(1, targetItemGroups.Count);
                Assert.Equal("'$(TargetFramework)' == '' AND '$(ExcludeRestorePackageImports)' != 'true'", targetItemGroups[0].Attribute(XName.Get("Condition")).Value.Trim());
                Assert.Equal(1, targetItemGroups[0].Elements().Count());
                Assert.EndsWith("x.targets", targetItemGroups[0].Elements().ToList()[0].Attribute(XName.Get("Project")).Value);
            }
        }

        [Fact]
        public async Task RestoreNetCore_VerifyBuildCrossTargeting_VerifyImportIsNotAddedForUAPAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectJson = JObject.Parse(@"{
                                                    'dependencies': {
                                                    },
                                                    'frameworks': {
                                                        'net45': {
                                                            'x': '1.0.0'
                                                    }
                                                  }
                                               }");

                var projectA = SimpleTestProjectContext.CreateUAP(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"),
                    projectJson);

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                packageX.AddFile("buildCrossTargeting/x.targets");
                packageX.AddFile("lib/net45/test.dll");

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                Assert.False(File.Exists(projectA.TargetsOutput), r.Item2);
            }
        }

        [Fact]
        public async Task RestoreNetCore_VerifyBuildCrossTargeting_VerifyImportRequiresPackageNameAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                packageX.AddFile("buildCrossTargeting/a.targets");
                packageX.AddFile("buildCrossTargeting/a.props");
                packageX.AddFile("buildCrossTargeting/a.txt");
                packageX.AddFile("lib/net45/test.dll");

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Act
                var r = Util.RestoreSolution(pathContext);

                var msbuildTargetsItems = TargetsUtility.GetMSBuildPackageImports(projectA.TargetsOutput);
                var msbuildPropsItems = TargetsUtility.GetMSBuildPackageImports(projectA.PropsOutput);

                // Assert
                Assert.Equal(0, msbuildTargetsItems.Count);
                Assert.Equal(0, msbuildPropsItems.Count);
            }
        }

        [Fact]
        public async Task RestoreNetCore_VerifyBuildCrossTargeting_VerifyImportNotAllowedInSubFolderAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                var packageY = new SimpleTestPackageContext()
                {
                    Id = "y",
                    Version = "1.0.0"
                };

                packageX.AddFile("buildCrossTargeting/net45/x.targets");
                packageX.AddFile("buildCrossTargeting/net45/x.props");
                packageX.AddFile("lib/net45/test.dll");

                packageY.AddFile("buildCrossTargeting/a.targets");
                packageY.AddFile("buildCrossTargeting/a.props");
                packageY.AddFile("buildCrossTargeting/net45/y.targets");
                packageY.AddFile("buildCrossTargeting/net45/y.props");
                packageY.AddFile("lib/net45/test.dll");

                projectA.AddPackageToAllFrameworks(packageX);
                projectA.AddPackageToAllFrameworks(packageY);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX,
                    packageY);

                // Act
                var r = Util.RestoreSolution(pathContext);

                var msbuildTargetsItems = TargetsUtility.GetMSBuildPackageImports(projectA.TargetsOutput);
                var msbuildPropsItems = TargetsUtility.GetMSBuildPackageImports(projectA.PropsOutput);

                // Assert
                Assert.Equal(0, msbuildTargetsItems.Count);
                Assert.Equal(0, msbuildPropsItems.Count);
            }
        }

        [Fact]
        public async Task RestoreNetCore_NETCoreImports_VerifyImportFromPackageIsIgnoredAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                packageX.AddFile("build/x.props", "<Project>This is a bad props file!!!!<");
                packageX.AddFile("build/x.targets", "<Project>This is a bad target file!!!!<");
                packageX.AddFile("lib/net45/test.dll");

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Restore one
                var r = Util.RestoreSolution(pathContext);

                var msbuildTargetsItems = TargetsUtility.GetMSBuildPackageImports(projectA.TargetsOutput);
                var msbuildPropsItems = TargetsUtility.GetMSBuildPackageImports(projectA.PropsOutput);

                // Assert
                Assert.True(File.Exists(projectA.TargetsOutput), r.Item2);
                Assert.Equal(1, msbuildTargetsItems.Count);
                Assert.Equal(1, msbuildPropsItems.Count);


                // Act
                r = Util.RestoreSolution(pathContext);
                Assert.True(File.Exists(projectA.TargetsOutput), r.Item2);

                msbuildTargetsItems = TargetsUtility.GetMSBuildPackageImports(projectA.TargetsOutput);
                msbuildPropsItems = TargetsUtility.GetMSBuildPackageImports(projectA.PropsOutput);

                Assert.Equal(1, msbuildTargetsItems.Count);
                Assert.Equal(1, msbuildPropsItems.Count);

                Assert.True(r.Item1 == 0);
            }
        }

        [Fact]
        public async Task RestoreNetCore_UAPImports_VerifyImportFromPackageIsIgnoredAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectJson = JObject.Parse(@"{
                                                    'dependencies': {
                                                        'x': '1.0.0'
                                                    },
                                                    'frameworks': {
                                                        'net45': {
                                                    }
                                                  }
                                               }");

                var projectA = SimpleTestProjectContext.CreateUAP(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"),
                    projectJson);

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                packageX.AddFile("build/x.props", "<Project>This is a bad props file!!!!<");
                packageX.AddFile("build/x.targets", "<Project>This is a bad target file!!!!<");
                packageX.AddFile("lib/net45/test.dll");

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Restore one
                var r = Util.RestoreSolution(pathContext);
                Assert.True(File.Exists(projectA.TargetsOutput), r.Item2);

                // Act
                r = Util.RestoreSolution(pathContext);
                Assert.True(r.Item1 == 0);
                Assert.True(File.Exists(projectA.TargetsOutput), r.Item2);
            }
        }

        [Fact]
        public async Task RestoreNetCore_ProjectToProject_InterweavingAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                //  A         B        C          D         E       F          G
                // NETCore -> UAP -> Unknown -> NETCore -> UAP -> Unknown -> NETCore

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var projectJson = JObject.Parse(@"{
                                                    'dependencies': {
                                                    },
                                                    'frameworks': {
                                                        'net45': { }
                                                  }
                                               }");

                var projectB = SimpleTestProjectContext.CreateUAP(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"),
                    projectJson);

                var projectC = SimpleTestProjectContext.CreateNonNuGet(
                    "c",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var projectD = SimpleTestProjectContext.CreateNETCore(
                    "d",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var projectE = SimpleTestProjectContext.CreateUAP(
                    "e",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"),
                    projectJson);

                var projectF = SimpleTestProjectContext.CreateNonNuGet(
                    "f",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var projectG = SimpleTestProjectContext.CreateNETCore(
                    "g",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                // Link everything
                projectA.AddProjectToAllFrameworks(projectB);
                projectB.AddProjectToAllFrameworks(projectC);
                projectC.AddProjectToAllFrameworks(projectD);
                projectD.AddProjectToAllFrameworks(projectE);
                projectE.AddProjectToAllFrameworks(projectF);
                projectF.AddProjectToAllFrameworks(projectG);

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0-beta"
                };

                // G -> X
                projectG.AddPackageToAllFrameworks(packageX);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);
                solution.Projects.Add(projectC);
                solution.Projects.Add(projectD);
                solution.Projects.Add(projectE);
                solution.Projects.Add(projectF);
                solution.Projects.Add(projectG);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                var targets = projectA.AssetsFile.Targets.Single(target => string.IsNullOrEmpty(target.RuntimeIdentifier)).Libraries.ToDictionary(e => e.Name);
                var libs = projectA.AssetsFile.Libraries.ToDictionary(e => e.Name);

                // Verify everything showed up
                Assert.Equal(new[] { "b", "c", "d", "e", "f", "g", "x" }, libs.Keys.OrderBy(s => s, StringComparer.OrdinalIgnoreCase));

                Assert.Equal("1.0.0-beta", targets["x"].Version.ToNormalizedString());
                Assert.Equal("package", targets["x"].Type);

                Assert.Equal("1.0.0", targets["g"].Version.ToNormalizedString());
                Assert.Equal("project", targets["g"].Type);
                Assert.Equal(".NETFramework,Version=v4.5", targets["g"].Framework);

                Assert.Equal("1.0.0", libs["g"].Version.ToNormalizedString());
                Assert.Equal("project", libs["g"].Type);
                Assert.Equal("../g/g.csproj", libs["g"].MSBuildProject);
                Assert.Equal("../g/g.csproj", libs["g"].Path);
            }
        }

        [Fact]
        public void RestoreNetCore_ProjectToProject_NETCoreToUnknown()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var projectB = SimpleTestProjectContext.CreateNonNuGet(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                // A -> B
                projectA.AddProjectToAllFrameworks(projectB);

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                var targetB = projectA.AssetsFile.Targets.Single(target => string.IsNullOrEmpty(target.RuntimeIdentifier)).Libraries.SingleOrDefault(e => e.Name == "b");
                var libB = projectA.AssetsFile.Libraries.SingleOrDefault(e => e.Name == "b");

                Assert.Equal("1.0.0", targetB.Version.ToNormalizedString());
                Assert.Equal("project", targetB.Type);

                // This is not populated for unknown project types, but this may change in the future.
                Assert.Null(targetB.Framework);
                Assert.Equal("../b/b.csproj", libB.Path);

                Assert.Equal("1.0.0", libB.Version.ToNormalizedString());
                Assert.Equal("project", libB.Type);
                Assert.Equal("../b/b.csproj", libB.MSBuildProject);
            }
        }

        [Fact]
        public void RestoreNetCore_ProjectToProject_NETCoreToUAP()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var projectJson = JObject.Parse(@"{
                                                    'dependencies': {
                                                    },
                                                    'frameworks': {
                                                        'net45': { }
                                                  }
                                               }");

                var projectB = SimpleTestProjectContext.CreateUAP(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"),
                    projectJson);

                // A -> B
                projectA.AddProjectToAllFrameworks(projectB);

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                var targetB = projectA.AssetsFile.Targets.Single(target => string.IsNullOrEmpty(target.RuntimeIdentifier)).Libraries.SingleOrDefault(e => e.Name == "b");
                var libB = projectA.AssetsFile.Libraries.SingleOrDefault(e => e.Name == "b");

                Assert.Equal("1.0.0", targetB.Version.ToNormalizedString());
                Assert.Equal("project", targetB.Type);
                Assert.Equal(".NETFramework,Version=v4.5", targetB.Framework);

                Assert.Equal("1.0.0", libB.Version.ToNormalizedString());
                Assert.Equal("project", libB.Type);
                Assert.Equal("../b/b.csproj", libB.MSBuildProject);
                Assert.Equal("../b/project.json", libB.Path); // TODO: is this right?
            }
        }

        [Fact]
        public async Task RestoreNetCore_ProjectToProject_UAPToNetCoreAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectJson = JObject.Parse(@"{
                                                    'dependencies': {
                                                    },
                                                    'frameworks': {
                                                        'UAP10.0': { }
                                                  }
                                               }");

                var projectA = SimpleTestProjectContext.CreateUAP(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("UAP10.0"),
                    projectJson);

                var projectB = SimpleTestProjectContext.CreateNETCore(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netstandard1.3"),
                    NuGetFramework.Parse("net45"));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                var packageY = new SimpleTestPackageContext()
                {
                    Id = "y",
                    Version = "1.0.0"
                };

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX,
                    packageY);

                projectB.Frameworks[0].PackageReferences.Add(packageX);
                projectB.Frameworks[1].PackageReferences.Add(packageY);

                // A -> B
                projectA.AddProjectToAllFrameworks(projectB);

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                var tfm = NuGetFramework.Parse("UAP10.0");
                var target = projectA.AssetsFile.GetTarget(tfm, runtimeIdentifier: null);
                var targetB = target.Libraries.SingleOrDefault(e => e.Name == "b");
                var libB = projectA.AssetsFile.Libraries.SingleOrDefault(e => e.Name == "b");

                var targetX = projectA.AssetsFile.Targets.Single(t => string.IsNullOrEmpty(t.RuntimeIdentifier)).Libraries.SingleOrDefault(e => e.Name == "x");
                var targetY = projectA.AssetsFile.Targets.Single(t => string.IsNullOrEmpty(t.RuntimeIdentifier)).Libraries.SingleOrDefault(e => e.Name == "y");

                Assert.Equal("1.0.0", targetB.Version.ToNormalizedString());
                Assert.Equal("project", targetB.Type);
                Assert.Equal(".NETStandard,Version=v1.3", targetB.Framework);

                Assert.Equal("1.0.0", libB.Version.ToNormalizedString());
                Assert.Equal("project", libB.Type);
                Assert.Equal("../b/b.csproj", libB.MSBuildProject);
                Assert.Equal("../b/b.csproj", libB.Path);

                Assert.Equal("1.0.0", targetX.Version.ToNormalizedString());
                Assert.Null(targetY);
            }
        }

        [Fact]
        public void RestoreNetCore_ProjectToProject_UAPToUnknown()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectJson = JObject.Parse(@"{
                                                    'dependencies': {
                                                    },
                                                    'frameworks': {
                                                        'UAP10.0': { }
                                                  }
                                               }");

                var projectA = SimpleTestProjectContext.CreateUAP(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("UAP10.0"),
                    projectJson);

                var projectB = SimpleTestProjectContext.CreateNonNuGet(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netstandard1.3"));

                // A -> B
                projectA.AddProjectToAllFrameworks(projectB);

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                var targetB = projectA.AssetsFile.Targets.Single(e => e.TargetFramework.Equals(NuGetFramework.Parse("UAP10.0"))).Libraries.SingleOrDefault(e => e.Name == "b");
                var libB = projectA.AssetsFile.Libraries.SingleOrDefault(e => e.Name == "b");

                Assert.Equal("1.0.0", targetB.Version.ToNormalizedString());
                Assert.Equal("project", targetB.Type);
                Assert.Null(targetB.Framework);

                Assert.Equal("1.0.0", libB.Version.ToNormalizedString());
                Assert.Equal("project", libB.Type);
                Assert.Equal("../b/b.csproj", libB.MSBuildProject);
                Assert.Equal("../b/b.csproj", libB.Path);
            }
        }

        [Fact]
        public void RestoreNetCore_ProjectToProject_UAPToUAP_RestoreCSProjDirect()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectJsonA = JObject.Parse(@"{
                                                    'dependencies': {
                                                    },
                                                    'frameworks': {
                                                        'NETCoreApp1.0': { }
                                                  }
                                               }");

                var projectA = SimpleTestProjectContext.CreateUAP(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("NETCoreApp1.0"),
                    projectJsonA);

                var projectJsonB = JObject.Parse(@"{
                                                    'dependencies': {
                                                    },
                                                    'frameworks': {
                                                        'netstandard1.5': { }
                                                  }
                                               }");

                var projectB = SimpleTestProjectContext.CreateUAP(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("NETCoreApp1.0"),
                    projectJsonB);

                // A -> B
                projectA.AddProjectToAllFrameworks(projectB);

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var nugetexe = Util.GetNuGetExePath();

                var args = new string[] {
                    "restore",
                    projectA.ProjectPath,
                    "-Verbosity",
                    "detailed"
                };

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    pathContext.WorkingDirectory.Path,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);

                // Assert
                var targetB = projectA.AssetsFile.Targets.Single(e => e.TargetFramework.Equals(NuGetFramework.Parse("NETCoreApp1.0"))).Libraries.SingleOrDefault(e => e.Name == "b");
                var libB = projectA.AssetsFile.Libraries.SingleOrDefault(e => e.Name == "b");

                Assert.Equal("1.0.0", targetB.Version.ToNormalizedString());
                Assert.Equal("project", targetB.Type);
                Assert.Equal(NuGetFramework.Parse("netstandard1.5"), NuGetFramework.Parse(targetB.Framework));

                Assert.Equal("1.0.0", libB.Version.ToNormalizedString());
                Assert.Equal("project", libB.Type);
                Assert.Equal("../b/b.csproj", libB.MSBuildProject);
                Assert.Equal("../b/project.json", libB.Path);
            }
        }

        [Fact]
        public void RestoreNetCore_ProjectToProject_NETCore_TransitiveForAllEdges()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net462"));

                var projectB = SimpleTestProjectContext.CreateNETCore(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net462"));

                var projectJson = JObject.Parse(@"{
                                                    'dependencies': {
                                                    },
                                                    'frameworks': {
                                                        'net462': { }
                                                  }
                                               }");

                var projectC = SimpleTestProjectContext.CreateUAP(
                    "c",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net462"),
                    projectJson);

                var projectD = SimpleTestProjectContext.CreateNonNuGet(
                    "d",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net462"));

                var projectE = SimpleTestProjectContext.CreateNETCore(
                    "e",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net462"));

                // Straight line
                projectA.AddProjectToAllFrameworks(projectB);
                projectB.AddProjectToAllFrameworks(projectC);
                projectC.AddProjectToAllFrameworks(projectD);
                projectD.AddProjectToAllFrameworks(projectE);

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);
                solution.Projects.Add(projectC);
                solution.Projects.Add(projectD);
                solution.Projects.Add(projectE);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var nugetexe = Util.GetNuGetExePath();

                var args = new string[] {
                    "restore",
                    projectA.ProjectPath,
                    "-Verbosity",
                    "detailed"
                };

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    pathContext.WorkingDirectory.Path,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);

                // Assert
                var assetsFile = projectA.AssetsFile;

                // Find all non _._ compile assets
                var flowingCompile = assetsFile.Targets.Single(target => string.IsNullOrEmpty(target.RuntimeIdentifier)).Libraries
                    .Where(e => e.Type == "project")
                    .Where(e => e.CompileTimeAssemblies.Where(f => !f.Path.EndsWith("_._")).Any())
                    .Select(e => e.Name)
                    .OrderBy(s => s, StringComparer.OrdinalIgnoreCase);

                Assert.Equal("bcde", string.Join("", flowingCompile));

                // Runtime should always flow
                var flowingRuntime = assetsFile.Targets.Single(target => string.IsNullOrEmpty(target.RuntimeIdentifier)).Libraries
                    .Where(e => e.Type == "project")
                    .Where(e => e.RuntimeAssemblies.Where(f => !f.Path.EndsWith("_._")).Any())
                    .Select(e => e.Name)
                    .OrderBy(s => s, StringComparer.OrdinalIgnoreCase);

                Assert.Equal("bcde", string.Join("", flowingRuntime));
            }
        }

        [Fact]
        public void RestoreNetCore_ProjectToProject_NETCore_TransitiveOffForAllEdges()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net462"));
                projectA.PrivateAssets = "compile";

                var projectB = SimpleTestProjectContext.CreateNETCore(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net462"));
                projectB.PrivateAssets = "compile";

                var projectJson = JObject.Parse(@"{
                                                    'dependencies': {
                                                    },
                                                    'frameworks': {
                                                        'net462': { }
                                                  }
                                               }");

                var projectC = SimpleTestProjectContext.CreateUAP(
                    "c",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net462"),
                    projectJson);
                projectC.PrivateAssets = "compile";

                var projectD = SimpleTestProjectContext.CreateNonNuGet(
                    "d",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net462"));
                projectD.PrivateAssets = "compile";

                var projectE = SimpleTestProjectContext.CreateNETCore(
                    "e",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net462"));
                projectE.PrivateAssets = "compile";

                // Straight line
                projectA.AddProjectToAllFrameworks(projectB);
                projectB.AddProjectToAllFrameworks(projectC);
                projectC.AddProjectToAllFrameworks(projectD);
                projectD.AddProjectToAllFrameworks(projectE);

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);
                solution.Projects.Add(projectC);
                solution.Projects.Add(projectD);
                solution.Projects.Add(projectE);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var nugetexe = Util.GetNuGetExePath();

                var args = new string[] {
                    "restore",
                    projectA.ProjectPath,
                    "-Verbosity",
                    "detailed"
                };

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    pathContext.WorkingDirectory.Path,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);

                // Assert
                var assetsFile = projectA.AssetsFile;

                // Find all non _._ compile assets
                var flowingCompile = assetsFile.Targets.Single(e => string.IsNullOrEmpty(e.RuntimeIdentifier)).Libraries
                    .Where(e => e.Type == "project")
                    .Where(e => e.CompileTimeAssemblies.Where(f => !f.Path.EndsWith("_._")).Any())
                    .Select(e => e.Name)
                    .OrderBy(s => s, StringComparer.OrdinalIgnoreCase);

                Assert.Equal("b", string.Join("", flowingCompile));

                // Runtime should always flow
                var flowingRuntime = assetsFile.Targets.Single(e => string.IsNullOrEmpty(e.RuntimeIdentifier)).Libraries
                    .Where(e => e.Type == "project")
                    .Where(e => e.RuntimeAssemblies.Where(f => !f.Path.EndsWith("_._")).Any())
                    .Select(e => e.Name)
                    .OrderBy(s => s, StringComparer.OrdinalIgnoreCase);

                Assert.Equal("bcde", string.Join("", flowingRuntime));
            }
        }

        [Fact]
        public void RestoreNetCore_ProjectToProject_NETCoreToNETCore_RestoreCSProjDirect()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("NETCoreApp1.0"));

                var projectB = SimpleTestProjectContext.CreateNETCore(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("NETStandard1.5"));

                // A -> B
                projectA.AddProjectToAllFrameworks(projectB);

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var nugetexe = Util.GetNuGetExePath();

                var args = new string[] {
                    "restore",
                    projectA.ProjectPath,
                    "-Verbosity",
                    "detailed"
                };

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    pathContext.WorkingDirectory.Path,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);

                // Assert
                var targetB = projectA.AssetsFile.Targets.Single(e => e.TargetFramework.Equals(NuGetFramework.Parse("NETCoreApp1.0")) && string.IsNullOrEmpty(e.RuntimeIdentifier)).Libraries.SingleOrDefault(e => e.Name == "b");
                var libB = projectA.AssetsFile.Libraries.SingleOrDefault(e => e.Name == "b");

                Assert.Equal("1.0.0", targetB.Version.ToNormalizedString());
                Assert.Equal("project", targetB.Type);
                Assert.Equal(NuGetFramework.Parse("netstandard1.5"), NuGetFramework.Parse(targetB.Framework));

                Assert.Equal("1.0.0", libB.Version.ToNormalizedString());
                Assert.Equal("project", libB.Type);
                Assert.Equal("../b/b.csproj", libB.MSBuildProject);
                Assert.Equal("../b/b.csproj", libB.Path);
            }
        }

        [Fact]
        public void RestoreNetCore_ProjectToProject_NETCoreToNETCore_VerifyVersionForDependency()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("NETCoreApp1.0"));

                var projectB = SimpleTestProjectContext.CreateNETCore(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("NETStandard1.5"));

                var projectC = SimpleTestProjectContext.CreateNETCore(
                    "c",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("NETStandard1.5"));

                var projectD = SimpleTestProjectContext.CreateNETCore(
                    "d",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("NETStandard1.5"));

                // A -> B
                projectA.AddProjectToAllFrameworks(projectB);

                // A -> C
                projectA.AddProjectToAllFrameworks(projectC);

                // C -> D
                projectC.AddProjectToAllFrameworks(projectD);

                // Set project versions
                projectB.Version = "2.4.5-alpha.1.2+build.a.b.c";
                projectC.Version = "2.4.5-ignorethis";
                projectD.Version = "1.4.9-child.project";

                // Override with PackageVersion
                projectC.Properties.Add("PackageVersion", "2.4.5-alpha.7+use.this");

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);
                solution.Projects.Add(projectC);
                solution.Projects.Add(projectD);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var nugetexe = Util.GetNuGetExePath();

                var args = new string[] {
                    "restore",
                    projectA.ProjectPath,
                    "-Verbosity",
                    "detailed"
                };

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    pathContext.WorkingDirectory.Path,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);

                // Assert
                var targetB = projectA.AssetsFile.Targets.Single(e => e.TargetFramework.Equals(NuGetFramework.Parse("NETCoreApp1.0")) && string.IsNullOrEmpty(e.RuntimeIdentifier)).Libraries.SingleOrDefault(e => e.Name == "b");
                var libB = projectA.AssetsFile.Libraries.SingleOrDefault(e => e.Name == "b");

                var targetC = projectA.AssetsFile.Targets.Single(e => e.TargetFramework.Equals(NuGetFramework.Parse("NETCoreApp1.0")) && string.IsNullOrEmpty(e.RuntimeIdentifier)).Libraries.SingleOrDefault(e => e.Name == "c");
                var libC = projectA.AssetsFile.Libraries.SingleOrDefault(e => e.Name == "c");

                var dDep = targetC.Dependencies.Single();

                Assert.Equal("project", targetB.Type);
                Assert.Equal("2.4.5-alpha.1.2", targetB.Version.ToNormalizedString());
                Assert.Equal("2.4.5-alpha.1.2", libB.Version.ToNormalizedString());

                Assert.Equal("project", targetC.Type);
                Assert.Equal("2.4.5-alpha.7", targetC.Version.ToNormalizedString());
                Assert.Equal("2.4.5-alpha.7", libC.Version.ToNormalizedString());

                // Verify the correct version of D is shown under project C
                Assert.Equal("[1.4.9-child.project, )", dDep.VersionRange.ToNormalizedString());
            }
        }

        [Fact]
        public void RestoreNetCore_ProjectToProject_NETCoreToNETCore_VerifyVersionForDependency_WithSnapshotsFails()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("NETCoreApp1.0"));

                var projectB = SimpleTestProjectContext.CreateNETCore(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("NETStandard1.5"));

                var projectC = SimpleTestProjectContext.CreateNETCore(
                    "c",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("NETStandard1.5"));

                var projectD = SimpleTestProjectContext.CreateNETCore(
                    "d",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("NETStandard1.5"));

                // A -> B
                projectA.AddProjectToAllFrameworks(projectB);

                // A -> C
                projectA.AddProjectToAllFrameworks(projectC);

                // C -> D
                projectC.AddProjectToAllFrameworks(projectD);

                // Set project versions
                projectA.Version = "2.0.0-a.*";
                projectB.Version = "2.0.0-b.*";
                projectC.Version = "2.0.0-c.*";
                projectD.Version = "2.0.0-d.*";

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);
                solution.Projects.Add(projectC);
                solution.Projects.Add(projectD);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var nugetexe = Util.GetNuGetExePath();

                var args = new string[] {
                    "restore",
                    projectA.ProjectPath,
                    "-Verbosity",
                    "detailed"
                };

                // Act
                var r = CommandRunner.Run(
                    nugetexe,
                    pathContext.WorkingDirectory.Path,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Assert.False(0 == r.Item1, r.Item2 + " " + r.Item3);
            }
        }

        [Fact]
        public async Task RestoreNetCore_VerifyPropsAndTargetsAreWrittenWhenRestoreFailsAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                packageX.AddFile("build/net45/x.targets");

                var packageY = new SimpleTestPackageContext("y");
                packageX.Dependencies.Add(packageY);

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                var yPath = await SimpleTestPackageUtility.CreateFullPackageAsync(pathContext.PackageSource, packageY);
                await SimpleTestPackageUtility.CreateFullPackageAsync(pathContext.PackageSource, packageX);

                // y does not exist
                yPath.Delete();

                // Act
                // Verify failure
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 1);

                var targets = TargetsUtility.GetMSBuildPackageImports(projectA.TargetsOutput);

                // Assert
                // Verify all files were written
                Assert.True(File.Exists(projectA.AssetsFileOutputPath), r.Item2);
                Assert.True(File.Exists(projectA.TargetsOutput), r.Item2);
                Assert.True(File.Exists(projectA.PropsOutput), r.Item2);
                Assert.Equal(1, targets.Count);
            }
        }

        [Fact]
        public async Task RestoreNetCore_SingleProjectAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                Assert.True(File.Exists(projectA.AssetsFileOutputPath), r.Item2);
                Assert.True(File.Exists(projectA.TargetsOutput), r.Item2);
                Assert.True(File.Exists(projectA.PropsOutput), r.Item2);
                r.AllOutput.Should().NotContain("NU1503");
            }
        }

        [Fact]
        public async Task RestoreNetCore_SingleProjectWithPackageTargetFallbackAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                packageX.AddFile("lib/dnxcore50/a.dll");

                projectA.AddPackageToAllFrameworks(packageX);

                // Add imports property
                projectA.Properties.Add("PackageTargetFallback", "portable-net45+win8;dnxcore50");

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Act
                var r = Util.RestoreSolution(pathContext);
                var xTarget = projectA.AssetsFile.Targets.Single(target => string.IsNullOrEmpty(target.RuntimeIdentifier)).Libraries.Single();

                // Assert
                Assert.Equal("lib/dnxcore50/a.dll", xTarget.CompileTimeAssemblies.Single());
            }
        }

        [Fact]
        public async Task RestoreNetCore_SingleProjectWithPackageTargetFallbackAndWhitespaceAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                packageX.AddFile("lib/dnxcore50/a.dll");

                projectA.AddPackageToAllFrameworks(packageX);

                // Add imports property with whitespace
                projectA.Properties.Add("PackageTargetFallback", "\n\t   portable-net45+win8 ; ; dnxcore50\n   ");

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Act
                var r = Util.RestoreSolution(pathContext);
                var xTarget = projectA.AssetsFile.Targets.Single(target => string.IsNullOrEmpty(target.RuntimeIdentifier)).Libraries.Single();

                // Assert
                Assert.Equal("lib/dnxcore50/a.dll", xTarget.CompileTimeAssemblies.Single());
            }
        }

        [Fact]
        public async Task RestoreNetCore_SingleProject_SingleTFMAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                var xml = File.ReadAllText(projectA.ProjectPath);
                xml = xml.Replace("<TargetFrameworks>", "<TargetFramework>");
                xml = xml.Replace("</TargetFrameworks>", "</TargetFramework>");
                File.WriteAllText(projectA.ProjectPath, xml);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                Assert.True(File.Exists(projectA.AssetsFileOutputPath), r.Item2);
                Assert.True(File.Exists(projectA.TargetsOutput), r.Item2);
                Assert.True(File.Exists(projectA.PropsOutput), r.Item2);

                Assert.Equal(NuGetFramework.Parse("net45"), projectA.AssetsFile.Targets.Single(e => string.IsNullOrEmpty(e.RuntimeIdentifier)).TargetFramework);
            }
        }

        [Fact]
        public void RestoreNetCore_SingleProject_NonNuGet()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNonNuGet(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                // Act && Assert
                // Verify this is a noop and not a failure
                var r = Util.RestoreSolution(pathContext);
            }
        }

        [Fact]
        public void RestoreNetCore_NETCore_ProjectToProject_VerifyProjectInTarget()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var projectB = SimpleTestProjectContext.CreateNETCore(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                // A -> B
                projectA.AddProjectToAllFrameworks(projectB);

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                var targetB = projectA.AssetsFile.Targets.Single(target => string.IsNullOrEmpty(target.RuntimeIdentifier)).Libraries.SingleOrDefault(e => e.Name == "b");
                var libB = projectA.AssetsFile.Libraries.SingleOrDefault(e => e.Name == "b");

                Assert.Equal("1.0.0", targetB.Version.ToNormalizedString());
                Assert.Equal("project", targetB.Type);
                Assert.Equal(".NETFramework,Version=v4.5", targetB.Framework);

                Assert.Equal("1.0.0", libB.Version.ToNormalizedString());
                Assert.Equal("project", libB.Type);
                Assert.Equal("../b/b.csproj", libB.Path);
                Assert.Equal("../b/b.csproj", libB.MSBuildProject);
            }
        }

        [Fact]
        public void RestoreNetCore_NETCore_ProjectToProject_VerifyPackageIdUsed()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var projectB = SimpleTestProjectContext.CreateNETCore(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                // A -> B
                projectA.AddProjectToAllFrameworks(projectB);

                // Add package ids
                projectA.Properties.Add("PackageId", "x");
                projectB.Properties.Add("PackageId", "y");

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                var targetB = projectA.AssetsFile
                    .GetTarget(NuGetFramework.Parse("net45"), runtimeIdentifier: null)
                    .Libraries
                    .SingleOrDefault(e => e.Name == "y");

                var libB = projectA.AssetsFile.Libraries.SingleOrDefault(e => e.Name == "y");

                Assert.Equal("1.0.0", targetB.Version.ToNormalizedString());
                Assert.Equal("project", targetB.Type);
                Assert.Equal(".NETFramework,Version=v4.5", targetB.Framework);

                Assert.Equal("1.0.0", libB.Version.ToNormalizedString());
                Assert.Equal("project", libB.Type);
                Assert.Equal("y", libB.Name);
                Assert.Equal("../b/b.csproj", libB.Path);
                Assert.Equal("../b/b.csproj", libB.MSBuildProject);

                // Verify project name is used
                var group = projectA.AssetsFile.ProjectFileDependencyGroups.ToArray()[0];
                Assert.Equal("y >= 1.0.0", group.Dependencies.Single());
            }
        }

        [Fact]
        public void RestoreNetCore_NETCore_ProjectToProject_IgnoreMissingProjectReference()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var projectB = SimpleTestProjectContext.CreateNETCore(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                // A -> B
                projectA.AddProjectToAllFrameworks(projectB);

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);
                solution.Create(pathContext.SolutionRoot);

                // Delete B
                File.Delete(projectB.ProjectPath);

                // Act && Assert
                // Missing projects are ignored during restore. These issues are reported at build time.
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 0);
            }
        }

        [Fact]
        public async Task RestoreNetCore_NETCore_ProjectToProject_VerifyTransitivePackageAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var projectB = SimpleTestProjectContext.CreateNETCore(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                // A -> B
                projectA.AddProjectToAllFrameworks(projectB);

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                // B -> X
                projectB.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                var targetX = projectA.AssetsFile.Targets.Single(target => string.IsNullOrEmpty(target.RuntimeIdentifier)).Libraries.SingleOrDefault(e => e.Name == "x");

                Assert.Equal("1.0.0", targetX.Version.ToNormalizedString());
            }
        }

        [Fact]
        public async Task RestoreNetCore_NETCore_ProjectToProjectMultipleTFM_VerifyTransitivePackagesAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net46"),
                    NuGetFramework.Parse("netstandard1.6"));

                var projectB = SimpleTestProjectContext.CreateNETCore(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"),
                    NuGetFramework.Parse("netstandard1.3"));

                // A -> B
                projectA.AddProjectToAllFrameworks(projectB);

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                var packageY = new SimpleTestPackageContext()
                {
                    Id = "y",
                    Version = "1.0.0"
                };

                // B -> X
                projectB.Frameworks[0].PackageReferences.Add(packageX);

                // B -> Y
                projectB.Frameworks[1].PackageReferences.Add(packageY);

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX,
                    packageY);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                var targetNet = projectA.AssetsFile.Targets.Single(e => e.TargetFramework.Equals(NuGetFramework.Parse("net46")) && string.IsNullOrEmpty(e.RuntimeIdentifier));
                var targetNS = projectA.AssetsFile.Targets.Single(e => e.TargetFramework.Equals(NuGetFramework.Parse("netstandard1.6")) && string.IsNullOrEmpty(e.RuntimeIdentifier));

                Assert.Equal("x", targetNet.Libraries.Single(e => e.Type == "package").Name);
                Assert.Equal("y", targetNS.Libraries.Single(e => e.Type == "package").Name);
            }
        }

        [Fact]
        public async Task RestoreNetCore_NETCoreAndUAP_ProjectToProjectMultipleTFM_VerifyTransitivePackagesAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net46"),
                    NuGetFramework.Parse("netstandard1.6"));

                var projectJson = JObject.Parse(@"{
                                                    'dependencies': {
                                                        'x': '1.0.0'
                                                    },
                                                    'frameworks': {
                                                        'netstandard1.3': { }
                                                  }
                                               }");

                var projectB = SimpleTestProjectContext.CreateUAP(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("netstandard1.3"),
                    projectJson);

                // A -> B
                projectA.AddProjectToAllFrameworks(projectB);

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                // B -> X
                projectB.Frameworks[0].PackageReferences.Add(packageX);

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                var targetNet = projectA.AssetsFile.Targets.Single(e => e.TargetFramework.Equals(NuGetFramework.Parse("net46")) && string.IsNullOrEmpty(e.RuntimeIdentifier));
                var targetNS = projectA.AssetsFile.Targets.Single(e => e.TargetFramework.Equals(NuGetFramework.Parse("netstandard1.6")) && string.IsNullOrEmpty(e.RuntimeIdentifier));

                Assert.Equal("x", targetNet.Libraries.Single(e => e.Type == "package").Name);
                Assert.Equal("x", targetNS.Libraries.Single(e => e.Type == "package").Name);
            }
        }

        [Fact]
        public async Task RestoreNetCore_LegacyPackagesDirectorySettingsIsIsolatedToProjectAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                projectA.Properties.Add("RestoreLegacyPackagesDirectory", "true");

                var projectB = SimpleTestProjectContext.CreateNETCore(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var projectC = SimpleTestProjectContext.CreateNETCore(
                    "c",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                projectC.Properties.Add("RestoreLegacyPackagesDirectory", "true");

                // A -> B
                projectA.AddProjectToAllFrameworks(projectB);

                // B -> C
                projectB.AddProjectToAllFrameworks(projectC);

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "PackageX",
                    Version = "1.0.0-BETA"
                };

                // C -> X
                projectC.Frameworks[0].PackageReferences.Add(packageX);

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);
                solution.Projects.Add(projectC);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                var xLibraryInA = projectA.AssetsFile.Libraries.Single(x => x.Name == packageX.Id);
                var xLibraryInB = projectB.AssetsFile.Libraries.Single(x => x.Name == packageX.Id);
                var xLibraryInC = projectC.AssetsFile.Libraries.Single(x => x.Name == packageX.Id);
                Assert.Equal("PackageX/1.0.0-BETA", xLibraryInA.Path);
                Assert.Equal("packagex/1.0.0-beta", xLibraryInB.Path);
                Assert.Equal("PackageX/1.0.0-BETA", xLibraryInC.Path);
            }
        }

        [Fact]
        public async Task RestoreNetCore_LegacyPackagesDirectoryEnabledInProjectFileAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "PackageX",
                    Version = "1.0.0-BETA"
                };

                packageX.AddFile("lib/net45/a.dll");

                projectA.AddPackageToAllFrameworks(packageX);

                projectA.Properties.Add("RestoreLegacyPackagesDirectory", "true");

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Act
                var r = Util.RestoreSolution(pathContext);
                var xLibrary = projectA.AssetsFile.Libraries.Single();

                // Assert
                Assert.Equal("PackageX/1.0.0-BETA", xLibrary.Path);
            }
        }

        [Fact]
        public async Task RestoreNetCore_LegacyPackagesDirectoryDisabledInProjectFileAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "PackageX",
                    Version = "1.0.0-BETA"
                };

                packageX.AddFile("lib/net45/a.dll");

                projectA.AddPackageToAllFrameworks(packageX);

                projectA.Properties.Add("RestoreLegacyPackagesDirectory", "false");

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Act
                var r = Util.RestoreSolution(pathContext);
                var xLibrary = projectA.AssetsFile.Libraries.Single();

                // Assert
                Assert.Equal("packagex/1.0.0-beta", xLibrary.Path);
            }
        }

        [Fact]
        public async Task RestoreNetCore_AssetTargetFallbackVerifyFallbackToNet46AssetsAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp2);
                projectA.Properties.Add("AssetTargetFallback", "net461");

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };
                packageX.Files.Clear();
                packageX.AddFile("lib/net45/a.dll");

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Act
                var r = Util.RestoreSolution(pathContext);
                var graph = projectA.AssetsFile.GetTarget(netcoreapp2, runtimeIdentifier: null);
                var lib = graph.GetTargetLibrary("x");

                // Assert
                lib.CompileTimeAssemblies.Select(e => e.Path)
                    .ShouldBeEquivalentTo(new[] { "lib/net45/a.dll" },
                    "no compatible assets were found for ns2.0");

                r.AllOutput.Should().Contain("This package may not be fully compatible with your project.");
            }
        }

        [Fact]
        public async Task RestoreNetCore_AssetTargetFallbackVerifyNoFallbackToNet46AssetsAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp2);
                projectA.Properties.Add("AssetTargetFallback", "net461");
                projectA.Properties.Add("RuntimeIdentifiers", "win10");

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };
                packageX.Files.Clear();
                packageX.AddFile("lib/net461/a.dll");
                packageX.AddFile("ref/netstandard1.0/a.dll");
                packageX.AddFile("ref/net461/a.dll");
                packageX.AddFile("runtimes/win10/native/a.dll");
                packageX.AddFile("runtimes/win10/lib/net461/a.dll");
                packageX.AddFile("build/net461/x.targets");
                packageX.AddFile("buildMultiTargeting/net461/x.targets");
                packageX.AddFile("contentFiles/any/net461/a.txt");

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                foreach (var graph in projectA.AssetsFile.Targets)
                {
                    var lib = graph.GetTargetLibrary("x");

                    lib.CompileTimeAssemblies.Select(e => e.Path)
                        .ShouldBeEquivalentTo(new[] { "ref/netstandard1.0/a.dll" },
                        "ATF does not fallback to lib/net45 if other assets were found.");

                    lib.RuntimeAssemblies.Should().BeEmpty();
                    lib.BuildMultiTargeting.Should().BeEmpty();
                    lib.Build.Should().BeEmpty();
                    lib.ContentFiles.Should().BeEmpty();
                    lib.ResourceAssemblies.Should().BeEmpty();
                    // Native will contain a.dll for RID targets
                }
            }
        }

        [Fact]
        public void RestoreNetCore_AssetTargetFallbackWithProjectReference_VerifyFallbackToNet46Assets()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");
                var ne461 = NuGetFramework.Parse("net461");

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp2);
                projectA.Properties.Add("AssetTargetFallback", "net461");
                projectA.Properties.Add("RuntimeIdentifiers", "win10");

                var projectB = SimpleTestProjectContext.CreateNETCore(
                    "b",
                    pathContext.SolutionRoot,
                    ne461);
                projectA.AddProjectToAllFrameworks(projectB);

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                r.Success.Should().BeTrue();
                foreach (var graph in projectA.AssetsFile.Targets)
                {
                    var lib = graph.GetTargetLibrary("b");

                    lib.CompileTimeAssemblies.Select(e => e.Path)
                        .ShouldBeEquivalentTo(new[] { "bin/placeholder/b.dll" });

                    lib.RuntimeAssemblies.Select(e => e.Path)
                        .ShouldBeEquivalentTo(new[] { "bin/placeholder/b.dll" });

                    lib.BuildMultiTargeting.Should().BeEmpty();
                    lib.Build.Should().BeEmpty();
                    lib.ContentFiles.Should().BeEmpty();
                    lib.ResourceAssemblies.Should().BeEmpty();
                    // Native will contain a.dll for RID targets
                }
            }
        }

        [Fact]
        public void RestoreNetCore_AssetTargetFallbackWithProjectReference_VerifyNoFallbackToNet46Assets()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");
                var ne461 = NuGetFramework.Parse("net461");

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp2);
                projectA.Properties.Add("AssetTargetFallback", "net45");
                projectA.Properties.Add("RuntimeIdentifiers", "win10");

                var projectB = SimpleTestProjectContext.CreateNETCore(
                    "b",
                    pathContext.SolutionRoot,
                    ne461);
                projectA.AddProjectToAllFrameworks(projectB);

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 1);

                // Assert
                r.Success.Should().BeFalse();
                r.AllOutput.Should().Contain("NU1201");
            }
        }

        [Fact]
        public async Task RestoreNetCore_BothAssetTargetFallbackPackageTargetFallbackVerifyErrorAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp2);
                projectA.Properties.Add("AssetTargetFallback", "net461");
                projectA.Properties.Add("PackageTargetFallback", "dnxcore50");

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 1);

                // Assert
                r.AllOutput.Should().Contain("PackageTargetFallback and AssetTargetFallback cannot be used together.");
            }
        }

        [Fact]
        public async Task RestoreNetCore_VerifyAdditionalSourcesAppliedAsync()
        {
            // Arrange
            using (var extraSource = TestDirectory.Create())
            using (var extraFallback = TestDirectory.Create())
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp2);

                projectA.Properties.Add("RestoreAdditionalProjectSources", extraSource.Path);
                projectA.Properties.Add("RestoreAdditionalProjectFallbackFolders", extraFallback.Path);

                var packageM = new SimpleTestPackageContext()
                {
                    Id = "m",
                    Version = "1.0.0"
                };

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                var packageY = new SimpleTestPackageContext()
                {
                    Id = "y",
                    Version = "1.0.0"
                };

                var packageZ = new SimpleTestPackageContext()
                {
                    Id = "z",
                    Version = "1.0.0"
                };

                projectA.AddPackageToAllFrameworks(packageM);
                projectA.AddPackageToAllFrameworks(packageX);
                projectA.AddPackageToAllFrameworks(packageY);
                projectA.AddPackageToAllFrameworks(packageZ);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                // M is only in the fallback folder
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.FallbackFolder,
                    PackageSaveMode.Defaultv3,
                    packageM);

                // X is only in the source
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Y is only in the extra source
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    extraSource,
                    PackageSaveMode.Defaultv3,
                    packageY);

                // Z is only in the extra fallback
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    extraFallback,
                    PackageSaveMode.Defaultv3,
                    packageZ);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                projectA.AssetsFile.Libraries.Select(e => e.Name).OrderBy(e => e).ShouldBeEquivalentTo(new[] { "m", "x", "y", "z" });
            }
        }

        [Fact]
        public async Task RestoreNetCore_VerifyAdditionalSourcesConditionalOnFrameworkAsync()
        {
            // Arrange
            using (var extraSourceA = TestDirectory.Create())
            using (var extraSourceB = TestDirectory.Create())
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp1 = NuGetFramework.Parse("netcoreapp1.0");
                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp1,
                    netcoreapp2);

                // Add conditional sources
                projectA.Frameworks[0].Properties.Add("RestoreAdditionalProjectSources", extraSourceA.Path);
                projectA.Frameworks[1].Properties.Add("RestoreAdditionalProjectSources", extraSourceB.Path);

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                var packageY = new SimpleTestPackageContext()
                {
                    Id = "y",
                    Version = "1.0.0"
                };

                projectA.AddPackageToAllFrameworks(packageX);
                projectA.AddPackageToAllFrameworks(packageY);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                // X is only in the source
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    extraSourceA,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Y is only in the extra source
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    extraSourceB,
                    PackageSaveMode.Defaultv3,
                    packageY);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                projectA.AssetsFile.Libraries.Select(e => e.Name).OrderBy(e => e).ShouldBeEquivalentTo(new[] { "x", "y" });
            }
        }

        [Fact]
        public async Task RestoreNetCore_VerifyAdditionalFallbackFolderConditionalOnFrameworkAsync()
        {
            // Arrange
            using (var extraSourceA = TestDirectory.Create())
            using (var extraSourceB = TestDirectory.Create())
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp1 = NuGetFramework.Parse("netcoreapp1.0");
                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp1,
                    netcoreapp2);

                // Add conditional sources
                projectA.Frameworks[0].Properties.Add("RestoreAdditionalProjectFallbackFolders", extraSourceA.Path);
                projectA.Frameworks[1].Properties.Add("RestoreAdditionalProjectFallbackFolders", extraSourceB.Path);

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                var packageY = new SimpleTestPackageContext()
                {
                    Id = "y",
                    Version = "1.0.0"
                };

                projectA.AddPackageToAllFrameworks(packageX);
                projectA.AddPackageToAllFrameworks(packageY);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                // X is only in the source
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    extraSourceA,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Y is only in the extra source
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    extraSourceB,
                    PackageSaveMode.Defaultv3,
                    packageY);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                projectA.AssetsFile.Libraries.Select(e => e.Name).OrderBy(e => e).ShouldBeEquivalentTo(new[] { "x", "y" });

                // Verify fallback folder added
                projectA.AssetsFile.PackageFolders.Select(e => e.Path).Should().Contain(extraSourceA);
            }
        }

        [Fact]
        public async Task RestoreNetCore_VerifyAdditionalFallbackFolderExcludeAsync()
        {
            // Arrange
            using (var extraSourceA = TestDirectory.Create())
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp1 = NuGetFramework.Parse("netcoreapp1.0");
                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp1,
                    netcoreapp2);

                // Add and remove a fallback source, also add it as a source
                projectA.Frameworks[0].Properties.Add("RestoreAdditionalProjectFallbackFolders", extraSourceA.Path);
                projectA.Frameworks[1].Properties.Add("RestoreAdditionalProjectFallbackFoldersExcludes", extraSourceA.Path);
                projectA.Frameworks[1].Properties.Add("RestoreAdditionalProjectSources", extraSourceA.Path);

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                var packageY = new SimpleTestPackageContext()
                {
                    Id = "y",
                    Version = "1.0.0"
                };

                projectA.AddPackageToAllFrameworks(packageX);
                projectA.AddPackageToAllFrameworks(packageY);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                // X is only in the source
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    extraSourceA,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Y is only in the extra source
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    extraSourceA,
                    PackageSaveMode.Defaultv3,
                    packageY);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                projectA.AssetsFile.Libraries.Select(e => e.Name).OrderBy(e => e).ShouldBeEquivalentTo(new[] { "x", "y" });

                // Verify the fallback folder was not added
                projectA.AssetsFile.PackageFolders.Select(e => e.Path).Should().NotContain(extraSourceA);
            }
        }

        [Fact]
        public async Task RestoreNetCore_VerifyAdditionalSourcesAppliedWithSingleFrameworkAsync()
        {
            // Arrange
            using (var extraSource = TestDirectory.Create())
            using (var extraFallback = TestDirectory.Create())
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp2 = NuGetFramework.Parse("netcoreapp1.0");

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp2);

                projectA.Properties.Add("RestoreAdditionalProjectSources", extraSource.Path);
                projectA.Properties.Add("RestoreAdditionalProjectFallbackFoldersExcludes", extraFallback.Path);

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                // X is only in the source
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    extraSource.Path,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                projectA.AssetsFile.Libraries.Select(e => e.Name).OrderBy(e => e).ShouldBeEquivalentTo(new[] { "x" });

                // Verify the fallback folder was not added
                projectA.AssetsFile.PackageFolders.Select(e => e.Path).Should().NotContain(extraFallback.Path);
            }
        }

        [Fact]
        public async Task RestoreNetCore_VerifyAdditionalFallbackFolderAppliedWithSingleFrameworkAsync()
        {
            // Arrange
            using (var extraSource = TestDirectory.Create())
            using (var extraFallback = TestDirectory.Create())
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp2);

                projectA.Properties.Add("RestoreAdditionalProjectFallbackFolders", extraFallback.Path);

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                // X is only in the source
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    extraFallback.Path,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                projectA.AssetsFile.Libraries.Select(e => e.Name).OrderBy(e => e).ShouldBeEquivalentTo(new[] { "x" });

                // Verify the fallback folder was added
                projectA.AssetsFile.PackageFolders.Select(e => e.Path).Should().Contain(extraFallback.Path);
            }
        }

        [Fact]
        public async Task RestoreNetCore_VerifyPackagesFolderPathResolvedAgainstWorkingDirAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp2);

                projectA.Properties.Add("RestorePackagesPath", "invalid");

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                // X is only in the source
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                var expectedFolder = Path.Combine(pathContext.WorkingDirectory, "pkgs");
                var unexpectedFolder = Path.Combine(Path.GetDirectoryName(projectA.ProjectPath), "invalid");

                // Act
                var r = Util.RestoreSolution(pathContext, 0, "-PackagesDirectory", "pkgs");

                // Assert
                Directory.GetDirectories(expectedFolder).Should().NotBeEmpty();
                Directory.Exists(unexpectedFolder).Should().BeFalse();
                Directory.GetDirectories(pathContext.UserPackagesFolder).Should().BeEmpty();
            }
        }

        [Fact]
        public async Task RestoreNetCore_VerifyAdditionalSourcesAppliedToToolsAsync()
        {
            // Arrange
            using (var extraSource = TestDirectory.Create())
            using (var extraFallback = TestDirectory.Create())
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp2);

                projectA.Properties.Add("RestoreAdditionalProjectSources", extraSource.Path);
                projectA.Properties.Add("RestoreAdditionalProjectFallbackFolders", extraFallback.Path);

                var packageM = new SimpleTestPackageContext()
                {
                    Id = "m",
                    Version = "1.0.0",
                    PackageType = PackageType.DotnetCliTool
                };

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0",
                    PackageType = PackageType.DotnetCliTool
                };

                var packageY = new SimpleTestPackageContext()
                {
                    Id = "y",
                    Version = "1.0.0",
                    PackageType = PackageType.DotnetCliTool
                };

                var packageZ = new SimpleTestPackageContext()
                {
                    Id = "z",
                    Version = "1.0.0",
                    PackageType = PackageType.DotnetCliTool
                };

                projectA.DotnetCLIToolReferences.Add(packageM);
                projectA.DotnetCLIToolReferences.Add(packageX);
                projectA.DotnetCLIToolReferences.Add(packageY);
                projectA.DotnetCLIToolReferences.Add(packageZ);


                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                // M is only in the fallback folder
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.FallbackFolder,
                    PackageSaveMode.Defaultv3,
                    packageM);

                // X is only in the source
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Y is only in the extra source
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    extraSource,
                    PackageSaveMode.Defaultv3,
                    packageY);

                // Z is only in the extra fallback
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    extraFallback,
                    PackageSaveMode.Defaultv3,
                    packageZ);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                Directory.Exists(new ToolPathResolver(pathContext.UserPackagesFolder).GetToolDirectoryPath(packageM.Id, NuGetVersion.Parse(packageM.Version), netcoreapp2));
                Directory.Exists(new ToolPathResolver(pathContext.UserPackagesFolder).GetToolDirectoryPath(packageX.Id, NuGetVersion.Parse(packageX.Version), netcoreapp2));
                Directory.Exists(new ToolPathResolver(pathContext.UserPackagesFolder).GetToolDirectoryPath(packageY.Id, NuGetVersion.Parse(packageY.Version), netcoreapp2));
                Directory.Exists(new ToolPathResolver(pathContext.UserPackagesFolder).GetToolDirectoryPath(packageZ.Id, NuGetVersion.Parse(packageZ.Version), netcoreapp2));

            }
        }

        [Fact]
        public async Task RestoreNetCore_VerifyPackagesFolderPathResolvedAgainstProjectPropertyAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp2);

                projectA.Properties.Add("RestorePackagesPath", "valid");

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                // X is only in the source
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                var expectedFolder = Path.Combine(Path.GetDirectoryName(projectA.ProjectPath), "valid");

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                Directory.GetDirectories(expectedFolder).Should().NotBeEmpty();
                Directory.GetDirectories(pathContext.UserPackagesFolder).Should().BeEmpty();
            }
        }
        // The scenario here is 2 different projects are setting RestoreSources, and the caching of the sources takes this into consideration
        [Fact]
        public async Task RestoreNetCore_VerifySourcesResolvedCorrectlyForMultipleProjectsAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                var packageY = new SimpleTestPackageContext()
                {
                    Id = "y",
                    Version = "1.0.0"
                };

                var source1 = Path.Combine(pathContext.SolutionRoot, "source1");
                var source2 = Path.Combine(pathContext.SolutionRoot, "source2");

                // X is only in source1
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    source1,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Y is only in source2
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    source2,
                    PackageSaveMode.Defaultv3,
                    packageY);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp2);

                var projectB = SimpleTestProjectContext.CreateNETCore(
                    "b",
                    pathContext.SolutionRoot,
                    netcoreapp2);

                projectA.Properties.Add("RestoreSources", source1);
                projectB.Properties.Add("RestoreSources", source2);

                projectA.AddPackageToAllFrameworks(packageX);
                projectB.AddPackageToAllFrameworks(packageY);


                solution.Projects.Add(projectB);
                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                r.Success.Should().BeTrue();
            }
        }

        [Fact]
        public async Task RestoreNetCore_VerifySourcesResolvedAgainstProjectPropertyAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp2);

                projectA.Properties.Add("RestoreSources", "sub");
                var source = Path.Combine(Path.GetDirectoryName(projectA.ProjectPath), "sub");

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                // X is only in the source
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    source,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                r.Success.Should().BeTrue();
            }
        }

        [Fact]
        public async Task RestoreNetCore_VerifySourcesResolvedAgainstWorkingDirAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp2);

                projectA.Properties.Add("RestoreSources", "invalid");

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                var source = Path.Combine(pathContext.WorkingDirectory, "valid");

                // X is only in the source
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    source,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Act
                var r = Util.RestoreSolution(pathContext, 0, "-Source", source);

                // Assert
                r.Success.Should().BeTrue();
            }
        }


        [Fact]
        public async Task RestoreNetCore_VerifyFallbackFoldersResolvedAgainstProjectPropertyAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var netcoreapp2 = NuGetFramework.Parse("netcoreapp2.0");

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    netcoreapp2);

                projectA.Properties.Add("RestoreFallbackFolders", "sub");
                var fallback = Path.Combine(Path.GetDirectoryName(projectA.ProjectPath), "sub");

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                // X is only in the source
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    fallback,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                r.Success.Should().BeTrue();
                Directory.GetDirectories(pathContext.UserPackagesFolder).Should().BeEmpty();
            }
        }

        [Fact]
        public async Task RestoreNetCore_VerifyDisabledSourcesAreNotUsedAsync()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // create a config file, no disabled sources
                var projectDir = Path.GetDirectoryName(projectA.ProjectPath);

                var configPath = Path.Combine(pathContext.SolutionRoot, "NuGet.Config");

                var doc = new XDocument();
                var configuration = new XElement(XName.Get("configuration"));
                doc.Add(configuration);

                var packageSources = new XElement(XName.Get("packageSources"));
                configuration.Add(packageSources);

                packageSources.Add(new XElement(XName.Get("clear")));

                var localSource = new XElement(XName.Get("add"));
                localSource.Add(new XAttribute(XName.Get("key"), "localSource"));
                localSource.Add(new XAttribute(XName.Get("value"), pathContext.PackageSource));
                packageSources.Add(localSource);

                var brokenSource = new XElement(XName.Get("add"));
                brokenSource.Add(new XAttribute(XName.Get("key"), "brokenLocalSource"));
                brokenSource.Add(new XAttribute(XName.Get("value"), pathContext.PackageSource + "brokenLocalSource"));
                packageSources.Add(brokenSource);

                // Disable that config
                var disabledPackageSources = new XElement(XName.Get("disabledPackageSources"));
                var disabledBrokenSource = new XElement(XName.Get("add"));
                disabledBrokenSource.Add(new XAttribute(XName.Get("key"), "brokenLocalSource"));
                disabledBrokenSource.Add(new XAttribute(XName.Get("value"), "true"));
                disabledPackageSources.Add(disabledBrokenSource);

                configuration.Add(disabledPackageSources);
                File.WriteAllText(configPath, doc.ToString());

                // Act 
                var r2 = Util.RestoreSolution(pathContext);

                // Assert
                r2.Success.Should().BeTrue();
                r2.AllOutput.Should().NotContain("brokenLocalSource");

            }
        }


        [Fact]
        public async Task RestoreNetCore_VerifyOrderOfConfigsAsync()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // create a config file, no disabled sources
                var projectDir = Path.GetDirectoryName(projectA.ProjectPath);

                var configPath = Path.Combine(pathContext.SolutionRoot, "NuGet.Config");

                var doc = new XDocument();
                var configuration = new XElement(XName.Get("configuration"));
                doc.Add(configuration);

                var packageSources = new XElement(XName.Get("packageSources"));
                configuration.Add(packageSources);

                packageSources.Add(new XElement(XName.Get("clear")));

                var localSource = new XElement(XName.Get("add"));
                localSource.Add(new XAttribute(XName.Get("key"), "localSource"));
                localSource.Add(new XAttribute(XName.Get("value"), pathContext.PackageSource));
                packageSources.Add(localSource);

                File.WriteAllText(configPath, doc.ToString());

                var solutionParent = Directory.GetParent(pathContext.SolutionRoot);
                var configPath2 = Path.Combine(solutionParent.FullName, "NuGet.Config");

                var doc2 = new XDocument();
                var configuration2 = new XElement(XName.Get("configuration"));
                doc2.Add(configuration2);

                var packageSources2 = new XElement(XName.Get("packageSources"));
                configuration2.Add(packageSources2);

                packageSources2.Add(new XElement(XName.Get("clear")));

                var brokenSource = new XElement(XName.Get("add"));
                brokenSource.Add(new XAttribute(XName.Get("key"), "brokenLocalSource"));
                brokenSource.Add(new XAttribute(XName.Get("value"), pathContext.PackageSource + "brokenLocalSource"));
                packageSources2.Add(brokenSource);

                // Disable that config
                var disabledPackageSources = new XElement(XName.Get("disabledPackageSources"));
                var disabledBrokenSource = new XElement(XName.Get("add"));
                disabledBrokenSource.Add(new XAttribute(XName.Get("key"), "brokenLocalSource"));
                disabledBrokenSource.Add(new XAttribute(XName.Get("value"), "true"));
                disabledPackageSources.Add(disabledBrokenSource);

                configuration2.Add(disabledPackageSources);

                File.WriteAllText(configPath2, doc2.ToString());

                // Act 
                var r2 = Util.RestoreSolution(pathContext);

                // Assert
                r2.Success.Should().BeTrue();

                // Configs closer to the user should be first
                Regex.Replace(r2.AllOutput, @"\s", "").Should().Contain($"NuGetConfigfilesused:{configPath}{configPath2}");
            }
        }

        [Fact]
        public async Task RestoreNetCore_VerifyConfigFileWithRelativePathIsUsedAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                var xml = File.ReadAllText(projectA.ProjectPath);
                xml = xml.Replace("<TargetFrameworks>", "<TargetFramework>");
                xml = xml.Replace("</TargetFrameworks>", "</TargetFramework>");
                File.WriteAllText(projectA.ProjectPath, xml);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                var subDir = Path.Combine(pathContext.SolutionRoot, "sub");
                var configPath = Path.Combine(subDir, "nuget.config");
                Directory.CreateDirectory(subDir);
                File.Move(pathContext.NuGetConfig, configPath);

                var relativePathToConfig = PathUtility.GetRelativePath(pathContext.WorkingDirectory + Path.DirectorySeparatorChar, configPath);

                // Act
                var r = Util.RestoreSolution(pathContext, 0, $"-ConfigFile {relativePathToConfig}");

                // Assert
                Assert.True(File.Exists(projectA.AssetsFileOutputPath), r.Item2);
                Assert.True(File.Exists(projectA.TargetsOutput), r.Item2);
                Assert.True(File.Exists(projectA.PropsOutput), r.Item2);

                Assert.Equal(NuGetFramework.Parse("net45"), projectA.AssetsFile.Targets.Single(e => string.IsNullOrEmpty(e.RuntimeIdentifier)).TargetFramework);
            }
        }

        [Fact]
        public async Task RestoreNetCore_WithMultipleProjectToProjectReferences_NoOpsAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var parentProject = SimpleTestProjectContext.CreateNETCore(
                    "parent",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var one = SimpleTestProjectContext.CreateNETCore(
                    "child1",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var two = SimpleTestProjectContext.CreateNETCore(
                    "child2",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var three = SimpleTestProjectContext.CreateNETCore(
                    "child3",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));
                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX
                    );
                var rnd = new Random();

                var projects = new SimpleTestProjectContext[] { one, two, three }.OrderBy(item => rnd.Next());

                // Parent -> children. Very important that these are added in a random order

                foreach (var project in projects)
                {
                    parentProject.AddProjectToAllFrameworks(project);
                }
                solution.Projects.Add(one);
                solution.Projects.Add(two);
                solution.Projects.Add(three);
                solution.Projects.Add(parentProject);
                solution.Create(pathContext.SolutionRoot);

                // Act && Assert
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 0);

                Assert.Equal(0, r.Item1);
                Assert.Contains("Writing cache file", r.Item2);

                // Do it again, it should no-op now.
                // Act && Assert
                var r2 = Util.RestoreSolution(pathContext, expectedExitCode: 0);

                Assert.Equal(0, r2.Item1);
                Assert.DoesNotContain("Writing cache file", r2.Item2);
                Assert.Contains("The restore inputs for 'parent' have not changed. No further actions are required to complete the restore.", r2.Item2);
            }
        }

        [Fact]
        public async Task RestoreNetCore_PackageTypesDoNotAffectAssetsFileAsync()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var project = SimpleTestProjectContext.CreateNETCore(
                    "project",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };
                packageX.PackageTypes.Add(PackageType.Dependency);
                packageX.PackageTypes.Add(PackageType.DotnetCliTool);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                solution.Projects.Add(project);
                solution.Create(pathContext.SolutionRoot);

                // Act && Assert
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 0);

                Assert.Equal(0, r.Item1);
                Assert.Contains("Writing cache file", r.Item2);
                Assert.Contains("Writing assets file to disk", r.Item2);

                // Pre-condition, Assert deleting the correct file
                Assert.True(File.Exists(project.CacheFileOutputPath));
                File.Delete(project.CacheFileOutputPath);

                r = Util.RestoreSolution(pathContext, expectedExitCode: 0);

                Assert.Equal(0, r.Item1);
                Assert.Contains("Writing cache file", r.Item2);
                Assert.DoesNotContain("Writing assets file to disk", r.Item2);


            }
        }

        [Fact]
        public async Task RestoreNetCore_LongPathInPackage()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };
                packageX.AddFile(@"content/2.5.6/core/store/x64/netcoreapp2.0/microsoft.extensions.configuration.environmentvariables/2.0.0/lib/netstandard2.0/Microsoft.Extensions.Configuration.EnvironmentVariables.dll ");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    packageX);

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);


                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                r.Success.Should().BeTrue();
            }
        }

        [Fact]
        public async Task RestoreNetCore_NoOp_MultipleProjectsInSameDirectoryDoNotNoOp()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                var projects = new List<SimpleTestProjectContext>();

                var project = SimpleTestProjectContext.CreateNETCore(
                    $"proj",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                project.AddPackageToAllFrameworks(packageX);
                solution.Projects.Add(project);
                solution.Create(pathContext.SolutionRoot);

                var secondaryProjectName = Path.Combine(Path.GetDirectoryName(project.ProjectPath), "proj-copy.csproj");

                File.Copy(project.ProjectPath, secondaryProjectName);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Prerequisites
                var r1 = Util.Restore(pathContext,project.ProjectPath);
                Assert.Equal(0, r1.Item1);
                Assert.Contains("Writing cache file", r1.Item2);
                Assert.Contains("Writing assets file to disk", r1.Item2);

                var r2 = Util.Restore(pathContext, secondaryProjectName);
                Assert.Contains("Writing cache file", r2.Item2);
                Assert.Equal(0, r2.Item1);
                Assert.Contains("Writing assets file to disk", r2.Item2);

                // Act
                var result = Util.Restore(pathContext, project.ProjectPath);

                // Assert
                Assert.Equal(0, result.Item1);
                Assert.Contains("Writing cache file", result.Item2);
                Assert.Contains("Writing assets file to disk", result.Item2);
            }
        }

        [Fact]
        public async Task RestoreNetCore_InteropTypePackage()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net461"));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };
                packageX.Files.Clear();
                packageX.AddFile("lib/net461/a.dll");
                packageX.AddFile("embed/net461/a.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    packageX);

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);


                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                r.Success.Should().BeTrue();
                Assert.NotNull(projectA.AssetsFile);

                foreach (var target in projectA.AssetsFile.Targets)
                {
                    var library = target.Libraries.FirstOrDefault(lib => lib.Name.Equals("x"));
                    Assert.NotNull(library);
                    Assert.True(library.EmbedAssemblies.Any(embed => embed.Path.Equals("embed/net461/a.dll")));
                    Assert.True(library.CompileTimeAssemblies.Any(embed => embed.Path.Equals("lib/net461/a.dll")));
                    Assert.True(library.RuntimeAssemblies.Any(embed => embed.Path.Equals("lib/net461/a.dll")));
                }
            }
        }

        [Fact]
        public async Task RestoreNetCore_MultiTFM_ProjectToProject_PackagesLockFile()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net46"),
                    NuGetFramework.Parse("net45"));

                var projectB = SimpleTestProjectContext.CreateNETCore(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"),
                    NuGetFramework.Parse("net46"));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };
                packageX.Files.Clear();
                packageX.AddFile("lib/net45/x.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    packageX);

                // A -> B
                projectA.Properties.Add("RestorePackagesWithLockFile", "true");
                projectA.Properties.Add("RestoreLockedMode", "true");
                projectA.AddProjectToAllFrameworks(projectB);

                // B
                projectB.AddPackageToFramework("net45", packageX);

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                r.Success.Should().BeTrue();
                Assert.True(File.Exists(projectA.AssetsFileOutputPath));
                Assert.True(File.Exists(projectB.AssetsFileOutputPath));
                Assert.True(File.Exists(projectA.NuGetLockFileOutputPath));

                // Second Restore
                r = Util.RestoreSolution(pathContext);

                // Assert
                r.Success.Should().BeTrue();
            }
        }

        [Fact]
        public async Task RestoreNetCore_BuildTransitive()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net461"));

                var packageY = new SimpleTestPackageContext()
                {
                    Id = "y",
                    Version = "1.0.0"
                };
                packageY.Files.Clear();
                packageY.AddFile("lib/net461/y.dll");
                packageY.AddFile("build/y.targets");
                packageY.AddFile("buildCrossTargeting/y.targets");
                packageY.AddFile("buildTransitive/y.targets");
                packageY.Exclude = "build;analyzer";

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };
                packageX.Files.Clear();
                packageX.AddFile("lib/net461/x.dll");
                packageX.Dependencies.Add(packageY);

                projectA.AddPackageToAllFrameworks(packageX);
                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX,
                    packageY);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                r.Success.Should().BeTrue();
                var assetsFile = projectA.AssetsFile;
                Assert.NotNull(assetsFile);

                foreach (var target in assetsFile.Targets)
                {
                    var library = target.Libraries.FirstOrDefault(lib => lib.Name.Equals("y"));
                    Assert.NotNull(library);
                    Assert.True(library.Build.Any(build => build.Path.Equals("buildTransitive/y.targets")));
                }
            }
        }

        [Fact]
        public async Task RestoreNetCore_SkipBuildTransitive()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var projectA = SimpleTestProjectContext.CreateNETCore(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net461"));

                var packageY = new SimpleTestPackageContext()
                {
                    Id = "y",
                    Version = "1.0.0"
                };
                packageY.Files.Clear();
                packageY.AddFile("lib/net461/y.dll");
                packageY.AddFile("build/y.targets");
                packageY.AddFile("buildCrossTargeting/y.targets");
                packageY.AddFile("buildTransitive/y.targets");
                packageY.Exclude = "buildTransitive";

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };
                packageX.Files.Clear();
                packageX.AddFile("lib/net461/x.dll");
                packageX.Dependencies.Add(packageY);

                projectA.AddPackageToAllFrameworks(packageX);
                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX,
                    packageY);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                r.Success.Should().BeTrue();
                var assetsFile = projectA.AssetsFile;
                Assert.NotNull(assetsFile);

                foreach (var target in assetsFile.Targets)
                {
                    var library = target.Libraries.FirstOrDefault(lib => lib.Name.Equals("y"));
                    Assert.NotNull(library);
                    Assert.False(library.Build.Any(build => build.Path.Equals("buildTransitive/y.targets")));
                }
            }
        }

        [Fact]
        public async Task RestoreNetCore_NoOp_DgSpecJsonIsNotOverridenDuringNoOp()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                 var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                 var projects = new List<SimpleTestProjectContext>();

                 var project = SimpleTestProjectContext.CreateNETCore(
                    $"proj",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                 project.AddPackageToAllFrameworks(packageX);
                solution.Projects.Add(project);
                solution.Create(pathContext.SolutionRoot);


                 await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                 // Prerequisites
                var result = Util.Restore(pathContext, project.ProjectPath, additionalArgs: "-verbosity Detailed");
                Assert.Equal(0, result.Item1);
                Assert.Contains("Writing cache file", result.Item2);
                Assert.Contains("Writing assets file to disk", result.Item2);
                Assert.Contains("Persisting no-op dg", result.Item2);

                 var dgSpecFileName = Path.Combine(Path.GetDirectoryName(project.AssetsFileOutputPath), $"{Path.GetFileName(project.ProjectPath)}.nuget.dgspec.json");

                 var fileInfo = new FileInfo(dgSpecFileName);
                Assert.True(fileInfo.Exists);
                var lastWriteTime = fileInfo.LastWriteTime;

                 // Act
                result = Util.Restore(pathContext, project.ProjectPath, additionalArgs: "-verbosity Detailed");

                 // Assert
                Assert.Equal(0, result.Item1);
                Assert.DoesNotContain("Writing cache file", result.Item2);
                Assert.DoesNotContain("Writing assets file to disk", result.Item2);
                Assert.DoesNotContain("Persisting no-op dg", result.Item2);

                 fileInfo = new FileInfo(dgSpecFileName);
                Assert.True(fileInfo.Exists);
                Assert.Equal(lastWriteTime, fileInfo.LastWriteTime);
            }
        }

        [Fact]
        public async Task RestoreNetCore_NoOp_DgSpecJsonIsWrittenInNoopCaseIfNotExists()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                 var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                 var projects = new List<SimpleTestProjectContext>();

                 var project = SimpleTestProjectContext.CreateNETCore(
                    $"proj",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse("net45"));

                 project.AddPackageToAllFrameworks(packageX);
                solution.Projects.Add(project);
                solution.Create(pathContext.SolutionRoot);


                 await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                 // Prerequisites
                var result = Util.Restore(pathContext, project.ProjectPath, additionalArgs: "-verbosity Detailed");
                Assert.Equal(0, result.Item1);
                Assert.Contains("Writing cache file", result.Item2);
                Assert.Contains("Writing assets file to disk", result.Item2);
                Assert.Contains("Persisting no-op dg", result.Item2);

                 var dgSpecFileName = Path.Combine(Path.GetDirectoryName(project.AssetsFileOutputPath), $"{Path.GetFileName(project.ProjectPath)}.nuget.dgspec.json");

                 var fileInfo = new FileInfo(dgSpecFileName);
                Assert.True(fileInfo.Exists);
                fileInfo.Delete();
                fileInfo = new FileInfo(dgSpecFileName);
                Assert.False(fileInfo.Exists);


                 // Act
                result = Util.Restore(pathContext, project.ProjectPath, additionalArgs: "-verbosity Detailed");

                 // Assert
                Assert.Equal(0, result.Item1);
                Assert.DoesNotContain("Writing cache file", result.Item2);
                Assert.DoesNotContain("Writing assets file to disk", result.Item2);
                Assert.Contains("Persisting no-op dg", result.Item2);

                 fileInfo = new FileInfo(dgSpecFileName);
                Assert.True(fileInfo.Exists);
            }
        }

        [Fact]
        public async Task RestoreNetCore_SingleTFM_SimplePackageDownload()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
                var projectFrameworks = "net46";
                
                var projectA = SimpleTestProjectContext.CreateNETCoreWithSDK(
                            projectName: "a",
                            solutionRoot: pathContext.SolutionRoot,
                            frameworks: MSBuildStringUtility.Split(projectFrameworks));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };
                packageX.Files.Clear();
                packageX.AddFile("lib/net45/x.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    packageX);

                projectA.AddPackageDownloadToAllFrameworks(packageX);
                
                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                Assert.True(r.Success, r.AllOutput);
                Assert.True(File.Exists(projectA.AssetsFileOutputPath), r.AllOutput);

                var lockFile = LockFileUtilities.GetLockFile(projectA.AssetsFileOutputPath, Common.NullLogger.Instance);
                Assert.Equal(0, lockFile.Libraries.Count);
                Assert.Equal(1, lockFile.PackageSpec.TargetFrameworks.Count);
                Assert.Equal(1, lockFile.PackageSpec.TargetFrameworks.First().DownloadDependencies.Count);
                Assert.Equal("x", lockFile.PackageSpec.TargetFrameworks.Last().DownloadDependencies.First().Name);
                Assert.True(Directory.Exists(Path.Combine(pathContext.UserPackagesFolder, packageX.Identity.Id, packageX.Version)), $"{packageX.ToString()} is not installed");
            }
        }

        [Fact]
        public async Task RestoreNetCore_SingleTFM_SameIdMultiPackageDownload()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
                var projectFrameworks = "net46";

                var projectA = SimpleTestProjectContext.CreateNETCoreWithSDK(
                            projectName: "a",
                            solutionRoot: pathContext.SolutionRoot,
                            frameworks: MSBuildStringUtility.Split(projectFrameworks));

                var packageX1 = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                var packageX2 = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "2.0.0"
                };

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    packageX1,
                    packageX2);

                projectA.AddPackageDownloadToAllFrameworks(packageX1);
                projectA.AddPackageDownloadToAllFrameworks(packageX2);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                Assert.True(r.Success, r.AllOutput);
                Assert.True(File.Exists(projectA.AssetsFileOutputPath), r.AllOutput);

                var lockFile = LockFileUtilities.GetLockFile(projectA.AssetsFileOutputPath, Common.NullLogger.Instance);
                Assert.Equal(0, lockFile.Libraries.Count);
                Assert.Equal(1, lockFile.PackageSpec.TargetFrameworks.Count);
                Assert.Equal(2, lockFile.PackageSpec.TargetFrameworks.First().DownloadDependencies.Count);
                Assert.Equal("x", lockFile.PackageSpec.TargetFrameworks.Last().DownloadDependencies.First().Name);
                Assert.Equal($"[{packageX2.Version}, {packageX2.Version}]", lockFile.PackageSpec.TargetFrameworks.Last().DownloadDependencies.First().VersionRange.ToNormalizedString());
                Assert.Equal("x", lockFile.PackageSpec.TargetFrameworks.Last().DownloadDependencies.Last().Name);
                Assert.Equal($"[{packageX1.Version}, {packageX1.Version}]", lockFile.PackageSpec.TargetFrameworks.Last().DownloadDependencies.Last().VersionRange.ToNormalizedString());
                
                Assert.True(Directory.Exists(Path.Combine(pathContext.UserPackagesFolder, packageX1.Identity.Id, packageX1.Version)), $"{packageX1.ToString()} is not installed");
                Assert.True(Directory.Exists(Path.Combine(pathContext.UserPackagesFolder, packageX2.Identity.Id, packageX2.Version)), $"{packageX2.ToString()} is not installed");
            }
        }

        [Fact]
        public async Task RestoreNetCore_SingleTFM_SameIdSameVersionMultiDeclaration_MultiPackageDownload()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
                var projectFrameworks = "net46";

                var projectA = SimpleTestProjectContext.CreateNETCoreWithSDK(
                            projectName: "a",
                            solutionRoot: pathContext.SolutionRoot,
                            frameworks: MSBuildStringUtility.Split(projectFrameworks));

                var packageX1 = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                var packageX2 = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "2.0.0"
                };

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    packageX1,
                    packageX2);

                projectA.AddPackageDownloadToAllFrameworks(packageX1);
                projectA.AddPackageDownloadToAllFrameworks(packageX2);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                // Add one more - Adding them through the Test Context adds only exact versions.

                var xml = projectA.GetXML();

                var props = new Dictionary<string, string>();
                var attributes = new Dictionary<string, string>();
                attributes.Add("Version", "[1.0.0, 1.0.0]");
                ProjectFileUtils.AddItem(
                                    xml,
                                    "PackageDownload",
                                    packageX1.Id,
                                    NuGetFramework.AnyFramework,
                                    props,
                                    attributes);

                xml.Save(projectA.ProjectPath);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                Assert.True(r.Success, r.AllOutput);
                Assert.True(File.Exists(projectA.AssetsFileOutputPath), r.AllOutput);

                var lockFile = LockFileUtilities.GetLockFile(projectA.AssetsFileOutputPath, Common.NullLogger.Instance);
                Assert.Equal(0, lockFile.Libraries.Count);
                Assert.Equal(1, lockFile.PackageSpec.TargetFrameworks.Count);
                Assert.Equal(2, lockFile.PackageSpec.TargetFrameworks.First().DownloadDependencies.Count);
                Assert.Equal("x", lockFile.PackageSpec.TargetFrameworks.Last().DownloadDependencies.First().Name);
                Assert.Equal($"[{packageX2.Version}, {packageX2.Version}]", lockFile.PackageSpec.TargetFrameworks.Last().DownloadDependencies.First().VersionRange.ToNormalizedString());
                Assert.Equal("x", lockFile.PackageSpec.TargetFrameworks.Last().DownloadDependencies.Last().Name);
                Assert.Equal($"[{packageX1.Version}, {packageX1.Version}]", lockFile.PackageSpec.TargetFrameworks.Last().DownloadDependencies.Last().VersionRange.ToNormalizedString());

                Assert.True(Directory.Exists(Path.Combine(pathContext.UserPackagesFolder, packageX1.Identity.Id, packageX1.Version)), $"{packageX1.ToString()} is not installed");
                Assert.True(Directory.Exists(Path.Combine(pathContext.UserPackagesFolder, packageX2.Identity.Id, packageX2.Version)), $"{packageX2.ToString()} is not installed");
            }
        }

        [Fact]
        public async Task RestoreNetCore_MultiTfm_PackageDownloadAndPackageReference()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
                var projectFrameworks = "net45;net46";

                var projectA = SimpleTestProjectContext.CreateNETCoreWithSDK(
                            projectName: "a",
                            solutionRoot: pathContext.SolutionRoot,
                            frameworks: MSBuildStringUtility.Split(projectFrameworks));

                var packageX1 = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                var packageX2 = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "2.0.0"
                };

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    packageX1,
                    packageX2);

                projectA.AddPackageToFramework("net45", packageX1);

                projectA.AddPackageDownloadToFramework("net46", packageX2);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                Assert.True(r.Success, r.AllOutput);
                Assert.True(File.Exists(projectA.AssetsFileOutputPath), r.AllOutput);

                var lockFile = LockFileUtilities.GetLockFile(projectA.AssetsFileOutputPath, Common.NullLogger.Instance);

                Assert.Equal(1, lockFile.Libraries.Count);
                Assert.Equal(2, lockFile.PackageSpec.TargetFrameworks.Count);
                Assert.Equal(1, lockFile.PackageSpec.TargetFrameworks.First().Dependencies.Count);
                Assert.Equal(1, lockFile.PackageSpec.TargetFrameworks.Last().DownloadDependencies.Count);
                Assert.Equal("x", lockFile.PackageSpec.TargetFrameworks.Last().DownloadDependencies.First().Name);
                Assert.Equal($"[{packageX2.Version}, {packageX2.Version}]",
                    lockFile.PackageSpec.TargetFrameworks.Last().DownloadDependencies.First().VersionRange.ToNormalizedString());
                Assert.Equal("x", lockFile.PackageSpec.TargetFrameworks.First().Dependencies.Last().Name);
                Assert.Equal($"[{packageX1.Version}, )",
                    lockFile.PackageSpec.TargetFrameworks.First().Dependencies.First().LibraryRange.VersionRange.ToNormalizedString());
                Assert.True(Directory.Exists(Path.Combine(pathContext.UserPackagesFolder, packageX1.Identity.Id, packageX1.Version)),
                    $"{packageX1.ToString()} is not installed");
                Assert.True(Directory.Exists(Path.Combine(pathContext.UserPackagesFolder, packageX2.Identity.Id, packageX2.Version)),
                    $"{packageX2.ToString()} is not installed");
            }
        }

        [Fact]
        public async Task RestoreNetCore_MultiTfm_MultiPackageDownload()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
                var projectFrameworks = "net45;net46";

                var projectA = SimpleTestProjectContext.CreateNETCoreWithSDK(
                            projectName: "a",
                            solutionRoot: pathContext.SolutionRoot,
                            frameworks: MSBuildStringUtility.Split(projectFrameworks));

                var packageX1 = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                var packageX2 = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "2.0.0"
                };

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    packageX1,
                    packageX2);

                projectA.AddPackageDownloadToFramework("net45", packageX1);

                projectA.AddPackageDownloadToFramework("net46", packageX2);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                Assert.True(r.Success, r.AllOutput);
                Assert.True(File.Exists(projectA.AssetsFileOutputPath), r.AllOutput);

                var lockFile = LockFileUtilities.GetLockFile(projectA.AssetsFileOutputPath, Common.NullLogger.Instance);

                Assert.Equal(0, lockFile.Libraries.Count);
                Assert.Equal(2, lockFile.PackageSpec.TargetFrameworks.Count);
                Assert.Equal(0, lockFile.PackageSpec.TargetFrameworks.First().Dependencies.Count);
                Assert.Equal(0, lockFile.PackageSpec.TargetFrameworks.Last().Dependencies.Count);
                Assert.Equal(1, lockFile.PackageSpec.TargetFrameworks.First().DownloadDependencies.Count);
                Assert.Equal(1, lockFile.PackageSpec.TargetFrameworks.Last().DownloadDependencies.Count);

                Assert.Equal("x", lockFile.PackageSpec.TargetFrameworks.Last().DownloadDependencies.First().Name);
                Assert.Equal($"[{packageX2.Version}, {packageX2.Version}]",
                    lockFile.PackageSpec.TargetFrameworks.Last().DownloadDependencies.First().VersionRange.ToNormalizedString());
                Assert.Equal("x", lockFile.PackageSpec.TargetFrameworks.First().DownloadDependencies.Last().Name);
                Assert.Equal($"[{packageX1.Version}, {packageX1.Version}]",
                    lockFile.PackageSpec.TargetFrameworks.First().DownloadDependencies.First().VersionRange.ToNormalizedString());
                Assert.True(Directory.Exists(Path.Combine(pathContext.UserPackagesFolder, packageX1.Identity.Id, packageX1.Version)),
                    $"{packageX1.ToString()} is not installed");
                Assert.True(Directory.Exists(Path.Combine(pathContext.UserPackagesFolder, packageX2.Identity.Id, packageX2.Version)),
                    $"{packageX2.ToString()} is not installed");
            }
        }

        [Fact]
        public async Task RestoreNetCore_SingleTFM_PackageDownload_NonExactVersion_Fails()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
                var projectFrameworks = "net46";

                var projectA = SimpleTestProjectContext.CreateNETCoreWithSDK(
                            projectName: "a",
                            solutionRoot: pathContext.SolutionRoot,
                            frameworks: MSBuildStringUtility.Split(projectFrameworks));

                var packageX1 = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    packageX1);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                var xml = projectA.GetXML();

                var props = new Dictionary<string, string>();
                var attributes = new Dictionary<string, string>();
                attributes.Add("Version", "1.0.0");
                ProjectFileUtils.AddItem(
                                    xml,
                                    "PackageDownload",
                                    packageX1.Id,
                                    NuGetFramework.AnyFramework,
                                    props,
                                    attributes);

                xml.Save(projectA.ProjectPath);

                // Act
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 1);

                // Assert
                Assert.False(r.Success, r.AllOutput);
                Assert.False(File.Exists(projectA.AssetsFileOutputPath), r.AllOutput);
            }
        }

        [Fact]
        public async Task RestoreNetCore_PackageDownload_NoOpAccountsForMissingPackages()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
                var projectFrameworks = "net46";

                var projectA = SimpleTestProjectContext.CreateNETCoreWithSDK(
                            projectName: "a",
                            solutionRoot: pathContext.SolutionRoot,
                            frameworks: MSBuildStringUtility.Split(projectFrameworks));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };
                packageX.Files.Clear();
                packageX.AddFile("lib/net45/x.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    packageX);

                projectA.AddPackageDownloadToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                var r = Util.RestoreSolution(pathContext);

                // Preconditions
                Assert.True(r.Success, r.AllOutput);
                Assert.True(File.Exists(projectA.AssetsFileOutputPath), r.AllOutput);
                var lockFile = LockFileUtilities.GetLockFile(projectA.AssetsFileOutputPath, Common.NullLogger.Instance);
                Assert.Equal(0, lockFile.Libraries.Count);
                Assert.Equal(1, lockFile.PackageSpec.TargetFrameworks.Count);
                Assert.Equal(1, lockFile.PackageSpec.TargetFrameworks.First().DownloadDependencies.Count);
                Assert.Equal("x", lockFile.PackageSpec.TargetFrameworks.Last().DownloadDependencies.First().Name);
                var packagePath = Path.Combine(pathContext.UserPackagesFolder, packageX.Identity.Id, packageX.Version);
                Assert.True(Directory.Exists(packagePath), $"{packageX.ToString()} is not installed");

                Directory.Delete(packagePath, true);

                Assert.False(Directory.Exists(packagePath), $"{packageX.ToString()} should not be installed anymore.");

                // Act
                r = Util.RestoreSolution(pathContext);
                Assert.True(r.Success, r.AllOutput);

                Assert.True(Directory.Exists(packagePath), $"{packageX.ToString()} is not installed");
            }
        }

        [Fact]
        public async Task RestoreNetCore_PackageDownload_DoesNotAFfectNoOp()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
                var projectFrameworks = "net46";

                var projectA = SimpleTestProjectContext.CreateNETCoreWithSDK(
                            projectName: "a",
                            solutionRoot: pathContext.SolutionRoot,
                            frameworks: MSBuildStringUtility.Split(projectFrameworks));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };
                packageX.Files.Clear();
                packageX.AddFile("lib/net45/x.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    packageX);

                projectA.AddPackageDownloadToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                var r = Util.RestoreSolution(pathContext);

                // Preconditions
                Assert.True(r.Success, r.AllOutput);
                Assert.True(File.Exists(projectA.AssetsFileOutputPath), r.AllOutput);
                var lockFile = LockFileUtilities.GetLockFile(projectA.AssetsFileOutputPath, Common.NullLogger.Instance);
                Assert.Equal(0, lockFile.Libraries.Count);
                Assert.Equal(1, lockFile.PackageSpec.TargetFrameworks.Count);
                Assert.Equal(1, lockFile.PackageSpec.TargetFrameworks.First().DownloadDependencies.Count);
                Assert.Equal("x", lockFile.PackageSpec.TargetFrameworks.Last().DownloadDependencies.First().Name);
                var packagePath = Path.Combine(pathContext.UserPackagesFolder, packageX.Identity.Id, packageX.Version);
                Assert.True(Directory.Exists(packagePath), $"{packageX.ToString()} is not installed");
                Assert.Contains("Writing cache file", r.Item2);

                // Act
                r = Util.RestoreSolution(pathContext);
                Assert.True(r.Success, r.AllOutput);

                Assert.True(Directory.Exists(packagePath), $"{packageX.ToString()} is not installed");

                Assert.Equal(0, r.Item1);
                Assert.DoesNotContain("Writing cache file", r.Item2);
                Assert.Contains("No further actions are required to complete", r.Item2);
            }
        }

        [Fact]
        public async Task RestoreNetCore_SingleTFM_FrameworkReferenceFromPackage()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
                var projectFrameworks = "net46";

                var projectA = SimpleTestProjectContext.CreateNETCoreWithSDK(
                            projectName: "a",
                            solutionRoot: pathContext.SolutionRoot,
                            frameworks: MSBuildStringUtility.Split(projectFrameworks));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };
                packageX.FrameworkReferences.Add(NuGetFramework.Parse("net45"), new string[] { "FrameworkRef" });
                packageX.Files.Clear();
                packageX.AddFile("lib/net45/x.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    packageX);

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                Assert.True(r.Success, r.AllOutput);
                Assert.True(File.Exists(projectA.AssetsFileOutputPath), r.AllOutput);

                var lockFile = LockFileUtilities.GetLockFile(projectA.AssetsFileOutputPath, Common.NullLogger.Instance);
                Assert.Equal(1, lockFile.Libraries.Count);
                Assert.Equal(1, lockFile.PackageSpec.TargetFrameworks.Count);
                Assert.Equal(1, lockFile.Targets.First().Libraries.Count);
                Assert.Equal(1, lockFile.Targets.First().Libraries.Single().FrameworkReferences.Count);
                Assert.Equal("FrameworkRef", lockFile.Targets.First().Libraries.Single().FrameworkReferences.Single());
                Assert.True(Directory.Exists(Path.Combine(pathContext.UserPackagesFolder, packageX.Identity.Id, packageX.Version)), $"{packageX.ToString()} is not installed");
            }
        }

        [Fact]
        public async Task RestoreNetCore_SingleTFM_FrameworkReference_TransitivePackageToPackage()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
                var projectFrameworks = "net46";

                var projectA = SimpleTestProjectContext.CreateNETCoreWithSDK(
                            projectName: "a",
                            solutionRoot: pathContext.SolutionRoot,
                            frameworks: MSBuildStringUtility.Split(projectFrameworks));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };
                packageX.FrameworkReferences.Add(NuGetFramework.Parse("net45"), new string[] { "FrameworkRef" });
                packageX.Files.Clear();
                packageX.AddFile("lib/net45/x.dll");

                var packageY = new SimpleTestPackageContext()
                {
                    Id = "y",
                    Version = "1.0.0"
                };
                packageY.Files.Clear();
                packageY.UseDefaultRuntimeAssemblies = false;
                packageY.AddFile("lib/net45/y.dll");
                packageY.FrameworkReferences.Add(NuGetFramework.Parse("net45"), new string[] { "FrameworkRefY" });

                packageX.Dependencies.Add(packageY);


                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    packageX,
                    packageY);

                projectA.AddPackageToAllFrameworks(packageX);

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                Assert.True(r.Success, r.AllOutput);
                Assert.True(File.Exists(projectA.AssetsFileOutputPath), r.AllOutput);

                var lockFile = LockFileUtilities.GetLockFile(projectA.AssetsFileOutputPath, Common.NullLogger.Instance);
                Assert.Equal(2, lockFile.Libraries.Count);
                Assert.Equal(1, lockFile.PackageSpec.TargetFrameworks.Count);
                Assert.Equal(2, lockFile.Targets.First().Libraries.Count);
                Assert.Equal("FrameworkRef", lockFile.Targets.First().Libraries.First().FrameworkReferences.Single());
                Assert.Equal("FrameworkRefY", lockFile.Targets.First().Libraries.Last().FrameworkReferences.Single());
                Assert.True(Directory.Exists(Path.Combine(pathContext.UserPackagesFolder, packageX.Identity.Id, packageX.Version)), $"{packageX.ToString()} is not installed");
            }
        }

        [Fact]
        public async Task RestoreNetCore_SingleTFM_FrameworkReference_TransitiveProjectToProject()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);
                var projectFrameworks = "net46";

                var projectA = SimpleTestProjectContext.CreateNETCoreWithSDK(
                            projectName: "a",
                            solutionRoot: pathContext.SolutionRoot,
                            frameworks: MSBuildStringUtility.Split(projectFrameworks));

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };
                packageX.FrameworkReferences.Add(NuGetFramework.Parse("net45"), new string[] { "FrameworkRef" });
                packageX.Files.Clear();
                packageX.AddFile("lib/net45/x.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    packageX);

                projectA.AddPackageToAllFrameworks(packageX);


                var projectB = SimpleTestProjectContext.CreateNETCoreWithSDK(
                    projectName: "b",
                    solutionRoot: pathContext.SolutionRoot,
                    frameworks: MSBuildStringUtility.Split(projectFrameworks));

                projectA.AddProjectToAllFrameworks(projectB);

                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);

                solution.Create(pathContext.SolutionRoot);

                var xml = projectB.GetXML();

                var props = new Dictionary<string, string>();
                var attributes = new Dictionary<string, string>();
                ProjectFileUtils.AddItem(
                                    xml,
                                    "FrameworkReference",
                                    "FrameworkRefY",
                                    NuGetFramework.AnyFramework,
                                    props,
                                    attributes);

                xml.Save(projectB.ProjectPath);

                // Act
                var r = Util.RestoreSolution(pathContext);

                // Assert
                Assert.True(r.Success, r.AllOutput);
                Assert.True(File.Exists(projectA.AssetsFileOutputPath), r.AllOutput);

                var lockFile = LockFileUtilities.GetLockFile(projectA.AssetsFileOutputPath, Common.NullLogger.Instance);
                Assert.Equal(2, lockFile.Libraries.Count);
                Assert.Equal(1, lockFile.PackageSpec.TargetFrameworks.Count);
                Assert.Equal(2, lockFile.Targets.First().Libraries.Count);
                Assert.Equal("FrameworkRef", lockFile.Targets.First().Libraries.First().FrameworkReferences.Single());
                Assert.Equal("FrameworkRefY", lockFile.Targets.First().Libraries.Last().FrameworkReferences.Single());
                Assert.True(Directory.Exists(Path.Combine(pathContext.UserPackagesFolder, packageX.Identity.Id, packageX.Version)), $"{packageX.ToString()} is not installed");

                // Assert 2
                Assert.True(File.Exists(projectB.AssetsFileOutputPath), r.AllOutput);

                lockFile = LockFileUtilities.GetLockFile(projectB.AssetsFileOutputPath, Common.NullLogger.Instance);
                Assert.Equal(0, lockFile.Libraries.Count);
                Assert.Equal(1, lockFile.PackageSpec.TargetFrameworks.Count);
                Assert.Equal(0, lockFile.Targets.First().Libraries.Count);
                Assert.Equal("FrameworkRefY", lockFile.PackageSpec.TargetFrameworks.Single().FrameworkReferences.Single());
            }
        }
    }
}
