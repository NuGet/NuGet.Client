using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class RestoreNetCoreTest
    {
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

                // Assert
                Assert.True(r.Item1 == 1);
                Assert.Contains("no run-time assembly compatible", r.Item3);
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

                // Assert
                Assert.True(r.Item1 == 1);
                Assert.Contains("no run-time assembly compatible", r.Item3);
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

                // Assert
                Assert.True(File.Exists(projectA.AssetsFileOutputPath), r.Item2);
                Assert.Contains($"Compatibility Profile: {guid}", r.Item2);
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

        [Fact(Skip = "Not supported")]
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

                var path = Path.Combine(pathContext.UserPackagesFolder, ".tools", "z", "1.0.0", "netcoreapp1.0", "project.assets.json");

                // Act
                var r = Util.RestoreSolution(pathContext);

                File.AppendAllText(path, "\n\n\n\n\n");

                r = Util.RestoreSolution(pathContext);

                var text = File.ReadAllText(path);

                // Assert
                Assert.True(File.Exists(path), r.Item2);
                Assert.EndsWith("\n\n\n\n\n", text);
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
        public void RestoreNetCore_NETCore_ProjectToProject_MissingProjectReference()
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
                var r = Util.RestoreSolution(pathContext, expectedExitCode: 1);
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
    }
}