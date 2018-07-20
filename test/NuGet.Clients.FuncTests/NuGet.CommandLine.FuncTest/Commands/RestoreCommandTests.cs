// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet.CommandLine.Test;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.CommandLine.FuncTest.Commands
{
    public class RestoreCommandTests
    {
        [Fact]
        public async Task Restore_LegacyPackageReference_WithNuGetLockFile()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var net461 = NuGetFramework.Parse("net461");

                var projectA = SimpleTestProjectContext.CreateLegacyPackageReference(
                    "a",
                    pathContext.SolutionRoot,
                    net461);

                projectA.Properties.Add("RestorePackagesWithLockFile", "true");

                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };
                packageX.Files.Clear();
                packageX.AddFile("lib/net461/a.dll");

                projectA.AddPackageToAllFrameworks(packageX);
                solution.Projects.Add(projectA);
                solution.Create(pathContext.SolutionRoot);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageX);

                // Act
                var result = RunRestore(pathContext);

                // Assert
                Assert.True(File.Exists(projectA.NuGetLockFileOutputPath));

                var lockFile = PackagesLockFileFormat.Read(projectA.NuGetLockFileOutputPath);
                Assert.Equal(4, lockFile.Targets.Count);

                var targets = lockFile.Targets.Where(t => t.Dependencies.Count > 0).ToList();
                Assert.Equal(1, targets.Count);
                Assert.Equal(".NETFramework,Version=v4.6.1", targets[0].Name);
                Assert.Equal(1, targets[0].Dependencies.Count);
                Assert.Equal("x", targets[0].Dependencies[0].Id);
            }
        }

        [Fact]
        public async Task Restore_LegacyPackageReference_WithNuGetLockFilePath()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                // Set up solution, project, and packages
                var solution = new SimpleTestSolutionContext(pathContext.SolutionRoot);

                var net461 = "net461";

                var projectA = SimpleTestProjectContext.CreateLegacyPackageReference(
                    "a",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse(net461));

                var projectB = SimpleTestProjectContext.CreateLegacyPackageReference(
                    "b",
                    pathContext.SolutionRoot,
                    NuGetFramework.Parse(net461));

                // set up packages
                var packageX = new SimpleTestPackageContext()
                {
                    Id = "x",
                    Version = "1.0.0"
                };
                packageX.Files.Clear();
                packageX.AddFile($"lib/{0}/x.dll", net461);

                var packageY = new SimpleTestPackageContext()
                {
                    Id = "y",
                    Version = "1.0.0"
                };
                packageY.Files.Clear();
                packageY.AddFile($"lib/{0}/y.dll", net461);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                   pathContext.PackageSource,
                   PackageSaveMode.Defaultv3,
                   packageX,
                   packageY);

                // set up projects and solution
                projectB.AddPackageToAllFrameworks(packageY);
                projectA.Properties.Add("RestorePackagesWithLockFile", "true");
                var packagesLockFilePath = Path.Combine(Path.GetDirectoryName(projectA.ProjectPath), "packages.custom.lock.json");
                projectA.Properties.Add("NuGetLockFilePath", packagesLockFilePath);
                projectA.AddProjectToAllFrameworks(projectB);
                projectA.AddPackageToAllFrameworks(packageX);
                solution.Projects.Add(projectA);
                solution.Projects.Add(projectB);
                solution.Create(pathContext.SolutionRoot);

                // Act
                var result = RunRestore(pathContext);

                // Assert
                Assert.True(File.Exists(projectA.NuGetLockFileOutputPath));
                Assert.Equal(packagesLockFilePath, projectA.NuGetLockFileOutputPath);

                var lockFile = PackagesLockFileFormat.Read(projectA.NuGetLockFileOutputPath);
                Assert.Equal(4, lockFile.Targets.Count);

                var targets = lockFile.Targets.Where(t => t.Dependencies.Count > 0).ToList();
                Assert.Equal(1, targets.Count);
                Assert.Equal(".NETFramework,Version=v4.6.1", targets[0].Name);
                Assert.Equal(3, targets[0].Dependencies.Count);
                Assert.Equal("x", targets[0].Dependencies[0].Id);
                Assert.Equal(PackageDependencyType.Direct, targets[0].Dependencies[0].Type);
                Assert.Equal("y", targets[0].Dependencies[1].Id);
                Assert.Equal(PackageDependencyType.Transitive, targets[0].Dependencies[1].Type);
                Assert.Equal("b", targets[0].Dependencies[2].Id);
                Assert.Equal(PackageDependencyType.Project, targets[0].Dependencies[2].Type);
            }
        }

        public static CommandRunnerResult RunRestore(SimpleTestPathContext pathContext, int expectedExitCode = 0)
        {
            var nugetExe = Util.GetNuGetExePath();

            // Store the dg file for debugging
            var envVars = new Dictionary<string, string>()
            {
                { "NUGET_HTTP_CACHE_PATH", pathContext.HttpCacheFolder }
            };

            var args = new string[]
            {
                "restore",
                pathContext.SolutionRoot,
                "-Verbosity",
                "detailed"
            };

            // Act
            var r = CommandRunner.Run(
                nugetExe,
                pathContext.WorkingDirectory.Path,
                string.Join(" ", args),
                waitForExit: true,
                environmentVariables: envVars);

            // Assert
            Assert.True(expectedExitCode == r.ExitCode, r.AllOutput);

            return r;
        }
    }
}
