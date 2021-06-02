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

namespace NuGet.Commands.Test
{
    [Collection(nameof(NotThreadSafeResourceCollection))]
    public class LockFileBuilderCacheTest
    {
        [Theory]
        [InlineData(false, false)]
        [InlineData(true, true)]
        public async Task LockFileBuilderCache_DifferentPackageContent_WillNotCache(bool identicalContent, bool cachingIsExpected)
        {
            // Arrange
            using (var tmpPath = new SimpleTestPathContext())
            {
                var packageA1 = new SimpleTestPackageContext
                {
                    Id = "PackageA",
                    Version = "1.0.0",
                };

                var packageA2 = new SimpleTestPackageContext
                {
                    Id = "PackageA",
                    Version = "1.0.0",
                };

                // It is possible for different projects to use different NuGet configurations, so in consequence they can use different package sources.
                // It is also possible that packages with the same identity (name and version) can have different content if it comes from two different sources.
                // So to test whether the LockFileBuilderCache properly supports this scenario we create two packages with same identity but different lib content.
                if (!identicalContent)
                {
                    packageA2.Files.Add(new KeyValuePair<string, byte[]>("Foo.txt", System.Array.Empty<byte>()));
                }

                var logger = new TestLogger();
                var lockFileBuilderCache = new LockFileBuilderCache();
                var project1Directory = new DirectoryInfo(Path.Combine(tmpPath.SolutionRoot, "Library1"));
                var project2Directory = new DirectoryInfo(Path.Combine(tmpPath.SolutionRoot, "Library2"));
                var globalPackages1 = new DirectoryInfo(Path.Combine(tmpPath.WorkingDirectory, "globalPackages1"));
                var globalPackages2 = new DirectoryInfo(Path.Combine(tmpPath.WorkingDirectory, "globalPackages2"));
                var package1Source = new DirectoryInfo(Path.Combine(tmpPath.WorkingDirectory, "packageSource1"));
                var package2Source = new DirectoryInfo(Path.Combine(tmpPath.WorkingDirectory, "packageSource2"));

                globalPackages1.Create();
                globalPackages2.Create();
                package1Source.Create();
                package2Source.Create();

                var project1Spec = PackageReferenceSpecBuilder.Create("Library1", project1Directory.FullName)
                    .WithTargetFrameworks(new[]
                    {
                        new TargetFrameworkInformation
                        {
                            FrameworkName = NuGetFramework.Parse("net471"),
                            Dependencies = new List<LibraryDependency>(
                                new[]
                                {
                                    new LibraryDependency
                                    {
                                        LibraryRange = new LibraryRange("PackageA", VersionRange.Parse("1.0.0"),
                                            LibraryDependencyTarget.All)
                                    },
                                })
                        }
                    })
                    .Build();

                var project2Spec = PackageReferenceSpecBuilder.Create("Library2", project2Directory.FullName)
                    .WithTargetFrameworks(new[]
                    {
                        new TargetFrameworkInformation
                        {
                            FrameworkName = NuGetFramework.Parse("net471"),
                            Dependencies = new List<LibraryDependency>(
                                new[]
                                {
                                    new LibraryDependency
                                    {
                                        LibraryRange = new LibraryRange("PackageA", VersionRange.Parse("1.0.0"),
                                            LibraryDependencyTarget.All)
                                    },
                                })
                        }
                    })
                    .Build();

                var sources1 = new[] { new PackageSource(package1Source.FullName) }
                    .Select(source => Repository.Factory.GetCoreV3(source))
                    .ToList();

                var sources2 = new[] { new PackageSource(package2Source.FullName) }
                    .Select(source => Repository.Factory.GetCoreV3(source))
                    .ToList();

                var request1 = new TestRestoreRequest(
                    project1Spec,
                    sources1,
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
                    sources2,
                    globalPackages2.FullName,
                    new List<string>(),
                    new TestSourceCacheContext(),
                    ClientPolicyContext.GetClientPolicy(NullSettings.Instance, logger),
                    logger,
                    lockFileBuilderCache
                )
                { LockFilePath = Path.Combine(project2Directory.FullName, "project.lock.json") };

                await SimpleTestPackageUtility.CreatePackagesAsync(
                    package1Source.FullName,
                    packageA1);

                await SimpleTestPackageUtility.CreatePackagesAsync(
                    package2Source.FullName,
                    packageA2);

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
                            Dependencies = new List<LibraryDependency>(
                                new[]
                                {
                                    new LibraryDependency
                                    {
                                        LibraryRange = new LibraryRange("PackageA", VersionRange.Parse("1.0.0"),
                                            LibraryDependencyTarget.All)
                                    },
                                })
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
                            Dependencies = new List<LibraryDependency>(
                                new[]
                                {
                                    new LibraryDependency
                                    {
                                        LibraryRange = new LibraryRange("PackageA", VersionRange.Parse("1.0.0"),
                                            LibraryDependencyTarget.All)
                                    },
                                })
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
                            Dependencies = new List<LibraryDependency>(
                                new[]
                                {
                                    new LibraryDependency
                                    {
                                        LibraryRange = new LibraryRange("PackageA", VersionRange.Parse("1.0.0"),
                                            LibraryDependencyTarget.All)
                                    },
                                }),
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
                            Dependencies = new List<LibraryDependency>(
                                new[]
                                {
                                    new LibraryDependency
                                    {
                                        LibraryRange = new LibraryRange("PackageA", VersionRange.Parse("1.0.0"),
                                            LibraryDependencyTarget.All)
                                    },
                                }),
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
                            Dependencies = new List<LibraryDependency>(
                                new[]
                                {
                                    new LibraryDependency
                                    {
                                        LibraryRange = new LibraryRange("PackageA", VersionRange.Parse("1.0.0"),
                                            LibraryDependencyTarget.All)
                                    },
                                })
                        }
                    })
                    .Build();

                var project2Spec = PackageReferenceSpecBuilder.Create("Library2", project2Directory.FullName)
                    .WithTargetFrameworks(new[]
                    {
                        new TargetFrameworkInformation
                        {
                            FrameworkName = NuGetFramework.Parse(framework),
                            Dependencies = new List<LibraryDependency>(
                                new[]
                                {
                                    new LibraryDependency
                                    {
                                        LibraryRange = new LibraryRange("PackageA", VersionRange.Parse(version),
                                            LibraryDependencyTarget.All),
                                        Aliases = aliases,
                                        IncludeType = includeFlags,
                                    },
                                })
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
    }
}
