using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.RuntimeModel;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Commands.FuncTest
{
    public class RestoreCommandTests
    {
        [Theory]
        [InlineData("https://www.nuget.org/api/v2/", new Type[0])]
        [InlineData("https://api.nuget.org/v3/index.json", new[] { typeof(RemoteV3FindPackageByIdResourceProvider) })]
        [InlineData("https://api.nuget.org/v3/index.json", new[] { typeof(HttpFileSystemBasedFindPackageByIdResourceProvider) })]
        public async Task RestoreCommand_LockFileHasOriginalPackageIdCase(string source, Type[] excludedProviders)
        {
            // Arrange
            var providers = Repository
                .Provider
                .GetCoreV3()
                .Where(x => !excludedProviders.Contains(x.Value.GetType()));
            var sourceRepository = Repository.CreateSource(providers, source);
            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfigWithNet46.ToString(), "TestProject", specPath);

                AddDependency(spec, "ENTITYFRAMEWORK", "6.1.3-BETA1");
                var logger = new TestLogger();
                var request = new RestoreRequest(spec, new[] { sourceRepository }, packagesDir, Enumerable.Empty<string>(), logger);
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
        public async Task RestoreCommand_LockFileHasOriginalVersionCase()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
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
                JsonPackageSpecWriter.WritePackageSpec(referenceSpec, referenceSpecPath);

                var logger = new TestLogger();
                var request = new RestoreRequest(projectSpec, sources, packagesDir, logger);
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
        public async Task RestoreCommand_CannotFindProjectReferenceWithDifferentNameCase()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                Directory.CreateDirectory(Path.Combine(projectDir, "TestProject"));
                var projectSpecPath = Path.Combine(projectDir, "TestProject", "project.json");
                var projectSpec = JsonPackageSpecReader.GetPackageSpec(BasicConfigWithNet46.ToString(), "TestProject", projectSpecPath);
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
                JsonPackageSpecWriter.WritePackageSpec(referenceSpec, referenceSpecPath);

                var logger = new TestLogger();
                var request = new RestoreRequest(projectSpec, sources, packagesDir, logger);
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
        public async Task RestoreCommand_DependenciesOfDifferentCase()
        {
            // Arrange
            var sources = new List<PackageSource> { new PackageSource("https://www.nuget.org/api/v2/") };

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
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
                var request = new RestoreRequest(specA, sources, packagesDir, logger)
                {
                    LockFilePath = Path.Combine(specDirA, "project.lock.json")
                };

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
        public async Task RestoreCommand_VerifyMinClientVersionV2Source()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath);

                // This package has a minclientversion of 9999
                AddDependency(spec, "TestPackage.MinClientVersion", "1.0.0");

                var logger = new TestLogger();
                var request = new RestoreRequest(spec, sources, packagesDir, logger);

                request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

                var lockFileFormat = new LockFileFormat();
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
        public async Task RestoreCommand_VerifyMinClientVersionV3Source()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://api.nuget.org/v3/index.json"));

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath);

                // This package has a minclientversion of 9999
                AddDependency(spec, "TestPackage.MinClientVersion", "1.0.0");

                var logger = new TestLogger();
                var request = new RestoreRequest(spec, sources, packagesDir, logger);

                request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

                var lockFileFormat = new LockFileFormat();

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
        public async Task RestoreCommand_VerifyMinClientVersionLocalFolder()
        {
            // Arrange
            var sources = new List<PackageSource>();

            using (var sourceDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                sources.Add(new PackageSource(sourceDir));
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath);

                // This package has a minclientversion of 9.9999.0
                AddDependency(spec, "packageA", "1.0.0");

                var logger = new TestLogger();
                var request = new RestoreRequest(spec, sources, packagesDir, logger);

                request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

                var lockFileFormat = new LockFileFormat();

                var packageContext = new SimpleTestPackageContext()
                {
                    Id = "packageA",
                    Version = "1.0.0",
                    MinClientVersion = "9.9.9"
                };

                SimpleTestPackageUtility.CreatePackages(sourceDir, packageContext);

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
        public async Task RestoreCommand_VerifyMinClientVersionAlreadyInstalled()
        {
            // Arrange
            var sources = new List<PackageSource>();

            using (var emptyDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                sources.Add(new PackageSource(emptyDir));
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath);

                // This package has a minclientversion of 9.9999.0
                AddDependency(spec, "packageA", "1.0.0");

                var logger = new TestLogger();
                var request = new RestoreRequest(spec, sources, packagesDir, logger);

                request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

                var lockFileFormat = new LockFileFormat();

                var packageContext = new SimpleTestPackageContext()
                {
                    Id = "packageA",
                    Version = "1.0.0",
                    MinClientVersion = "9.9.9"
                };

                var packagePath = Path.Combine(workingDir, "packageA.1.0.0.nupkg");

                SimpleTestPackageUtility.CreatePackages(workingDir, packageContext);

                // install the package
                using (var fileStream = File.OpenRead(packagePath))
                {
                    await PackageExtractor.InstallFromSourceAsync((stream) =>
                        fileStream.CopyToAsync(stream, 4096, CancellationToken.None),
                        new VersionFolderPathContext(
                            new PackageIdentity("packageA", NuGetVersion.Parse("1.0.0")),
                            packagesDir,
                            logger,
                            PackageSaveMode.Defaultv3,
                            XmlDocFileSaveMode.None),
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
        public async Task RestoreCommand_FrameworkImportRulesAreApplied()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
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
                var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);

                var logger = new TestLogger();
                var request = new RestoreRequest(spec, sources, packagesDir, logger);

                request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

                var lockFileFormat = new LockFileFormat();
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
        public async Task RestoreCommand_FrameworkImportArray()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
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
                var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);

                var logger = new TestLogger();
                var request = new RestoreRequest(spec, sources, packagesDir, logger);

                request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

                var lockFileFormat = new LockFileFormat();
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
        public async Task RestoreCommand_FrameworkImportRulesAreApplied_Noop()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
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
                var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);

                var logger = new TestLogger();
                var request = new RestoreRequest(spec, sources, packagesDir, logger);

                request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

                var lockFileFormat = new LockFileFormat();
                var command = new RestoreCommand(request);
                var framework = new FallbackFramework(NuGetFramework.Parse("dotnet"), new List<NuGetFramework> { NuGetFramework.Parse("portable-net452+win81") });
                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);
                logger.Clear();

                // Act
                request = new RestoreRequest(spec, sources, packagesDir, logger);

                request.LockFilePath = Path.Combine(projectDir, "project.lock.json");
                request.ExistingLockFile = result.LockFile;
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
        public async Task RestoreCommand_LeftOverNupkg_Overwritten()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
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
                var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);
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
                var request = new RestoreRequest(spec, sources, packagesDir, logger);

                var command = new RestoreCommand(request);

                // Act
                var result = await command.ExecuteAsync();

                // Assert
                var newFileSize = new FileInfo(nupkgPath).Length;

                Assert.True(newFileSize > 0, "Downloaded file not overriding the dummy nupkg");
            }
        }

        [Fact]
        public async Task RestoreCommand_FrameworkImport_WarnOn()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
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
                var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);

                var logger = new TestLogger();
                var request = new RestoreRequest(spec, sources, packagesDir, logger);

                request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

                var lockFileFormat = new LockFileFormat();
                var command = new RestoreCommand(request);
                var framework = new FallbackFramework(NuGetFramework.Parse("dotnet"), new List<NuGetFramework> { NuGetFramework.Parse("portable-net452+win81") });
                var warning = "Package 'Newtonsoft.Json 7.0.1' was restored using '.NETPortable,Version=v0.0,Profile=net452+win81' instead the project target framework '.NETPlatform,Version=v5.0'. This may cause compatibility problems.";

                // Act
                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);
                var runtimeAssemblies = GetRuntimeAssemblies(result.LockFile.Targets, framework, null);
                var runtimeAssembly = runtimeAssemblies.FirstOrDefault();

                // Assert
                Assert.Equal(1, result.GetAllInstalled().Count);
                Assert.Equal(0, logger.Errors);
                Assert.Equal(1, logger.Warnings);
                Assert.Equal(1, logger.Messages.Where(message => message.Equals(warning)).Count());
            }
        }

        [Fact]
        public async Task RestoreCommand_FollowFallbackDependencies()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
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
                var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);

                var logger = new TestLogger();
                var request = new RestoreRequest(spec, sources, packagesDir, logger);

                request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

                var lockFileFormat = new LockFileFormat();
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
        public async Task RestoreCommand_FrameworkImportValidateLockFile()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
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
                var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);

                var logger = new TestLogger();
                var request = new RestoreRequest(spec, sources, packagesDir, logger);

                request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

                var lockFileFormat = new LockFileFormat();
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
        public async Task RestoreCommand_DependenciesDifferOnCase()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var json = new JObject();

                var frameworks = new JObject();
                frameworks["net46"] = new JObject();

                json["dependencies"] = new JObject();

                json["frameworks"] = frameworks;

                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(json.ToString(), "TestProject", specPath);

                AddDependency(spec, "nEwTonSoft.JSon", "6.0.8");
                AddDependency(spec, "json-ld.net", "1.0.4");

                var logger = new TestLogger();
                var request = new RestoreRequest(spec, sources, packagesDir, logger);

                request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

                var lockFileFormat = new LockFileFormat();
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
        public async Task RestoreCommand_DependenciesDifferOnCase_Downgrade()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var json = new JObject();

                var frameworks = new JObject();
                frameworks["net46"] = new JObject();

                json["dependencies"] = new JObject();

                json["frameworks"] = frameworks;

                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(json.ToString(), "TestProject", specPath);

                AddDependency(spec, "nEwTonSoft.JSon", "4.0.1");
                AddDependency(spec, "dotNetRDF", "1.0.8.3533");

                var logger = new TestLogger();
                var request = new RestoreRequest(spec, sources, packagesDir, logger);

                request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

                var lockFileFormat = new LockFileFormat();
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
        public async Task RestoreCommand_TestLockFileWrittenOnLockFileChange()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath);

                AddDependency(spec, "NuGet.Versioning", "1.0.7");

                var logger = new TestLogger();
                var request = new RestoreRequest(spec, sources, packagesDir, logger);

                request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

                var lockFileFormat = new LockFileFormat();
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();

                var lockFilePath = Path.Combine(projectDir, "project.lock.json");

                // Change the lock file and write it out to disk
                var modifiedLockFile = result.LockFile;
                modifiedLockFile.Version = 1000;

                var lockFormat = new LockFileFormat();
                lockFormat.Write(File.OpenWrite(lockFilePath), modifiedLockFile);

                request = new RestoreRequest(spec, sources, packagesDir, logger);

                request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

                // Act
                request.ExistingLockFile = modifiedLockFile;

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
        public async Task RestoreCommand_WriteLockFileOnForce()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath);

                AddDependency(spec, "NuGet.Versioning", "1.0.7");

                var logger = new TestLogger();
                var request = new RestoreRequest(spec, sources, packagesDir, logger);

                request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

                var lockFileFormat = new LockFileFormat();
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);

                var lockFilePath = Path.Combine(projectDir, "project.lock.json");

                // Add white space to the end of the file
                var whitespace = $"{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}";
                File.AppendAllText(lockFilePath, whitespace);

                // Act
                request = new RestoreRequest(spec, sources, packagesDir, logger);

                request.LockFilePath = Path.Combine(projectDir, "project.lock.json");
                var previousLockFile = result.LockFile;
                request.ExistingLockFile = result.LockFile;

                command = new RestoreCommand(request);
                result = await command.ExecuteAsync();
                await result.CommitAsync(logger, true, CancellationToken.None);

                var output = File.ReadAllText(lockFilePath);

                // Assert
                // The file should committed and it should clear the whitespace
                Assert.False(output.EndsWith(whitespace, StringComparison.OrdinalIgnoreCase));
            }
        }

        [Fact]
        public async Task RestoreCommand_NoopOnLockFileWriteIfFilesMatch()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath);

                AddDependency(spec, "NuGet.Versioning", "1.0.7");

                var logger = new TestLogger();
                var request = new RestoreRequest(spec, sources, packagesDir, logger);

                request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

                var lockFileFormat = new LockFileFormat();
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);

                var lockFilePath = Path.Combine(projectDir, "project.lock.json");

                // Act
                var lastDate = File.GetLastWriteTime(lockFilePath);

                request = new RestoreRequest(spec, sources, packagesDir, logger);

                request.LockFilePath = Path.Combine(projectDir, "project.lock.json");
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
        public async Task RestoreCommand_NuGetVersioning107RuntimeAssemblies()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath);

                AddDependency(spec, "NuGet.Versioning", "1.0.7");

                var logger = new TestLogger();
                var request = new RestoreRequest(spec, sources, packagesDir, logger);

                request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

                var lockFileFormat = new LockFileFormat();

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
        public async Task RestoreCommand_InstallPackageWithDependencies()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath);

                AddDependency(spec, "WebGrease", "1.6.0");

                var logger = new TestLogger();
                var request = new RestoreRequest(spec, sources, packagesDir, logger);

                request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

                var lockFileFormat = new LockFileFormat();

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
        public async Task RestoreCommand_InstallPackageWithManyDependencies()
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

            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
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
                var spec1 = JsonPackageSpecReader.GetPackageSpec(project1Json, "project1", specPath1);
                var logger = new TestLogger();
                var request = new RestoreRequest(spec1, sources, packagesDir.FullName, logger);

                request.LockFilePath = Path.Combine(project1.FullName, "project.lock.json");

                var packages = new List<SimpleTestPackageContext>();
                var dependencies = new List<SimpleTestPackageContext>();

                for (int i = 0; i < 500; i++)
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
                SimpleTestPackageUtility.CreatePackages(packages, packageSource.FullName);

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
        public async Task RestoreCommand_InstallPackageWithReferenceDependencies()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfigWithNet46.ToString(), "TestProject", specPath);

                AddDependency(spec, "Moon.Owin.Localization", "1.3.1");

                var logger = new TestLogger();
                var request = new RestoreRequest(spec, sources, packagesDir, logger);

                request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

                var lockFileFormat = new LockFileFormat();

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
        public async Task RestoreCommand_RestoreWithNoChanges()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath);

                AddDependency(spec, "NuGet.Versioning", "1.0.7");

                var logger = new TestLogger();
                var request = new RestoreRequest(spec, sources, packagesDir, logger);

                request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

                var command = new RestoreCommand(request);
                var firstRun = await command.ExecuteAsync();

                // Act
                request = new RestoreRequest(spec, sources, packagesDir, logger);
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
        [InlineData("https://www.nuget.org/api/v2/")]
        [InlineData("https://api.nuget.org/v3/index.json")]
        public async Task RestoreCommand_PackageIsAddedToPackageCache(string source)
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource(source));

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath);

                AddDependency(spec, "NuGet.Versioning", "1.0.7");

                var logger = new TestLogger();
                var request = new RestoreRequest(spec, sources, packagesDir, logger);

                request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

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
        [InlineData("https://www.nuget.org/api/v2/")]
        [InlineData("https://api.nuget.org/v3/index.json")]
        public async Task RestoreCommand_PackagesAreExtractedToTheNormalizedPath(string source)
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource(source));

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath);

                AddDependency(spec, "owin", "1.0");

                var logger = new TestLogger();
                var request = new RestoreRequest(spec, sources, packagesDir, logger);

                request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

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
        public async Task RestoreCommand_WarnWhenWeBumpYouUp()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath);

                AddDependency(spec, "Newtonsoft.Json", "7.0.0"); // 7.0.0 does not exist so we'll bump up to 7.0.1

                var logger = new TestLogger();
                var request = new RestoreRequest(spec, sources, packagesDir, logger);

                request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

                var lockFileFormat = new LockFileFormat();

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                var installed = result.GetAllInstalled();
                var unresolved = result.GetAllUnresolved();
                var runtimeAssemblies = GetRuntimeAssemblies(result.LockFile.Targets, "netcore50", null);

                var runtimeAssembly = runtimeAssemblies.FirstOrDefault();

                // Assert
                Assert.Equal(3, logger.Warnings); // We'll get the warning for each runtime and for the runtime-less restore.
                Assert.Contains("Dependency specified was Newtonsoft.Json (>= 7.0.0) but ended up with Newtonsoft.Json 7.0.1.", logger.Messages);
            }
        }

        [Fact]
        public async Task RestoreCommand_JsonNet701RuntimeAssemblies()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath);

                AddDependency(spec, "Newtonsoft.Json", "7.0.1");

                var logger = new TestLogger();
                var request = new RestoreRequest(spec, sources, packagesDir, logger);

                request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

                var lockFileFormat = new LockFileFormat();

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
        public async Task RestoreCommand_NoCompatibleRuntimeAssembliesForProject()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath);

                AddDependency(spec, "NuGet.Core", "2.8.3");

                var logger = new TestLogger();
                var request = new RestoreRequest(spec, sources, packagesDir, logger);

                request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

                var lockFileFormat = new LockFileFormat();

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
                Assert.Contains(expectedIssue.Format(), logger.Messages);

                Assert.Equal(9, logger.Errors);
                Assert.Equal(2, installed.Count);
                Assert.Equal(0, unresolved.Count);
                Assert.Equal(0, runtimeAssemblies.Count);
            }
        }

        [Fact]
        public async Task RestoreCommand_CorrectlyIdentifiesUnresolvedPackages()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath);

                AddDependency(spec, "NotARealPackage.ThisShouldNotExists.DontCreateIt.Seriously.JustDontDoIt.Please", "2.8.3");

                var logger = new TestLogger();
                var request = new RestoreRequest(spec, sources, packagesDir, logger);

                request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

                var lockFileFormat = new LockFileFormat();

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
        public async Task RestoreCommand_PopulatesProjectFileDependencyGroupsCorrectly()
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
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(project, "TestProject", specPath);

                var logger = new TestLogger();
                var request = new RestoreRequest(spec, sources, packagesDir, logger);

                request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

                var lockFileFormat = new LockFileFormat();

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
        public async Task RestoreCommand_CanInstallPackageWithSatelliteAssemblies()
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
            var sources = new List<PackageSource>();

            sources.Add(new PackageSource("https://www.nuget.org/api/v2"));

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(project, "TestProject", specPath);

                var logger = new TestLogger();
                var request = new RestoreRequest(spec, sources, packagesDir, logger);

                request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

                var lockFileFormat = new LockFileFormat();

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();

                // Assert
                Assert.True(result.Success);
            }
        }

        [Fact]
        public async Task RestoreCommand_UnmatchedRefAndLibAssemblies()
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
            var sources = new List<PackageSource>();

            sources.Add(new PackageSource("https://nuget.org/api/v2/"));

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(project, "TestProject", specPath);

                var logger = new TestLogger();
                var request = new RestoreRequest(spec, sources, packagesDir, logger);

                request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

                var lockFileFormat = new LockFileFormat();

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

        [Fact]
        public async Task RestoreCommand_LockedLockFileWithOutOfDateProject()
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
            var sources = new List<PackageSource>();

            sources.Add(new PackageSource("https://nuget.org/api/v2/"));

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(project, "TestProject", specPath);

                var lockFileFormat = new LockFileFormat();
                var lockFile = lockFileFormat.Parse(lockFileContent, "In Memory");

                var logger = new TestLogger();
                var request = new RestoreRequest(spec, sources, packagesDir, logger);

                request.ExistingLockFile = lockFile;

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
        public async Task RestoreCommand_RestoreExactVersionWithFailingSource()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://failingSource"));
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
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
                var spec = JsonPackageSpecReader.GetPackageSpec(configJson.ToString(), "TestProject", specPath);

                var logger = new TestLogger();
                var request = new RestoreRequest(spec, sources, packagesDir, logger);

                request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

                var lockFileFormat = new LockFileFormat();
                var command = new RestoreCommand(request);

                // Act
                var result = await command.ExecuteAsync();

                // Assert
                Assert.True(result.Success);
            }
        }

        [Fact]
        public async Task RestoreCommand_RestoreFloatingVersionWithFailingHttpSource()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://failingSource"));
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
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
                var request = new RestoreRequest(spec, sources, packagesDir, logger);

                request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

                var lockFileFormat = new LockFileFormat();
                var command = new RestoreCommand(request);

                // Act & Assert
                var ex = await Assert.ThrowsAsync<FatalProtocolException>(async () => await command.ExecuteAsync());
                Assert.NotNull(ex);
            }
        }

        [Fact]
        public async Task RestoreCommand_RestoreFloatingVersionWithFailingLocalSource()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("\\failingSource"));
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
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
                var request = new RestoreRequest(spec, sources, packagesDir, logger);

                request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

                var lockFileFormat = new LockFileFormat();
                var command = new RestoreCommand(request);

                // Act & Assert
                var ex = await Assert.ThrowsAsync<FatalProtocolException>(async () => await command.ExecuteAsync());
                Assert.NotNull(ex);
            }
        }

        [Fact]
        public async Task RestoreCommand_RestoreFloatingVersionWithIgnoreFailingLocalSource()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("\\failingSource"));
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
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
                var context = new SourceCacheContext();
                context.IgnoreFailedSources = true;
                var cachingSourceProvider = new CachingSourceProvider(new PackageSourceProvider(NullSettings.Instance));

                var provider = RestoreCommandProviders.Create(packagesDir, new List<string>(), sources.Select(p => cachingSourceProvider.CreateRepository(p)), context, logger);
                var request = new RestoreRequest(spec, provider, logger);

                request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

                var lockFileFormat = new LockFileFormat();
                var command = new RestoreCommand(request);

                // Act
                var result = await command.ExecuteAsync();

                // Assert
                Assert.True(result.Success);
            }
        }

        [Fact]
        public async Task RestoreCommand_RestoreFloatingVersionWithIgnoreFailingHttpSource()
        {
            // Arrange
            var sources = new List<PackageSource>();
            sources.Add(new PackageSource("https://failingSource"));
            sources.Add(new PackageSource("https://www.nuget.org/api/v2/"));

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
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
                var context = new SourceCacheContext();
                context.IgnoreFailedSources = true;
                var cachingSourceProvider = new CachingSourceProvider(new PackageSourceProvider(NullSettings.Instance));

                var provider = RestoreCommandProviders.Create(packagesDir, new List<string>(), sources.Select(p => cachingSourceProvider.CreateRepository(p)), context, logger);
                var request = new RestoreRequest(spec, provider, logger);

                request.LockFilePath = Path.Combine(projectDir, "project.lock.json");

                var lockFileFormat = new LockFileFormat();
                var command = new RestoreCommand(request);

                // Act
                var result = await command.ExecuteAsync();

                // Assert
                Assert.True(result.Success);
            }
        }

        [Fact]
        public async Task RestoreCommand_DirectDownloadByProjectJSon()
        {
            // Arrange
            var sources = new List<PackageSource>();
            var packageSource = new PackageSource("https://api.nuget.org/v3/index.json");
            sources.Add(packageSource);

            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var settings = new Settings(projectDir);
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = JsonPackageSpecReader.GetPackageSpec(BasicConfig.ToString(), "TestProject", specPath);

                AddDependency(spec, "NuGet.Versioning", "1.0.7");

                var cachingSourceProvider = new CachingSourceProvider(new PackageSourceProvider(NullSettings.Instance));
                using (var cacheContext = new SourceCacheContext())
                {
                    cacheContext.NoCache = true;
                    var logger = new TestLogger();
                    var provider = RestoreCommandProviders.Create(packagesDir, new List<string>(), sources.Select(p => cachingSourceProvider.CreateRepository(p)), cacheContext, logger);
                    var request = new RestoreRequest(spec, provider, logger);

                    // Act
                    var command = new RestoreCommand(request);
                    var result = await command.ExecuteAsync();

                    // Assert
                    var pathResolver = new VersionFolderPathResolver(packagesDir);
                    var nuspecPath = pathResolver.GetManifestFilePath("NuGet.Versioning", new NuGetVersion("1.0.7"));
                    Assert.True(File.Exists(nuspecPath));

                    // TODO: Check that the v3-cache was not populated
                }
            }
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

        private static void AddRuntime(PackageSpec spec, string rid)
        {
            spec.RuntimeGraph = RuntimeGraph.Merge(
                spec.RuntimeGraph,
                new RuntimeGraph(new[]
                {
                    new RuntimeDescription(rid)
                }));
        }

        private static void AddDependency(PackageSpec spec, string id, string version)
        {
            var target = new LibraryDependency()
            {
                LibraryRange = new LibraryRange()
                {
                    Name = id,
                    VersionRange = VersionRange.Parse(version)
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
                var json = new JObject();

                var frameworks = new JObject();
                frameworks["netcore50"] = new JObject();

                json["dependencies"] = new JObject();

                json["frameworks"] = frameworks;

                json.Add("runtimes", JObject.Parse("{ \"uap10-x86\": { }, \"uap10-x86-aot\": { } }"));

                return json;
            }
        }

        private static JObject BasicConfigWithNet46
        {
            get
            {
                var json = new JObject();

                var frameworks = new JObject();
                frameworks["net46"] = new JObject();

                json["dependencies"] = new JObject();

                json["frameworks"] = frameworks;

                json.Add("runtimes", JObject.Parse("{ \"uap10-x86\": { }, \"uap10-x86-aot\": { } }"));

                return json;
            }
        }
    }
}
