// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.Signing;
using NuGet.ProjectModel;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Test.Utility;
using Test.Utility.Signing;
using Xunit;

namespace NuGet.Commands.Test
{
    [Collection(nameof(NotThreadSafeResourceCollection))]
    public class RestoreCommandTests
    {
        private static SignedPackageVerifierSettings DefaultSettings = SignedPackageVerifierSettings.GetDefault(TestEnvironmentVariableReader.EmptyInstance);

        [Fact]
        public async Task RestoreCommand_VerifyRuntimeSpecificAssetsAreNotIncludedForCompile_RuntimeOnlyAsync()
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
                        ""packageA"": ""1.0.0""
                    }
                }
              },
              ""runtimes"": {
                ""win7-x64"": {}
              }
            }";

            using (var workingDir = TestDirectory.Create())
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
                var request = new TestRestoreRequest(spec1, sources, packagesDir.FullName, logger)
                {
                    LockFilePath = Path.Combine(project1.FullName, "project.lock.json")
                };

                var packageAContext = new SimpleTestPackageContext("packageA");
                packageAContext.AddFile("runtimes/win7-x64/lib/netstandard1.5/a.dll");

                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, packageAContext);

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                var lockFile = result.LockFile;
                await result.CommitAsync(logger, CancellationToken.None);

                var targetLib = lockFile.GetTarget(NuGetFramework.Parse("netstandard1.5"), "win7-x64")
                    .Libraries
                    .Single(library => library.Name == "packageA");

                // Assert
                Assert.True(result.Success);
                Assert.Equal(0, targetLib.CompileTimeAssemblies.Count);
                Assert.Equal("runtimes/win7-x64/lib/netstandard1.5/a.dll", targetLib.RuntimeAssemblies.Single());
            }
        }

        [Fact]
        public async Task RestoreCommand_VerifyRuntimeSpecificAssetsAreNotIncludedForCompile_RuntimeAndRefAsync()
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
                        ""packageA"": ""1.0.0""
                    }
                }
              },
              ""runtimes"": {
                ""win7-x64"": {}
              }
            }";

            using (var workingDir = TestDirectory.Create())
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
                var request = new TestRestoreRequest(spec1, sources, packagesDir.FullName, logger)
                {
                    LockFilePath = Path.Combine(project1.FullName, "project.lock.json")
                };

                var packageAContext = new SimpleTestPackageContext("packageA");
                packageAContext.AddFile("ref/netstandard1.5/a.dll");
                packageAContext.AddFile("runtimes/win7-x64/lib/netstandard1.5/a.dll");

                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, packageAContext);

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                var lockFile = result.LockFile;
                await result.CommitAsync(logger, CancellationToken.None);

                var targetLib = lockFile.GetTarget(NuGetFramework.Parse("netstandard1.5"), "win7-x64")
                    .Libraries
                    .Single(library => library.Name == "packageA");

                // Assert
                Assert.True(result.Success);
                Assert.Equal("ref/netstandard1.5/a.dll", targetLib.CompileTimeAssemblies.Single());
                Assert.Equal("runtimes/win7-x64/lib/netstandard1.5/a.dll", targetLib.RuntimeAssemblies.Single());
            }
        }

        [Fact]
        public async Task RestoreCommand_CompileAssetsWithBothRefAndLib_VerifyRefWinsAsync()
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
                        ""packageA"": ""1.0.0""
                    }
                }
              },
              ""runtimes"": {
                ""win7-x64"": {}
              }
            }";

            using (var workingDir = TestDirectory.Create())
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
                var request = new TestRestoreRequest(spec1, sources, packagesDir.FullName, logger)
                {
                    LockFilePath = Path.Combine(project1.FullName, "project.lock.json")
                };

                var packageAContext = new SimpleTestPackageContext("packageA");
                packageAContext.AddFile("ref/netstandard1.5/a.dll");
                packageAContext.AddFile("lib/netstandard1.5/a.dll");

                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, packageAContext);

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                var lockFile = result.LockFile;
                await result.CommitAsync(logger, CancellationToken.None);

                var targetLib = lockFile.GetTarget(NuGetFramework.Parse("netstandard1.5"), "win7-x64")
                    .Libraries
                    .Single(library => library.Name == "packageA");

                var compile = targetLib.CompileTimeAssemblies.OrderBy(s => s).ToArray();

                // Assert
                Assert.True(result.Success);
                Assert.Equal(1, compile.Length);
                Assert.Equal("ref/netstandard1.5/a.dll", compile[0]);
                Assert.Equal("lib/netstandard1.5/a.dll", targetLib.RuntimeAssemblies.Single());
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task RestoreCommand_ObservesLowercaseFlagAsync(bool isLowercase)
        {
            // Arrange
            using (var workingDir = TestDirectory.Create())
            {
                var packagesDir = new DirectoryInfo(Path.Combine(workingDir, "globalPackages"));
                var sourceDir = new DirectoryInfo(Path.Combine(workingDir, "packageSource"));
                var projectDir = new DirectoryInfo(Path.Combine(workingDir, "projects", "project1"));
                packagesDir.Create();
                sourceDir.Create();
                projectDir.Create();

                var resolver = new VersionFolderPathResolver(packagesDir.FullName, isLowercase);

                var sources = new List<string>
                {
                    sourceDir.FullName
                };

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
                var request = new TestRestoreRequest(
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

                await SimpleTestPackageUtility.CreateFullPackageAsync(sourceDir.FullName, packageAContext);

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
        public async Task RestoreCommand_WhenSwitchingBetweenLowercaseSettings_LockFileAlwaysRespectsLatestSettingAsync(bool isLowercase)
        {
            // Arrange
            using (var workingDir = TestDirectory.Create())
            {
                var packagesDir = new DirectoryInfo(Path.Combine(workingDir, "globalPackages"));
                var sourceDir = new DirectoryInfo(Path.Combine(workingDir, "packageSource"));
                var projectDir = new DirectoryInfo(Path.Combine(workingDir, "projects", "project1"));
                packagesDir.Create();
                sourceDir.Create();
                projectDir.Create();

                var resolverA = new VersionFolderPathResolver(packagesDir.FullName, !isLowercase);
                var resolverB = new VersionFolderPathResolver(packagesDir.FullName, isLowercase);

                var sources = new List<string>
                {
                    sourceDir.FullName
                };

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
                var lockFileFormat = new LockFileFormat();

                var packageId = "PackageA";
                var packageVersion = "1.0.0-Beta";
                var packageAContext = new SimpleTestPackageContext(packageId, packageVersion);
                packageAContext.AddFile("lib/netstandard1.0/a.dll");

                await SimpleTestPackageUtility.CreateFullPackageAsync(sourceDir.FullName, packageAContext);

                // Act
                // Execute the first restore with the opposite lowercase setting.
                var requestA = new TestRestoreRequest(
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
                var requestB = new TestRestoreRequest(
                    spec,
                    sources.Select(x => Repository.Factory.GetCoreV3(x)),
                    packagesDir.FullName,
                    Enumerable.Empty<string>(),
                    logger)
                {
                    LockFilePath = lockFilePath,
                    IsLowercasePackagesDirectory = isLowercase,
                    ExistingLockFile = lockFileFormat.Read(lockFilePath)
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
        public async Task RestoreCommand_FileUriV3FolderAsync()
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

            using (var workingDir = TestDirectory.Create())
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
                var request = new TestRestoreRequest(spec1, sources, packagesDir.FullName, logger)
                {
                    LockFilePath = Path.Combine(project1.FullName, "project.lock.json")
                };

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
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
        public async Task RestoreCommand_FileUriV2FolderAsync()
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

            using (var workingDir = TestDirectory.Create())
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
                var request = new TestRestoreRequest(spec1, sources, packagesDir.FullName, logger)
                {
                    LockFilePath = Path.Combine(project1.FullName, "project.lock.json")
                };

                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageA", "4.0.0");

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
        public async Task RestoreCommand_FindInV2FolderWithDifferentCasingAsync()
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

            using (var workingDir = TestDirectory.Create())
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
                var request = new TestRestoreRequest(spec1, sources, packagesDir.FullName, logger)
                {
                    LockFilePath = Path.Combine(project1.FullName, "project.lock.json")
                };

                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageA", "4.0.0");

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
        public async Task RestoreCommand_ReferenceWithSameNameDifferentCasingAsync()
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

            using (var workingDir = TestDirectory.Create())
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
                var request = new TestRestoreRequest(spec1, sources, packagesDir.FullName, logger)
                {
                    LockFilePath = Path.Combine(project1.FullName, "project.lock.json")
                };

                var aContext = new SimpleTestPackageContext()
                {
                    Id = "packageA",
                    Version = "4.0.0"
                };

                aContext.Dependencies.Add(new SimpleTestPackageContext("proJect1"));

                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "projeCt1", "4.0.0");

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
        public async Task RestoreCommand_ImportsWithHigherVersion_NoFallbackAsync()
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

            using (var workingDir = TestDirectory.Create())
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
                var request = new TestRestoreRequest(spec1, sources, packagesDir.FullName, logger)
                {
                    LockFilePath = Path.Combine(project1.FullName, "project.lock.json")
                };

                var packageAContext = new SimpleTestPackageContext("packageA");
                packageAContext.AddFile("lib/netstandard1.0/a.dll");
                packageAContext.AddFile("lib/netstandard1.5/a.dll");
                packageAContext.AddFile("lib/net46/a.dll");

                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, packageAContext);

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
        public async Task RestoreCommand_ImportsWithHigherVersion_FallbackAsync()
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

            using (var workingDir = TestDirectory.Create())
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
                var request = new TestRestoreRequest(spec1, sources, packagesDir.FullName, logger)
                {
                    LockFilePath = Path.Combine(project1.FullName, "project.lock.json")
                };

                var packageAContext = new SimpleTestPackageContext("packageA");
                packageAContext.AddFile("lib/netstandard1.5/a.dll");
                packageAContext.AddFile("lib/netstandard1.6/a.dll");
                packageAContext.AddFile("lib/net46/a.dll");

                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, packageAContext);

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
        public async Task RestoreCommand_ImportsWithHigherVersion_MultiFallbackAsync()
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

            using (var workingDir = TestDirectory.Create())
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
                var request = new TestRestoreRequest(spec1, sources, packagesDir.FullName, logger)
                {
                    LockFilePath = Path.Combine(project1.FullName, "project.lock.json")
                };

                var packageAContext = new SimpleTestPackageContext("packageA");
                packageAContext.AddFile("lib/netstandard1.2/a.dll");
                packageAContext.AddFile("lib/netstandard1.5/a.dll");
                packageAContext.AddFile("lib/net46/a.dll");

                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, packageAContext);

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
        public async Task RestoreCommand_ImportsNoMatchAsync()
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

            using (var workingDir = TestDirectory.Create())
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
                var request = new TestRestoreRequest(spec1, sources, packagesDir.FullName, logger)
                {
                    LockFilePath = Path.Combine(project1.FullName, "project.lock.json")
                };

                var packageAContext = new SimpleTestPackageContext("packageA");
                packageAContext.AddFile("lib/netstandard1.5/a.dll");
                packageAContext.AddFile("lib/netstandard1.6/a.dll");
                packageAContext.AddFile("lib/net46/a.dll");

                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, packageAContext);

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

#if IS_SIGNING_SUPPORTED
        [Fact]
        public async Task RestoreCommand_InvalidSignedPackageAsync()
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
                ""net46"": {
                    ""dependencies"": {
                        ""packageA"": ""1.0.0""
                    }
                }
              }
            }";

            using (var workingDir = TestDirectory.Create())
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

                var signedPackageVerifier = new Mock<IPackageSignatureVerifier>(MockBehavior.Strict);

                signedPackageVerifier.Setup(x => x.VerifySignaturesAsync(
                    It.IsAny<ISignedPackageReader>(),
                    It.Is<SignedPackageVerifierSettings>(s => SigningTestUtility.AreVerifierSettingsEqual(s, DefaultSettings)),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Guid>())).
                    ReturnsAsync(new VerifySignaturesResult(isValid: false, isSigned: true));

                var clientPolicyContext = ClientPolicyContext.GetClientPolicy(NullSettings.Instance, logger);
                var request = new TestRestoreRequest(spec1, sources, packagesDir.FullName, clientPolicyContext, logger)
                {
                    LockFilePath = Path.Combine(project1.FullName, "project.lock.json"),
                    SignedPackageVerifier = signedPackageVerifier.Object
                };

                var packageAContext = new SimpleTestPackageContext("packageA");
                packageAContext.AddFile("lib/net46/a.dll");

                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, packageAContext);

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();

                // Assert
                Assert.False(result.Success);
            }
        }

        [Fact]
        public async Task RestoreCommand_SignedPackageAsync()
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
                ""net46"": {
                    ""dependencies"": {
                        ""packageA"": ""1.0.0""
                    }
                }
              }
            }";

            using (var workingDir = TestDirectory.Create())
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

                var signedPackageVerifier = new Mock<IPackageSignatureVerifier>(MockBehavior.Strict);

                signedPackageVerifier.Setup(x => x.VerifySignaturesAsync(
                    It.IsAny<ISignedPackageReader>(),
                    It.Is<SignedPackageVerifierSettings>(s => SigningTestUtility.AreVerifierSettingsEqual(s, DefaultSettings)),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Guid>())).
                    ReturnsAsync(new VerifySignaturesResult(isValid: true, isSigned: true));

                var clientPolicyContext = ClientPolicyContext.GetClientPolicy(NullSettings.Instance, logger);
                var request = new TestRestoreRequest(spec1, sources, packagesDir.FullName, clientPolicyContext, logger)
                {
                    LockFilePath = Path.Combine(project1.FullName, "project.lock.json"),
                    SignedPackageVerifier = signedPackageVerifier.Object
                };

                var packageAContext = new SimpleTestPackageContext("packageA");
                packageAContext.AddFile("lib/net46/a.dll");

                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, packageAContext);

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();

                // Assert
                Assert.True(result.Success);
            }
        }
#endif

        [Fact]
        public async Task RestoreCommand_PathInPackageLibraryAsync()
        {
            // Arrange
            using (var workingDir = TestDirectory.Create())
            {
                var packagesDir = new DirectoryInfo(Path.Combine(workingDir, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(workingDir, "packageSource"));
                var project1 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project1"));
                packagesDir.Create();
                packageSource.Create();
                project1.Create();

                var sources = new List<PackageSource>
                {
                    new PackageSource(packageSource.FullName)
                };

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
                var request = new TestRestoreRequest(spec1, sources, packagesDir.FullName, logger)
                {
                    LockFilePath = Path.Combine(project1.FullName, "project.lock.json")
                };

                var packageAContext = new SimpleTestPackageContext("packageA");
                packageAContext.AddFile("lib/netstandard1.0/a.dll");

                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, packageAContext);

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
        public async Task RestoreCommand_PackageWithSameNameAsync()
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

            using (var workingDir = TestDirectory.Create())
            {
                var packagesDir = new DirectoryInfo(Path.Combine(workingDir, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(workingDir, "packageSource"));
                var project1 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project1"));
                packagesDir.Create();
                packageSource.Create();
                project1.Create();

                File.WriteAllText(Path.Combine(project1.FullName, "project.json"), project1Json);

                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "project1", "1.0.0");

                var specPath1 = Path.Combine(project1.FullName, "project.json");
                var spec1 = JsonPackageSpecReader.GetPackageSpec(project1Json, "project1", specPath1);

                var logger = new TestLogger();
                var request = new TestRestoreRequest(spec1, sources, packagesDir.FullName, logger)
                {
                    LockFilePath = Path.Combine(project1.FullName, "project.lock.json")
                };

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                var lockFile = result.LockFile;
                await result.CommitAsync(logger, CancellationToken.None);

                // Assert
                Assert.False(result.Success);
                Assert.True(logger.ErrorMessages.Any(s => s.Contains("Cycle detected")));
            }
        }

        [Fact]
        public async Task RestoreCommand_PackageAndReferenceWithSameNameAndVersionAsync()
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

            using (var workingDir = TestDirectory.Create())
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
                var request = new TestRestoreRequest(spec1, sources, packagesDir.FullName, logger)
                {
                    LockFilePath = Path.Combine(project1.FullName, "project.lock.json")
                };

                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageA", "4.0.0");

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
        public async Task RestoreCommand_RestoreProjectWithNoDependenciesAsync()
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

            using (var workingDir = TestDirectory.Create())
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
                var request = new TestRestoreRequest(spec1, sources, packagesDir.FullName, logger)
                {
                    LockFilePath = Path.Combine(project1.FullName, "project.lock.json")
                };

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
        public async Task RestoreCommand_MinimalProjectWithAdditionalMessages_WritesAssetsFileWithMessages()
        {
            // Arrange
            var sources = new List<PackageSource>();

            using (var workingDir = TestDirectory.Create())
            {
                var packagesDir = new DirectoryInfo(Path.Combine(workingDir, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(workingDir, "packageSource"));
                var project1 = new DirectoryInfo(Path.Combine(workingDir, "projects", "project1"));
                var project1Obj = new DirectoryInfo(Path.Combine(project1.FullName, "obj"));
                packagesDir.Create();
                packageSource.Create();
                project1.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                var dgspec1 = CreateMinimalDependencyGraphSpec(Path.Combine(project1.FullName, "project1.csproj"), project1Obj.FullName);
                var spec1 = dgspec1.Projects[0];

                var logger = new TestLogger();
                var request = new TestRestoreRequest(spec1, sources, packagesDir.FullName, logger)
                {
                    AdditionalMessages = new List<IAssetsLogMessage>()
                    {
                        new AssetsLogMessage(LogLevel.Error, NuGetLogCode.NU1105, "Test error")
                    }
                };

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                var lockFile = result.LockFile;
                await result.CommitAsync(logger, CancellationToken.None);

                // Assert
                Assert.False(result.Success);
                Assert.Equal(1, lockFile.LogMessages.Count);
            }
        }

        [Fact]
        public async Task RestoreCommand_CentralVersion_ErrorWhenDependenciesHaveVersion()
        {
            // Arrange
            var dependencyBar = new LibraryDependency(new LibraryRange("bar", VersionRange.Parse("3.0.0"), LibraryDependencyTarget.All),
               LibraryDependencyType.Default,
               LibraryIncludeFlags.All,
               LibraryIncludeFlags.All,
               new List<NuGetLogCode>(),
               autoReferenced: false,
               generatePathProperty: true,
               versionCentrallyManaged: false,
               LibraryDependencyReferenceType.Direct,
               aliases: null);

            var centralVersionFoo = new CentralPackageVersion("foo", VersionRange.Parse("1.0.0"));
            var centralVersionBar = new CentralPackageVersion("bar", VersionRange.Parse("2.0.0"));

            var tfi = CreateTargetFrameworkInformation(new List<LibraryDependency>() { dependencyBar }, new List<CentralPackageVersion>() { centralVersionFoo, centralVersionBar });
            var packageSpec = new PackageSpec(new List<TargetFrameworkInformation>() { tfi });
            packageSpec.RestoreMetadata = new ProjectRestoreMetadata() { ProjectUniqueName = "a", CentralPackageVersionsEnabled = true };
            var sources = new List<PackageSource>();
            var logger = new TestLogger();

            var request = new TestRestoreRequest(packageSpec, sources, "", logger);

            var restoreCommand = new RestoreCommand(request);

            var result = await restoreCommand.ExecuteAsync();

            // Assert
            Assert.False(result.Success);
            Assert.Equal(1, logger.ErrorMessages.Count);
            logger.ErrorMessages.TryDequeue(out var errorMessage);
            Assert.True(errorMessage.Contains("Projects that use central package version management should not define the version on the PackageReference items but on the PackageVersion"));
            Assert.True(errorMessage.Contains("bar"));
        }

        [Theory]
        [InlineData("bar")]
        [InlineData("Bar")]
        public async Task RestoreCommand_CentralVersion_ErrorWhenCentralPackageVersionFileContainsAutoReferencedReferences(string autoreferencedpackageId)
        {
            // Arrange
            var dependencyBar = new LibraryDependency(new LibraryRange(autoreferencedpackageId, VersionRange.Parse("3.0.0"), LibraryDependencyTarget.All),
               LibraryDependencyType.Default,
               LibraryIncludeFlags.All,
               LibraryIncludeFlags.All,
               new List<NuGetLogCode>(),
               autoReferenced: true,
               generatePathProperty: true,
               versionCentrallyManaged: false,
               LibraryDependencyReferenceType.Direct,
               aliases: null);

            var centralVersionFoo = new CentralPackageVersion("foo", VersionRange.Parse("1.0.0"));
            var centralVersionBar = new CentralPackageVersion(autoreferencedpackageId.ToLowerInvariant(), VersionRange.Parse("2.0.0"));

            var tfi = CreateTargetFrameworkInformation(new List<LibraryDependency>() { dependencyBar }, new List<CentralPackageVersion>() { centralVersionFoo, centralVersionBar });
            var packageSpec = new PackageSpec(new List<TargetFrameworkInformation>() { tfi });
            packageSpec.RestoreMetadata = new ProjectRestoreMetadata() { ProjectUniqueName = "a", CentralPackageVersionsEnabled = true };
            var sources = new List<PackageSource>();
            var logger = new TestLogger();

            var request = new TestRestoreRequest(packageSpec, sources, "", logger);

            var restoreCommand = new RestoreCommand(request);

            var result = await restoreCommand.ExecuteAsync();

            // Assert
            Assert.False(result.Success);
            Assert.Equal(1, logger.ErrorMessages.Count);
            logger.ErrorMessages.TryDequeue(out var errorMessage);
            Assert.True(errorMessage.Contains("You do not typically need to reference them from your project or in your central package versions management file. For more information, see https://aka.ms/sdkimplicitrefs"));
            Assert.True(errorMessage.Contains(autoreferencedpackageId));
        }

        [Fact]
        public async Task RestoreCommand_LogDowngradeWarningsOrErrorsAsync_ErrorWhenCpvmEnabled()
        {
            // Arrange
            // create graph with a downgrade
            var centralPackageName = "D";
            var centralPackageVersion = "2.0.0";
            var otherVersion = "3.0.0";
            NuGetFramework framework = NuGetFramework.Parse("net45");
            var logger = new TestLogger();

            var context = new TestRemoteWalkContext();
            var provider = new DependencyProvider();
            // D is a transitive dependency for package A through package B -> C -> D
            // D is defined as a Central Package Version
            // In this context Package D with version centralPackageVersion will be added as inner node of Node A, next to B 

            // Input 
            // A -> B (version = 3.0.0) -> C (version = 3.0.0) -> D (version = 3.0.0)
            // A ~> D (version = 2.0.0
            //         the dependency is not direct,
            //         it simulates the fact that there is a centrally defined "D" package
            //         the information is added to the provider)

            // The expected output graph
            //    -> B (version = 3.0.0) -> C (version = 3.0.0)
            // A
            //    -> D (version = 2.0.0)
            provider.Package("A", otherVersion)
                    .DependsOn("B", otherVersion)
                    .DependsOn(centralPackageName, centralPackageVersion, LibraryDependencyTarget.Package, versionCentrallyManaged: true, libraryDependencyReferenceType: LibraryDependencyReferenceType.None);

            provider.Package("B", otherVersion)
                   .DependsOn("C", otherVersion);

            provider.Package("C", otherVersion)
                  .DependsOn(centralPackageName, otherVersion);

            // Simulates the existence of a D centrally defined package that is not direct dependency
            provider.Package("A", otherVersion)
                     .DependsOn(centralPackageName, centralPackageVersion, LibraryDependencyTarget.Package, versionCentrallyManaged: true, libraryDependencyReferenceType: LibraryDependencyReferenceType.None);

            // Add central package to the source with multiple versions
            provider.Package(centralPackageName, "1.0.0");
            provider.Package(centralPackageName, centralPackageVersion);
            provider.Package(centralPackageName, "3.0.0");

            context.LocalLibraryProviders.Add(provider);
            var walker = new RemoteDependencyWalker(context);

            // Act
            var rootNode = await DoWalkAsync(walker, "A", framework);
            RestoreTargetGraph restoreTargetGraph = RestoreTargetGraph.Create(new List<GraphNode<RemoteResolveResult>>() { rootNode }, context, logger, framework);

            await RestoreCommand.LogDowngradeWarningsOrErrorsAsync(new List<RestoreTargetGraph>() { restoreTargetGraph }, logger);

            // Assert
            Assert.Equal(1, logger.Errors);
            Assert.Equal(1, logger.LogMessages.Count);
            var logMessage = logger.LogMessages.First();
            Assert.Equal(LogLevel.Error, logMessage.Level);
            Assert.True(logMessage.Message.Contains("Detected package downgrade: D from 3.0.0 to centrally defined 2.0.0. "));
            Assert.Equal(NuGetLogCode.NU1109, logMessage.Code);
        }

        [Fact]
        public async Task RestoreCommand_DowngradeIsErrorWhen_DowngradedByCentralTransitiveDependency()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var logger = new TestLogger();
                var projectName = "TestProject";
                var projectPath = Path.Combine(pathContext.SolutionRoot, projectName);
                var sources = new List<PackageSource> { new PackageSource(pathContext.PackageSource) };

                var project1Json = @"
                {
                  ""version"": ""1.0.0"",
                    ""restore"": {
                                    ""projectUniqueName"": ""TestProject"",
                                    ""centralPackageVersionsManagementEnabled"": true
                    },
                  ""frameworks"": {
                    ""net472"": {
                        ""dependencies"": {
                                ""packageA"": {
                                    ""version"": ""[2.0.0)"",
                                    ""target"": ""Package"",
                                    ""versionCentrallyManaged"": true
                                }
                        },
                        ""centralPackageVersions"": {
                            ""packageA"": ""[2.0.0)"",
                            ""packageB"": ""[1.0.0)""
                        }
                    }
                  }
                }";

                var packageA_Version200 = new SimpleTestPackageContext("packageA", "2.0.0");
                var packageB_Version100 = new SimpleTestPackageContext("packageB", "1.0.0");
                var packageB_Version200 = new SimpleTestPackageContext("packageB", "2.0.0");

                packageA_Version200.Dependencies.Add(packageB_Version200);


                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageA_Version200,
                    packageB_Version100,
                    packageB_Version200
                    );

                // set up the project
                var spec = JsonPackageSpecReader.GetPackageSpec(project1Json, projectName, Path.Combine(projectPath, $"{projectName}.json")).WithTestRestoreMetadata();

                var request = new TestRestoreRequest(spec, sources, pathContext.UserPackagesFolder, logger)
                {
                    LockFilePath = Path.Combine(projectPath, "project.assets.json"),
                    ProjectStyle = ProjectStyle.PackageReference
                };

                var command = new RestoreCommand(request);

                // Act
                var result = await command.ExecuteAsync();

                // Assert
                Assert.False(result.Success);
                var downgradeErrorMessages = logger.Messages.Where(s => s.Contains("Detected package downgrade: packageB from 2.0.0 to centrally defined 1.0.0.")).ToList();
                Assert.Equal(1, downgradeErrorMessages.Count);
            }
        }

        [Fact]
        public async Task RestoreCommand_DowngradeIsNotErrorWhen_DowngradedByCentralDirectDependency()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var logger = new TestLogger();
                var projectName = "TestProject";
                var projectPath = Path.Combine(pathContext.SolutionRoot, projectName);
                var sources = new List<PackageSource> { new PackageSource(pathContext.PackageSource) };

                var project1Json = @"
                {
                  ""version"": ""1.0.0"",
                    ""restore"": {
                                    ""projectUniqueName"": ""TestProject"",
                                    ""centralPackageVersionsManagementEnabled"": true
                    },
                  ""frameworks"": {
                    ""net472"": {
                        ""dependencies"": {
                                ""packageA"": {
                                    ""version"": ""[2.0.0)"",
                                    ""target"": ""Package"",
                                    ""versionCentrallyManaged"": true
                                },
                                ""packageB"": {
                                    ""version"": ""[1.0.0)"",
                                    ""target"": ""Package"",
                                    ""versionCentrallyManaged"": true
                                }
                        },
                        ""centralPackageVersions"": {
                            ""packageA"": ""[2.0.0)"",
                            ""packageB"": ""[1.0.0)""
                        }
                    }
                  }
                }";

                var packageA_Version200 = new SimpleTestPackageContext("packageA", "2.0.0");
                var packageB_Version100 = new SimpleTestPackageContext("packageB", "1.0.0");
                var packageB_Version200 = new SimpleTestPackageContext("packageB", "2.0.0");

                packageA_Version200.Dependencies.Add(packageB_Version200);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageA_Version200,
                    packageB_Version100,
                    packageB_Version200
                    );

                // set up the project
                var spec = JsonPackageSpecReader.GetPackageSpec(project1Json, projectName, Path.Combine(projectPath, $"{projectName}.json")).WithTestRestoreMetadata();

                var request = new TestRestoreRequest(spec, sources, pathContext.UserPackagesFolder, logger)
                {
                    LockFilePath = Path.Combine(projectPath, "project.assets.json"),
                    ProjectStyle = ProjectStyle.PackageReference
                };

                var command = new RestoreCommand(request);

                // Act
                var result = await command.ExecuteAsync();

                // Assert
                Assert.True(result.Success);
            }
        }

        private static TargetFrameworkInformation CreateTargetFrameworkInformation(List<LibraryDependency> dependencies, List<CentralPackageVersion> centralVersionsDependencies)
        {
            NuGetFramework nugetFramework = new NuGetFramework("net40");
            TargetFrameworkInformation tfi = new TargetFrameworkInformation()
            {
                AssetTargetFallback = true,
                Warn = false,
                FrameworkName = nugetFramework,
                Dependencies = dependencies,
            };

            foreach (var cvd in centralVersionsDependencies)
            {
                tfi.CentralPackageVersions.Add(cvd.Name, cvd);
            }

            return tfi;
        }

        private Task<GraphNode<RemoteResolveResult>> DoWalkAsync(RemoteDependencyWalker walker, string name, NuGetFramework framework)
        {
            var range = new LibraryRange
            {
                Name = name,
                VersionRange = new VersionRange(new NuGetVersion("1.0"))
            };

            return walker.WalkAsync(range, framework, runtimeIdentifier: null, runtimeGraph: null, recursive: true);
        }
        
        private static DependencyGraphSpec CreateMinimalDependencyGraphSpec(string projectPath, string outputPath)
        {
            var packageSpec = new PackageSpec();
            packageSpec.FilePath = projectPath;
            packageSpec.RestoreMetadata = new ProjectRestoreMetadata();
            packageSpec.RestoreMetadata.ProjectUniqueName = projectPath;
            packageSpec.RestoreMetadata.ProjectStyle = ProjectStyle.PackageReference;
            packageSpec.RestoreMetadata.OutputPath = outputPath;

            var dgSpec = new DependencyGraphSpec();
            dgSpec.AddProject(packageSpec);

            return dgSpec;
        }
    }
}
