using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Commands.Test
{
    public class CompatilibityCheckerTests
    {
        [Fact]
        public async Task CompatilibityChecker_PackageExcludeCompileRuntime_Success()
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
                ""netstandard1.5"": {
                    ""dependencies"": {
                        ""packageA"": {
                            ""version"": ""1.0.0"",
                            ""exclude"": ""compile,runtime""
                        }
                    }
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

                var logger = new TestLogger();
                var request = new RestoreRequest(spec1, sources, packagesDir.FullName, logger);

                request.LockFilePath = Path.Combine(project1.FullName, "project.lock.json");
                request.RequestedRuntimes.Add("win7-x86");

                var packageA = new SimpleTestPackageContext("packageA");
                packageA.AddFile("lib/net45/a.dll");

                SimpleTestPackageUtility.CreatePackages(packageSource.FullName, packageA);

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);

                // Assert
                Assert.True(result.Success, logger.ShowErrors());

                // Verify both libraries were installed
                Assert.Equal(1, result.LockFile.Libraries.Count);

                // Verify no compatibility issues
                Assert.True(result.CompatibilityCheckResults.All(check => check.Success));
            }
        }

        [Fact]
        public async Task CompatilibityChecker_PackageCompatibility_Fail()
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
                ""netstandard1.5"": {
                    ""dependencies"": {
                        ""packageA"": {
                            ""version"": ""1.0.0""
                        }
                    }
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

                var logger = new TestLogger();
                var request = new RestoreRequest(spec1, sources, packagesDir.FullName, logger);

                request.LockFilePath = Path.Combine(project1.FullName, "project.lock.json");
                request.RequestedRuntimes.Add("win7-x86");

                var packageA = new SimpleTestPackageContext("packageA");
                packageA.AddFile("lib/net45/a.dll");

                SimpleTestPackageUtility.CreatePackages(packageSource.FullName, packageA);

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);

                // Assert
                Assert.False(result.Success, logger.ShowErrors());

                // Verify both libraries were installed
                Assert.Equal(1, result.LockFile.Libraries.Count);

                // Verify no compatibility issues
                Assert.False(result.CompatibilityCheckResults.All(check => check.Success));
            }
        }

        [Fact]
        public async Task CompatilibityChecker_MissingRuntimeAssemblyCompileOnly_Success()
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
                ""netstandard1.5"": {
                    ""dependencies"": {
                        ""packageA"": {
                            ""version"": ""1.0.0"",
                            ""include"": ""compile""
                        }
                    }
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

                var logger = new TestLogger();
                var request = new RestoreRequest(spec1, sources, packagesDir.FullName, logger);

                request.LockFilePath = Path.Combine(project1.FullName, "project.lock.json");
                request.RequestedRuntimes.Add("win7-x86");

                var packageA = new SimpleTestPackageContext("packageA");
                packageA.AddFile("ref/netstandard1.3/a.dll");

                SimpleTestPackageUtility.CreatePackages(packageSource.FullName, packageA);

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);

                // Assert
                Assert.True(result.Success, logger.ShowErrors());

                // Verify both libraries were installed
                Assert.Equal(1, result.LockFile.Libraries.Count);

                // Verify no compatibility issues
                Assert.True(result.CompatibilityCheckResults.All(check => check.Success));
            }
        }

        [Fact]
        public async Task CompatilibityChecker_MissingRuntimeAssembly_Fail()
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
                ""netstandardapp1.5"": {
                    ""dependencies"": {
                        ""packageA"": {
                            ""version"": ""1.0.0""
                        }
                    }
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

                var logger = new TestLogger();
                var request = new RestoreRequest(spec1, sources, packagesDir.FullName, logger);

                request.LockFilePath = Path.Combine(project1.FullName, "project.lock.json");
                request.RequestedRuntimes.Add("win7-x86");

                var packageA = new SimpleTestPackageContext("packageA");
                packageA.AddFile("ref/netstandard1.3/a.dll");

                SimpleTestPackageUtility.CreatePackages(packageSource.FullName, packageA);

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);

                var failure = result.CompatibilityCheckResults.Where(r => !r.Success).Single().Issues.Single();

                // Assert
                Assert.False(result.Success, logger.ShowErrors());

                // Verify both libraries were installed
                Assert.Equal(1, result.LockFile.Libraries.Count);

                // Verify compatibility issue
                Assert.Equal("a", failure.AssemblyName);
                Assert.Equal("win7-x86", failure.RuntimeIdentifier);
            }
        }

        [Fact]
        public async Task CompatilibityChecker_RuntimeFoundInAnotherPackage_Success()
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
                ""netstandard1.5"": {
                    ""dependencies"": {
                        ""packageA"": {
                            ""version"": ""1.0.0""
                        },
                        ""packageB"": {
                            ""version"": ""1.0.0""
                        },
                        ""packageC"": {
                            ""version"": ""1.0.0""
                        },
                        ""packageD"": {
                            ""version"": ""1.0.0""
                        }
                    }
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

                var logger = new TestLogger();
                var request = new RestoreRequest(spec1, sources, packagesDir.FullName, logger);

                request.LockFilePath = Path.Combine(project1.FullName, "project.lock.json");
                request.RequestedRuntimes.Add("win7-x86");

                var packageA = new SimpleTestPackageContext("packageA");
                packageA.AddFile("ref/netstandard1.3/a.dll");
                packageA.AddFile("ref/netstandard1.3/b.dll");
                packageA.AddFile("ref/netstandard1.3/c.dll");

                var packageB = new SimpleTestPackageContext("packageB");
                packageB.AddFile("runtimes/win7-x86/lib/netstandard1.1/a.dll");

                var packageC = new SimpleTestPackageContext("packageC");
                packageC.AddFile("lib/netstandard1.1/b.ni.dll");

                var packageD = new SimpleTestPackageContext("packageD");
                packageD.AddFile("lib/netstandard1.1/c.dll");

                SimpleTestPackageUtility.CreatePackages(packageSource.FullName, packageA, packageB, packageC, packageD);

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);

                // Assert
                Assert.True(result.Success, logger.ShowErrors());

                // Verify both libraries were installed
                Assert.Equal(4, result.LockFile.Libraries.Count);

                // Verify no compatibility issues
                Assert.True(result.CompatibilityCheckResults.All(check => check.Success));
            }
        }

        [Fact]
        public async Task CompatilibityChecker_RuntimeFoundInSamePackage_Success()
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
                ""netstandard1.5"": {
                    ""dependencies"": {
                        ""packageA"": {
                            ""version"": ""1.0.0""
                        }
                    }
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

                var logger = new TestLogger();
                var request = new RestoreRequest(spec1, sources, packagesDir.FullName, logger);

                request.LockFilePath = Path.Combine(project1.FullName, "project.lock.json");
                request.RequestedRuntimes.Add("win7-x86");

                var packageA = new SimpleTestPackageContext("packageA");
                packageA.AddFile("ref/netstandard1.3/a.dll");
                packageA.AddFile("ref/netstandard1.3/b.dll");
                packageA.AddFile("ref/netstandard1.3/c.dll");
                packageA.AddFile("runtimes/win7-x86/lib/netstandard1.1/a.dll");
                packageA.AddFile("runtimes/win7-x86/lib/netstandard1.1/b.ni.dll");
                packageA.AddFile("runtimes/win7-x86/lib/netstandard1.1/c.dll");

                SimpleTestPackageUtility.CreatePackages(packageSource.FullName, packageA);

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);

                // Assert
                Assert.True(result.Success, logger.ShowErrors());

                // Verify both libraries were installed
                Assert.Equal(1, result.LockFile.Libraries.Count);

                // Verify no compatibility issues
                Assert.True(result.CompatibilityCheckResults.All(check => check.Success));
            }
        }
    }
}
