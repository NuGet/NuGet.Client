// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NuGet.Commands.Test;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Commands.FuncTest
{
    using static NuGet.Frameworks.FrameworkConstants;

    using LocalPackageArchiveDownloader = Protocol.LocalPackageArchiveDownloader;

    public class RestoreCommandTests
    {
        [Theory]
        [InlineData(NuGetConstants.V2FeedUrl, new Type[0])]
        [InlineData(NuGetConstants.V3FeedUrl, new[] { typeof(RemoteV3FindPackageByIdResourceProvider) })]
        [InlineData(NuGetConstants.V3FeedUrl, new[] { typeof(HttpFileSystemBasedFindPackageByIdResourceProvider) })]
        public async Task RestoreCommand_LockFileHasOriginalPackageIdCaseAsync(string source, Type[] excludedProviders)
        {
            // Arrange
            var providers = Repository
                .Provider
                .GetCoreV3()
                .Where(x => !excludedProviders.Contains(x.Value.GetType()));
            var sourceRepository = Repository.CreateSource(providers, source);
            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            {
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfigWithNet46.ToString(), "TestProject", specPath).EnsureProjectJsonRestoreMetadata();

                AddDependency(spec, "ENTITYFRAMEWORK", "6.1.3-BETA1");
                var logger = new TestLogger();
                var request = new TestRestoreRequest(spec, new[] { sourceRepository }, packagesDir, Enumerable.Empty<string>(), logger);
                var command = new RestoreCommand(request);
                // Act
                var result = await command.ExecuteAsync();

                // Assert
                Assert.True(result.Success, "The restore should have succeeded.");

                var library = result.LockFile.Libraries
                    .FirstOrDefault(x => StringComparer.OrdinalIgnoreCase.Equals(x.Name, "EntityFramework"));
                Assert.Equal("EntityFramework", library.Name);
                Assert.Equal("6.1.3-beta1", library.Version.ToNormalizedString());

                var targetLibrary = result.LockFile.Targets.First().Libraries
                    .FirstOrDefault(x => StringComparer.OrdinalIgnoreCase.Equals(x.Name, "EntityFramework"));
                Assert.Equal("EntityFramework", targetLibrary.Name);
                Assert.Equal("6.1.3-beta1", targetLibrary.Version.ToNormalizedString());
            }
        }

        [Fact]
        public async Task RestoreCommand_LockFileHasOriginalVersionCaseAsync()
        {
            // Arrange
            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            {
                Directory.CreateDirectory(Path.Combine(projectDir, "TestProject"));
                var projectSpecPath = Path.Combine(projectDir, "TestProject", "project.json");
                var projectSpec = JsonPackageSpecReader.GetPackageSpec(BasicConfigWithNet46.ToString(), "TestProject", projectSpecPath);
                projectSpec.Dependencies = new List<LibraryDependency>
                {
                    new LibraryDependency()
                    {
                        LibraryRange = new LibraryRange(
                            "ReferencedProject",
                            VersionRange.Parse("2.0.0-beta1"),
                            LibraryDependencyTarget.Project)
                    }
                };

                Directory.CreateDirectory(Path.Combine(projectDir, "ReferencedProject"));
                var referenceSpecPath = Path.Combine(projectDir, "ReferencedProject", "project.json");
                var referenceSpec = JsonPackageSpecReader.GetPackageSpec(BasicConfigWithNet46.ToString(), "ReferencedProject", referenceSpecPath);
                referenceSpec.Version = new NuGetVersion("2.0.0-BETA1");
                PackageSpecWriter.WriteToFile(referenceSpec, referenceSpecPath);

                var logger = new TestLogger();

                var restoreContext = new RestoreArgs()
                {
                    Sources = new List<string>() { NuGetConstants.V3FeedUrl },
                    GlobalPackagesFolder = packagesDir,
                    Log = logger,
                    CacheContext = new SourceCacheContext()
                };

                // Modify specs for netcore
                referenceSpec = referenceSpec.WithTestRestoreMetadata();
                projectSpec = projectSpec.WithTestRestoreMetadata().WithTestProjectReference(referenceSpec);

                var request = await ProjectJsonTestHelpers.GetRequestAsync(restoreContext, projectSpec, referenceSpec);

                var command = new RestoreCommand(request);

                // Act
                var result = await command.ExecuteAsync();

                // Assert
                Assert.True(result.Success, "The restore should have succeeded.");

                var library = result.LockFile.Libraries
                    .FirstOrDefault(x => StringComparer.OrdinalIgnoreCase.Equals(x.Name, "ReferencedProject"));
                Assert.Equal("2.0.0-BETA1", library.Version.ToNormalizedString());

                var targetLibrary = result.LockFile.Targets.First().Libraries
                    .FirstOrDefault(x => StringComparer.OrdinalIgnoreCase.Equals(x.Name, "ReferencedProject"));
                Assert.Equal("2.0.0-BETA1", targetLibrary.Version.ToNormalizedString());
            }
        }

        [Fact]
        public async Task RestoreCommand_CannotFindProjectReferenceWithDifferentNameCaseAsync()
        {
            // Arrange
            var sources = new List<PackageSource>
            {
                new PackageSource(NuGetConstants.V3FeedUrl)
            };

            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            {
                Directory.CreateDirectory(Path.Combine(projectDir, "TestProject"));
                var projectSpecPath = Path.Combine(projectDir, "TestProject", "project.json");
                var projectSpec = JsonPackageSpecReader.GetPackageSpec(BasicConfigWithNet46.ToString(), "TestProject", projectSpecPath).EnsureProjectJsonRestoreMetadata();
                projectSpec.Dependencies = new List<LibraryDependency>
                {
                    new LibraryDependency()
                    {
                        LibraryRange = new LibraryRange(
                            "REFERENCEDPROJECT",
                            VersionRange.Parse("*"),
                            LibraryDependencyTarget.Project)
                    }
                };

                Directory.CreateDirectory(Path.Combine(projectDir, "ReferencedProject"));
                var referenceSpecPath = Path.Combine(projectDir, "ReferencedProject", "project.json");
                var referenceSpec = JsonPackageSpecReader.GetPackageSpec(BasicConfigWithNet46.ToString(), "ReferencedProject", referenceSpecPath);
                PackageSpecWriter.WriteToFile(referenceSpec, referenceSpecPath);

                var logger = new TestLogger();
                var request = new TestRestoreRequest(projectSpec, sources, packagesDir, logger);
                var command = new RestoreCommand(request);

                // Act
                var result = await command.ExecuteAsync();

                // Assert
                Assert.False(result.Success, "The restore should not have succeeded.");
                var libraryRange = result
                    .RestoreGraphs
                    .First()
                    .Unresolved
                    .FirstOrDefault(g => g.Name == "REFERENCEDPROJECT");
                Assert.NotNull(libraryRange);
            }
        }

        /// <summary>
        /// This test fixes https://github.com/NuGet/Home/issues/2901.
        /// </summary>
        [Fact]
        public async Task RestoreCommand_DependenciesOfDifferentCaseAsync()
        {
            // Arrange
            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            {
                // This test is verifying the following dependency graph:
                //
                // A ---> B -> D -> F
                //   \
                //    --> C -> d -> F
                //
                // D and d are the same package ID but with different casing. The resulting restore
                // graph should see both F nodes as the same dependency since D and d refer to the
                // same thing. No dependency conflicts should occur.
                var specDirB = Path.Combine(projectDir, "projects", "b");
                Directory.CreateDirectory(specDirB);
                var specPathB = Path.Combine(specDirB, "project.json");
                File.WriteAllText(
                    specPathB,
                    @"
                    {
                      ""dependencies"": {
                        ""microsoft.NETCore.Runtime.CoreCLR"": ""1.0.2-rc2-24027""
                      },
                      ""frameworks"": {
                        ""netstandard1.5"": {}
                      }
                    }
                    ");
                var specB = JsonPackageSpecReader.GetPackageSpec("b", specPathB);

                var specDirA = Path.Combine(projectDir, "projects", "a");
                Directory.CreateDirectory(specDirA);
                var specPathA = Path.Combine(specDirA, "project.json");
                File.WriteAllText(
                    specPathA,
                    @"
                    {
                      ""dependencies"": {
                        ""b"": {
                          ""target"": ""project""
                        },
                        ""Microsoft.NETCore.Runtime"": ""1.0.2-rc2-24027""
                      },
                      ""frameworks"": {
                        ""netcoreapp1.0"": {}
                      }
                    }
                    ");
                var specA = JsonPackageSpecReader.GetPackageSpec("a", specPathA);

                var logger = new TestLogger();

                var restoreContext = new RestoreArgs()
                {
                    Sources = new List<string>() { NuGetConstants.V3FeedUrl },
                    GlobalPackagesFolder = packagesDir,
                    Log = logger,
                    CacheContext = new SourceCacheContext()
                };

                // Modify specs for netcore
                specB = specB.WithTestRestoreMetadata();
                specA = specA.WithTestRestoreMetadata().WithTestProjectReference(specB);

                var request = await ProjectJsonTestHelpers.GetRequestAsync(restoreContext, specA, specB);

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();

                // Assert
                Assert.Equal(1, result.RestoreGraphs.Count());
                var graph = result.RestoreGraphs.First();
                Assert.Equal(0, graph.Conflicts.Count());
                Assert.True(result.Success, "The restore should have been successful.");
            }
        }

        [Fact]
        public async Task RestoreCommand_VerifyMinClientVersionV2SourceAsync()
        {
            // Arrange
            var sources = new List<PackageSource>
            {
                new PackageSource(NuGetConstants.V3FeedUrl)
            };

            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            {
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath).EnsureProjectJsonRestoreMetadata();

                // This package has a minclientversion of 9999
                AddDependency(spec, "TestPackage.MinClientVersion", "1.0.0");

                var logger = new TestLogger();
                var request = new TestRestoreRequest(spec, sources, packagesDir, logger)
                {
                    LockFilePath = Path.Combine(projectDir, "project.lock.json")
                };

                Exception ex = null;

                // Act
                var command = new RestoreCommand(request);

                try
                {
                    await command.ExecuteAsync();
                }
                catch (Exception thrownEx)
                {
                    ex = thrownEx;
                }

                // Assert
                Assert.Contains("'TestPackage.MinClientVersion 1.0.0' package requires NuGet client version '9.9999.0' or above", ex.Message);
                Assert.False(File.Exists(request.LockFilePath));
            }
        }

        [Fact]
        public async Task RestoreCommand_VerifyMinClientVersionV3SourceAsync()
        {
            // Arrange
            var sources = new List<PackageSource>
            {
                new PackageSource(NuGetConstants.V3FeedUrl)
            };

            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            {
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath).EnsureProjectJsonRestoreMetadata();

                // This package has a minclientversion of 9999
                AddDependency(spec, "TestPackage.MinClientVersion", "1.0.0");

                var logger = new TestLogger();
                var request = new TestRestoreRequest(spec, sources, packagesDir, logger)
                {
                    LockFilePath = Path.Combine(projectDir, "project.lock.json")
                };

                Exception ex = null;

                // Act
                var command = new RestoreCommand(request);

                try
                {
                    await command.ExecuteAsync();
                }
                catch (Exception thrownEx)
                {
                    ex = thrownEx;
                }

                // Assert
                Assert.Contains("'TestPackage.MinClientVersion 1.0.0' package requires NuGet client version '9.9999.0' or above", ex.Message);
                Assert.False(File.Exists(request.LockFilePath));
            }
        }

        [Fact]
        public async Task RestoreCommand_VerifyMinClientVersionLocalFolderAsync()
        {
            // Arrange
            var sources = new List<PackageSource>();

            using (var sourceDir = TestDirectory.Create())
            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            {
                sources.Add(new PackageSource(sourceDir));
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath).EnsureProjectJsonRestoreMetadata();

                // This package has a minclientversion of 9.9999.0
                AddDependency(spec, "packageA", "1.0.0");

                var logger = new TestLogger();
                var request = new TestRestoreRequest(spec, sources, packagesDir, logger)
                {
                    LockFilePath = Path.Combine(projectDir, "project.lock.json")
                };

                var packageContext = new SimpleTestPackageContext()
                {
                    Id = "packageA",
                    Version = "1.0.0",
                    MinClientVersion = "9.9.9"
                };

                await SimpleTestPackageUtility.CreatePackagesAsync(sourceDir, packageContext);

                Exception ex = null;

                // Act
                var command = new RestoreCommand(request);

                try
                {
                    await command.ExecuteAsync();
                }
                catch (Exception thrownEx)
                {
                    ex = thrownEx;
                }

                // Assert
                Assert.Contains("'packageA 1.0.0' package requires NuGet client version '9.9.9' or above", ex.Message);
                Assert.False(File.Exists(request.LockFilePath));
            }
        }

        [Fact]
        public async Task RestoreCommand_VerifyMinClientVersionAlreadyInstalledAsync()
        {
            // Arrange
            var sources = new List<PackageSource>();

            using (var emptyDir = TestDirectory.Create())
            using (var workingDir = TestDirectory.Create())
            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            {
                sources.Add(new PackageSource(emptyDir));
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath).EnsureProjectJsonRestoreMetadata();

                // This package has a minclientversion of 9.9999.0
                AddDependency(spec, "packageA", "1.0.0");

                var logger = new TestLogger();
                var request = new TestRestoreRequest(spec, sources, packagesDir, logger)
                {
                    LockFilePath = Path.Combine(projectDir, "project.lock.json")
                };

                var packageContext = new SimpleTestPackageContext()
                {
                    Id = "packageA",
                    Version = "1.0.0",
                    MinClientVersion = "9.9.9"
                };

                var packagePath = Path.Combine(workingDir, "packageA.1.0.0.nupkg");

                await SimpleTestPackageUtility.CreatePackagesAsync(workingDir, packageContext);

                // install the package
                var packageIdentity = new PackageIdentity("packageA", NuGetVersion.Parse("1.0.0"));

                using (var packageDownloader = new LocalPackageArchiveDownloader(
                    workingDir,
                    packagePath,
                    packageIdentity,
                    logger))
                {
                    await PackageExtractor.InstallFromSourceAsync(
                        new PackageIdentity("packageA", NuGetVersion.Parse("1.0.0")),
                        packageDownloader,
                        new VersionFolderPathResolver(packagesDir),
                        new PackageExtractionContext(
                            PackageSaveMode.Defaultv3,
                            XmlDocFileSaveMode.None,
                            clientPolicyContext: null,
                            logger: logger),
                        CancellationToken.None);
                }

                Exception ex = null;

                // Act
                var command = new RestoreCommand(request);

                try
                {
                    await command.ExecuteAsync();
                }
                catch (Exception thrownEx)
                {
                    ex = thrownEx;
                }

                // Assert
                Assert.Contains("'packageA 1.0.0' package requires NuGet client version '9.9.9' or above", ex.Message);
                Assert.False(File.Exists(request.LockFilePath));
            }
        }

        [Fact]
        public async Task RestoreCommand_FrameworkImportRulesAreAppliedAsync()
        {
            // Arrange
            var sources = new List<PackageSource>
            {
                new PackageSource(NuGetConstants.V3FeedUrl)
            };

            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            {
                var configJson = JObject.Parse(@"
                {
                    ""dependencies"": {
                        ""Newtonsoft.Json"": ""7.0.1""
                    },
                    ""frameworks"": {
                        ""dotnet"": {
                            ""imports"": ""portable-net452+win81"",
                            ""warn"": false
                        }
                    }
                }");

                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath).EnsureProjectJsonRestoreMetadata();

                var logger = new TestLogger();
                var request = new TestRestoreRequest(spec, sources, packagesDir, logger)
                {
                    LockFilePath = Path.Combine(projectDir, "project.lock.json")
                };

                var command = new RestoreCommand(request);
                var framework = new FallbackFramework(NuGetFramework.Parse("dotnet"), new List<NuGetFramework> { NuGetFramework.Parse("portable-net452+win81") });

                // Act

                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);
                var runtimeAssemblies = GetRuntimeAssemblies(result.LockFile.Targets, framework, null);
                var runtimeAssembly = runtimeAssemblies.FirstOrDefault();

                // Assert
                Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
                Assert.Equal(0, logger.Errors);
                Assert.Equal(0, logger.Warnings);
                Assert.Equal(1, result.GetAllInstalled().Count);
                Assert.Equal("Newtonsoft.Json", result.GetAllInstalled().Single().Name);
                Assert.Equal("7.0.1", result.GetAllInstalled().Single().Version.ToNormalizedString());
                Assert.Equal(1, runtimeAssemblies.Count);
                Assert.Equal("lib/portable-net45+wp80+win8+wpa81+dnxcore50/Newtonsoft.Json.dll", runtimeAssembly.Path);
            }
        }

        [Fact]
        public async Task RestoreCommand_FrameworkImportArrayAsync()
        {
            // Arrange
            var sources = new List<PackageSource>
            {
                new PackageSource(NuGetConstants.V3FeedUrl)
            };

            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            {
                var configJson = JObject.Parse(@"
                {
                    ""dependencies"": {
                        ""Newtonsoft.Json"": ""7.0.1""
                    },
                    ""frameworks"": {
                        ""netstandard1.2"": {
                            ""imports"": [""dotnet5.3"",""portable-net452+win81""],
                            ""warn"": false
                        }
                    }
                }");

                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath).EnsureProjectJsonRestoreMetadata();

                var logger = new TestLogger();
                var request = new TestRestoreRequest(spec, sources, packagesDir, logger)
                {
                    LockFilePath = Path.Combine(projectDir, "project.lock.json")
                };

                var command = new RestoreCommand(request);
                var framework = new FallbackFramework(NuGetFramework.Parse("netstandard1.2"), new List<NuGetFramework> { NuGetFramework.Parse("dotnet5.3"),
                                                                                                                         NuGetFramework.Parse("portable-net452+win81")});

                // Act

                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);
                var runtimeAssemblies = GetRuntimeAssemblies(result.LockFile.Targets, framework, null);
                var runtimeAssembly = runtimeAssemblies.FirstOrDefault();

                // Assert
                Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
                Assert.Equal(0, logger.Errors);
                Assert.Equal(0, logger.Warnings);
                Assert.Equal(1, result.GetAllInstalled().Count);
                Assert.Equal("Newtonsoft.Json", result.GetAllInstalled().Single().Name);
                Assert.Equal("7.0.1", result.GetAllInstalled().Single().Version.ToNormalizedString());
                Assert.Equal(1, runtimeAssemblies.Count);
                Assert.Equal("lib/portable-net45+wp80+win8+wpa81+dnxcore50/Newtonsoft.Json.dll", runtimeAssembly.Path);

            }
        }

        [Fact]
        public async Task RestoreCommand_FrameworkImportRulesAreApplied_NoopAsync()
        {
            // Arrange
            var sources = new List<PackageSource>
            {
                new PackageSource(NuGetConstants.V3FeedUrl)
            };

            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            {
                var configJson = JObject.Parse(@"
                {
                    ""dependencies"": {
                        ""Newtonsoft.Json"": ""7.0.1""
                    },
                    ""frameworks"": {
                        ""dotnet"": {
                            ""imports"": ""portable-net452+win81"",
                            ""warn"": false
                        }
                    }
                }");

                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath).EnsureProjectJsonRestoreMetadata();

                var logger = new TestLogger();
                var request = new TestRestoreRequest(spec, sources, packagesDir, logger)
                {
                    LockFilePath = Path.Combine(projectDir, "project.lock.json")
                };

                var command = new RestoreCommand(request);
                var framework = new FallbackFramework(NuGetFramework.Parse("dotnet"), new List<NuGetFramework> { NuGetFramework.Parse("portable-net452+win81") });
                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);
                logger.Clear();

                // Act
                request = new TestRestoreRequest(spec, sources, packagesDir, logger)
                {
                    LockFilePath = Path.Combine(projectDir, "project.lock.json"),
                    ExistingLockFile = result.LockFile
                };
                command = new RestoreCommand(request);
                result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);

                // Assert
                Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
                Assert.Equal(0, logger.Errors);
                Assert.Equal(0, logger.Warnings);
                Assert.Equal(0, result.GetAllInstalled().Count);
            }
        }

        [Fact]
        public async Task RestoreCommand_LeftOverNupkg_OverwrittenAsync()
        {
            // Arrange
            var sources = new List<PackageSource>
            {
                new PackageSource(NuGetConstants.V3FeedUrl)
            };

            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            {
                var configJson = JObject.Parse(@"
                {
                    ""dependencies"": {
                        ""Newtonsoft.Json"": ""7.0.1""
                    },
                    ""frameworks"": {
                        ""dotnet"": {
                            ""imports"": ""portable-net452+win81"",
                            ""warn"": false
                        }
                    }
                }");

                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath).EnsureProjectJsonRestoreMetadata();
                var logger = new TestLogger();

                // Create left over nupkg to simulate a corrupted install
                var pathResolver = new VersionFolderPathResolver(packagesDir);
                var nupkgFolder = pathResolver.GetInstallPath("Newtonsoft.Json", new NuGetVersion("7.0.1"));
                var nupkgPath = pathResolver.GetPackageFilePath("Newtonsoft.Json", new NuGetVersion("7.0.1"));

                Directory.CreateDirectory(nupkgFolder);

                using (File.Create(nupkgPath))
                {
                }

                Assert.True(File.Exists(nupkgPath));

                var fileSize = new FileInfo(nupkgPath).Length;

                Assert.True(fileSize == 0, "Dummy nupkg file bigger than expected");

                // create the request
                var request = new TestRestoreRequest(spec, sources, packagesDir, logger);

                var command = new RestoreCommand(request);

                // Act
                var result = await command.ExecuteAsync();

                // Assert
                var newFileSize = new FileInfo(nupkgPath).Length;

                Assert.True(newFileSize > 0, "Downloaded file not overriding the dummy nupkg");
            }
        }

        [Fact]
        public async Task RestoreCommand_FrameworkImport_WarnOnAsync()
        {
            // Arrange
            var sources = new List<PackageSource>
            {
                new PackageSource(NuGetConstants.V3FeedUrl)
            };

            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            {
                var configJson = JObject.Parse(@"
                {
                    ""dependencies"": {
                        ""Newtonsoft.Json"": ""7.0.1""
                    },
                    ""frameworks"": {
                        ""dotnet"": {
                            ""imports"": ""portable-net452+win81"",
                            ""warn"": true
                        }
                    }
                }");

                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath).EnsureProjectJsonRestoreMetadata();

                var logger = new TestLogger();
                var request = new TestRestoreRequest(spec, sources, packagesDir, logger)
                {
                    LockFilePath = Path.Combine(projectDir, "project.lock.json")
                };

                var command = new RestoreCommand(request);
                var framework = new FallbackFramework(NuGetFramework.Parse("dotnet"), new List<NuGetFramework> { NuGetFramework.Parse("portable-net452+win81") });
                var warning = "Package 'Newtonsoft.Json 7.0.1' was restored using '.NETPortable,Version=v0.0,Profile=net452+win81' instead of the project target framework '.NETPlatform,Version=v5.0'. This package may not be fully compatible with your project.";

                // Act
                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);
                var runtimeAssemblies = GetRuntimeAssemblies(result.LockFile.Targets, framework, null);
                var runtimeAssembly = runtimeAssemblies.FirstOrDefault();

                // Assert
                Assert.Equal(1, result.GetAllInstalled().Count);
                Assert.Equal(0, logger.Errors);
                Assert.Equal(1, logger.Warnings);
                Assert.Equal(1, logger.Messages.Where(message => message.Contains(warning)).Count());
            }
        }

        [Fact]
        public async Task RestoreCommand_FollowFallbackDependenciesAsync()
        {
            // Arrange
            var sources = new List<PackageSource>
            {
                new PackageSource(NuGetConstants.V3FeedUrl)
            };

            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            {
                var configJson = JObject.Parse(@"
                {
                    ""dependencies"": {
                        ""WindowsAzure.Storage"": ""4.4.1-preview""
                    },
                    ""frameworks"": {
                        ""dotnet"": {
                            ""imports"": ""portable-net452+win81"",
                            ""warn"": false
                        }
                    }
                }");

                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath).EnsureProjectJsonRestoreMetadata();

                var logger = new TestLogger();
                var request = new TestRestoreRequest(spec, sources, packagesDir, logger)
                {
                    LockFilePath = Path.Combine(projectDir, "project.lock.json")
                };

                var command = new RestoreCommand(request);
                var framework = new FallbackFramework(NuGetFramework.Parse("dotnet"), new List<NuGetFramework> { NuGetFramework.Parse("portable-net452+win81") });

                // Act
                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);
                var runtimeAssemblies = GetRuntimeAssemblies(result.LockFile.Targets, framework, null);
                var runtimeAssembly = runtimeAssemblies.FirstOrDefault();
                var dependencies = string.Join("|", result.GetAllInstalled().Select(dependency => dependency.Name)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase));

                // Assert
                Assert.Equal(4, result.GetAllInstalled().Count);
                Assert.Equal("Microsoft.Data.Edm|Microsoft.Data.OData|System.Spatial|WindowsAzure.Storage", dependencies);
                Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
                Assert.Equal(0, logger.Errors);
                Assert.Equal(0, logger.Warnings);
            }
        }

        [Fact]
        public async Task RestoreCommand_FrameworkImportValidateLockFileAsync()
        {
            // Arrange
            var sources = new List<PackageSource>
            {
                new PackageSource(NuGetConstants.V3FeedUrl)
            };

            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            {
                var configJson = JObject.Parse(@"
                {
                    ""dependencies"": {
                        ""Newtonsoft.Json"": ""7.0.1""
                    },
                    ""frameworks"": {
                        ""dotnet"": {
                            ""imports"": ""portable-net452+win81"",
                            ""warn"": false
                        }
                    }
                }");

                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath).EnsureProjectJsonRestoreMetadata();

                var logger = new TestLogger();
                var request = new TestRestoreRequest(spec, sources, packagesDir, logger)
                {
                    LockFilePath = Path.Combine(projectDir, "project.lock.json")
                };

                var command = new RestoreCommand(request);
                var framework = new FallbackFramework(NuGetFramework.Parse("dotnet"), new List<NuGetFramework> { NuGetFramework.Parse("portable-net452+win81") });
                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);

                // Act
                var valid = result.LockFile.IsValidForPackageSpec(spec);

                // Assert
                Assert.True(valid);
            }
        }

        [Fact]
        public async Task RestoreCommand_DependenciesDifferOnCaseAsync()
        {
            // Arrange
            var sources = new List<PackageSource>
            {
                new PackageSource(NuGetConstants.V3FeedUrl)
            };

            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            {
                var json = new JObject();

                var frameworks = new JObject
                {
                    ["net46"] = new JObject()
                };

                json["dependencies"] = new JObject();

                json["frameworks"] = frameworks;

                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(json.ToString(), "TestProject", specPath).EnsureProjectJsonRestoreMetadata();

                AddDependency(spec, "nEwTonSoft.JSon", "6.0.8");
                AddDependency(spec, "json-ld.net", "1.0.4");

                var logger = new TestLogger();
                var request = new TestRestoreRequest(spec, sources, packagesDir, logger)
                {
                    LockFilePath = Path.Combine(projectDir, "project.lock.json")
                };

                var command = new RestoreCommand(request);

                // Act
                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);
                var assemblies = GetRuntimeAssemblies(result.LockFile.Targets, "net46", null);

                // Build again to verify the noop works also
                result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);
                var assemblies2 = GetRuntimeAssemblies(result.LockFile.Targets, "net46", null);

                // Assert
                Assert.Equal(2, assemblies.Count);
                Assert.Equal("lib/net45/Newtonsoft.Json.dll", assemblies[1].Path);
                Assert.Equal(2, assemblies2.Count);
                Assert.Equal("lib/net45/Newtonsoft.Json.dll", assemblies2[1].Path);
            }
        }

        [Fact]
        public async Task RestoreCommand_DependenciesDifferOnCase_DowngradeAsync()
        {
            // Arrange
            var sources = new List<PackageSource>
            {
                new PackageSource(NuGetConstants.V3FeedUrl)
            };

            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            {
                var json = new JObject();

                var frameworks = new JObject
                {
                    ["net46"] = new JObject()
                };

                json["dependencies"] = new JObject();

                json["frameworks"] = frameworks;

                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(json.ToString(), "TestProject", specPath).EnsureProjectJsonRestoreMetadata();

                AddDependency(spec, "nEwTonSoft.JSon", "4.0.1");
                AddDependency(spec, "dotNetRDF", "1.0.8.3533");

                var logger = new TestLogger();
                var request = new TestRestoreRequest(spec, sources, packagesDir, logger)
                {
                    LockFilePath = Path.Combine(projectDir, "project.lock.json")
                };

                var command = new RestoreCommand(request);

                // Act
                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);
                var assemblies = GetRuntimeAssemblies(result.LockFile.Targets, "net46", null);

                // Assert
                Assert.Equal(4, assemblies.Count);
                Assert.Equal("lib/40/Newtonsoft.Json.dll", assemblies[2].Path);
            }
        }

        [Fact]
        public async Task RestoreCommand_TestLockFileWrittenOnLockFileChangeAsync()
        {
            // Arrange
            var sources = new List<PackageSource>
            {
                new PackageSource(NuGetConstants.V3FeedUrl)
            };

            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            {
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath).EnsureProjectJsonRestoreMetadata();

                AddDependency(spec, "NuGet.Versioning", "1.0.7");

                var logger = new TestLogger();
                var request = new TestRestoreRequest(spec, sources, packagesDir, logger)
                {
                    LockFilePath = Path.Combine(projectDir, "project.lock.json")
                };

                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();

                var lockFilePath = Path.Combine(projectDir, "project.lock.json");

                // Change the lock file and write it out to disk
                var modifiedLockFile = result.LockFile;
                modifiedLockFile.Version = 1000;

                var lockFormat = new LockFileFormat();

                using (var stream = File.OpenWrite(lockFilePath))
                {
                    lockFormat.Write(stream, modifiedLockFile);
                }

                request = new TestRestoreRequest(spec, sources, packagesDir, logger)
                {
                    LockFilePath = Path.Combine(projectDir, "project.lock.json"),

                    // Act
                    ExistingLockFile = modifiedLockFile
                };

                command = new RestoreCommand(request);
                result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);

                var lockFileFromDisk = lockFormat.Read(lockFilePath);

                // Assert
                // The file should be written out and the version should be updated
                Assert.Equal(LockFileFormat.Version, lockFileFromDisk.Version);
            }
        }

        [Fact]
        public async Task RestoreCommand_NoopOnLockFileWriteIfFilesMatchAsync()
        {
            // Arrange
            var sources = new List<PackageSource>
            {
                new PackageSource(NuGetConstants.V3FeedUrl)
            };

            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            {
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath).EnsureProjectJsonRestoreMetadata();

                AddDependency(spec, "NuGet.Versioning", "1.0.7");

                var logger = new TestLogger();
                var request = new TestRestoreRequest(spec, sources, packagesDir, logger)
                {
                    LockFilePath = Path.Combine(projectDir, "project.lock.json")
                };

                var lockFileFormat = new LockFileFormat();
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);

                var lockFilePath = Path.Combine(projectDir, "project.lock.json");

                // Act
                var lastDate = File.GetLastWriteTime(lockFilePath);

                request = new TestRestoreRequest(spec, sources, packagesDir, logger)
                {
                    LockFilePath = Path.Combine(projectDir, "project.lock.json")
                };
                var previousLockFile = result.LockFile;
                request.ExistingLockFile = result.LockFile;

                // Act 2
                // Read the file from disk to verify the reader
                var fromDisk = lockFileFormat.Read(lockFilePath);

                // wait half a second to make sure the time difference can be picked up
                await Task.Delay(500);

                command = new RestoreCommand(request);
                result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);

                var currentDate = File.GetLastWriteTime(lockFilePath);

                // Assert
                // The file should not be written out
                Assert.Equal(lastDate, currentDate);

                // Verify the files are equal
                Assert.True(previousLockFile.Equals(result.LockFile));
                Assert.True(fromDisk.Equals(result.LockFile));

                // Verify the hash codes are the same
                Assert.Equal(previousLockFile.GetHashCode(), result.LockFile.GetHashCode());
                Assert.Equal(fromDisk.GetHashCode(), result.LockFile.GetHashCode());
            }
        }

        [Fact]
        public async Task RestoreCommand_NuGetVersioning107RuntimeAssembliesAsync()
        {
            // Arrange
            var sources = new List<PackageSource>
            {
                new PackageSource(NuGetConstants.V3FeedUrl)
            };

            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            {
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath).EnsureProjectJsonRestoreMetadata();

                AddDependency(spec, "NuGet.Versioning", "1.0.7");

                var logger = new TestLogger();
                var request = new TestRestoreRequest(spec, sources, packagesDir, logger)
                {
                    LockFilePath = Path.Combine(projectDir, "project.lock.json")
                };

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                var installed = result.GetAllInstalled();
                var unresolved = result.GetAllUnresolved();
                var runtimeAssemblies = GetRuntimeAssemblies(result.LockFile.Targets, "netcore50", null);

                var runtimeAssembly = runtimeAssemblies.FirstOrDefault();

                // Assert
                Assert.Equal(0, logger.Errors);
                Assert.Equal(1, installed.Count);
                Assert.Equal(0, unresolved.Count);
                Assert.Equal("NuGet.Versioning", installed.Single().Name);
                Assert.Equal("1.0.7", installed.Single().Version.ToNormalizedString());

                Assert.Equal(1, runtimeAssemblies.Count);
                Assert.Equal("lib/portable-net40+win/NuGet.Versioning.dll", runtimeAssembly.Path);
            }
        }

        [Fact]
        public async Task RestoreCommand_InstallPackageWithDependenciesAsync()
        {
            // Arrange
            var sources = new List<PackageSource>
            {
                new PackageSource(NuGetConstants.V3FeedUrl)
            };

            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            {
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath).EnsureProjectJsonRestoreMetadata();

                AddDependency(spec, "WebGrease", "1.6.0");

                var logger = new TestLogger();
                var request = new TestRestoreRequest(spec, sources, packagesDir, logger)
                {
                    LockFilePath = Path.Combine(projectDir, "project.lock.json")
                };

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                var installed = result.GetAllInstalled();
                var unresolved = result.GetAllUnresolved();
                var runtimeAssemblies = GetRuntimeAssemblies(result.LockFile.Targets, "netcore50", null);
                var jsonNetReference = runtimeAssemblies.SingleOrDefault(assembly => assembly.Path == "lib/netcore45/Newtonsoft.Json.dll");
                var jsonNetPackage = installed.SingleOrDefault(package => package.Name == "Newtonsoft.Json");

                // Assert
                // There will be compatibility errors, but we don't care
                Assert.Equal(3, installed.Count);
                Assert.Equal(0, unresolved.Count);
                Assert.Equal("5.0.4", jsonNetPackage.Version.ToNormalizedString());

                Assert.Equal(1, runtimeAssemblies.Count);
                Assert.NotNull(jsonNetReference);
            }
        }

        [Fact]
        public async Task RestoreCommand_InstallPackageWithManyDependenciesAsync()
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
              ""dependencies"": {
                ""packageA"": ""1.0.0""
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
                sources.Add(new PackageSource(packageSource.FullName));
                packagesDir.Create();
                packageSource.Create();
                project1.Create();

                File.WriteAllText(Path.Combine(project1.FullName, "project.json"), project1Json);

                var specPath1 = Path.Combine(project1.FullName, "project.json");
                var spec1 = JsonPackageSpecReader.GetPackageSpec(project1Json, "project1", specPath1).EnsureProjectJsonRestoreMetadata();
                var logger = new TestLogger();
                var request = new TestRestoreRequest(spec1, sources, packagesDir.FullName, logger)
                {
                    LockFilePath = Path.Combine(project1.FullName, "project.lock.json")
                };

                var packages = new List<SimpleTestPackageContext>();
                var dependencies = new List<SimpleTestPackageContext>();

                for (var i = 0; i < 500; i++)
                {
                    var package = new SimpleTestPackageContext()
                    {
                        Id = $"package{i}"
                    };
                    packages.Add(package);
                    dependencies.Add(package);
                }

                var packageA = new SimpleTestPackageContext()
                {
                    Id = "packageA",
                    Dependencies = dependencies
                };
                packages.Add(packageA);
                await SimpleTestPackageUtility.CreatePackagesAsync(packages, packageSource.FullName);

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                var lockFile = result.LockFile;
                var installed = result.GetAllInstalled();
                var unresolved = result.GetAllUnresolved();
                await result.CommitAsync(logger, CancellationToken.None);

                // Assert
                Assert.True(result.Success);
                Assert.Equal(501, installed.Count);
                Assert.Equal(0, unresolved.Count);
            }
        }

        [Fact]
        public async Task RestoreCommand_InstallPackageWithReferenceDependenciesAsync()
        {
            // Arrange
            var sources = new List<PackageSource>
            {
                new PackageSource(NuGetConstants.V3FeedUrl)
            };

            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            {
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfigWithNet46.ToString(), "TestProject", specPath).EnsureProjectJsonRestoreMetadata();

                AddDependency(spec, "Moon.Owin.Localization", "1.3.1");

                var logger = new TestLogger();
                var request = new TestRestoreRequest(spec, sources, packagesDir, logger)
                {
                    LockFilePath = Path.Combine(projectDir, "project.lock.json")
                };

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                var installed = result.GetAllInstalled();
                var unresolved = result.GetAllUnresolved();
                var runtimeAssemblies = GetRuntimeAssemblies(result.LockFile.Targets, "net46", null);
                var jsonNetReference = runtimeAssemblies.SingleOrDefault(assembly => assembly.Path == "lib/net45/Newtonsoft.Json.dll");
                var jsonNetPackage = installed.SingleOrDefault(package => package.Name == "Newtonsoft.Json");

                // Assert
                // There will be compatibility errors, but we don't care
                Assert.Equal(25, installed.Count);
                Assert.Equal(0, unresolved.Count);
                Assert.Equal("7.0.1", jsonNetPackage.Version.ToNormalizedString());

                Assert.Equal(24, runtimeAssemblies.Count);
                Assert.NotNull(jsonNetReference);
            }
        }

        [Fact]
        public async Task RestoreCommand_RestoreWithNoChangesAsync()
        {
            // Arrange
            var sources = new List<PackageSource>
            {
                new PackageSource(NuGetConstants.V3FeedUrl)
            };

            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            {
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath).EnsureProjectJsonRestoreMetadata();

                AddDependency(spec, "NuGet.Versioning", "1.0.7");

                var logger = new TestLogger();
                var request = new TestRestoreRequest(spec, sources, packagesDir, logger)
                {
                    LockFilePath = Path.Combine(projectDir, "project.lock.json")
                };

                var command = new RestoreCommand(request);
                var firstRun = await command.ExecuteAsync();

                // Act
                request = new TestRestoreRequest(spec, sources, packagesDir, logger);
                command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                var installed = result.GetAllInstalled();
                var unresolved = result.GetAllUnresolved();

                // Assert
                Assert.Equal(0, logger.Errors);
                Assert.Equal(0, installed.Count);
                Assert.Equal(0, unresolved.Count);
            }
        }

        [Theory]
        [InlineData(NuGetConstants.V2FeedUrl)]
        [InlineData(NuGetConstants.V3FeedUrl)]
        public async Task RestoreCommand_PackageIsAddedToPackageCacheAsync(string source)
        {
            // Arrange
            var sources = new List<PackageSource>
            {
                new PackageSource(source)
            };

            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            {
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath).EnsureProjectJsonRestoreMetadata();

                AddDependency(spec, "NuGet.Versioning", "1.0.7");

                var logger = new TestLogger();
                var request = new TestRestoreRequest(spec, sources, packagesDir, logger)
                {
                    LockFilePath = Path.Combine(projectDir, "project.lock.json")
                };

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();

                // Assert
                var pathResolver = new VersionFolderPathResolver(packagesDir);
                var nuspecPath = pathResolver.GetManifestFilePath("NuGet.Versioning", new NuGetVersion("1.0.7"));
                Assert.True(File.Exists(nuspecPath));
            }
        }

        [Theory]
        [InlineData(NuGetConstants.V2FeedUrl)]
        [InlineData(NuGetConstants.V3FeedUrl)]
        public async Task RestoreCommand_PackagesAreExtractedToTheNormalizedPathAsync(string source)
        {
            // Arrange
            var sources = new List<PackageSource>
            {
                new PackageSource(source)
            };

            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            {
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath).EnsureProjectJsonRestoreMetadata();

                AddDependency(spec, "owin", "1.0");

                var logger = new TestLogger();
                var request = new TestRestoreRequest(spec, sources, packagesDir, logger)
                {
                    LockFilePath = Path.Combine(projectDir, "project.lock.json")
                };

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();

                // Assert
                var pathResolver = new VersionFolderPathResolver(packagesDir);
                var nuspecPath = pathResolver.GetManifestFilePath("owin", new NuGetVersion("1.0.0"));
                Assert.True(File.Exists(nuspecPath));
            }
        }

        [Fact]
        public async Task RestoreCommand_WarnWhenWeBumpYouUpAsync()
        {
            // Arrange
            var sources = new List<PackageSource>
            {
                new PackageSource(NuGetConstants.V3FeedUrl)
            };

            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            {
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath).EnsureProjectJsonRestoreMetadata();

                AddDependency(spec, "Newtonsoft.Json", "7.0.0"); // 7.0.0 does not exist so we'll bump up to 7.0.1

                var logger = new TestLogger();
                var request = new TestRestoreRequest(spec, sources, packagesDir, logger)
                {
                    LockFilePath = Path.Combine(projectDir, "project.lock.json")
                };

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                var installed = result.GetAllInstalled();
                var unresolved = result.GetAllUnresolved();
                var runtimeAssemblies = GetRuntimeAssemblies(result.LockFile.Targets, "netcore50", null);

                var runtimeAssembly = runtimeAssemblies.FirstOrDefault();

                // Assert
                Assert.Equal(1, logger.Warnings); // Warnings are combined on message
                Assert.Contains("TestProject depends on Newtonsoft.Json (>= 7.0.0) but Newtonsoft.Json 7.0.0 was not found. An approximate best match of Newtonsoft.Json 7.0.1 was resolved.", logger.Messages);
            }
        }

        [Fact]
        public async Task RestoreCommand_WarnWhenWeBumpYouUpOnSubsequentRestoresAsync()
        {
            // Arrange
            var sources = new List<PackageSource>
            {
                new PackageSource(NuGetConstants.V3FeedUrl)
            };

            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            {
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath).EnsureProjectJsonRestoreMetadata();

                AddDependency(spec, "Newtonsoft.Json", "7.0.0"); // 7.0.0 does not exist so we'll bump up to 7.0.1

                // Execute the first restore
                var requestA = new TestRestoreRequest(spec, sources, packagesDir, new TestLogger())
                {
                    LockFilePath = Path.Combine(projectDir, "project.lock.json")
                };
                var commandA = new RestoreCommand(requestA);
                var resultA = await commandA.ExecuteAsync();

                var logger = new TestLogger();
                var requestB = new TestRestoreRequest(spec, sources, packagesDir, logger)
                {
                    LockFilePath = Path.Combine(projectDir, "project.lock.json"),
                    ExistingLockFile = resultA.LockFile
                };
                var commandB = new RestoreCommand(requestB);

                // Act
                var resultB = await commandB.ExecuteAsync();

                // Assert
                Assert.Equal(1, logger.Warnings); // Warnings for all graphs are combined
                Assert.Contains("TestProject depends on Newtonsoft.Json (>= 7.0.0) but Newtonsoft.Json 7.0.0 was not found. An approximate best match of Newtonsoft.Json 7.0.1 was resolved.", logger.Messages);
            }
        }

        [Fact]
        public async Task RestoreCommand_JsonNet701RuntimeAssembliesAsync()
        {
            // Arrange
            var sources = new List<PackageSource>
            {
                new PackageSource(NuGetConstants.V3FeedUrl)
            };

            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            {
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath).EnsureProjectJsonRestoreMetadata();

                AddDependency(spec, "Newtonsoft.Json", "7.0.1");

                var logger = new TestLogger();
                var request = new TestRestoreRequest(spec, sources, packagesDir, logger)
                {
                    LockFilePath = Path.Combine(projectDir, "project.lock.json")
                };

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                var installed = result.GetAllInstalled();
                var unresolved = result.GetAllUnresolved();
                var runtimeAssemblies = GetRuntimeAssemblies(result.LockFile.Targets, "netcore50", null);

                var runtimeAssembly = runtimeAssemblies.FirstOrDefault();

                // Assert
                Assert.Equal(0, logger.Errors);
                Assert.Equal(1, installed.Count);
                Assert.Equal(0, unresolved.Count);
                Assert.Equal("Newtonsoft.Json", installed.Single().Name);
                Assert.Equal("7.0.1", installed.Single().Version.ToNormalizedString());

                Assert.Equal(1, runtimeAssemblies.Count);
                Assert.Equal("lib/portable-net45+wp80+win8+wpa81+dnxcore50/Newtonsoft.Json.dll", runtimeAssembly.Path);
            }
        }

        [Fact]
        public async Task RestoreCommand_NoCompatibleRuntimeAssembliesForProjectAsync()
        {
            // Arrange
            var sources = new List<PackageSource>
            {
                new PackageSource(NuGetConstants.V3FeedUrl)
            };
            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            {
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath).EnsureProjectJsonRestoreMetadata();

                AddDependency(spec, "NuGet.Core", "2.8.3");

                var logger = new TestLogger();
                var request = new TestRestoreRequest(spec, sources, packagesDir, logger)
                {
                    LockFilePath = Path.Combine(projectDir, "project.lock.json")
                };

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                var installed = result.GetAllInstalled();
                var unresolved = result.GetAllUnresolved();
                var runtimeAssemblies = GetRuntimeAssemblies(result.LockFile.Targets, "netcore50", null);

                var runtimeAssembly = runtimeAssemblies.FirstOrDefault();

                // Assert
                var expectedIssue = CompatibilityIssue.IncompatiblePackage(
                    new PackageIdentity("NuGet.Core", new NuGetVersion(2, 8, 3)),
                    FrameworkConstants.CommonFrameworks.NetCore50,
                    null,
                    new[] { NuGetFramework.Parse("net40-client") });

                Assert.Contains(expectedIssue, result.CompatibilityCheckResults.SelectMany(c => c.Issues).ToArray());
                Assert.False(result.CompatibilityCheckResults.Any(c => c.Success));
                Assert.True(logger.Messages.Any(e => e.Contains(expectedIssue.Format())));

                Assert.Equal(6, logger.Errors);
                Assert.Equal(2, installed.Count);
                Assert.Equal(0, unresolved.Count);
                Assert.Equal(0, runtimeAssemblies.Count);
            }
        }

        [Fact]
        public async Task RestoreCommand_CorrectlyIdentifiesUnresolvedPackagesAsync()
        {
            // Arrange
            var sources = new List<PackageSource>
            {
                new PackageSource(NuGetConstants.V3FeedUrl)
            };

            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            using (var sourceCacheContext = new SourceCacheContext())
            {
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath).EnsureProjectJsonRestoreMetadata();

                AddDependency(spec, "NotARealPackage.ThisShouldNotExists.DontCreateIt.Seriously.JustDontDoIt.Please", "2.8.3");

                var logger = new TestLogger();
                var request = new TestRestoreRequest(spec, sources, packagesDir, sourceCacheContext, logger)
                {
                    LockFilePath = Path.Combine(projectDir, "project.lock.json")
                };

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                var installed = result.GetAllInstalled();
                var unresolved = result.GetAllUnresolved();

                // Assert
                Assert.False(result.Success);

                Assert.Equal(1, logger.Errors);
                Assert.Empty(result.CompatibilityCheckResults);
                Assert.DoesNotContain("compatible with", logger.Messages);
                Assert.Equal(1, unresolved.Count);
                Assert.Equal(0, installed.Count);
            }
        }

        [Fact]
        public async Task RestoreCommand_PopulatesProjectFileDependencyGroupsCorrectlyAsync()
        {
            const string project = @"{
    ""dependencies"": {
        ""Newtonsoft.Json"": ""6.0.4""
    },
    ""frameworks"": {
        ""net45"": {}
    },
    ""supports"": {
    }
}
";
            // Arrange
            var sources = new List<PackageSource>
            {
                new PackageSource(NuGetConstants.V3FeedUrl)
            };

            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            {
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(project, "TestProject", specPath).EnsureProjectJsonRestoreMetadata();

                var logger = new TestLogger();
                var request = new TestRestoreRequest(spec, sources, packagesDir, logger)
                {
                    LockFilePath = Path.Combine(projectDir, "project.lock.json")
                };

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                var installed = result.GetAllInstalled();
                var unresolved = result.GetAllUnresolved();

                // Assert
                Assert.Equal(2, result.LockFile.ProjectFileDependencyGroups.Count);
                Assert.True(string.IsNullOrEmpty(result.LockFile.ProjectFileDependencyGroups[0].FrameworkName));
                Assert.Equal(new[] { "Newtonsoft.Json >= 6.0.4" }, result.LockFile.ProjectFileDependencyGroups[0].Dependencies.ToArray());
                Assert.Equal(".NETFramework,Version=v4.5", result.LockFile.ProjectFileDependencyGroups[1].FrameworkName);
                Assert.Empty(result.LockFile.ProjectFileDependencyGroups[1].Dependencies);
            }
        }

        [Fact]
        public async Task RestoreCommand_CanInstallPackageWithSatelliteAssembliesAsync()
        {
            const string project = @"
{
    ""dependencies"": {
        ""Microsoft.OData.Client"": ""6.12.0"",
    },
    ""frameworks"": {
        ""net40"": {}
    }
}
";

            // Arrange
            var sources = new List<PackageSource>
            {
                new PackageSource("https://www.nuget.org/api/v2")
            };

            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            {
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(project, "TestProject", specPath).EnsureProjectJsonRestoreMetadata();

                var logger = new TestLogger();
                var request = new TestRestoreRequest(spec, sources, packagesDir, logger)
                {
                    LockFilePath = Path.Combine(projectDir, "project.lock.json")
                };

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();

                // Assert
                Assert.True(result.Success);
            }
        }

        [Fact(Skip = "https://github.com/NuGet/Home/issues/8766")]
        public async Task RestoreCommand_UnmatchedRefAndLibAssembliesAsync()
        {
            const string project = @"
{
    ""dependencies"": {
        ""System.Runtime.WindowsRuntime"": ""4.0.11-beta-*"",
        ""Microsoft.NETCore.Targets"": ""1.0.0-beta-*""
    },
    ""frameworks"": {
        ""dotnet"": {}
    },
    ""supports"": {
        ""dnxcore50.app"": {}
    }
}
";

            // Arrange
            var sources = new List<PackageSource>
            {
                new PackageSource("https://nuget.org/api/v2/")
            };

            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            {
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(project, "TestProject", specPath);

                var logger = new TestLogger();
                var request = new TestRestoreRequest(spec, sources, packagesDir, logger)
                {
                    LockFilePath = Path.Combine(projectDir, "project.lock.json")
                };

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                var installed = result.GetAllInstalled();
                var unresolved = result.GetAllUnresolved();
                var brokenPackages = result.CompatibilityCheckResults.FirstOrDefault(c =>
                    c.Graph.Framework == FrameworkConstants.CommonFrameworks.DnxCore50 &&
                    !string.IsNullOrEmpty(c.Graph.RuntimeIdentifier)).Issues.Where(c => c.Type == CompatibilityIssueType.ReferenceAssemblyNotImplemented).ToArray();

                // Assert
                Assert.True(brokenPackages.Length >= 1);
                Assert.True(brokenPackages.Any(c => c.Package.Id.Equals("System.Runtime.WindowsRuntime") && c.AssemblyName.Equals("System.Runtime.WindowsRuntime")));
            }
        }

        [Fact(Skip = "https://github.com/NuGet/Home/issues/8765")]
        public async Task RestoreCommand_LockedLockFileWithOutOfDateProjectAsync()
        {
            const string project = @"
{
    ""dependencies"": {
        ""System.Runtime"": ""4.0.20-beta-*""
    },
    ""frameworks"": {
        ""dotnet"": { }
    }
}";

            const string lockFileContent = @"{
  ""version"": 1,
  ""targets"": {
    "".NETPlatform,Version=v5.0"": {
      ""System.Runtime/4.0.10-beta-23008"": {
        ""compile"": {
          ""ref/dotnet/System.Runtime.dll"": {}
        }
      }
    }
  },
  ""libraries"": {
    ""System.Runtime/4.0.10-beta-23008"": {
      ""sha512"": ""JkGp8sCzxxRY1GS+p1SEk8WcaT8pu++/5b94ar2i/RaUN/OzkcGP/6OLFUxUf1uar75pUvotpiMawVt1dCEUVA=="",
      ""type"": ""Package"",
      ""files"": [
        ""_rels/.rels"",
        ""System.Runtime.nuspec"",
        ""License.rtf"",
        ""ref/dotnet/System.Runtime.dll"",
        ""ref/net451/_._"",
        ""lib/net451/_._"",
        ""ref/win81/_._"",
        ""lib/win81/_._"",
        ""ref/netcore50/System.Runtime.dll"",
        ""package/services/metadata/core-properties/cdec43993f064447a2d882cbfd022539.psmdcp"",
        ""[Content_Types].xml""
      ]
    }
  },
  ""projectFileDependencyGroups"": {
    """": [
      ""System.Runtime >= 4.0.10-beta-*""
    ],
    "".NETPlatform,Version=v5.0"": []
  }
}
";

            // Arrange
            var sources = new List<PackageSource>
            {
                new PackageSource("https://nuget.org/api/v2/")
            };

            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            {
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(project, "TestProject", specPath);

                var lockFileFormat = new LockFileFormat();
                var lockFile = lockFileFormat.Parse(lockFileContent, "In Memory");

                var logger = new TestLogger();
                var request = new TestRestoreRequest(spec, sources, packagesDir, logger)
                {
                    ExistingLockFile = lockFile
                };

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                var installed = result.GetAllInstalled();

                // Assert
                Assert.Equal(1, installed.Count);
                Assert.Equal("System.Runtime", installed.Single().Name);
                Assert.Equal(4, installed.Single().Version.Major);
                Assert.Equal(0, installed.Single().Version.Minor);
                Assert.Equal(20, installed.Single().Version.Patch);
                // Don't assert the pre-release tag since it may vary
            }
        }

        [Fact]
        public void RestoreCommand_PathTooLongException()
        {
            // Arrange
            var sources = new List<PackageSource>
            {
                new PackageSource(NuGetConstants.V3FeedUrl)
            };

            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            using (var cacheContext = new SourceCacheContext())
            {
                var configJson = JObject.Parse(@"
                {
                    ""dependencies"": {
                        ""Newtonsoft.Json"": ""7.0.1""
                    },
                     ""frameworks"": {
                        ""net45"": { }
                    }
                }");

                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath).EnsureProjectJsonRestoreMetadata();

                var logger = new TestLogger();
                string longPath = packagesDir + new string('_', 300);
                try
                {
                    // This test is pointless if the machine has long paths enabled.
                    Path.GetFullPath(longPath);
                    return;
                }
                catch (PathTooLongException)
                {
                }

                var request = new TestRestoreRequest(spec, sources, longPath, cacheContext, logger)
                {
                    LockFilePath = Path.Combine(projectDir, "project.lock.json")
                };

                var command = new RestoreCommand(request);

                // Act
                new Func<Task>(async () => await command.ExecuteAsync()).Should().Throw<PathTooLongException>();
            }
        }

        [Fact]
        public async Task RestoreCommand_RestoreExactVersionWithFailingSourceAsync()
        {
            // Arrange
            var sources = new List<PackageSource>
            {
                new PackageSource("https://failingSource"),
                new PackageSource(NuGetConstants.V3FeedUrl)
            };

            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            using (var cacheContext = new SourceCacheContext())
            {
                var configJson = JObject.Parse(@"
                {
                    ""dependencies"": {
                        ""Newtonsoft.Json"": ""7.0.1""
                    },
                     ""frameworks"": {
                        ""net45"": { }
                    }
                }");

                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath).EnsureProjectJsonRestoreMetadata();

                var logger = new TestLogger();
                var request = new TestRestoreRequest(spec, sources, packagesDir, cacheContext, logger)
                {
                    LockFilePath = Path.Combine(projectDir, "project.lock.json")
                };

                var command = new RestoreCommand(request);

                // Act
                var result = await command.ExecuteAsync();

                // Assert
                Assert.True(result.Success);
            }
        }

        [Fact]
        public async Task RestoreCommand_RestoreFloatingVersionWithFailingHttpSourceAsync()
        {
            // Arrange
            var sources = new List<PackageSource>
            {
                new PackageSource("https://failingSource"),
                new PackageSource(NuGetConstants.V3FeedUrl)
            };

            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            using (var cacheContext = new SourceCacheContext())
            {
                var configJson = JObject.Parse(@"
                {
                    ""dependencies"": {
                        ""Newtonsoft.Json"": ""7.0.1-*""
                    },
                     ""frameworks"": {
                        ""net45"": { }
                    }
                }");

                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath).EnsureProjectJsonRestoreMetadata();

                var logger = new TestLogger();
                var request = new TestRestoreRequest(spec, sources, packagesDir, cacheContext, logger)
                {
                    LockFilePath = Path.Combine(projectDir, "project.lock.json")
                };

                var command = new RestoreCommand(request);

                // Act & Assert
                var ex = await Assert.ThrowsAsync<FatalProtocolException>(async () => await command.ExecuteAsync());
                Assert.NotNull(ex);
            }
        }

        [Fact]
        public async Task RestoreCommand_RestoreFloatingVersionWithFailingLocalSourceAsync()
        {
            // Arrange
            var sources = new List<PackageSource>
            {
                new PackageSource("\\failingSource"),
                new PackageSource(NuGetConstants.V3FeedUrl)
            };

            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            {
                var configJson = JObject.Parse(@"
                {
                    ""dependencies"": {
                        ""Newtonsoft.Json"": ""7.0.1-*""
                    },
                     ""frameworks"": {
                        ""net45"": { }
                    }
                }");

                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath).EnsureProjectJsonRestoreMetadata();

                var logger = new TestLogger();
                var request = new TestRestoreRequest(spec, sources, packagesDir, logger)
                {
                    LockFilePath = Path.Combine(projectDir, "project.lock.json")
                };

                var command = new RestoreCommand(request);

                // Act & Assert
                var ex = await Assert.ThrowsAsync<FatalProtocolException>(async () => await command.ExecuteAsync());
                Assert.NotNull(ex);
            }
        }

        [Fact]
        public async Task RestoreCommand_RestoreFloatingVersionWithIgnoreFailingLocalSourceAsync()
        {
            // Arrange
            var sources = new List<PackageSource>
            {
                new PackageSource("\\failingSource"),
                new PackageSource(NuGetConstants.V3FeedUrl)
            };

            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            {
                var configJson = JObject.Parse(@"
                {
                    ""dependencies"": {
                        ""Newtonsoft.Json"": ""7.0.1-*""
                    },
                     ""frameworks"": {
                        ""net45"": { }
                    }
                }");

                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);

                var logger = new TestLogger();
                using (var context = new SourceCacheContext())
                {
                    context.IgnoreFailedSources = true;
                    var cachingSourceProvider = new CachingSourceProvider(new PackageSourceProvider(NullSettings.Instance));

                    var provider = RestoreCommandProviders.Create(packagesDir, new List<string>(), sources.Select(p => cachingSourceProvider.CreateRepository(p)), context, new LocalPackageFileCache(), logger);
                    var request = new RestoreRequest(spec, provider, context, clientPolicyContext: null, log: logger)
                    {
                        LockFilePath = Path.Combine(projectDir, "project.lock.json")
                    };

                    var command = new RestoreCommand(request);

                    // Act
                    var result = await command.ExecuteAsync();

                    // Assert
                    Assert.True(result.Success);
                }
            }
        }

        [Fact]
        public async Task RestoreCommand_RestoreFloatingVersionWithIgnoreFailingHttpSourceAsync()
        {
            // Arrange
            var sources = new List<PackageSource>
            {
                new PackageSource("https://failingSource"),
                new PackageSource(NuGetConstants.V3FeedUrl)
            };

            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            {
                var configJson = JObject.Parse(@"
                {
                    ""dependencies"": {
                        ""Newtonsoft.Json"": ""7.0.1-*""
                    },
                     ""frameworks"": {
                        ""net45"": { }
                    }
                }");

                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);

                var logger = new TestLogger();
                using (var context = new SourceCacheContext())
                {
                    context.IgnoreFailedSources = true;
                    var cachingSourceProvider = new CachingSourceProvider(new PackageSourceProvider(NullSettings.Instance));

                    var provider = RestoreCommandProviders.Create(packagesDir, new List<string>(), sources.Select(p => cachingSourceProvider.CreateRepository(p)), context, new LocalPackageFileCache(), logger);
                    var request = new RestoreRequest(spec, provider, context, clientPolicyContext: null, log: logger)
                    {
                        LockFilePath = Path.Combine(projectDir, "project.lock.json")
                    };

                    var command = new RestoreCommand(request);

                    // Act
                    var result = await command.ExecuteAsync();

                    // Assert
                    Assert.True(result.Success);
                }
            }
        }

        [Fact]
        public async Task RestoreCommand_RestoreNonExistingWithIgnoreFailingLocalSourceAsync()
        {
            // Arrange
            var sources = new List<PackageSource>
            {
                new PackageSource("\\failingSource"),
                new PackageSource(NuGetConstants.V3FeedUrl)
            };

            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            {
                var configJson = JObject.Parse(@"
                {
                    ""dependencies"": {
                        ""XXX"": ""7.0.91""
                    },
                     ""frameworks"": {
                        ""net45"": { }
                    }
                }");

                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);

                var logger = new TestLogger();
                using (var context = new SourceCacheContext())
                {
                    context.IgnoreFailedSources = true;
                    var cachingSourceProvider = new CachingSourceProvider(new PackageSourceProvider(NullSettings.Instance));

                    var provider = RestoreCommandProviders.Create(packagesDir, new List<string>(), sources.Select(p => cachingSourceProvider.CreateRepository(p)), context, new LocalPackageFileCache(), logger);
                    var request = new RestoreRequest(spec, provider, context, clientPolicyContext: null, log: logger)
                    {
                        LockFilePath = Path.Combine(projectDir, "project.lock.json")
                    };

                    var command = new RestoreCommand(request);

                    // Act
                    var result = await command.ExecuteAsync();

                    // Assert
                    Assert.False(result.Success);
                }
            }
        }

        [Fact]
        public async Task RestoreCommand_RestoreNonExistingWithIgnoreFailingHttpSourceAsync()
        {
            // Arrange
            var sources = new List<PackageSource>
            {
                new PackageSource("https://failingSource"),
                new PackageSource(NuGetConstants.V3FeedUrl)
            };

            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            {
                var configJson = JObject.Parse(@"
                {
                    ""dependencies"": {
                        ""XXX"": ""7.0.91""
                    },
                     ""frameworks"": {
                        ""net45"": { }
                    }
                }");

                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);

                var logger = new TestLogger();
                using (var context = new SourceCacheContext())
                {
                    context.IgnoreFailedSources = true;
                    var cachingSourceProvider = new CachingSourceProvider(new PackageSourceProvider(NullSettings.Instance));

                    var provider = RestoreCommandProviders.Create(packagesDir, new List<string>(), sources.Select(p => cachingSourceProvider.CreateRepository(p)), context, new LocalPackageFileCache(), logger);
                    var request = new RestoreRequest(spec, provider, context, clientPolicyContext: null, log: logger)
                    {
                        LockFilePath = Path.Combine(projectDir, "project.lock.json")
                    };

                    var command = new RestoreCommand(request);

                    // Act
                    var result = await command.ExecuteAsync();

                    // Assert
                    Assert.False(result.Success);
                }
            }
        }

        [Fact]
        public async Task RestoreCommand_RestoreNonExistingWithIgnoreFailingV3HttpSourceAsync()
        {
            // Arrange
            var sources = new List<PackageSource>
            {
                new PackageSource("https://failingSource.json"),
                new PackageSource(NuGetConstants.V3FeedUrl)
            };

            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            {
                var configJson = JObject.Parse(@"
                {
                    ""dependencies"": {
                        ""XXX"": ""7.0.91""
                    },
                     ""frameworks"": {
                        ""net45"": { }
                    }
                }");

                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);

                var logger = new TestLogger();
                using (var context = new SourceCacheContext())
                {
                    context.IgnoreFailedSources = true;
                    var cachingSourceProvider = new CachingSourceProvider(new PackageSourceProvider(NullSettings.Instance));

                    var provider = RestoreCommandProviders.Create(packagesDir, new List<string>(), sources.Select(p => cachingSourceProvider.CreateRepository(p)), context, new LocalPackageFileCache(), logger);
                    var request = new RestoreRequest(spec, provider, context, clientPolicyContext: null, log: logger)
                    {
                        LockFilePath = Path.Combine(projectDir, "project.lock.json")
                    };

                    var command = new RestoreCommand(request);

                    // Act
                    var result = await command.ExecuteAsync();

                    // Assert
                    Assert.False(result.Success);
                }
            }
        }

        [Fact]
        public async Task RestoreCommand_RestoreInexactWithIgnoreFailingLocalSourceAsync()
        {
            // Arrange
            var sources = new List<PackageSource>
            {
                new PackageSource("\\failingSource"),
                new PackageSource(NuGetConstants.V3FeedUrl)
            };

            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            {
                var configJson = JObject.Parse(@"
                {
                    ""dependencies"": {
                        ""Newtonsoft.Json"": ""7.0.91""
                    },
                     ""frameworks"": {
                        ""net45"": { }
                    }
                }");

                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);

                var logger = new TestLogger();
                using (var context = new SourceCacheContext())
                {
                    context.IgnoreFailedSources = true;
                    var cachingSourceProvider = new CachingSourceProvider(new PackageSourceProvider(NullSettings.Instance));

                    var provider = RestoreCommandProviders.Create(packagesDir, new List<string>(), sources.Select(p => cachingSourceProvider.CreateRepository(p)), context, new LocalPackageFileCache(), logger);
                    var request = new RestoreRequest(spec, provider, context, clientPolicyContext: null, log: logger)
                    {
                        LockFilePath = Path.Combine(projectDir, "project.lock.json")
                    };

                    var command = new RestoreCommand(request);

                    // Act
                    var result = await command.ExecuteAsync();

                    // Assert
                    Assert.True(result.Success);
                }
            }
        }

        [Fact]
        public async Task RestoreCommand_RestoreInexactWithIgnoreFailingHttpSourceAsync()
        {
            // Arrange
            var sources = new List<PackageSource>
            {
                new PackageSource("https://failingSource"),
                new PackageSource(NuGetConstants.V3FeedUrl)
            };

            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            {
                var configJson = JObject.Parse(@"
                {
                    ""dependencies"": {
                        ""Newtonsoft.Json"": ""7.0.91""
                    },
                     ""frameworks"": {
                        ""net45"": { }
                    }
                }");

                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);

                var logger = new TestLogger();
                using (var context = new SourceCacheContext())
                {
                    context.IgnoreFailedSources = true;
                    var cachingSourceProvider = new CachingSourceProvider(new PackageSourceProvider(NullSettings.Instance));

                    var provider = RestoreCommandProviders.Create(packagesDir, new List<string>(), sources.Select(p => cachingSourceProvider.CreateRepository(p)), context, new LocalPackageFileCache(), logger);
                    var request = new RestoreRequest(spec, provider, context, clientPolicyContext: null, log: logger)
                    {
                        LockFilePath = Path.Combine(projectDir, "project.lock.json")
                    };

                    var command = new RestoreCommand(request);

                    // Act
                    var result = await command.ExecuteAsync();

                    // Assert
                    Assert.True(result.Success);
                }
            }
        }

        [Fact]
        public async Task RestoreCommand_RestoreInexactWithIgnoreFailingV3HttpSourceAsync()
        {
            // Arrange
            var sources = new List<PackageSource>
            {
                new PackageSource("https://failingSource.json"),
                new PackageSource(NuGetConstants.V3FeedUrl)
            };

            using (var packagesDir = TestDirectory.Create())
            using (var projectDir = TestDirectory.Create())
            {
                var configJson = JObject.Parse(@"
                {
                    ""dependencies"": {
                        ""NewtonSoft.Json"": ""7.0.91""
                    },
                     ""frameworks"": {
                        ""net45"": { }
                    }
                }");

                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);

                var logger = new TestLogger();
                using (var context = new SourceCacheContext())
                {
                    context.IgnoreFailedSources = true;
                    var cachingSourceProvider = new CachingSourceProvider(new PackageSourceProvider(NullSettings.Instance));

                    var provider = RestoreCommandProviders.Create(packagesDir, new List<string>(), sources.Select(p => cachingSourceProvider.CreateRepository(p)), context, new LocalPackageFileCache(), logger);
                    var request = new RestoreRequest(spec, provider, context, clientPolicyContext: null, log: logger)
                    {
                        LockFilePath = Path.Combine(projectDir, "project.lock.json")
                    };

                    var command = new RestoreCommand(request);

                    // Act
                    var result = await command.ExecuteAsync();

                    // Assert
                    Assert.True(result.Success);
                }
            }
        }

        [Fact]
        public async Task RestoreCommand_RuntimeIdentifierGraph_SelectsCorrectRuntimeAssemblies()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var logger = new TestLogger();
                var projectPath = Path.Combine(pathContext.SolutionRoot, "TestProject");


                // Set up the package and source
                var packageA = new SimpleTestPackageContext()
                {
                    Id = "a",
                    Version = "1.0.0"
                };
                packageA.Files.Clear();
                packageA.AddFile("lib/net46/a.dll");
                packageA.AddFile("ref/net46/a.dll");
                packageA.AddFile("runtimes/win7/lib/net46/a.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageA);
                var sources = new List<PackageSource> { new PackageSource(pathContext.PackageSource) };

                // Set up the rid graph
                var ridGraphPath = Path.Combine(pathContext.WorkingDirectory, "runtime.json");
                File.WriteAllBytes(ridGraphPath, GetTestUtilityResource("runtime.json"));

                // set up the project

                var json = $@"
                {{
                    ""frameworks"": {{
                        ""net46"": {{
                            ""dependencies"" : {{
                                    ""a"": {{
                                        ""version"": ""1.0.0"",
                                    }},
                            }},
                            ""runtimeIdentifierGraphPath"" : ""{ridGraphPath.Replace($"{Path.DirectorySeparatorChar}", $"{Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar}")}""
                        }}
                    }},
                    ""runtimes"": {{
                      ""win7-x86"": {{
                        ""#import"": []
                        }}
                    }}
                }}";

                var spec = JsonPackageSpecReader.GetPackageSpec(JObject.Parse(json).ToString(), "TestProject", Path.Combine(projectPath, "spec.json")).WithTestRestoreMetadata();

                var request = new TestRestoreRequest(spec, sources, pathContext.UserPackagesFolder, logger)
                {
                    LockFilePath = Path.Combine(projectPath, "project.assets.json"),
                    ProjectStyle = ProjectStyle.PackageReference
                };

                var command = new RestoreCommand(request);

                // Act

                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);

                // Assert
                Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
                Assert.Equal(string.Empty, logger.ShowErrors());
                Assert.Equal(string.Empty, logger.ShowWarnings());
                Assert.Equal(1, result.GetAllInstalled().Count);
                Assert.Equal("a", result.GetAllInstalled().Single().Name);
                Assert.Equal("1.0.0", result.GetAllInstalled().Single().Version.ToNormalizedString());
                Assert.Equal(2, result.LockFile.Targets.Count);
                Assert.Equal("runtimes/win7/lib/net46/a.dll",
                    string.Join(";", result.LockFile.Targets.First(e => string.Equals("win7-x86", e.RuntimeIdentifier)).Libraries.Single().RuntimeAssemblies.Select(e => e.Path)));
                Assert.Equal("ref/net46/a.dll",
                    string.Join(";", result.LockFile.Targets.First(e => string.Equals("win7-x86", e.RuntimeIdentifier)).Libraries.Single().CompileTimeAssemblies.Select(e => e.Path)));
            }
        }

        [Fact]
        public async Task RestoreCommand_WithFrameworkSpecificRuntimeIdentifierGraph_SelectsCorrectRuntimeAssemblies()
        {
            // Arrange
            using (var pathContext = new SimpleTestPathContext())
            {
                var logger = new TestLogger();
                var projectPath = Path.Combine(pathContext.SolutionRoot, "TestProject");


                // Set up the package and source
                var packageA = new SimpleTestPackageContext()
                {
                    Id = "a",
                    Version = "1.0.0"
                };
                packageA.Files.Clear();
                packageA.AddFile("lib/net46/a.dll");
                packageA.AddFile("ref/net46/a.dll");
                packageA.AddFile("runtimes/win7/lib/net46/a.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageA);
                var sources = new List<PackageSource> { new PackageSource(pathContext.PackageSource) };

                // Set up the rid graph
                var ridGraphPath = Path.Combine(pathContext.WorkingDirectory, "runtime.json");
                File.WriteAllBytes(ridGraphPath, GetTestUtilityResource("runtime.json"));

                // set up the project

                var json = $@"
                {{
                    ""frameworks"": {{
                        ""net46"": {{
                            ""dependencies"" : {{
                                    ""a"": {{
                                        ""version"": ""1.0.0"",
                                    }},
                            }},
                            ""runtimeIdentifierGraphPath"" : ""{ridGraphPath.Replace($"{Path.DirectorySeparatorChar}", $"{Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar}")}""
                        }},
                        ""net47"": {{
                            ""dependencies"" : {{
                                    ""a"": {{
                                        ""version"": ""1.0.0"",
                                    }},
                            }}
                        }}
                    }},
                    ""runtimes"": {{
                      ""win7-x86"": {{
                        ""#import"": []
                        }}
                    }}
                }}";

                var spec = JsonPackageSpecReader.GetPackageSpec(JObject.Parse(json).ToString(), "TestProject", Path.Combine(projectPath, "spec.json")).WithTestRestoreMetadata();

                var request = new TestRestoreRequest(spec, sources, pathContext.UserPackagesFolder, logger)
                {
                    LockFilePath = Path.Combine(projectPath, "project.assets.json"),
                    ProjectStyle = ProjectStyle.PackageReference
                };

                var command = new RestoreCommand(request);

                // Act

                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);

                // Assert
                Assert.Equal(0, result.CompatibilityCheckResults.Sum(checkResult => checkResult.Issues.Count));
                Assert.Equal(string.Empty, logger.ShowErrors());
                Assert.Equal(string.Empty, logger.ShowWarnings());
                Assert.Equal(1, result.GetAllInstalled().Count);
                Assert.Equal("a", result.GetAllInstalled().Single().Name);
                Assert.Equal("1.0.0", result.GetAllInstalled().Single().Version.ToNormalizedString());
                Assert.Equal(4, result.LockFile.Targets.Count);
                Assert.Equal("runtimes/win7/lib/net46/a.dll",
                    string.Join(";", result.LockFile.Targets.First(e => string.Equals("win7-x86", e.RuntimeIdentifier) && string.Equals(e.TargetFramework.GetShortFolderName(), "net46")).Libraries.Single().RuntimeAssemblies.Select(e => e.Path)));
                Assert.Equal("ref/net46/a.dll",
                    string.Join(";", result.LockFile.Targets.First(e => string.Equals("win7-x86", e.RuntimeIdentifier) && string.Equals(e.TargetFramework.GetShortFolderName(), "net46")).Libraries.Single().CompileTimeAssemblies.Select(e => e.Path)));
                Assert.Equal("lib/net46/a.dll",
                                    string.Join(";", result.LockFile.Targets.First(e => string.Equals("win7-x86", e.RuntimeIdentifier) && string.Equals(e.TargetFramework.GetShortFolderName(), "net47")).Libraries.Single().RuntimeAssemblies.Select(e => e.Path)));
                Assert.Equal("ref/net46/a.dll",
                    string.Join(";", result.LockFile.Targets.First(e => string.Equals("win7-x86", e.RuntimeIdentifier) && string.Equals(e.TargetFramework.GetShortFolderName(), "net47")).Libraries.Single().CompileTimeAssemblies.Select(e => e.Path)));
            }
        }

        [Fact]
        public async Task RestoreCommand_WithoutCommit_DoesNotWriteAnyAssetsInMSBuildProjectExtensionsPath()
        {
            using (var pathContext = new SimpleTestPathContext())
            using (var context = new SourceCacheContext())
            {
                var configJson = JObject.Parse(@"
                {
                    ""dependencies"": {
                    },
                     ""frameworks"": {
                        ""net45"": { }
                    }
                }");

                // Arrange
                var sources = new List<PackageSource>
                {
                    new PackageSource(pathContext.PackageSource)
                };
                var logger = new TestLogger();

                var projectDirectory = Path.Combine(pathContext.SolutionRoot, "TestProject");
                var cachingSourceProvider = new CachingSourceProvider(new PackageSourceProvider(NullSettings.Instance));

                var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", Path.Combine(projectDirectory, "project.csproj")).WithTestRestoreMetadata();
                var dgSpec = new DependencyGraphSpec();
                dgSpec.AddProject(spec);
                dgSpec.AddRestore(spec.Name);

                var request = new TestRestoreRequest(spec, sources, pathContext.UserPackagesFolder, logger)
                {
                    ProjectStyle = ProjectStyle.PackageReference,
                    DependencyGraphSpec = dgSpec,
                };
                var command = new RestoreCommand(request);

                // Act
                var result = await command.ExecuteAsync();

                // Assert
                Assert.True(result.Success);
                Assert.False(Directory.Exists(projectDirectory));
            }
        }

        [Fact]
        public async Task RestoreCommand_WithCommit_WritesAllAssetsInMSBuildProjectExtensionsPath()
        {
            using (var pathContext = new SimpleTestPathContext())
            using (var context = new SourceCacheContext())
            {
                var configJson = JObject.Parse(@"
                {
                    ""dependencies"": {
                    },
                     ""frameworks"": {
                        ""net45"": { }
                    }
                }");

                // Arrange
                var sources = new List<PackageSource>
                {
                    new PackageSource(pathContext.PackageSource)
                };
                var logger = new TestLogger();

                var projectDirectory = Path.Combine(pathContext.SolutionRoot, "TestProject");
                var cachingSourceProvider = new CachingSourceProvider(new PackageSourceProvider(NullSettings.Instance));

                var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", Path.Combine(projectDirectory, "project.csproj")).WithTestRestoreMetadata();
                var dgSpec = new DependencyGraphSpec();
                dgSpec.AddProject(spec);
                dgSpec.AddRestore(spec.Name);

                var request = new TestRestoreRequest(spec, sources, pathContext.UserPackagesFolder, logger)
                {
                    ProjectStyle = ProjectStyle.PackageReference,
                    DependencyGraphSpec = dgSpec,
                };
                var command = new RestoreCommand(request);

                // Act
                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);

                // Assert
                Assert.True(result.Success);
                Assert.True(Directory.Exists(projectDirectory));
                Assert.True(File.Exists(Path.Combine(projectDirectory, "project.assets.json")));
                Assert.True(File.Exists(Path.Combine(projectDirectory, "project.nuget.cache")));
                Assert.True(File.Exists(Path.Combine(projectDirectory, "TestProject.csproj.nuget.dgspec.json")));
                Assert.True(File.Exists(Path.Combine(projectDirectory, "TestProject.csproj.nuget.g.props")));
                Assert.True(File.Exists(Path.Combine(projectDirectory, "TestProject.csproj.nuget.g.targets")));
            }
        }

        [Fact]
        public async Task RestoreCommand_WhenPackageReferenceHasAliases_IsReflectedInTheCompileTimeAssemblies()
        {
            using (var pathContext = new SimpleTestPathContext())
            using (var context = new SourceCacheContext())
            {
                var configJson = JObject.Parse(@"
                {
                    ""frameworks"": {
                        ""net5.0"": {
                            ""dependencies"": {
                                ""A"": {
                                    ""version"" : ""1.0.0"",
                                    ""aliases"" : ""Core"",
                                }
                            }
                        }
                    }
                }");

                // Arrange
                var packageA = new SimpleTestPackageContext()
                {
                    Id = "a",
                    Version = "1.0.0"
                };
                packageA.Files.Clear();
                packageA.AddFile("lib/net5.0/a.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageA);

                var sources = new List<PackageSource>
                {
                    new PackageSource(pathContext.PackageSource)
                };
                var logger = new TestLogger();

                var projectDirectory = Path.Combine(pathContext.SolutionRoot, "TestProject");
                var cachingSourceProvider = new CachingSourceProvider(new PackageSourceProvider(NullSettings.Instance));

                var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", Path.Combine(projectDirectory, "project.csproj")).WithTestRestoreMetadata();
                var dgSpec = new DependencyGraphSpec();
                dgSpec.AddProject(spec);
                dgSpec.AddRestore(spec.Name);

                var request = new TestRestoreRequest(spec, sources, pathContext.UserPackagesFolder, logger)
                {
                    ProjectStyle = ProjectStyle.PackageReference,
                    DependencyGraphSpec = dgSpec,
                };
                var command = new RestoreCommand(request);

                // Act
                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);

                // Assert
                result.Success.Should().BeTrue(because: logger.ShowMessages());
                var library = result.LockFile.Targets.First(e => e.TargetFramework.Equals(CommonFrameworks.Net50)).Libraries.Single();
                library.Should().NotBeNull("The assets file is expect to have a single library");
                library.CompileTimeAssemblies.Count.Should().Be(1, because: "The package has only 1 compatible file");
                library.CompileTimeAssemblies.Single().Path.Should().Be("lib/net5.0/a.dll");
                library.CompileTimeAssemblies.Single().Properties.Should().Contain(new KeyValuePair<string, string>(LockFileItem.AliasesProperty, "Core"));
            }
        }

        [Fact]
        public async Task RestoreCommand_WhenPackageReferenceWithAlisesHasMultipleAssemblies_ItIsReflectedInAllCompileTimeAssemblies()
        {
            using (var pathContext = new SimpleTestPathContext())
            using (var context = new SourceCacheContext())
            {
                var configJson = JObject.Parse(@"
                {
                    ""frameworks"": {
                        ""net5.0"": {
                            ""dependencies"": {
                                ""A"": {
                                    ""version"" : ""1.0.0"",
                                    ""aliases"" : ""Core"",
                                }
                            }
                        }
                    }
                }");

                // Arrange
                var packageA = new SimpleTestPackageContext()
                {
                    Id = "a",
                    Version = "1.0.0"
                };
                packageA.Files.Clear();
                packageA.AddFile("lib/net5.0/a.dll");
                packageA.AddFile("lib/net5.0/a2.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageA);

                var sources = new List<PackageSource>
                {
                    new PackageSource(pathContext.PackageSource)
                };
                var logger = new TestLogger();

                var projectDirectory = Path.Combine(pathContext.SolutionRoot, "TestProject");
                var cachingSourceProvider = new CachingSourceProvider(new PackageSourceProvider(NullSettings.Instance));

                var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", Path.Combine(projectDirectory, "project.csproj")).WithTestRestoreMetadata();
                var dgSpec = new DependencyGraphSpec();
                dgSpec.AddProject(spec);
                dgSpec.AddRestore(spec.Name);

                var request = new TestRestoreRequest(spec, sources, pathContext.UserPackagesFolder, logger)
                {
                    ProjectStyle = ProjectStyle.PackageReference,
                    DependencyGraphSpec = dgSpec,
                };
                var command = new RestoreCommand(request);

                // Act
                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);

                // Assert
                result.Success.Should().BeTrue(because: logger.ShowMessages());
                var library = result.LockFile.Targets.First(e => e.TargetFramework.Equals(CommonFrameworks.Net50)).Libraries.Single();
                library.Should().NotBeNull("The assets file is expect to have a single library");
                library.CompileTimeAssemblies.Count.Should().Be(2, because: "The package has 2 compatible files");
                foreach (var assembly in library.CompileTimeAssemblies)
                {
                    assembly.Properties.Should().Contain(new KeyValuePair<string, string>(LockFileItem.AliasesProperty, "Core"));
                }
            }
        }

        [Fact]
        public async Task RestoreCommand_WhenPackageReferenceWithInvalidAliasValueIsSpecified_ItIsPassedThrough()
        {
            using (var pathContext = new SimpleTestPathContext())
            using (var context = new SourceCacheContext())
            {
                var configJson = JObject.Parse(@"
                {
                    ""frameworks"": {
                        ""net5.0"": {
                            ""dependencies"": {
                                ""A"": {
                                    ""version"" : ""1.0.0"",
                                    ""aliases"" : ""invalid-alias:yes"",
                                }
                            }
                        }
                    }
                }");

                // Arrange
                var packageA = new SimpleTestPackageContext()
                {
                    Id = "a",
                    Version = "1.0.0"
                };
                packageA.Files.Clear();
                packageA.AddFile("lib/net5.0/a.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageA);

                var sources = new List<PackageSource>
                {
                    new PackageSource(pathContext.PackageSource)
                };
                var logger = new TestLogger();

                var projectDirectory = Path.Combine(pathContext.SolutionRoot, "TestProject");
                var cachingSourceProvider = new CachingSourceProvider(new PackageSourceProvider(NullSettings.Instance));

                var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", Path.Combine(projectDirectory, "project.csproj")).WithTestRestoreMetadata();
                var dgSpec = new DependencyGraphSpec();
                dgSpec.AddProject(spec);
                dgSpec.AddRestore(spec.Name);

                var request = new TestRestoreRequest(spec, sources, pathContext.UserPackagesFolder, logger)
                {
                    ProjectStyle = ProjectStyle.PackageReference,
                    DependencyGraphSpec = dgSpec,
                };
                var command = new RestoreCommand(request);

                // Act
                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);

                // Assert
                result.Success.Should().BeTrue(because: logger.ShowMessages());
                var library = result.LockFile.Targets.First(e => e.TargetFramework.Equals(CommonFrameworks.Net50)).Libraries.Single();
                library.Should().NotBeNull("The assets file is expect to have a single library");
                library.CompileTimeAssemblies.Count.Should().Be(1, because: "The package has 1 compatible file");
                foreach (var assembly in library.CompileTimeAssemblies)
                {
                    assembly.Properties.Should().Contain(new KeyValuePair<string, string>(LockFileItem.AliasesProperty, "invalid-alias:yes"));
                }
            }
        }

        [Fact]
        public async Task RestoreCommand_WhenRestoreNoOps_TheAssetsFileIsNotRead()
        {
            using (var pathContext = new SimpleTestPathContext())
            using (var context = new SourceCacheContext())
            {
                var configJson = JObject.Parse(@"
                {
                    ""frameworks"": {
                        ""net5.0"": {
                            ""dependencies"": {
                                ""A"": {
                                    ""version"" : ""1.0.0"",
                                }
                            }
                        }
                    }
                }");

                // Arrange
                var packageA = new SimpleTestPackageContext("a", "1.0.0");
                packageA.Files.Clear();
                packageA.AddFile("lib/net5.0/a.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageA);

                var sources = new List<PackageSource>
                {
                    new PackageSource(pathContext.PackageSource)
                };
                var logger = new TestLogger();

                var projectDirectory = Path.Combine(pathContext.SolutionRoot, "TestProject");
                var cachingSourceProvider = new CachingSourceProvider(new PackageSourceProvider(NullSettings.Instance));

                var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", Path.Combine(projectDirectory, "project.csproj")).WithTestRestoreMetadata();
                var dgSpec = new DependencyGraphSpec();
                dgSpec.AddProject(spec);
                dgSpec.AddRestore(spec.Name);

                var request = new TestRestoreRequest(spec, sources, pathContext.UserPackagesFolder, logger)
                {
                    ProjectStyle = ProjectStyle.PackageReference,
                    DependencyGraphSpec = dgSpec,
                    AllowNoOp = true,
                };
                var command = new RestoreCommand(request);

                // Preconditions
                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);
                result.Success.Should().BeTrue(because: logger.ShowMessages());

                // Modify the assets file. No-op restore should not read the assets file, if it does, it will throw.
                File.WriteAllText(Path.Combine(spec.RestoreMetadata.OutputPath, "project.assets.json"), "<xml> </xml>");
                var newSpec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", Path.Combine(projectDirectory, "project.csproj")).WithTestRestoreMetadata();
                var newDgSpec = new DependencyGraphSpec();
                newDgSpec.AddProject(newSpec);
                newDgSpec.AddRestore(newSpec.Name);

                var newRequest = new TestRestoreRequest(spec, sources, pathContext.UserPackagesFolder, logger)
                {
                    ProjectStyle = ProjectStyle.PackageReference,
                    DependencyGraphSpec = dgSpec,
                    AllowNoOp = true,
                };
                var newCommand = new RestoreCommand(newRequest);

                // Act
                result = await newCommand.ExecuteAsync();
                // Assert

                await result.CommitAsync(logger, CancellationToken.None);
                result.Success.Should().BeTrue(because: logger.ShowMessages());
                result.Should().BeAssignableTo<NoOpRestoreResult>(because: "This should be a no-op restore.");
            }
        }

        [Fact]
        public async Task RestoreCommand_WhenPlatformVersionIsEmpty_ThrowsError()
        {
            using (var pathContext = new SimpleTestPathContext())
            using (var context = new SourceCacheContext())
            {
                var configJson = JObject.Parse(@"
                {
                    ""frameworks"": {
                        ""net5.0-windows"": {
                            ""dependencies"": {
                                ""A"": {
                                    ""version"" : ""1.0.0"",
                                }
                            }
                        }
                    }
                }");

                // Arrange
                var packageA = new SimpleTestPackageContext("a", "1.0.0");
                packageA.Files.Clear();
                packageA.AddFile("lib/net5.0-windows/a.dll");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    pathContext.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageA);

                var sources = new List<PackageSource>
                {
                    new PackageSource(pathContext.PackageSource)
                };
                var logger = new TestLogger();

                var projectDirectory = Path.Combine(pathContext.SolutionRoot, "TestProject");
                var cachingSourceProvider = new CachingSourceProvider(new PackageSourceProvider(NullSettings.Instance));

                var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", Path.Combine(projectDirectory, "project.csproj")).WithTestRestoreMetadata();
                var dgSpec = new DependencyGraphSpec();
                dgSpec.AddProject(spec);
                dgSpec.AddRestore(spec.Name);

                var request = new TestRestoreRequest(spec, sources, pathContext.UserPackagesFolder, logger)
                {
                    ProjectStyle = ProjectStyle.PackageReference,
                    DependencyGraphSpec = dgSpec,
                    AllowNoOp = true,
                };
                var command = new RestoreCommand(request);

                // Act
                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);

                // Assert
                Assert.False(result.Success);
                Assert.Equal(1, logger.ErrorMessages.Count);
                logger.ErrorMessages.TryDequeue(out var errorMessage);
                Assert.True(errorMessage.Contains("Platform version"));
                var messagesForNU1012 = result.LockFile.LogMessages.Where(m => m.Code == NuGetLogCode.NU1012);
                Assert.Equal(1, messagesForNU1012.Count());
            }
        }

        private static byte[] GetTestUtilityResource(string name)
        {
            return ResourceTestUtility.GetResourceBytes(
                $"Test.Utility.compiler.resources.{name}",
                typeof(ResourceTestUtility));
        }

        private static List<LockFileItem> GetRuntimeAssemblies(IList<LockFileTarget> targets, string framework, string runtime)
        {
            return GetRuntimeAssemblies(targets, NuGetFramework.Parse(framework), runtime);
        }

        private static List<LockFileItem> GetRuntimeAssemblies(IList<LockFileTarget> targets, NuGetFramework framework, string runtime)
        {
            return targets.Where(target => target.TargetFramework.Equals(framework))
                .Where(target => target.RuntimeIdentifier == runtime)
                .SelectMany(target => target.Libraries)
                .SelectMany(library => library.RuntimeAssemblies)
                .ToList();
        }

        private static void AddDependency(PackageSpec spec, string id, string version)
        {
            var target = new LibraryDependency()
            {
                LibraryRange = new LibraryRange()
                {
                    Name = id,
                    VersionRange = VersionRange.Parse(version),
                    TypeConstraint = LibraryDependencyTarget.Package
                }
            };

            if (spec.Dependencies == null)
            {
                spec.Dependencies = new List<LibraryDependency>();
            }

            spec.Dependencies.Add(target);
        }

        private static JObject BasicConfig
        {
            get
            {


                var frameworks = new JObject
                {
                    ["netcore50"] = new JObject()
                };

                var json = new JObject
                {
                    ["dependencies"] = new JObject(),

                    ["frameworks"] = frameworks
                };

                json.Add("runtimes", JObject.Parse("{ \"uap10-x86\": { }, \"uap10-x86-aot\": { } }"));

                return json;
            }
        }

        private static JObject BasicConfigWithNet46
        {
            get
            {


                var frameworks = new JObject
                {
                    ["net46"] = new JObject()
                };

                var json = new JObject
                {
                    ["dependencies"] = new JObject(),

                    ["frameworks"] = frameworks
                };

                json.Add("runtimes", JObject.Parse("{ \"uap10-x86\": { }, \"uap10-x86-aot\": { } }"));

                return json;
            }
        }
    }
}
