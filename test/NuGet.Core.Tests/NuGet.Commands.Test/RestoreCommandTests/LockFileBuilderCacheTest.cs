// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Signing;
using NuGet.ProjectModel;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Test;
using NuGet.RuntimeModel;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Test.Utility.Commands;
using Xunit;

namespace NuGet.Commands.Test.RestoreCommandTests
{
    [Collection(nameof(NotThreadSafeResourceCollection))]
    public class LockFileBuilderCacheTest
    {
        [Theory]
        [InlineData(64, 0, 1)]
        [InlineData(65, 0, 64)]
        public void GetLockFileTargetLibrary_DifferentPruningOutcomes_DoesNotReuseTargetLibrary(int originalDependencyCount, int firstRemovedPosition, int secondRemovedPosition)
        {
            // Arrange
            var framework = NuGetFramework.Parse("net10.0");
            var graph = RestoreTargetGraph.Create(
                runtimeGraph: RuntimeGraph.Empty,
                graphs: [],
                context: null!,
                targetAlias: "net10.0",
                framework: framework,
                runtimeIdentifier: "");
            var package = new NuGet.Repositories.LocalPackageInfo(
                packageId: "A",
                version: NuGetVersion.Parse("1.0.0"),
                path: "packageA",
                manifestPath: "",
                zipPath: "",
                sha512Path: "",
                nuspec: new Lazy<NuspecReader>(() => null!),
                files: new Lazy<IReadOnlyList<string>>(() => []),
                sha512: new Lazy<string>(() => ""),
                runtimeGraph: new Lazy<RuntimeGraph?>(() => RuntimeGraph.Empty));
            var cache = new LockFileBuilderCache();
            var firstDependencies = CreatePrunedDependencies(originalDependencyCount, firstRemovedPosition);
            var secondDependencies = CreatePrunedDependencies(originalDependencyCount, secondRemovedPosition);
            var firstPruning = CreatePruningResult(originalDependencyCount, firstRemovedPosition);
            var secondPruning = CreatePruningResult(originalDependencyCount, secondRemovedPosition);
            var firstExpected = new LockFileTargetLibrary { Name = "First" };
            var secondExpected = new LockFileTargetLibrary { Name = "Second" };

            // Act
            (var firstActual, _, _, _) = cache.GetLockFileTargetLibrary(
                graph: graph,
                framework: framework,
                localPackageInfo: package,
                aliases: "alias",
                libraryIncludeFlags: LibraryIncludeFlags.All,
                dependencies: firstDependencies,
                restoreEnableAnalyzerAssets: false,
                valueFactory: () => (firstExpected, false, framework, framework),
                pruningResult: firstPruning);
            (var secondActual, _, _, _) = cache.GetLockFileTargetLibrary(
                graph: graph,
                framework: framework,
                localPackageInfo: package,
                aliases: "alias",
                libraryIncludeFlags: LibraryIncludeFlags.All,
                dependencies: secondDependencies,
                restoreEnableAnalyzerAssets: false,
                valueFactory: () => (secondExpected, false, framework, framework),
                pruningResult: secondPruning);

            // Assert
            Assert.Equal(firstDependencies.Count, secondDependencies.Count);
            Assert.Same(firstExpected, firstActual);
            Assert.Same(secondExpected, secondActual);
        }

        [Theory]
        [InlineData(64, 0)]
        [InlineData(65, 64)]
        public void GetLockFileTargetLibrary_IdenticalPruningOutcomes_ReusesTargetLibrary(int originalDependencyCount, int removedPosition)
        {
            // Arrange
            var framework = NuGetFramework.Parse("net10.0");
            var graph = RestoreTargetGraph.Create(
                runtimeGraph: RuntimeGraph.Empty,
                graphs: [],
                context: null!,
                targetAlias: "net10.0",
                framework: framework,
                runtimeIdentifier: "");
            var package = new NuGet.Repositories.LocalPackageInfo(
                packageId: "A",
                version: NuGetVersion.Parse("1.0.0"),
                path: "packageA",
                manifestPath: "",
                zipPath: "",
                sha512Path: "",
                nuspec: new Lazy<NuspecReader>(() => null!),
                files: new Lazy<IReadOnlyList<string>>(() => []),
                sha512: new Lazy<string>(() => ""),
                runtimeGraph: new Lazy<RuntimeGraph?>(() => RuntimeGraph.Empty));
            var cache = new LockFileBuilderCache();
            var firstDependencies = CreatePrunedDependencies(originalDependencyCount, removedPosition);
            var secondDependencies = CreatePrunedDependencies(originalDependencyCount, removedPosition);
            var firstPruning = CreatePruningResult(originalDependencyCount, removedPosition);
            var secondPruning = CreatePruningResult(originalDependencyCount, removedPosition);
            var expected = new LockFileTargetLibrary { Name = "First" };
            var alternative = new LockFileTargetLibrary { Name = "Second" };

            // Act
            (var firstActual, _, _, _) = cache.GetLockFileTargetLibrary(
                graph: graph,
                framework: framework,
                localPackageInfo: package,
                aliases: "alias",
                libraryIncludeFlags: LibraryIncludeFlags.All,
                dependencies: firstDependencies,
                restoreEnableAnalyzerAssets: false,
                valueFactory: () => (expected, false, framework, framework),
                pruningResult: firstPruning);
            (var secondActual, _, _, _) = cache.GetLockFileTargetLibrary(
                graph: graph,
                framework: framework,
                localPackageInfo: package,
                aliases: "alias",
                libraryIncludeFlags: LibraryIncludeFlags.All,
                dependencies: secondDependencies,
                restoreEnableAnalyzerAssets: false,
                valueFactory: () => (alternative, false, framework, framework),
                pruningResult: secondPruning);

            // Assert
            Assert.Same(expected, firstActual);
            Assert.Same(expected, secondActual);
        }

        [Fact]
        public async Task LockFileBuilderCache_DifferentPruningOutcomesForManyDependencies_PreservesPackageDependencies()
        {
            // Arrange
            using var tmpPath = new SimpleTestPathContext();
            const int OriginalDependencyCount = 65;
            var framework = NuGetFramework.Parse("net10.0");
            var packageA = new SimpleTestPackageContext("PackageA", "1.0.0")
            {
                Dependencies = Enumerable.Range(0, OriginalDependencyCount)
                    .Select(position => new SimpleTestPackageContext($"Dependency{position:D2}", "1.0.0"))
                    .ToList()
            };
            var logger = new TestLogger();
            var lockFileBuilderCache = new LockFileBuilderCache();
            var project1Directory = Path.Combine(tmpPath.SolutionRoot, "Library1");
            var project2Directory = Path.Combine(tmpPath.SolutionRoot, "Library2");
            Directory.CreateDirectory(project1Directory);
            Directory.CreateDirectory(project2Directory);

            PackageSpec CreateProjectSpec(string projectName, string projectDirectory, int removedPosition)
            {
                string dependencyToPrune = $"Dependency{removedPosition:D2}";
                return PackageReferenceSpecBuilder.Create(projectName, projectDirectory)
                    .WithTargetFrameworks(new[]
                    {
                        new TargetFrameworkInformation
                        {
                            FrameworkName = framework,
                            TargetAlias = "net10.0",
                            Dependencies = [
                                new LibraryDependency
                                {
                                    LibraryRange = new LibraryRange("PackageA", VersionRange.Parse("1.0.0"),
                                        LibraryDependencyTarget.All)
                                },
                            ],
                            PackagesToPrune = new Dictionary<string, PrunePackageReference>
                            {
                                [dependencyToPrune] = new PrunePackageReference(dependencyToPrune, VersionRange.Parse("(,1.0.0]"))
                            }
                        }
                    })
                    .Build();
            }

            var project1Spec = CreateProjectSpec("Library1", project1Directory, removedPosition: 0);
            var project2Spec = CreateProjectSpec("Library2", project2Directory, removedPosition: 64);
            var sources = new[] { new PackageSource(tmpPath.PackageSource) }
                .Select(source => Repository.Factory.GetCoreV3(source))
                .ToList();
            var request1 = new TestRestoreRequest(
                project1Spec,
                sources,
                tmpPath.UserPackagesFolder,
                new List<string>(),
                new TestSourceCacheContext(),
                ClientPolicyContext.GetClientPolicy(NullSettings.Instance, logger),
                logger,
                lockFileBuilderCache
            )
            { LockFilePath = Path.Combine(project1Directory, "project.lock.json") };
            var request2 = new TestRestoreRequest(
                project2Spec,
                sources,
                tmpPath.UserPackagesFolder,
                new List<string>(),
                new TestSourceCacheContext(),
                ClientPolicyContext.GetClientPolicy(NullSettings.Instance, logger),
                logger,
                lockFileBuilderCache
            )
            { LockFilePath = Path.Combine(project2Directory, "project.lock.json") };
            await SimpleTestPackageUtility.CreatePackagesAsync(tmpPath.PackageSource, packageA);

            // Act
            var result1 = await new RestoreCommand(request1).ExecuteAsync();
            var result2 = await new RestoreCommand(request2).ExecuteAsync();

            // Assert
            Assert.True(result1.Success, logger.ShowMessages());
            Assert.True(result2.Success, logger.ShowMessages());
            var project1Dependencies = result1.LockFile.Targets.Single().Libraries
                .Single(library => library.Name == "PackageA").Dependencies
                .Select(dependency => dependency.Id)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            var project2Dependencies = result2.LockFile.Targets.Single().Libraries
                .Single(library => library.Name == "PackageA").Dependencies
                .Select(dependency => dependency.Id)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            var expectedProject1Dependencies = Enumerable.Range(1, OriginalDependencyCount - 1)
                .Select(position => $"Dependency{position:D2}")
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            var expectedProject2Dependencies = Enumerable.Range(0, OriginalDependencyCount - 1)
                .Select(position => $"Dependency{position:D2}")
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(project1Dependencies.Length, project2Dependencies.Length);
            Assert.Equal(expectedProject1Dependencies, project1Dependencies);
            Assert.Equal(expectedProject2Dependencies, project2Dependencies);
        }

        [Theory]
        [InlineData(false, true)]
        [InlineData(true, false)]
        public async Task LockFileBuilderCache_DifferentPackagePath_WillNotCache(bool differentPath, bool cachingIsExpected)
        {
            // Arrange
            using (var tmpPath = new SimpleTestPathContext())
            {
                var packageA = new SimpleTestPackageContext
                {
                    Id = "PackageA",
                    Version = "1.0.0",
                };

                var logger = new TestLogger();
                var lockFileBuilderCache = new LockFileBuilderCache();
                var project1Directory = new DirectoryInfo(Path.Combine(tmpPath.SolutionRoot, "Library1"));
                var project2Directory = new DirectoryInfo(Path.Combine(tmpPath.SolutionRoot, "Library2"));
                var globalPackages1 = new DirectoryInfo(Path.Combine(tmpPath.WorkingDirectory, "globalPackages1"));
                var globalPackages2 = new DirectoryInfo(Path.Combine(tmpPath.WorkingDirectory, "globalPackages2"));
                var packageSource = new DirectoryInfo(Path.Combine(tmpPath.WorkingDirectory, "packageSource"));

                globalPackages1.Create();
                globalPackages2.Create();
                packageSource.Create();

                var project1Spec = PackageReferenceSpecBuilder.Create("Library1", project1Directory.FullName)
                    .WithTargetFrameworks(new[]
                    {
                        new TargetFrameworkInformation
                        {
                            FrameworkName = NuGetFramework.Parse("net471"),
                            Dependencies = [
                                new LibraryDependency
                                {
                                    LibraryRange = new LibraryRange("PackageA", VersionRange.Parse("1.0.0"),
                                        LibraryDependencyTarget.All)
                                },
                            ]
                        }
                    })
                    .Build();

                var project2Spec = PackageReferenceSpecBuilder.Create("Library2", project2Directory.FullName)
                    .WithTargetFrameworks(new[]
                    {
                        new TargetFrameworkInformation
                        {
                            FrameworkName = NuGetFramework.Parse("net471"),
                            Dependencies = [
                                new LibraryDependency
                                {
                                    LibraryRange = new LibraryRange("PackageA", VersionRange.Parse("1.0.0"),
                                        LibraryDependencyTarget.All)
                                },
                            ]
                        }
                    })
                    .Build();

                var sources = new[] { new PackageSource(packageSource.FullName) }
                    .Select(source => Repository.Factory.GetCoreV3(source))
                    .ToList();

                var request1 = new TestRestoreRequest(
                    project1Spec,
                    sources,
                    globalPackages1.FullName,
                    new List<string>(),
                    new TestSourceCacheContext(),
                    ClientPolicyContext.GetClientPolicy(NullSettings.Instance, logger),
                    logger,
                    lockFileBuilderCache
                )
                { LockFilePath = Path.Combine(project1Directory.FullName, "project.lock.json") };

                var request2 = new TestRestoreRequest(
                    project2Spec,
                    sources,
                    differentPath ? globalPackages2.FullName : globalPackages1.FullName,
                    new List<string>(),
                    new TestSourceCacheContext(),
                    ClientPolicyContext.GetClientPolicy(NullSettings.Instance, logger),
                    logger,
                    lockFileBuilderCache
                )
                { LockFilePath = Path.Combine(project2Directory.FullName, "project.lock.json") };

                await SimpleTestPackageUtility.CreatePackagesAsync(
                    packageSource.FullName,
                    packageA);

                // Act
                var command1 = new RestoreCommand(request1);
                var result1 = await command1.ExecuteAsync();
                var lockFile1 = result1.LockFile;

                var command2 = new RestoreCommand(request2);
                var result2 = await command2.ExecuteAsync();
                var lockFile2 = result2.LockFile;

                // Assert
                Assert.True(result1.Success);
                Assert.Equal(1, lockFile1.Libraries.Count);
                Assert.Equal(1, lockFile1.Targets.Single().Libraries.Count);

                Assert.True(result2.Success);
                Assert.Equal(1, lockFile2.Libraries.Count);
                Assert.Equal(1, lockFile2.Targets.Single().Libraries.Count);

                // Check whether packageA comes from the cache
                var lockFile1TargetLibraryA = lockFile1.Targets.Single().Libraries.Single(x => x.Name == "PackageA");
                var lockFile2TargetLibraryA = lockFile2.Targets.Single().Libraries.Single(x => x.Name == "PackageA");

                Assert.Equal(cachingIsExpected, ReferenceEquals(lockFile1TargetLibraryA, lockFile2TargetLibraryA));
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task LockFileBuilderCache_DifferentAssetTargetFallback_WillNotCache(bool withFallbackFramework)
        {
            // Arrange
            using (var tmpPath = new SimpleTestPathContext())
            {
                var packageA100 = new SimpleTestPackageContext
                {
                    Id = "PackageA",
                    Version = "1.0.0",
                };

                var logger = new TestLogger();
                var lockFileBuilderCache = new LockFileBuilderCache();
                var project1Directory = new DirectoryInfo(Path.Combine(tmpPath.SolutionRoot, "Library1"));
                var project2Directory = new DirectoryInfo(Path.Combine(tmpPath.SolutionRoot, "Library2"));
                var globalPackages = new DirectoryInfo(Path.Combine(tmpPath.WorkingDirectory, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(tmpPath.WorkingDirectory, "packageSource"));

                globalPackages.Create();
                packageSource.Create();

                var project1Spec = PackageReferenceSpecBuilder.Create("Library1", project1Directory.FullName)
                    .WithTargetFrameworks(new[]
                    {
                        new TargetFrameworkInformation
                        {
                            FrameworkName = NuGetFramework.Parse("net5.0"),
                            Dependencies = [
                                new LibraryDependency
                                {
                                    LibraryRange = new LibraryRange("PackageA", VersionRange.Parse("1.0.0"),
                                        LibraryDependencyTarget.All)
                                },
                            ]
                        }
                    })
                    .Build();

                var project2Spec = PackageReferenceSpecBuilder.Create("Library2", project2Directory.FullName)
                    .WithTargetFrameworks(new[]
                    {
                        new TargetFrameworkInformation
                        {
                            FrameworkName = withFallbackFramework
                                ? new AssetTargetFallbackFramework(NuGetFramework.Parse("net5.0"), new[] {NuGetFramework.Parse("net461")})
                                : NuGetFramework.Parse("net5.0"),
                            Dependencies = [
                                new LibraryDependency
                                {
                                    LibraryRange = new LibraryRange("PackageA", VersionRange.Parse("1.0.0"),
                                        LibraryDependencyTarget.All)
                                },
                            ]
                        }
                    })
                    .Build();

                var sources = new[] { new PackageSource(packageSource.FullName) }
                   .Select(source => Repository.Factory.GetCoreV3(source))
                   .ToList();

                var request1 = new TestRestoreRequest(
                    project1Spec,
                    sources,
                    globalPackages.FullName,
                    new List<string>(),
                    new TestSourceCacheContext(),
                    ClientPolicyContext.GetClientPolicy(NullSettings.Instance, logger),
                    logger,
                    lockFileBuilderCache
                )
                { LockFilePath = Path.Combine(project1Directory.FullName, "project.lock.json") };

                var request2 = new TestRestoreRequest(
                    project2Spec,
                    sources,
                    globalPackages.FullName,
                    new List<string>(),
                    new TestSourceCacheContext(),
                    ClientPolicyContext.GetClientPolicy(NullSettings.Instance, logger),
                    logger,
                    lockFileBuilderCache
                )
                { LockFilePath = Path.Combine(project2Directory.FullName, "project.lock.json") };

                await SimpleTestPackageUtility.CreatePackagesAsync(
                    packageSource.FullName,
                    packageA100);

                // Act
                var command1 = new RestoreCommand(request1);
                var result1 = await command1.ExecuteAsync();
                var lockFile1 = result1.LockFile;

                var command2 = new RestoreCommand(request2);
                var result2 = await command2.ExecuteAsync();
                var lockFile2 = result2.LockFile;

                // Assert
                Assert.True(result1.Success);
                Assert.Equal(1, lockFile1.Libraries.Count);
                Assert.Equal(1, lockFile1.Targets.Single().Libraries.Count);

                Assert.True(result2.Success);
                Assert.Equal(1, lockFile2.Libraries.Count);
                Assert.Equal(1, lockFile2.Targets.Single().Libraries.Count);

                // Check whether packageA comes from the cache
                var lockFile1TargetLibraryA = lockFile1.Targets.Single().Libraries.Single(x => x.Name == "PackageA");
                var lockFile2TargetLibraryA = lockFile2.Targets.Single().Libraries.Single(x => x.Name == "PackageA");

                Assert.Equal(!withFallbackFramework, ReferenceEquals(lockFile1TargetLibraryA, lockFile2TargetLibraryA));
            }
        }

        [Fact]
        public async Task LockFileBuilderCache_DifferentRuntimeGraph_WillNotCache()
        {
            // Arrange
            using (var tmpPath = new SimpleTestPathContext())
            {
                var packageA100 = new SimpleTestPackageContext
                {
                    Id = "PackageA",
                    Version = "1.0.0",
                    Files = new List<KeyValuePair<string, byte[]>>
                    {
                        new("lib/net45/a.dll", new byte[0]),
                        new("lib/netstandard2.0/a.dll", new byte[0]),
                        new("runtimes/runtime1/native/foo.dll", new byte[0]),
                        new("runtimes/runtime3/native/foo.dll", new byte[0]),
                    },
                };

                var logger = new TestLogger();
                var lockFileBuilderCache = new LockFileBuilderCache();
                var project1Directory = new DirectoryInfo(Path.Combine(tmpPath.SolutionRoot, "Library1"));
                var project2Directory = new DirectoryInfo(Path.Combine(tmpPath.SolutionRoot, "Library2"));
                var globalPackages = new DirectoryInfo(Path.Combine(tmpPath.WorkingDirectory, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(tmpPath.WorkingDirectory, "packageSource"));

                project1Directory.Create();
                project2Directory.Create();
                globalPackages.Create();
                packageSource.Create();

                var project1RuntimeGraph = new RuntimeGraph(new RuntimeDescription[]
                {
                    new("runtime1"), new("runtime2", new[] {"runtime1"}), new("runtime3"),
                });

                var runtimeJson1 = Path.Combine(project1Directory.FullName, "runtime.json");
                JsonRuntimeFormat.WriteRuntimeGraph(runtimeJson1, project1RuntimeGraph);

                var project1Spec = PackageReferenceSpecBuilder.Create("Library1", project1Directory.FullName)
                    .WithTargetFrameworks(new[]
                    {
                        new TargetFrameworkInformation
                        {
                            FrameworkName = NuGetFramework.Parse("net5.0"),
                            Dependencies = [
                                new LibraryDependency
                                {
                                    LibraryRange = new LibraryRange("PackageA", VersionRange.Parse("1.0.0"),
                                        LibraryDependencyTarget.All)
                                },
                            ],
                            RuntimeIdentifierGraphPath = runtimeJson1,
                        },
                    })
                    .WithRuntimeIdentifiers(new[] { "runtime2" }, Enumerable.Empty<string>())
                    .Build();

                var project2RuntimeGraph = new RuntimeGraph(new RuntimeDescription[]
                {
                    new("runtime1"), new("runtime2", new[] {"runtime3"}), new("runtime3"),
                });

                var runtimeJson2 = Path.Combine(project2Directory.FullName, "runtime.json");
                JsonRuntimeFormat.WriteRuntimeGraph(runtimeJson2, project2RuntimeGraph);

                var project2Spec = PackageReferenceSpecBuilder.Create("Library2", project2Directory.FullName)
                    .WithTargetFrameworks(new[]
                    {
                        new TargetFrameworkInformation
                        {
                            FrameworkName = NuGetFramework.Parse("net5.0"),
                            Dependencies = [
                                new LibraryDependency
                                {
                                    LibraryRange = new LibraryRange("PackageA", VersionRange.Parse("1.0.0"),
                                        LibraryDependencyTarget.All)
                                },
                            ],
                            RuntimeIdentifierGraphPath = runtimeJson2,
                        }
                    })
                    .WithRuntimeIdentifiers(new[] { "runtime2" }, Enumerable.Empty<string>())
                    .Build();

                var sources = new[] { new PackageSource(packageSource.FullName) }
                    .Select(source => Repository.Factory.GetCoreV3(source))
                    .ToList();

                var request1 = new TestRestoreRequest(
                    project1Spec,
                    sources,
                    globalPackages.FullName,
                    new List<string>(),
                    new TestSourceCacheContext(),
                    ClientPolicyContext.GetClientPolicy(NullSettings.Instance, logger),
                    logger,
                    lockFileBuilderCache
                )
                { LockFilePath = Path.Combine(project1Directory.FullName, "project.lock.json") };

                var request2 = new TestRestoreRequest(
                    project2Spec,
                    sources,
                    globalPackages.FullName,
                    new List<string>(),
                    new TestSourceCacheContext(),
                    ClientPolicyContext.GetClientPolicy(NullSettings.Instance, logger),
                    logger,
                    lockFileBuilderCache
                )
                { LockFilePath = Path.Combine(project2Directory.FullName, "project.lock.json") };

                await SimpleTestPackageUtility.CreatePackagesAsync(
                    packageSource.FullName,
                    packageA100);

                // Act
                var command1 = new RestoreCommand(request1);
                var result1 = await command1.ExecuteAsync();
                var lockFile1 = result1.LockFile;

                var command2 = new RestoreCommand(request2);
                var result2 = await command2.ExecuteAsync();
                var lockFile2 = result2.LockFile;

                // Check whether packageA comes from the cache
                var lockFile1TargetLibraryA = lockFile1.Targets
                    .Single(x => x.RuntimeIdentifier == "runtime2")
                    .Libraries.Single(x => x.Name == "PackageA");

                var lockFile2TargetLibraryA = lockFile2.Targets
                    .Single(x => x.RuntimeIdentifier == "runtime2")
                    .Libraries.Single(x => x.Name == "PackageA");

                // Assert
                Assert.True(result1.Success);
                Assert.Equal(1, lockFile1.Libraries.Count);
                Assert.Equal(1, lockFile1.Targets.Single(x => x.RuntimeIdentifier == "runtime2").Libraries.Count);

                Assert.True(result2.Success);
                Assert.Equal(1, lockFile2.Libraries.Count);
                Assert.Equal(1, lockFile2.Targets.Single(x => x.RuntimeIdentifier == "runtime2").Libraries.Count);

                Assert.False(ReferenceEquals(lockFile1TargetLibraryA, lockFile2TargetLibraryA));
                Assert.Equal("runtimes/runtime1/native/foo.dll", lockFile1TargetLibraryA.NativeLibraries.Single().Path);
                Assert.Equal("runtimes/runtime3/native/foo.dll", lockFile2TargetLibraryA.NativeLibraries.Single().Path);
            }
        }

        [Theory]
        [InlineData("1.0.0", "net471", null, LibraryIncludeFlags.All, true)]
        [InlineData("2.0.0", "net471", null, LibraryIncludeFlags.All, false)]
        [InlineData("1.0.0", "netstandard2.0", null, LibraryIncludeFlags.All, false)]
        [InlineData("1.0.0", "net471", "MyAlias", LibraryIncludeFlags.All, false)]
        [InlineData("1.0.0", "net471", null, LibraryIncludeFlags.Runtime, false)]
        public async Task LockFileBuilderCache_DependencyDifference_WillNotCache(
            string version,
            string framework,
            string aliases,
            LibraryIncludeFlags includeFlags,
            bool cachingIsExpected
        )
        {
            // Arrange
            using (var tmpPath = new SimpleTestPathContext())
            {
                var packageA100 = new SimpleTestPackageContext
                {
                    Id = "PackageA",
                    Version = "1.0.0",
                    Files = new List<KeyValuePair<string, byte[]>>
                    {
                        new KeyValuePair<string, byte[]>("lib/net45/a.dll", new byte[0]),
                        new KeyValuePair<string, byte[]>("lib/netstandard2.0/a.dll", new byte[0]),
                    },
                };

                var packageA200 = new SimpleTestPackageContext
                {
                    Id = "PackageA",
                    Version = "2.0.0",
                    Files = new List<KeyValuePair<string, byte[]>>
                    {
                        new KeyValuePair<string, byte[]>("lib/net45/a.dll", new byte[0]),
                        new KeyValuePair<string, byte[]>("lib/netstandard2.0/a.dll", new byte[0]),
                    },
                };

                var logger = new TestLogger();
                var lockFileBuilderCache = new LockFileBuilderCache();
                var project1Directory = new DirectoryInfo(Path.Combine(tmpPath.SolutionRoot, "Library1"));
                var project2Directory = new DirectoryInfo(Path.Combine(tmpPath.SolutionRoot, "Library2"));
                var globalPackages = new DirectoryInfo(Path.Combine(tmpPath.WorkingDirectory, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(tmpPath.WorkingDirectory, "packageSource"));

                globalPackages.Create();
                packageSource.Create();

                var project1Spec = PackageReferenceSpecBuilder.Create("Library1", project1Directory.FullName)
                    .WithTargetFrameworks(new[]
                    {
                        new TargetFrameworkInformation
                        {
                            FrameworkName = NuGetFramework.Parse("net471"),
                            Dependencies = [
                                new LibraryDependency
                                {
                                    LibraryRange = new LibraryRange("PackageA", VersionRange.Parse("1.0.0"),
                                        LibraryDependencyTarget.All)
                                },
                            ]
                        }
                    })
                    .Build();

                var project2Spec = PackageReferenceSpecBuilder.Create("Library2", project2Directory.FullName)
                    .WithTargetFrameworks(new[]
                    {
                        new TargetFrameworkInformation
                        {
                            FrameworkName = NuGetFramework.Parse(framework),
                            Dependencies = [
                                new LibraryDependency
                                {
                                    LibraryRange = new LibraryRange("PackageA", VersionRange.Parse(version),
                                        LibraryDependencyTarget.All),
                                    Aliases = aliases,
                                    IncludeType = includeFlags,
                                },
                            ]
                        }
                    })
                    .Build();

                var sources = new[] { new PackageSource(packageSource.FullName) }
                    .Select(source => Repository.Factory.GetCoreV3(source))
                    .ToList();

                var request1 = new TestRestoreRequest(
                    project1Spec,
                    sources,
                    globalPackages.FullName,
                    new List<string>(),
                    new TestSourceCacheContext(),
                    ClientPolicyContext.GetClientPolicy(NullSettings.Instance, logger),
                    logger,
                    lockFileBuilderCache
                )
                { LockFilePath = Path.Combine(project1Directory.FullName, "project.lock.json") };

                var request2 = new TestRestoreRequest(
                    project2Spec,
                    sources,
                    globalPackages.FullName,
                    new List<string>(),
                    new TestSourceCacheContext(),
                    ClientPolicyContext.GetClientPolicy(NullSettings.Instance, logger),
                    logger,
                    lockFileBuilderCache
                )
                { LockFilePath = Path.Combine(project2Directory.FullName, "project.lock.json") };

                await SimpleTestPackageUtility.CreatePackagesAsync(
                    packageSource.FullName,
                    packageA100,
                    packageA200);

                // Act
                var command1 = new RestoreCommand(request1);
                var result1 = await command1.ExecuteAsync();
                var lockFile1 = result1.LockFile;

                var command2 = new RestoreCommand(request2);
                var result2 = await command2.ExecuteAsync();
                var lockFile2 = result2.LockFile;

                // Assert
                Assert.True(result1.Success);
                Assert.Equal(1, lockFile1.Libraries.Count);
                Assert.Equal(1, lockFile1.Targets.Single().Libraries.Count);

                Assert.True(result2.Success);
                Assert.Equal(1, lockFile2.Libraries.Count);
                Assert.Equal(1, lockFile2.Targets.Single().Libraries.Count);

                // Check whether packageA comes from the cache
                var lockFile1TargetLibraryA = lockFile1.Targets.Single().Libraries.Single(x => x.Name == "PackageA");
                var lockFile2TargetLibraryA = lockFile2.Targets.Single().Libraries.Single(x => x.Name == "PackageA");

                Assert.Equal(cachingIsExpected, ReferenceEquals(lockFile1TargetLibraryA, lockFile2TargetLibraryA));

                Assert.Equal("lib/net45/a.dll", lockFile1TargetLibraryA.RuntimeAssemblies.Single().Path);
                Assert.Equal(framework == "net471" ? "lib/net45/a.dll" : "lib/netstandard2.0/a.dll",
                    lockFile2TargetLibraryA.RuntimeAssemblies.Single().Path);

                if (includeFlags.HasFlag(LibraryIncludeFlags.Runtime) &&
                    includeFlags.HasFlag(LibraryIncludeFlags.Compile))
                {
                    Assert.Equal(lockFile2TargetLibraryA.RuntimeAssemblies.Select(x => x.Path),
                        lockFile2TargetLibraryA.CompileTimeAssemblies.Select(x => x.Path));
                }

                Assert.Equal(NuGetVersion.Parse("1.0.0"), lockFile1TargetLibraryA.Version);
                Assert.Equal(NuGetVersion.Parse(version), lockFile2TargetLibraryA.Version);
            }
        }

        private static List<LibraryDependency> CreatePrunedDependencies(int originalDependencyCount, int removedPosition)
        {
            var dependencies = new List<LibraryDependency>();
            for (int position = 0; position < originalDependencyCount; position++)
            {
                if (position == removedPosition)
                {
                    continue;
                }

                var libraryRange = new LibraryRange("D" + position, VersionRange.Parse("1.0.0"), LibraryDependencyTarget.Package);
                dependencies.Add(new LibraryDependency(libraryRange));
            }

            return dependencies;
        }

        private static PackageDependencyPruningResult CreatePruningResult(int originalDependencyCount, int removedPosition)
        {
            if (originalDependencyCount <= 64)
            {
                return new PackageDependencyPruningResult(1UL << removedPosition);
            }

            return new PackageDependencyPruningResult([removedPosition]);
        }
    }
}
