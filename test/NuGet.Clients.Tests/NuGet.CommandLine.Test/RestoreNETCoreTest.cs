using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using Newtonsoft.Json.Linq;
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
        public void RestoreNetCore_VerifyPerProjectConfigSourcesAreUsedForChildProjectsWithoutSolution()
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
                    SimpleTestPackageUtility.CreatePackages(source, package);
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
        public void RestoreNetCore_VerifyProjectConfigCanOverrideSolutionConfig()
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
                SimpleTestPackageUtility.CreatePackages(source, packageGood, packageGoodDep);

                // The solution level source does not contain B
                SimpleTestPackageUtility.CreatePackages(pathContext.PackageSource, packageBad);

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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_VerifyPackageReference_WithoutRestoreProjectStyle()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Act
                var r = Util.RestoreSolution(pathContext);

                var dgPath = Path.Combine(pathContext.WorkingDirectory, "out.dg");
                var dgSpec = DependencyGraphSpec.Load(dgPath);

                var propsXML = XDocument.Load(projectA.PropsOutput);
                var styleNode = propsXML.Root.Elements().First().Elements(XName.Get("NuGetProjectStyle", "http://schemas.microsoft.com/developer/msbuild/2003")).FirstOrDefault();

                // Assert
                Assert.Equal(ProjectStyle.PackageReference, dgSpec.Projects.Single().RestoreMetadata.ProjectStyle);
                Assert.Equal("PackageReference", styleNode.Value);
            }
        }

        [Fact]
        public async Task RestoreNetCore_SetProjectStyleWithProperty_PackageReference()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Act
                var r = Util.RestoreSolution(pathContext);

                var dgPath = Path.Combine(pathContext.WorkingDirectory, "out.dg");
                var dgSpec = DependencyGraphSpec.Load(dgPath);

                var propsXML = XDocument.Load(projectA.PropsOutput);
                var styleNode = propsXML.Root.Elements().First().Elements(XName.Get("NuGetProjectStyle", "http://schemas.microsoft.com/developer/msbuild/2003")).FirstOrDefault();

                // Assert
                Assert.Equal(ProjectStyle.PackageReference, dgSpec.Projects.Single().RestoreMetadata.ProjectStyle);
                Assert.Equal("PackageReference", styleNode.Value);
            }
        }

        [Fact]
        public async Task RestoreNetCore_SetProjectStyleWithProperty_ProjectJson()
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

                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                File.WriteAllText(Path.Combine(Path.GetDirectoryName(projectA.ProjectPath), "project.json"), projectJson.ToString());

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };

                packageX.AddFile("build/net45/x.targets");

                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                var projectXML = XDocument.Load(projectA.ProjectPath);
                projectXML.Root.AddFirst(new XElement(XName.Get("Target", "http://schemas.microsoft.com/developer/msbuild/2003"), new XAttribute(XName.Get("Name"), "_SplitProjectReferencesByFileExistence")));
                projectXML.Save(projectA.ProjectPath);

                // Act
                var r = Util.RestoreSolution(pathContext);

                var dgPath = Path.Combine(pathContext.WorkingDirectory, "out.dg");
                var dgSpec = DependencyGraphSpec.Load(dgPath);

                // Assert
                Assert.Equal(ProjectStyle.ProjectJson, dgSpec.Projects.Single().RestoreMetadata.ProjectStyle);
                Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(projectA.ProjectPath), "project.lock.json")));
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

                // Store the dg file for debugging
                var dgPath = Path.Combine(pathContext.WorkingDirectory, "out.dg");
                var envVars = new Dictionary<string, string>()
                {
                    { "NUGET_PERSIST_DG", "true" },
                    { "NUGET_PERSIST_DG_PATH", dgPath }
                };

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
                    waitForExit: true,
                    environmentVariables: envVars);

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

                // Store the dg file for debugging
                var dgPath = Path.Combine(pathContext.WorkingDirectory, "out.dg");
                var envVars = new Dictionary<string, string>()
                {
                    { "NUGET_PERSIST_DG", "true" },
                    { "NUGET_PERSIST_DG_PATH", dgPath }
                };

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
                    waitForExit: true,
                    environmentVariables: envVars);

                // Assert
                Assert.True(0 == r.Item1, r.Item2 + " " + r.Item3);

                // Assert
                Assert.True(File.Exists(projectA.AssetsFileOutputPath));
                Assert.False(File.Exists(projectB.AssetsFileOutputPath));
                Assert.True(File.Exists(projectC.AssetsFileOutputPath));
            }
        }

        [Fact]
        public async Task RestoreNetCore_RestoreWithRID()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_RestoreWithRID_ValidateRID_Failure()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_RestoreWithRID_ValidateRID_IgnoreFailure()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_RestoreWithRID_ValidateRID_FailureForProjectJson()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_RestoreWithRIDSingle()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_RestoreWithRIDDuplicates()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_RestoreWithSupports()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_RestoreWithMultipleRIDs()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_MultipleProjects_SameToolDifferentVersionsWithMultipleHits()
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

                    await SimpleTestPackageUtility.CreateFolderFeedV3(
                        pathContext.PackageSource,
                        PackageSaveMode.Defaultv3,
                        packageZSub);

                    solution.Projects.Add(project);
                    solution.Create(pathContext.SolutionRoot);
                }

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_MultipleProjects_SameToolDifferentVersionsWithMultipleHits_NoOp()
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

                    await SimpleTestPackageUtility.CreateFolderFeedV3(
                        pathContext.PackageSource,
                        PackageSaveMode.Defaultv3,
                        packageZSub);

                    solution.Projects.Add(project);
                    solution.Create(pathContext.SolutionRoot);
                }

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
                Assert.Contains($"The restore inputs for 'DotnetCliToolReference-z' have not changed. No further actions are required to complete the restore.", r2.Item2);

                // Each project should have its own tool verion
                Assert.Equal(testCount, Directory.GetDirectories(zPath).Length);
            }
        }

        [Fact]
        public async Task RestoreNetCore_NoOp_AddingNewPackageRestores()
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


                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_NoOp_AddingANewProjectRestoresOnlyThatProject()
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


                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_NoOp_WarningsAndErrorsDontAffectHash()
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


                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_MultipleProjects_SameToolDifferentVersions()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_MultipleProjects_SameToolDifferentVersions_NoOp_Fails()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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

                Assert.Contains("The restore inputs for 'DotnetCliToolReference-z' have changed. Continuing restore.", r2.Item2);
                var count = Regex.Matches(r2.Item2, ("The restore inputs for 'DotnetCliToolReference-z' have changed. Continuing restore.")).Count;
                Assert.True(count == 9 || count == 10, $"{ count } needs to be 9 or 10 in \n: { r2.Item2 }");
            }
        }

        [Fact]
        public async Task RestoreNetCore_MultipleProjects_SameTool()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_MultipleProjects_SameTool_DifferentVersionRanges_DoesNotNoOp()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
                Assert.Contains($"The restore inputs for 'DotnetCliToolReference-z' have changed. Continuing restore.", r2.Item2);
                r = Util.RestoreSolution(pathContext);

            }
        }

        [Fact]
        public async Task RestoreNetCore_MultipleProjects_SameTool_OverlappingVersionRanges_DoesNoOp()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
                // This is a more complex scenario, since when we dedup 2.0.0 and 2.0.* we only look for 2.0.*...if 2.0.0 package exists, the 2.0.* would resolve to 2.0.0 so both cases would be covered
                // The issue is ofc when you have 2.5 package in your local, and a package with 2.0.0 was added remotely. Then we re-download
                Assert.Contains($"The restore inputs for 'DotnetCliToolReference-z' have not changed. No further actions are required to complete the restore.", r2.Item2);
            }
        }

        [Fact]
        public async Task RestoreNetCore_MultipleProjects_SameTool_OverlappingVersionRanges_OnlyOneMatchesPackage_DoesNoOp()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
                // This is a more complex scenario, since when we dedup 2.0.0 and 2.0.* we only look for 2.0.*...if 2.0.0 package exists, the 2.0.* would resolve to 2.0.0 so both cases would be covered
                // The issue is ofc when you have 2.5 package in your local, and a package with 2.0.0 was added remotely. Then we won't redownload
                Assert.False(File.Exists(assetsPath20));
                Assert.False(File.Exists(cachePath20));
                Assert.Contains($"The restore inputs for 'DotnetCliToolReference-z' have not changed. No further actions are required to complete the restore.", r2.Item2);
                r = Util.RestoreSolution(pathContext);
            }
        }


        [Fact]
        public async Task RestoreNetCore_MultipleProjects_SameTool_NoOp() 
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
                Assert.Contains($"The restore inputs for 'DotnetCliToolReference-z' have not changed. No further actions are required to complete the restore.", r2.Item2);

                r = Util.RestoreSolution(pathContext);

            }
        }

        [Fact]
        public async Task RestoreNetCore_SingleToolRestore()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_SingleToolRestore_Noop()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
                Assert.Contains($"The restore inputs for 'DotnetCliToolReference-z' have not changed. No further actions are required to complete the restore.", r2.Item2);

                r = Util.RestoreSolution(pathContext);
            }
        }

        // Just utlizing the infrastracture that we have here, rather than trying to create my own directory structure to test this :)
        [Theory]
        [InlineData("[1.0.0]", "1.0.0")]
        [InlineData("[5.0.0]", "5.0.0")]
        [InlineData("[1.5.0]", null)]
        [InlineData("1.1.*", "2.0.0")]
        public async Task ToolPathResolver_FindsBestMatchingToolVersion(string requestedVersion, string expectedVersion)
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

                    await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_RestoreToolInChildProjectWithRecursive_NoOp()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
                Assert.Contains($"The restore inputs for 'DotnetCliToolReference-x' have not changed. No further actions are required to complete the restore.", r2.Item2);
                Assert.Contains($"The restore inputs for 'a' have not changed. No further actions are required to complete the restore.", r2.Item2);
                Assert.Contains($"The restore inputs for 'b' have not changed. No further actions are required to complete the restore.", r2.Item2);

                r = Util.RestoreSolution(pathContext);
            }
        }

        [Fact]
        public async Task RestoreNetCore_RestoreToolInChildProjectWithRecursive()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_SkipRestoreToolInChildProjectForNonRecursive()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_VerifyBuildCrossTargeting_VerifyImportOrder()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_VerifyBuildCrossTargeting_VerifyImportIsAdded()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_VerifyBuildCrossTargeting_VerifyNoDuplicateImports()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_VerifyBuildCrossTargeting_VerifyImportIsNotAddedForUAP()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_VerifyBuildCrossTargeting_VerifyImportRequiresPackageName()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_VerifyBuildCrossTargeting_VerifyImportNotAllowedInSubFolder()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_NETCoreImports_VerifyImportFromPackageIsIgnored()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_UAPImports_VerifyImportFromPackageIsIgnored()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_ProjectToProject_Interweaving()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_ProjectToProject_UAPToNetCore()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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

                // Store the dg file for debugging
                var dgPath = Path.Combine(pathContext.WorkingDirectory, "out.dg");
                var envVars = new Dictionary<string, string>()
                {
                    { "NUGET_PERSIST_DG", "true" },
                    { "NUGET_PERSIST_DG_PATH", dgPath }
                };

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
                    waitForExit: true,
                    environmentVariables: envVars);

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

                // Store the dg file for debugging
                var dgPath = Path.Combine(pathContext.WorkingDirectory, "out.dg");
                var envVars = new Dictionary<string, string>()
                {
                    { "NUGET_PERSIST_DG", "true" },
                    { "NUGET_PERSIST_DG_PATH", dgPath }
                };

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
                    waitForExit: true,
                    environmentVariables: envVars);

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

                // Store the dg file for debugging
                var dgPath = Path.Combine(pathContext.WorkingDirectory, "out.dg");
                var envVars = new Dictionary<string, string>()
                {
                    { "NUGET_PERSIST_DG", "true" },
                    { "NUGET_PERSIST_DG_PATH", dgPath }
                };

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
                    waitForExit: true,
                    environmentVariables: envVars);

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

                // Store the dg file for debugging
                var dgPath = Path.Combine(pathContext.WorkingDirectory, "out.dg");
                var envVars = new Dictionary<string, string>()
                {
                    { "NUGET_PERSIST_DG", "true" },
                    { "NUGET_PERSIST_DG_PATH", dgPath }
                };

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
                    waitForExit: true,
                    environmentVariables: envVars);

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

                // Store the dg file for debugging
                var dgPath = Path.Combine(pathContext.WorkingDirectory, "out.dg");
                var envVars = new Dictionary<string, string>()
                {
                    { "NUGET_PERSIST_DG", "true" },
                    { "NUGET_PERSIST_DG_PATH", dgPath }
                };

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
                    waitForExit: true,
                    environmentVariables: envVars);

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

                // Store the dg file for debugging
                var dgPath = Path.Combine(pathContext.WorkingDirectory, "out.dg");
                var envVars = new Dictionary<string, string>()
                {
                    { "NUGET_PERSIST_DG", "true" },
                    { "NUGET_PERSIST_DG_PATH", dgPath }
                };

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
                    waitForExit: true,
                    environmentVariables: envVars);

                // Assert
                Assert.False(0 == r.Item1, r.Item2 + " " + r.Item3);
            }
        }

        [Fact]
        public void RestoreNetCore_VerifyPropsAndTargetsAreWrittenWhenRestoreFails()
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

                var yPath = SimpleTestPackageUtility.CreateFullPackage(pathContext.PackageSource, packageY);
                SimpleTestPackageUtility.CreateFullPackage(pathContext.PackageSource, packageX);

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
        public async Task RestoreNetCore_SingleProject()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_SingleProjectWithPackageTargetFallback()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_SingleProjectWithPackageTargetFallbackAndWhitespace()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_SingleProject_SingleTFM()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_NETCore_ProjectToProject_VerifyTransitivePackage()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_NETCore_ProjectToProjectMultipleTFM_VerifyTransitivePackages()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_NETCoreAndUAP_ProjectToProjectMultipleTFM_VerifyTransitivePackages()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_LegacyPackagesDirectorySettingsIsIsolatedToProject()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_LegacyPackagesDirectoryEnabledInProjectFile()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_LegacyPackagesDirectoryDisabledInProjectFile()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_AssetTargetFallbackVerifyFallbackToNet46Assets()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_AssetTargetFallbackVerifyNoFallbackToNet46Assets()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_BothAssetTargetFallbackPackageTargetFallbackVerifyError()
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

                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_VerifyAdditionalSourcesApplied()
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
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.FallbackFolder,
                    PackageSaveMode.Defaultv3,
                    packageM);

                // X is only in the source
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Y is only in the extra source
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    extraSource,
                    PackageSaveMode.Defaultv3,
                    packageY);

                // Z is only in the extra fallback
                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_VerifyAdditionalSourcesConditionalOnFramework()
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
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    extraSourceA,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Y is only in the extra source
                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_VerifyAdditionalFallbackFolderConditionalOnFramework()
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
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    extraSourceA,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Y is only in the extra source
                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_VerifyAdditionalFallbackFolderExclude()
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
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    extraSourceA,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Y is only in the extra source
                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_VerifyAdditionalSourcesAppliedWithSingleFramework()
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
                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_VerifyAdditionalFallbackFolderAppliedWithSingleFramework()
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
                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_VerifyPackagesFolderPathResolvedAgainstWorkingDir()
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
                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_VerifyAdditionalSourcesAppliedToTools()
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
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.FallbackFolder,
                    PackageSaveMode.Defaultv3,
                    packageM);

                // X is only in the source
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Y is only in the extra source
                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    extraSource,
                    PackageSaveMode.Defaultv3,
                    packageY);

                // Z is only in the extra fallback
                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_VerifyPackagesFolderPathResolvedAgainstProjectProperty()
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
                await SimpleTestPackageUtility.CreateFolderFeedV3(
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

        [Fact]
        public async Task RestoreNetCore_VerifySourcesResolvedAgainstProjectProperty()
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
                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_VerifySourcesResolvedAgainstWorkingDir()
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
                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
        public async Task RestoreNetCore_VerifyFallbackFoldersResolvedAgainstProjectProperty()
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
                await SimpleTestPackageUtility.CreateFolderFeedV3(
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
    }
}