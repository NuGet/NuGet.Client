// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Commands.Test
{
    public class RestoreCommandTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task RestoreCommand_ObservesLowercaseFlag(bool isLowercase)
        {
            // Arrange
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var packagesDir = new DirectoryInfo(Path.Combine(workingDir, "globalPackages"));
                var sourceDir = new DirectoryInfo(Path.Combine(workingDir, "packageSource"));
                var projectDir = new DirectoryInfo(Path.Combine(workingDir, "projects", "project1"));
                packagesDir.Create();
                sourceDir.Create();
                projectDir.Create();

                var resolver = new VersionFolderPathResolver(packagesDir.FullName, isLowercase);

                var sources = new List<string>();
                sources.Add(sourceDir.FullName);

                var projectJson = @"
                {
                  ""frameworks"": {
                    ""netstandard1.0"": {
                      ""dependencies"": {
                        ""PackageA"": ""1.0.0-Beta""
                      }
                    }
                  }
                }";

                File.WriteAllText(Path.Combine(projectDir.FullName, "project.json"), projectJson);

                var specPath = Path.Combine(projectDir.FullName, "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(projectJson, "project1", specPath);

                var logger = new TestLogger();
                var request = new RestoreRequest(
                    spec,
                    sources.Select(x => Repository.Factory.GetCoreV3(x)),
                    packagesDir.FullName,
                    Enumerable.Empty<string>(),
                    logger)
                {
                    IsLowercasePackagesDirectory = isLowercase
                };
                request.LockFilePath = Path.Combine(projectDir.FullName, "project.lock.json");

                var packageId = "PackageA";
                var packageVersion = "1.0.0-Beta";
                var packageAContext = new SimpleTestPackageContext(packageId, packageVersion);
                packageAContext.AddFile("lib/netstandard1.0/a.dll");

                SimpleTestPackageUtility.CreateFullPackage(sourceDir.FullName, packageAContext);

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                var lockFile = result.LockFile;

                // Assert
                Assert.True(result.Success);

                var library = lockFile
                    .Libraries
                    .FirstOrDefault(l => l.Name == packageId && l.Version.ToNormalizedString() == packageVersion);

                Assert.NotNull(library);
                Assert.Equal(
                    PathUtility.GetPathWithForwardSlashes(resolver.GetPackageDirectory(packageId, library.Version)),
                    library.Path);
                Assert.True(File.Exists(resolver.GetPackageFilePath(packageId, library.Version)));
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task RestoreCommand_WhenSwitchingBetweenLowercaseSettings_LockFileAlwaysRespectsLatestSetting(bool isLowercase)
        {
            // Arrange
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var packagesDir = new DirectoryInfo(Path.Combine(workingDir, "globalPackages"));
                var sourceDir = new DirectoryInfo(Path.Combine(workingDir, "packageSource"));
                var projectDir = new DirectoryInfo(Path.Combine(workingDir, "projects", "project1"));
                packagesDir.Create();
                sourceDir.Create();
                projectDir.Create();

                var resolverA = new VersionFolderPathResolver(packagesDir.FullName, !isLowercase);
                var resolverB = new VersionFolderPathResolver(packagesDir.FullName, isLowercase);

                var sources = new List<string>();
                sources.Add(sourceDir.FullName);

                var projectJson = @"
                {
                  ""frameworks"": {
                    ""netstandard1.0"": {
                      ""dependencies"": {
                        ""PackageA"": ""1.0.0-Beta""
                      }
                    }
                  }
                }";

                File.WriteAllText(Path.Combine(projectDir.FullName, "project.json"), projectJson);

                var specPath = Path.Combine(projectDir.FullName, "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(projectJson, "project1", specPath);

                var logger = new TestLogger();
                var lockFilePath = Path.Combine(projectDir.FullName, "project.lock.json");

                var packageId = "PackageA";
                var packageVersion = "1.0.0-Beta";
                var packageAContext = new SimpleTestPackageContext(packageId, packageVersion);
                packageAContext.AddFile("lib/netstandard1.0/a.dll");

                SimpleTestPackageUtility.CreateFullPackage(sourceDir.FullName, packageAContext);

                // Act
                // Execute the first restore with the opposite lowercase setting.
                var requestA = new RestoreRequest(
                    spec,
                    sources.Select(x => Repository.Factory.GetCoreV3(x)),
                    packagesDir.FullName,
                    Enumerable.Empty<string>(),
                    logger)
                {
                    LockFilePath = lockFilePath,
                    IsLowercasePackagesDirectory = !isLowercase
                };
                var commandA = new RestoreCommand(requestA);
                var resultA = await commandA.ExecuteAsync();
                await resultA.CommitAsync(logger, CancellationToken.None);

                // Execute the second restore with the request lowercase setting.
                var requestB = new RestoreRequest(
                    spec,
                    sources.Select(x => Repository.Factory.GetCoreV3(x)),
                    packagesDir.FullName,
                    Enumerable.Empty<string>(),
                    logger)
                {
                    LockFilePath = lockFilePath,
                    IsLowercasePackagesDirectory = isLowercase
                };
                var commandB = new RestoreCommand(requestB);
                var resultB = await commandB.ExecuteAsync();
                await resultB.CommitAsync(logger, CancellationToken.None);

                // Assert
                // Commands should have succeeded.
                Assert.True(resultA.Success);
                Assert.True(resultB.Success);

                // The lock file library path should match the requested case.
                var libraryA = resultA
                    .LockFile
                    .Libraries
                    .FirstOrDefault(l => l.Name == packageId && l.Version.ToNormalizedString() == packageVersion);
                Assert.NotNull(libraryA);
                Assert.Equal(
                    PathUtility.GetPathWithForwardSlashes(resolverA.GetPackageDirectory(packageId, libraryA.Version)),
                    libraryA.Path);
                Assert.True(File.Exists(resolverA.GetPackageFilePath(packageId, libraryA.Version)));

                var libraryB = resultB
                    .LockFile
                    .Libraries
                    .FirstOrDefault(l => l.Name == packageId && l.Version.ToNormalizedString() == packageVersion);
                Assert.NotNull(libraryB);
                Assert.Equal(
                    PathUtility.GetPathWithForwardSlashes(resolverB.GetPackageDirectory(packageId, libraryB.Version)),
                    libraryB.Path);
                Assert.True(File.Exists(resolverB.GetPackageFilePath(packageId, libraryB.Version)));

                // The lock file on disk should match the second restore's library.
                var lockFileFormat = new LockFileFormat();
                var diskLockFile = lockFileFormat.Read(lockFilePath);
                var lockFileLibrary = diskLockFile
                    .Libraries
                    .FirstOrDefault(l => l.Name == packageId && l.Version.ToNormalizedString() == packageVersion);
                Assert.NotNull(lockFileLibrary);
                Assert.Equal(
                    PathUtility.GetPathWithForwardSlashes(resolverB.GetPackageDirectory(packageId, libraryB.Version)),
                    libraryB.Path);
                Assert.Equal(libraryB, lockFileLibrary);
            }
        }

        [Fact]
        public async Task RestoreCommand_FileUriV3Folder()
        {
            // Arrange
            var sources = new List<PackageSource>();

            // Both TxMs reference packageA, but they are different types.
            // Verify that the reference does not show up under libraries.
            var project1Json = @"
            {
              ""version"": ""1.0.0"",
              ""description"": """",
              ""authors"": [ ""author"" ],
              ""tags"": [ """" ],
              ""projectUrl"": """",
              ""licenseUrl"": """",
              ""frameworks"": {
                ""netstandard1.3"": {
                    ""dependencies"": {
                        ""packageA"": ""4.0.0""
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
                sources.Add(new PackageSource(UriUtility.CreateSourceUri(packageSource.FullName).AbsoluteUri));

                File.WriteAllText(Path.Combine(project1.FullName, "project.json"), project1Json);

                var specPath1 = Path.Combine(project1.FullName, "project.json");
                var spec1 = JsonPackageSpecReader.GetPackageSpec(project1Json, "project1", specPath1);

                var logger = new TestLogger();
                var request = new RestoreRequest(spec1, sources, packagesDir.FullName, logger);

                request.LockFilePath = Path.Combine(project1.FullName, "project.lock.json");

                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    packageSource.FullName, 
                    new PackageIdentity("packageA", NuGetVersion.Parse("4.0.0")));

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                var lockFile = result.LockFile;
                await result.CommitAsync(logger, CancellationToken.None);

                // Assert
                Assert.True(result.Success);
                Assert.Equal(1, lockFile.Libraries.Count);
            }
        }

        [Fact]
        public async Task RestoreCommand_FileUriV2Folder()
        {
            // Arrange
            var sources = new List<PackageSource>();

            // Both TxMs reference packageA, but they are different types.
            // Verify that the reference does not show up under libraries.
            var project1Json = @"
            {
              ""version"": ""1.0.0"",
              ""description"": """",
              ""authors"": [ ""author"" ],
              ""tags"": [ """" ],
              ""projectUrl"": """",
              ""licenseUrl"": """",
              ""frameworks"": {
                ""netstandard1.3"": {
                    ""dependencies"": {
                        ""packageA"": ""4.0.0""
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
                sources.Add(new PackageSource(UriUtility.CreateSourceUri(packageSource.FullName).AbsoluteUri));

                File.WriteAllText(Path.Combine(project1.FullName, "project.json"), project1Json);

                var specPath1 = Path.Combine(project1.FullName, "project.json");
                var spec1 = JsonPackageSpecReader.GetPackageSpec(project1Json, "project1", specPath1);

                var logger = new TestLogger();
                var request = new RestoreRequest(spec1, sources, packagesDir.FullName, logger);

                request.LockFilePath = Path.Combine(project1.FullName, "project.lock.json");

                SimpleTestPackageUtility.CreateFullPackage(packageSource.FullName, "packageA", "4.0.0");

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                var lockFile = result.LockFile;
                await result.CommitAsync(logger, CancellationToken.None);

                // Assert
                Assert.True(result.Success);
                Assert.Equal(1, lockFile.Libraries.Count);
            }
        }

        [Fact]
        public async Task RestoreCommand_FindInV2FolderWithDifferentCasing()
        {
            // Arrange
            var sources = new List<PackageSource>();

            // Both TxMs reference packageA, but they are different types.
            // Verify that the reference does not show up under libraries.
            var project1Json = @"
            {
              ""version"": ""1.0.0"",
              ""description"": """",
              ""authors"": [ ""author"" ],
              ""tags"": [ """" ],
              ""projectUrl"": """",
              ""licenseUrl"": """",
              ""frameworks"": {
                ""netstandard1.3"": {
                    ""dependencies"": {
                        ""PACKAGEA"": ""4.0.0""
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

                SimpleTestPackageUtility.CreateFullPackage(packageSource.FullName, "packageA", "4.0.0");

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                var lockFile = result.LockFile;
                await result.CommitAsync(logger, CancellationToken.None);

                // Assert
                Assert.True(result.Success);
                Assert.Equal(1, lockFile.Libraries.Count);
            }
        }

        [Fact]
        public async Task RestoreCommand_ReferenceWithSameNameDifferentCasing()
        {
            // Arrange
            var sources = new List<PackageSource>();

            // Both TxMs reference packageA, but they are different types.
            // Verify that the reference does not show up under libraries.
            var project1Json = @"
            {
              ""version"": ""1.0.0"",
              ""description"": """",
              ""authors"": [ ""author"" ],
              ""tags"": [ """" ],
              ""projectUrl"": """",
              ""licenseUrl"": """",
              ""frameworks"": {
                ""netstandard1.3"": {
                    ""dependencies"": {
                        ""packageA"": ""4.0.0""
                    }
                }
              }
            }";

            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var packagesDir = new DirectoryInfo(Path.Combine(workingDir, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(workingDir, "packageSource"));
                var project1 = new DirectoryInfo(Path.Combine(workingDir, "projects", "PROJECT1"));
                packagesDir.Create();
                packageSource.Create();
                project1.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                File.WriteAllText(Path.Combine(project1.FullName, "project.json"), project1Json);

                var specPath1 = Path.Combine(project1.FullName, "project.json");
                var spec1 = JsonPackageSpecReader.GetPackageSpec(project1Json, "PROJECT1", specPath1);

                var logger = new TestLogger();
                var request = new RestoreRequest(spec1, sources, packagesDir.FullName, logger);

                request.LockFilePath = Path.Combine(project1.FullName, "project.lock.json");

                var aContext = new SimpleTestPackageContext()
                {
                    Id = "packageA",
                    Version = "4.0.0"
                };

                aContext.Dependencies.Add(new SimpleTestPackageContext("proJect1"));

                SimpleTestPackageUtility.CreateFullPackage(packageSource.FullName, "projeCt1", "4.0.0");

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                var lockFile = result.LockFile;
                await result.CommitAsync(logger, CancellationToken.None);

                // Assert
                // Verify no stack overflows from circular dependencies
                Assert.False(result.Success);
            }
        }

        [Fact]
        public async Task RestoreCommand_ImportsWithHigherVersion_NoFallback()
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
                ""netstandard1.3"": {
                    ""imports"": [ ""netstandard1.5"" ],
                    ""dependencies"": {
                        ""packageA"": ""1.0.0""
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

                var packageAContext = new SimpleTestPackageContext("packageA");
                packageAContext.AddFile("lib/netstandard1.0/a.dll");
                packageAContext.AddFile("lib/netstandard1.5/a.dll");
                packageAContext.AddFile("lib/net46/a.dll");

                SimpleTestPackageUtility.CreateFullPackage(packageSource.FullName, packageAContext);

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                var lockFile = result.LockFile;
                await result.CommitAsync(logger, CancellationToken.None);

                var targetLib = lockFile.GetTarget(NuGetFramework.Parse("netstandard1.3"), null)
                    .Libraries
                    .Single(library => library.Name == "packageA");

                var compile = targetLib.CompileTimeAssemblies.Single();

                // Assert
                Assert.True(result.Success);
                Assert.Equal("lib/netstandard1.0/a.dll", compile.Path);
            }
        }

        [Fact]
        public async Task RestoreCommand_ImportsWithHigherVersion_Fallback()
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
                ""netstandard1.3"": {
                    ""imports"": [ ""netstandard1.5"" ],
                    ""dependencies"": {
                        ""packageA"": ""1.0.0""
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

                var packageAContext = new SimpleTestPackageContext("packageA");
                packageAContext.AddFile("lib/netstandard1.5/a.dll");
                packageAContext.AddFile("lib/netstandard1.6/a.dll");
                packageAContext.AddFile("lib/net46/a.dll");

                SimpleTestPackageUtility.CreateFullPackage(packageSource.FullName, packageAContext);

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                var lockFile = result.LockFile;
                await result.CommitAsync(logger, CancellationToken.None);

                var targetLib = lockFile.GetTarget(NuGetFramework.Parse("netstandard1.3"), null)
                    .Libraries
                    .Single(library => library.Name == "packageA");

                var compile = targetLib.CompileTimeAssemblies.Single();

                // Assert
                Assert.True(result.Success);
                Assert.Equal("lib/netstandard1.5/a.dll", compile.Path);
            }
        }

        [Fact]
        public async Task RestoreCommand_ImportsWithHigherVersion_MultiFallback()
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
                ""netstandard1.0"": {
                    ""imports"": [ ""netstandard1.1"", ""netstandard1.2"", ""dnxcore50""  ],
                    ""dependencies"": {
                        ""packageA"": ""1.0.0""
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

                var packageAContext = new SimpleTestPackageContext("packageA");
                packageAContext.AddFile("lib/netstandard1.2/a.dll");
                packageAContext.AddFile("lib/netstandard1.5/a.dll");
                packageAContext.AddFile("lib/net46/a.dll");

                SimpleTestPackageUtility.CreateFullPackage(packageSource.FullName, packageAContext);

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                var lockFile = result.LockFile;
                await result.CommitAsync(logger, CancellationToken.None);

                var targetLib = lockFile.GetTarget(NuGetFramework.Parse("netstandard1.0"), null)
                    .Libraries
                    .Single(library => library.Name == "packageA");

                var compile = targetLib.CompileTimeAssemblies.Single();

                // Assert
                Assert.True(result.Success);
                Assert.Equal("lib/netstandard1.2/a.dll", compile.Path);
            }
        }

        [Fact]
        public async Task RestoreCommand_ImportsNoMatch()
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
                ""netstandard1.0"": {
                    ""imports"": [ ""netstandard1.1"", ""netstandard1.2""  ],
                    ""dependencies"": {
                        ""packageA"": ""1.0.0""
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

                var packageAContext = new SimpleTestPackageContext("packageA");
                packageAContext.AddFile("lib/netstandard1.5/a.dll");
                packageAContext.AddFile("lib/netstandard1.6/a.dll");
                packageAContext.AddFile("lib/net46/a.dll");

                SimpleTestPackageUtility.CreateFullPackage(packageSource.FullName, packageAContext);

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                var lockFile = result.LockFile;
                await result.CommitAsync(logger, CancellationToken.None);

                var targetLib = lockFile.GetTarget(NuGetFramework.Parse("netstandard1.0"), null)
                                    .Libraries
                                    .Single(library => library.Name == "packageA");

                var compileItems = targetLib.CompileTimeAssemblies;

                // Assert
                Assert.False(result.Success);
                Assert.Equal(0, compileItems.Count);
            }
        }

        [Fact]
        public async Task RestoreCommand_PathInPackageLibrary()
        {
            // Arrange
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var packagesDir = new DirectoryInfo(Path.Combine(workingDir, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(workingDir, "packageSource"));
                var project1 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project1"));
                packagesDir.Create();
                packageSource.Create();
                project1.Create();

                var sources = new List<PackageSource>();
                sources.Add(new PackageSource(packageSource.FullName));

                var project1Json = @"
                {
                  ""frameworks"": {
                    ""netstandard1.0"": {
                      ""dependencies"": {
                        ""packageA"": ""1.0.0""
                      }
                    }
                  }
                }";

                File.WriteAllText(Path.Combine(project1.FullName, "project.json"), project1Json);

                var specPath1 = Path.Combine(project1.FullName, "project.json");
                var spec1 = JsonPackageSpecReader.GetPackageSpec(project1Json, "project1", specPath1);

                var logger = new TestLogger();
                var request = new RestoreRequest(spec1, sources, packagesDir.FullName, logger);
                request.LockFilePath = Path.Combine(project1.FullName, "project.lock.json");

                var packageAContext = new SimpleTestPackageContext("packageA");
                packageAContext.AddFile("lib/netstandard1.0/a.dll");

                SimpleTestPackageUtility.CreateFullPackage(packageSource.FullName, packageAContext);

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                var lockFile = result.LockFile;

                // Assert
                Assert.True(result.Success);
                var library = lockFile.Libraries.FirstOrDefault(l => l.Name == "packageA");
                Assert.NotNull(library);
                Assert.Equal("packagea/1.0.0", library.Path);
            }
        }

        [Fact]
        public async Task RestoreCommand_PackageWithSameName()
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
                ""project1"": { ""version"": ""1.0.0"", ""target"": ""package"" }
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
                packagesDir.Create();
                packageSource.Create();
                project1.Create();

                File.WriteAllText(Path.Combine(project1.FullName, "project.json"), project1Json);

                SimpleTestPackageUtility.CreateFullPackage(packageSource.FullName, "project1", "1.0.0");

                var specPath1 = Path.Combine(project1.FullName, "project.json");
                var spec1 = JsonPackageSpecReader.GetPackageSpec(project1Json, "project1", specPath1);

                var logger = new TestLogger();
                var request = new RestoreRequest(spec1, sources, packagesDir.FullName, logger);

                request.LockFilePath = Path.Combine(project1.FullName, "project.lock.json");

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                var lockFile = result.LockFile;
                await result.CommitAsync(logger, CancellationToken.None);

                // Assert
                Assert.False(result.Success);
                Assert.Equal(1, result.GetAllUnresolved().Count);
                Assert.True(logger.ErrorMessages.Contains("Unable to resolve 'project1 (>= 1.0.0)' for '.NETFramework,Version=v4.5'."));
            }
        }

        [Fact]
        public async Task RestoreCommand_PackageAndReferenceWithSameNameAndVersion()
        {
            // Arrange
            var sources = new List<PackageSource>();

            // Both TxMs reference packageA, but they are different types.
            // Verify that the reference does not show up under libraries.
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
                    ""frameworkAssemblies"": {
                         ""packageA"": ""4.0.0""
                    }
                },
                ""netstandard1.3"": {
                    ""dependencies"": {
                        ""packageA"": ""4.0.0""
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

                SimpleTestPackageUtility.CreateFullPackage(packageSource.FullName, "packageA", "4.0.0");

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                var lockFile = result.LockFile;
                await result.CommitAsync(logger, CancellationToken.None);

                // Assert
                Assert.True(result.Success);
                Assert.Equal(1, lockFile.Libraries.Count);
            }
        }

        [Fact]
        public async Task RestoreCommand_RestoreProjectWithNoDependencies()
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
        public async Task RestoreCommand_RestoresTools()
        {
            // Arrange
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var tc = new ToolTestContext(workingDir)
                {
                    ProjectJson = @"
                    {
                        ""frameworks"": {
                            ""net45"": { }
                        },
                        ""tools"": {
                            ""packageB"": ""*""
                        }
                    }"
                };

                var packageA = new SimpleTestPackageContext("packageA");
                packageA.AddFile("lib/netstandard1.3/a.dll");

                var packageB = new SimpleTestPackageContext("packageB");
                packageB.AddFile("lib/netstandard1.4/b.dll");
                packageB.Dependencies.Add(packageA);

                SimpleTestPackageUtility.CreatePackages(tc.PackageSource.FullName, packageA, packageB);

                tc.Initialize();

                // Act
                var result = await tc.Command.ExecuteAsync();
                await result.CommitAsync(tc.Logger, CancellationToken.None);

                // Assert
                Assert.True(
                    result.Success,
                    "The command did not succeed. Error messages: "
                    + Environment.NewLine + tc.Logger.ShowErrors());
                Assert.Equal(1, result.ToolRestoreResults.Count());

                var toolResult = result.ToolRestoreResults.First();
                Assert.NotNull(toolResult.LockFilePath);
                Assert.True(
                    File.Exists(toolResult.LockFilePath),
                    $"The tool lock file at {toolResult.LockFilePath} does not exist.");
                Assert.NotNull(toolResult.LockFile);
                Assert.Equal(1, toolResult.LockFile.Targets.Count);

                var target = toolResult.LockFile.Targets[0];
                Assert.Null(target.RuntimeIdentifier);
                Assert.Equal(FrameworkConstants.CommonFrameworks.NetCoreApp10, target.TargetFramework);
                Assert.Equal(2, target.Libraries.Count);

                var library = target.Libraries.First(l => l.Name == "packageB");
                Assert.NotNull(library);
                Assert.Equal("lib/netstandard1.4/b.dll", library.RuntimeAssemblies[0].Path);
            }
        }

        [Fact]
        public async Task RestoreCommand_DoesNotRedoRestoreToolsWithValidLockFile()
        {
            // Arrange
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var tc = new ToolTestContext(workingDir)
                {
                    ProjectJson = @"
                    {
                        ""frameworks"": {
                            ""net45"": { }
                        },
                        ""tools"": {
                            ""packageA"": ""*""
                        }
                    }",
                };

                var packageA = new SimpleTestPackageContext("packageA");
                packageA.AddFile("lib/netstandard1.3/a.dll");

                SimpleTestPackageUtility.CreatePackages(tc.PackageSource.FullName, packageA);

                tc.Initialize();

                // the first restore
                await (await tc.Command.ExecuteAsync()).CommitAsync(tc.Logger, CancellationToken.None);

                // reset
                tc.Logger = new TestLogger();
                tc.Request.Log = tc.Logger;
                tc.Initialize();
                tc.Request.ExistingLockFile = LockFileUtilities.GetLockFile(tc.Request.LockFilePath, tc.Logger);

                // Act
                var result = await tc.Command.ExecuteAsync();
                await result.CommitAsync(tc.Logger, CancellationToken.None);

                // Assert
                Assert.True(
                    result.Success,
                    "The command did not succeed. Error messages: "
                    + Environment.NewLine + tc.Logger.ShowErrors());
                Assert.Equal(1, result.ToolRestoreResults.Count());

                Assert.Contains(
                    $"Lock file has not changed. Skipping lock file write. Path: {result.LockFilePath}",
                    tc.Logger.Messages);

                var toolResult = result.ToolRestoreResults.First();
                Assert.Contains(
                    $"Tool lock file has not changed. Skipping lock file write. Path: {toolResult.LockFilePath}",
                    tc.Logger.Messages);
            }
        }

        [Fact]
        public async Task RestoreCommand_FailsCommandWhenToolRestoreFails()
        {
            // Arrange
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var tc = new ToolTestContext(workingDir)
                {
                    ProjectJson = @"
                    {
                        ""frameworks"": {
                            ""net45"": { }
                        },
                        ""tools"": {
                            ""packageA"": ""*""
                        }
                    }"
                };

                // The tool is not available on the source.

                tc.Initialize();

                // Act
                var result = await tc.Command.ExecuteAsync();
                await result.CommitAsync(tc.Logger, CancellationToken.None);

                // Assert
                Assert.False(result.Success,
                    "The command should not have succeeded. Messages: "
                    + Environment.NewLine + tc.Logger.ShowMessages());
                Assert.Equal(1, result.ToolRestoreResults.Count());

                var toolResult = result.ToolRestoreResults.First();
                Assert.Null(toolResult.LockFilePath);
                Assert.NotNull(toolResult.LockFile);
                Assert.Equal(1, toolResult.LockFile.Targets.Count);

                var target = toolResult.LockFile.Targets[0];
                Assert.Null(target.RuntimeIdentifier);
                Assert.Equal(FrameworkConstants.CommonFrameworks.NetCoreApp10, target.TargetFramework);
                Assert.Equal(0, target.Libraries.Count);
            }
        }

        [Fact]
        public async Task RestoreCommand_HandlesMultipleToolRestores()
        {
            // Arrange
            using (var testDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var tc = new ToolTestContext(testDirectory)
                {
                    ProjectJson = @"
                    {
                        ""frameworks"": {
                            ""net45"": { }
                        },
                        ""tools"": {
                            ""packageA"": ""*"",
                            ""packageB"": ""*"",
                            ""packageC"": ""*""
                        }
                    }"
                };

                var packageB = new SimpleTestPackageContext("packageB");
                packageB.AddFile("lib/netstandard1.3/a.dll");

                // packageA and packageC are not on the source
                SimpleTestPackageUtility.CreatePackages(tc.PackageSource.FullName, packageB);

                tc.Initialize();

                // Act
                var result = await tc.Command.ExecuteAsync();
                await result.CommitAsync(tc.Logger, CancellationToken.None);

                // Assert
                Assert.False(result.Success,
                    "The command should not have succeeded. Messages: "
                    + Environment.NewLine + tc.Logger.ShowMessages());
                Assert.Equal(3, result.ToolRestoreResults.Count());

                var packageAResult = result.ToolRestoreResults.ElementAt(0);
                Assert.Null(packageAResult.LockFilePath);
                Assert.NotNull(packageAResult.LockFile);
                Assert.Equal(1, packageAResult.LockFile.Targets.Count);
                Assert.False(packageAResult.Success, "packageA tool restore should not have succeeded.");

                var packageBResult = result.ToolRestoreResults.ElementAt(1);
                Assert.NotNull(packageBResult.LockFilePath);
                Assert.NotNull(packageBResult.LockFile);
                Assert.Equal(1, packageBResult.LockFile.Targets.Count);
                Assert.True(packageBResult.Success, "packageB tool restore should have succeeded.");

                var packageCResult = result.ToolRestoreResults.ElementAt(2);
                Assert.Null(packageCResult.LockFilePath);
                Assert.NotNull(packageCResult.LockFile);
                Assert.Equal(1, packageCResult.LockFile.Targets.Count);
                Assert.False(packageCResult.Success, "packageC tool restore should not have succeeded.");
            }
        }

        [Fact]
        public async Task RestoreCommand_MatchingToolImports()
        {
            // Arrange
            using (var testDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var tc = new ToolTestContext(testDirectory)
                {
                    ProjectJson = @"
                    {
                        ""frameworks"": {
                            ""net45"": { }
                        },
                        ""tools"": {
                            ""packageA"": {
                                ""version"": ""*"",
                                ""imports"": [ ""net40"", ""net46"" ]
                            }
                        }
                    }"
                };

                var packageA = new SimpleTestPackageContext("packageA");
                packageA.AddFile("lib/net45/a.dll");

                SimpleTestPackageUtility.CreatePackages(tc.PackageSource.FullName, packageA);

                tc.Initialize();

                // Act
                var result = await tc.Command.ExecuteAsync();
                await result.CommitAsync(tc.Logger, CancellationToken.None);

                // Assert
                Assert.True(
                    result.Success,
                    "The command did not succeed. Error messages: "
                    + Environment.NewLine + tc.Logger.ShowErrors());
                Assert.Equal(1, result.ToolRestoreResults.Count());

                var toolResult = result.ToolRestoreResults.First();
                Assert.NotNull(toolResult.LockFilePath);
                Assert.True(
                    File.Exists(toolResult.LockFilePath),
                    $"The tool lock file at {toolResult.LockFilePath} does not exist.");
                Assert.NotNull(toolResult.LockFile);
                Assert.Equal(1, toolResult.LockFile.Targets.Count);

                var target = toolResult.LockFile.Targets[0];
                Assert.Null(target.RuntimeIdentifier);
                Assert.Equal(
                    new FallbackFramework(
                        FrameworkConstants.CommonFrameworks.NetCoreApp10,
                        new[] { NuGetFramework.Parse("net40"), NuGetFramework.Parse("net46") }),
                    (FallbackFramework) target.TargetFramework);
                Assert.Equal(1, target.Libraries.Count);
                
                var library = target.Libraries.First(l => l.Name == "packageA");
                Assert.NotNull(library);
                Assert.Equal("lib/net45/a.dll", library.RuntimeAssemblies[0].Path);
            }
        }

        [Fact]
        public async Task RestoreCommand_NoMatchingToolImportsForTool()
        {
            // Arrange
            using (var testDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var tc = new ToolTestContext(testDirectory)
                {
                    ProjectJson = @"
                    {
                        ""frameworks"": {
                            ""net45"": { }
                        },
                        ""tools"": {
                            ""packageA"": {
                                ""version"": ""*"",
                                ""imports"": [ ""net40"", ""net46"" ]
                            }
                        }
                    }"
                };

                var packageA = new SimpleTestPackageContext("packageA");
                packageA.AddFile("lib/win8/a.dll");

                SimpleTestPackageUtility.CreatePackages(tc.PackageSource.FullName, packageA);

                tc.Initialize();

                // Act
                var result = await tc.Command.ExecuteAsync();
                await result.CommitAsync(tc.Logger, CancellationToken.None);

                // Assert
                Assert.False(
                    result.Success,
                    "The command should not have succeeded. Messages: "
                    + Environment.NewLine + tc.Logger.ShowMessages());
                Assert.Equal(1, result.ToolRestoreResults.Count());

                Assert.Contains(
                    "Package packageA 1.0.0 is not compatible with netcoreapp1.0 (.NETCoreApp,Version=v1.0). Package packageA 1.0.0 supports: win8 (Windows,Version=v8.0)",
                    tc.Logger.ErrorMessages);
                Assert.Contains(
                    "One or more packages are incompatible with .NETCoreApp,Version=v1.0.",
                    tc.Logger.ErrorMessages);

                var toolResult = result.ToolRestoreResults.First();
                Assert.NotNull(toolResult.LockFilePath);
                Assert.True(
                    File.Exists(toolResult.LockFilePath),
                    $"The tool lock file at {toolResult.LockFilePath} does not exist.");
                Assert.NotNull(toolResult.LockFile);
                Assert.Equal(1, toolResult.LockFile.Targets.Count);

                var target = toolResult.LockFile.Targets[0];
                Assert.Null(target.RuntimeIdentifier);
                Assert.Equal(
                    new FallbackFramework(
                        FrameworkConstants.CommonFrameworks.NetCoreApp10,
                        new[] { NuGetFramework.Parse("net40"), NuGetFramework.Parse("net46") }),
                    (FallbackFramework) target.TargetFramework);
                Assert.Equal(1, target.Libraries.Count);
                
                var library = target.Libraries.First(l => l.Name == "packageA");
                Assert.NotNull(library);
                Assert.Equal(0, library.RuntimeAssemblies.Count);
            }
        }

        [Fact]
        public async Task RestoreCommand_NoMatchingToolImportsForToolDependency()
        {
            // Arrange
            using (var testDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var tc = new ToolTestContext(testDirectory)
                {
                    ProjectJson = @"
                    {
                        ""frameworks"": {
                            ""net45"": { }
                        },
                        ""tools"": {
                            ""packageB"": {
                                ""version"": ""*""
                            }
                        }
                    }"
                };

                var packageA = new SimpleTestPackageContext("packageA");
                packageA.AddFile("lib/win8/a.dll");
                packageA.AddFile("lib/net40/a.dll");

                var packageB = new SimpleTestPackageContext("packageB");
                packageB.AddFile("lib/netstandard1.4/b.dll");
                packageB.Dependencies.Add(packageA);

                SimpleTestPackageUtility.CreatePackages(tc.PackageSource.FullName, packageA, packageB);

                tc.Initialize();

                // Act
                var result = await tc.Command.ExecuteAsync();
                await result.CommitAsync(tc.Logger, CancellationToken.None);

                // Assert
                Assert.False(
                    result.Success,
                    "The command should not have succeeded. Messages: "
                    + Environment.NewLine + tc.Logger.ShowMessages());
                Assert.Equal(1, result.ToolRestoreResults.Count());

                Assert.Contains(
                    "Package packageA 1.0.0 is not compatible with netcoreapp1.0 (.NETCoreApp,Version=v1.0). Package packageA 1.0.0 supports:" +
                    Environment.NewLine + "  - net40 (.NETFramework,Version=v4.0)" +
                    Environment.NewLine + "  - win8 (Windows,Version=v8.0)",
                    tc.Logger.ErrorMessages);
                Assert.Contains(
                    "One or more packages are incompatible with .NETCoreApp,Version=v1.0.",
                    tc.Logger.ErrorMessages);

                var toolResult = result.ToolRestoreResults.First();
                Assert.NotNull(toolResult.LockFilePath);
                Assert.True(
                    File.Exists(toolResult.LockFilePath),
                    $"The tool lock file at {toolResult.LockFilePath} does not exist.");
                Assert.NotNull(toolResult.LockFile);
                Assert.Equal(1, toolResult.LockFile.Targets.Count);

                var target = toolResult.LockFile.Targets[0];
                Assert.Null(target.RuntimeIdentifier);
                Assert.Equal(
                    FrameworkConstants.CommonFrameworks.NetCoreApp10,
                    target.TargetFramework);
                Assert.Equal(2, target.Libraries.Count);

                var libraryA = target.Libraries.First(l => l.Name == "packageA");
                Assert.NotNull(libraryA);
                Assert.Equal(0, libraryA.RuntimeAssemblies.Count);

                var libraryB = target.Libraries.First(l => l.Name == "packageB");
                Assert.NotNull(libraryB);
                Assert.Equal(1, libraryB.RuntimeAssemblies.Count);
            }
        }

        private class ToolTestContext
        {
            public ToolTestContext(TestDirectory testDirectory)
            {
                // data
                Sources = new List<PackageSource>();
                ProjectJson = @"
                {
                  ""frameworks"": {
                    ""net45"": { }
                  },
                  ""tools"": {
                    ""packageB"": ""*""
                  }
                }";


                PackagesDir = new DirectoryInfo(Path.Combine(testDirectory, "globalPackages"));
                PackageSource = new DirectoryInfo(Path.Combine(testDirectory, "packageSource"));
                Project = new DirectoryInfo(Path.Combine(testDirectory, "projects", "project1"));
                PackagesDir.Create();
                PackageSource.Create();
                Project.Create();
                Sources.Add(new PackageSource(PackageSource.FullName));
                Logger = new TestLogger();
            }

            public DirectoryInfo PackageSource { get; }
            public TestLogger Logger { get; set; }
            public string ProjectJson { private get; set; }
            public RestoreRequest Request { get; set;  }
            public RestoreCommand Command { get; private set;  }

            private DirectoryInfo Project { get; }
            private DirectoryInfo PackagesDir { get; }
            private List<PackageSource> Sources { get; }

            public void Initialize()
            {
                File.WriteAllText(Path.Combine(Project.FullName, "project.json"), ProjectJson);

                var specPath1 = Path.Combine(Project.FullName, "project.json");
                var spec1 = JsonPackageSpecReader.GetPackageSpec(ProjectJson, "project1", specPath1);
                Request = new RestoreRequest(spec1, Sources, PackagesDir.FullName, Logger);

                Request.LockFilePath = Path.Combine(Project.FullName, "project.lock.json");
                Command = new RestoreCommand(Request);
            }
        }
    }
}
