using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
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
                var r = RestoreSolution(pathContext);

                // Assert
                var targetB = projectA.AssetsFile.Targets.Single().Libraries.SingleOrDefault(e => e.Name == "b");
                var libB = projectA.AssetsFile.Libraries.SingleOrDefault(e => e.Name == "b");

                Assert.Equal("1.0.0", targetB.Version.ToNormalizedString());
                Assert.Equal("project", targetB.Type);

                // This is not populated for unknown project types, but this may change in the future.
                Assert.Null(targetB.Framework);
                Assert.Null(libB.Path);

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
                var r = RestoreSolution(pathContext);

                // Assert
                var targetB = projectA.AssetsFile.Targets.Single().Libraries.SingleOrDefault(e => e.Name == "b");
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
                var r = RestoreSolution(pathContext);

                // Assert
                Assert.True(File.Exists(projectA.AssetsFileOutputPath), r.Item2);
                Assert.False(File.Exists(projectA.TargetsOutput), r.Item2);
                Assert.False(File.Exists(projectA.PropsOutput), r.Item2);
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
                var r = RestoreSolution(pathContext);

                // Assert
                var targetB = projectA.AssetsFile.Targets.Single().Libraries.SingleOrDefault(e => e.Name == "b");
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
                var r = RestoreSolution(pathContext);

                // Assert
                var targetX = projectA.AssetsFile.Targets.Single().Libraries.SingleOrDefault(e => e.Name == "x");

                Assert.Equal("1.0.0", targetX.Version.ToNormalizedString());
            }
        }

        private static CommandRunnerResult RestoreSolution(SimpleTestPathContext pathContext)
        {
            var nugetexe = Util.GetNuGetExePath();

            // Store the dg file for debugging
            var dgPath = Path.Combine(pathContext.WorkingDirectory, "out.dg");
            var envVars = new Dictionary<string, string>()
                {
                    { "NUGET_PERSIST_DG", "true" },
                    { "NUGET_PERSIST_DG_PATH", dgPath }
                };

            string[] args = new string[] {
                    "restore",
                    pathContext.SolutionRoot,
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

            return r;
        }
    }
}
