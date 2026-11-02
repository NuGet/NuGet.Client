// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.DependencyResolver;
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
    public class PackageDependencyPruningCacheTests
    {
        [Theory]
        [InlineData("B", "C", false)]
        [InlineData("C", "B", false)]
        [InlineData("B,X,Y", "B,Z", true)]
        [InlineData("", "X,Y", true)]
        [InlineData("B", "B", true)]
        public async Task SharedCache_RetainsPackageSpecificPruningVariants(string firstRules, string secondRules, bool reuse)
        {
            using var paths = new SimpleTestPathContext();
            var packageA = new SimpleTestPackageContext("A", "1.0.0")
            {
                Dependencies = [new SimpleTestPackageContext("B", "1.0.0"), new SimpleTestPackageContext("C", "1.0.0")]
            };
            await SimpleTestPackageUtility.CreatePackagesAsync(paths.PackageSource, packageA, packageA.Dependencies[0], packageA.Dependencies[1]);
            var cache = new LockFileBuilderCache();
            var entries = new List<LockFileTargetLibrary>();
            foreach (string rules in new[] { firstRules, secondRules, firstRules, secondRules })
            {
                var framework = new TargetFrameworkInformation
                {
                    FrameworkName = NuGetFramework.Parse("net10.0"),
                    Dependencies = [new LibraryDependency(new LibraryRange("A", VersionRange.Parse("1.0.0"), LibraryDependencyTarget.Package))],
                    PackagesToPrune = rules.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .ToDictionary(id => id, id => PrunePackageReference.Create(id, "1.0.0"), StringComparer.OrdinalIgnoreCase)
                };
                var spec = PackageReferenceSpecBuilder.Create("Project" + entries.Count, Path.Combine(paths.SolutionRoot, "Project" + entries.Count))
                    .WithTargetFrameworks([framework]).Build();
                var logger = new TestLogger();
                using var sourceCache = new TestSourceCacheContext();
                var request = new TestRestoreRequest(spec,
                    [Repository.Factory.GetCoreV3(new PackageSource(paths.PackageSource))],
                    paths.UserPackagesFolder, [], sourceCache,
                    ClientPolicyContext.GetClientPolicy(NullSettings.Instance, logger), logger, cache);
                var result = await new RestoreCommand(request).ExecuteAsync();
                Assert.True(result.Success, string.Join(Environment.NewLine, logger.Messages));
                var entry = result.LockFile.Targets.Single().Libraries.Single(library => library.Name == "A");
                Assert.Equal(new[] { "B", "C" }.Where(id => !framework.PackagesToPrune.ContainsKey(id)), entry.Dependencies.Select(dependency => dependency.Id));
                entries.Add(entry);
            }
            Assert.Equal(reuse, ReferenceEquals(entries[0], entries[1]));
            Assert.Same(entries[0], entries[2]);
            Assert.Same(entries[1], entries[3]);
        }

        [Fact]
        public async Task Producer_RecordsImmutablePositions_AndUsesDefaultForNoRemovals()
        {
            var original = CreateItem();
            var producer = new DependencyGraphResolver.DependencyGraphItem
            {
                FindLibraryTask = Task.FromResult(original),
                LibraryDependency = original.Data.Dependencies[0]
            };
            var noRules = await producer.GetGraphItemAsync(new ProjectRestoreMetadata(), null, false, false, "net10.0", NullLogger.Instance, cacheTargetLibrary: true);
            Assert.Same(original, noRules);
            var noRemoval = await producer.GetGraphItemAsync(new ProjectRestoreMetadata(), Rules("X"), false, false, "net10.0", NullLogger.Instance, cacheTargetLibrary: true);
            Assert.IsType<RemoteResolveResult>(noRemoval.Data);
            var pruned = await producer.GetGraphItemAsync(new ProjectRestoreMetadata(), Rules("B"), false, false, "net10.0", NullLogger.Instance, cacheTargetLibrary: true);
            Assert.Equal(new PackageDependencyPruningResult(1UL), Assert.IsType<PrunedRemoteResolveResult>(pruned.Data).PruningResult);
            Assert.Equal(["C"], pruned.Data.Dependencies.Select(dependency => dependency.Name));
            Assert.Equal(2, original.Data.Dependencies.Count);
            var positions = new List<int> { 0, 64 };
            var snapshot = new PackageDependencyPruningResult(positions);
            positions[0] = 1;
            positions.Add(99);
            Assert.Equal(new PackageDependencyPruningResult([0, 64]), snapshot);
            Assert.NotEqual(new PackageDependencyPruningResult(positions), snapshot);
        }

        [Theory]
        [InlineData(0, -1)]
        [InlineData(1, 0)]
        [InlineData(63, 62)]
        [InlineData(64, 63)]
        [InlineData(65, 63)]
        [InlineData(65, 64)]
        public async Task Producer_UsesOriginalPositionsAcrossMaskBoundary(int count, int removed)
        {
            var original = CreateItem();
            original.Data.Dependencies = Enumerable.Range(0, count).Select(i => new LibraryDependency(
                new LibraryRange("D" + i, VersionRange.Parse("1.0.0"), LibraryDependencyTarget.Package))).ToList();
            var producer = new DependencyGraphResolver.DependencyGraphItem
            {
                FindLibraryTask = Task.FromResult(original),
                LibraryDependency = new LibraryDependency(new LibraryRange("A", VersionRange.Parse("1.0.0"), LibraryDependencyTarget.Package))
            };
            foreach (bool cacheTargetLibrary in new[] { true, false })
            {
                var result = await producer.GetGraphItemAsync(new ProjectRestoreMetadata(), Rules("D" + removed),
                    false, false, "net10.0", NullLogger.Instance, cacheTargetLibrary);
                Assert.Equal(original.Data.Dependencies.Where((_, i) => i != removed), result.Data.Dependencies);
                Assert.Equal(count, original.Data.Dependencies.Count);
                if (cacheTargetLibrary && removed >= 0)
                {
                    var expected = count <= 64
                        ? new PackageDependencyPruningResult(1UL << removed)
                        : new PackageDependencyPruningResult([removed]);
                    Assert.Equal(expected, Assert.IsType<PrunedRemoteResolveResult>(result.Data).PruningResult);
                }
                else
                {
                    Assert.IsType<RemoteResolveResult>(result.Data);
                }
            }
        }

        [Fact]
        public async Task Producer_MultipleRemovals_DoNotShiftPositionsOrIncludeRuntimeReplacement()
        {
            var original = CreateItem();
            original.Data.Dependencies.Add(new LibraryDependency(
                new LibraryRange("D", VersionRange.Parse("1.0.0"), LibraryDependencyTarget.Package)));
            var producer = new DependencyGraphResolver.DependencyGraphItem
            {
                FindLibraryTask = Task.FromResult(original),
                LibraryDependency = original.Data.Dependencies[0],
                RuntimeDependencies = [original.Data.Dependencies[1]]
            };
            var rules = Rules("B");
            rules.Add("D", PrunePackageReference.Create("D", "1.0.0"));
            foreach (bool cacheTargetLibrary in new[] { true, false })
            {
                var result = await producer.GetGraphItemAsync(new ProjectRestoreMetadata(), rules, false, false,
                    "net10.0", NullLogger.Instance, cacheTargetLibrary);
                Assert.Equal(["C"], result.Data.Dependencies.Select(d => d.Name));
                if (cacheTargetLibrary)
                {
                    Assert.Equal(new PackageDependencyPruningResult(5UL),
                        Assert.IsType<PrunedRemoteResolveResult>(result.Data).PruningResult);
                }
                else
                {
                    Assert.IsType<RemoteResolveResult>(result.Data);
                }
            }
        }

        [Fact]
        public void PruningResult_DefaultAndMaskEquality_AreCollisionSafe()
        {
            Assert.Equal(default, new PackageDependencyPruningResult(0UL));
            Assert.Equal(default, new PackageDependencyPruningResult(new List<int>()));
            Assert.Equal(new PackageDependencyPruningResult(1UL << 63), new PackageDependencyPruningResult(1UL << 63));
            var first = new PackageDependencyPruningResult(1UL);
            var second = new PackageDependencyPruningResult(1UL << 32);
            Assert.Equal(first.GetHashCode(), second.GetHashCode());
            Assert.NotEqual(first, second);
            Assert.False(first.Equals(null));
            Assert.False(first.Equals("1"));
        }

        [Fact]
        public void PruningResult_HashCollision_DoesNotCompareEqual()
        {
            var first = new PackageDependencyPruningResult([0, 99]);
            var second = new PackageDependencyPruningResult([1, 2]);
            Assert.Equal(first.GetHashCode(), second.GetHashCode());
            Assert.NotEqual(first, second);
        }

        [Theory]
        [InlineData(64)]
        [InlineData(65)]
        public async Task Producer_AllRemoved_PreservesMaskAndOverflowIdentity(int count)
        {
            var original = CreateItem();
            original.Data.Dependencies = Enumerable.Range(0, count).Select(i => new LibraryDependency(
                new LibraryRange("D" + i, VersionRange.Parse("1.0.0"), LibraryDependencyTarget.Package))).ToList();
            var producer = new DependencyGraphResolver.DependencyGraphItem
            {
                FindLibraryTask = Task.FromResult(original),
                LibraryDependency = original.Data.Dependencies[0]
            };
            var rules = original.Data.Dependencies.ToDictionary(d => d.Name,
                d => PrunePackageReference.Create(d.Name, "1.0.0"), StringComparer.OrdinalIgnoreCase);
            var result = await producer.GetGraphItemAsync(new ProjectRestoreMetadata(), rules, false, false,
                "net10.0", NullLogger.Instance, cacheTargetLibrary: true);
            Assert.Empty(result.Data.Dependencies);
            var pruning = Assert.IsType<PrunedRemoteResolveResult>(result.Data).PruningResult;
            Assert.Equal(count == 64 ? new PackageDependencyPruningResult(ulong.MaxValue)
                : new PackageDependencyPruningResult(Enumerable.Range(0, count).ToList()), pruning);
            Assert.NotEqual(default, pruning);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Cache_ConcurrentCollidingPruningIdentities_RemainDistinct(bool overflow)
        {
            var framework = NuGetFramework.Parse("net10.0");
            var graph = RestoreTargetGraph.Create(RuntimeGraph.Empty, [], null!, "net10.0", framework, "");
            var cache = new LockFileBuilderCache();
            var package = new NuGet.Repositories.LocalPackageInfo("A", NuGetVersion.Parse("1.0.0"), "", "", "", "",
                new Lazy<NuspecReader>(() => null!), new Lazy<IReadOnlyList<string>>(() => []),
                new Lazy<string>(() => ""), new Lazy<RuntimeGraph?>(() => RuntimeGraph.Empty));
            PackageDependencyPruningResult[] pruning = overflow
                ? [new([0, 99]), new([1, 2])]
                : [new(1UL), new(1UL << 32)];
            Assert.Equal(pruning[0].GetHashCode(), pruning[1].GetHashCode());
            int calls = 0;
            var entries = await Task.WhenAll(Enumerable.Range(0, 40).Select(i => Task.Run(() =>
                cache.GetLockFileTargetLibrary(graph, framework, package, null!, LibraryIncludeFlags.All,
                    CreateItem().Data.Dependencies, false, () =>
                    {
                        Interlocked.Increment(ref calls);
                        return (new LockFileTargetLibrary { Name = i % 2 == 0 ? "B" : "C" }, false, framework, framework);
                    }, pruning[i % 2]).Item1)));
            Assert.Equal(2, calls);
            Assert.NotSame(entries[0], entries[1]);
            for (int i = 0; i < entries.Length; i++)
            {
                Assert.Same(entries[i % 2], entries[i]);
                Assert.Equal(i % 2 == 0 ? "B" : "C", entries[i].Name);
            }
        }

        [Theory]
        [InlineData("plain", "Z,B,C")]
        [InlineData("ptf", "C,Z,B")]
        [InlineData("atf", "Z,B,C")]
        public async Task SourceAndGlobalPackages_SelectSameOrderedDependencyGroup(string mode, string expectedNames)
        {
            using var paths = new SimpleTestPathContext();
            var package = new SimpleTestPackageContext("A", "1.0.0")
            {
                Nuspec = XDocument.Parse("""
                    <package><metadata><id>A</id><version>1.0.0</version><dependencies>
                      <group>
                        <dependency id="Z" version="[1.0.0,2.0.0)" include="Compile" />
                        <dependency id="B" version="[1.0.0]" exclude="Build" />
                        <dependency id="C" version="1.0.0" />
                      </group>
                      <group targetFramework="net46">
                        <dependency id="C" version="1.0.0" />
                        <dependency id="Z" version="[1.0.0,2.0.0)" include="Runtime" />
                        <dependency id="B" version="[1.0.0]" />
                      </group>
                    </dependencies></metadata></package>
                    """)
            };
            await SimpleTestPackageUtility.CreateFolderFeedV3Async(paths.PackageSource, PackageSaveMode.Defaultv3, package);
            await SimpleTestPackageUtility.CreateFolderFeedV3Async(paths.UserPackagesFolder, PackageSaveMode.Defaultv3, package);
            using var sourceCache = new SourceCacheContext();
            var logger = new TestLogger();
            var source = new SourceRepositoryDependencyProvider(Repository.Factory.GetCoreV3(paths.PackageSource),
                logger, sourceCache, false, false);
            var local = new SourceRepositoryDependencyProvider(
                Repository.Factory.GetCoreV3(paths.UserPackagesFolder, FeedType.FileSystemV3),
                logger, sourceCache, false, false, new LocalPackageFileCache(),
                isGlobalPackagesFolder: true, isFallbackFolderSource: false, EnvironmentVariableWrapper.Instance);
            var root = NuGetFramework.Parse("net10.0");
            NuGetFramework framework = mode == "plain" ? root : mode == "ptf"
                ? new FallbackFramework(root, [NuGetFramework.Parse("net46")])
                : new AssetTargetFallbackFramework(root, [NuGetFramework.Parse("net46")]);
            var identity = new LibraryIdentity("A", NuGetVersion.Parse("1.0.0"), LibraryType.Package);
            var remoteInfo = await source.GetDependenciesAsync(identity, framework, sourceCache, logger, CancellationToken.None);
            var localInfo = await local.GetDependenciesAsync(identity, framework, sourceCache, logger, CancellationToken.None);
            Assert.Equal(expectedNames.Split(','), remoteInfo.Dependencies.Select(d => d.Name));
            Assert.Equal(remoteInfo.Dependencies, localInfo.Dependencies);
            Assert.Equal(remoteInfo.Dependencies.Select(d => (d.LibraryRange, d.IncludeType, d.SuppressParent)),
                localInfo.Dependencies.Select(d => (d.LibraryRange, d.IncludeType, d.SuppressParent)));
            Assert.Equal(mode == "ptf" ? LibraryIncludeFlags.Runtime : LibraryIncludeFlags.Compile,
                remoteInfo.Dependencies.Single(d => d.Name == "Z").IncludeType);
        }

        [Fact]
        public async Task SolutionRequests_WithDifferentEqualCountRemovals_DoNotShareTargetLibrary()
        {
            using var paths = new SimpleTestPathContext();
            var b = new SimpleTestPackageContext("B", "1.0.0");
            var c = new SimpleTestPackageContext("C", "1.0.0");
            var a = new SimpleTestPackageContext("A", "1.0.0") { Dependencies = [b, c] };
            await SimpleTestPackageUtility.CreatePackagesAsync(paths.PackageSource, a, b, c);
            var projects = new[] { "B", "C" }.Select(id =>
                PackageReferenceSpecBuilder.Create("Prune" + id, Path.Combine(paths.SolutionRoot, "Prune" + id))
                    .WithTargetFrameworks([new TargetFrameworkInformation
                    {
                        FrameworkName = NuGetFramework.Parse("net10.0"),
                        Dependencies = [new LibraryDependency(new LibraryRange("A", VersionRange.Parse("1.0.0"), LibraryDependencyTarget.Package))],
                        PackagesToPrune = Rules(id)
                    }]).Build()).ToArray();
            var logger = new TestLogger();
            using var sourceCache = new SourceCacheContext();
            var args = new RestoreArgs
            {
                CacheContext = sourceCache,
                Log = logger,
                Sources = [paths.PackageSource],
                GlobalPackagesFolder = paths.UserPackagesFolder,
                AllowNoOp = false
            };
            var requests = (await new DependencyGraphSpecRequestProvider(new RestoreCommandProvidersCache(),
                ProjectTestHelpers.GetDGSpecForAllProjects(projects)).CreateRequests(args)).ToArray();
            Assert.Same(requests[0].Request.LockFileBuilderCache, requests[1].Request.LockFileBuilderCache);
            foreach (var request in requests)
            {
                var result = await new RestoreCommand(request.Request).ExecuteAsync();
                Assert.True(result.Success, logger.ShowMessages());
                string removed = request.Request.Project.TargetFrameworks.Single().PackagesToPrune.Single().Key;
                Assert.Equal(removed == "B" ? "C" : "B",
                    result.LockFile.Targets.Single().Libraries.Single(l => l.Name == "A").Dependencies.Single().Id);
            }
        }

        [Theory]
        [InlineData(0, "")]
        [InlineData(1, "")]
        [InlineData(63, "")]
        [InlineData(64, "")]
        [InlineData(65, "")]
        [InlineData(64, "win-x64")]
        [InlineData(65, "win-x64")]
        public async Task Restore_PreservesBoundaryPruningAndRidMetadataBypass(int count, string rid)
        {
            using var paths = new SimpleTestPathContext();
            var dependencies = Enumerable.Range(0, count).Select(i => new SimpleTestPackageContext("D" + i, "1.0.0")).ToArray();
            var a = new SimpleTestPackageContext("A", "1.0.0") { Dependencies = dependencies.ToList() };
            await SimpleTestPackageUtility.CreatePackagesAsync(paths.PackageSource, dependencies.Append(a).ToArray());
            var framework = new TargetFrameworkInformation
            {
                FrameworkName = NuGetFramework.Parse("net10.0"),
                Dependencies = [new LibraryDependency(new LibraryRange("A", VersionRange.Parse("1.0.0"), LibraryDependencyTarget.Package))],
                PackagesToPrune = Rules("D" + (count - 1))
            };
            var spec = PackageReferenceSpecBuilder.Create("Project", paths.SolutionRoot)
                .WithTargetFrameworks([framework]).Build();
            if (rid.Length > 0)
            {
                spec.RuntimeGraph = new RuntimeGraph([new RuntimeDescription(rid)]);
            }
            var logger = new TestLogger();
            using var sourceCache = new TestSourceCacheContext();
            var cache = new LockFileBuilderCache();
            LockFileTargetLibrary? previous = null;
            for (int iteration = 0; iteration < 2; iteration++)
            {
                var request = new TestRestoreRequest(spec,
                    [Repository.Factory.GetCoreV3(new PackageSource(paths.PackageSource))],
                    paths.UserPackagesFolder, [], sourceCache,
                    ClientPolicyContext.GetClientPolicy(NullSettings.Instance, logger), logger, cache);
                var result = await new RestoreCommand(request).ExecuteAsync();
                Assert.True(result.Success, logger.ShowMessages());
                Assert.Equal(rid.Length == 0 ? 1 : 2, result.RestoreGraphs.Count());
                Assert.Contains(result.LockFile.Targets, target => (target.RuntimeIdentifier ?? "") == rid);
                foreach (var target in result.LockFile.Targets)
                {
                    var library = target.Libraries.Single(l => l.Name == "A");
                    Assert.Equal(dependencies.Take(Math.Max(0, count - 1)).Select(d => d.Id),
                        library.Dependencies.Select(d => d.Id));
                    if ((target.RuntimeIdentifier ?? "") == rid && previous is not null)
                    {
                        Assert.Equal(rid.Length == 0, ReferenceEquals(previous, library));
                    }
                    if ((target.RuntimeIdentifier ?? "") == rid)
                    {
                        previous = library;
                    }
                }
                foreach (var graph in result.RestoreGraphs)
                {
                    var data = graph.Flattened.Single(i => i.Key.Name == "A").Data;
                    if (string.IsNullOrEmpty(graph.RuntimeIdentifier) && count > 0)
                    {
                        Assert.IsType<PrunedRemoteResolveResult>(data);
                    }
                    else
                    {
                        Assert.IsType<RemoteResolveResult>(data);
                    }
                }
            }
        }

        [Theory]
        [InlineData("", true)]
        [InlineData("win-x64", false)]
        public async Task Cache_PreservesLazyConcurrencyAndRuntimeBypass(string rid, bool reuse)
        {
            var framework = NuGetFramework.Parse("net10.0");
            var graph = RestoreTargetGraph.Create(RuntimeGraph.Empty, [], null!, "net10.0", framework, rid);
            var cache = new LockFileBuilderCache();
            var package = new NuGet.Repositories.LocalPackageInfo("A", NuGetVersion.Parse("1.0.0"), "", "", "", "",
                new Lazy<NuspecReader>(() => null!), new Lazy<IReadOnlyList<string>>(() => []),
                new Lazy<string>(() => ""), new Lazy<RuntimeGraph?>(() => RuntimeGraph.Empty));
            int calls = 0;
            var entries = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() =>
                cache.GetLockFileTargetLibrary(graph, framework, package, null!, LibraryIncludeFlags.All,
                    CreateItem().Data.Dependencies, false, () =>
                    {
                        Interlocked.Increment(ref calls);
                        return (new LockFileTargetLibrary(), false, framework, framework);
                    }, new PackageDependencyPruningResult(1UL)).Item1)));
            Assert.Equal(reuse ? 1 : 20, calls);
            Assert.Equal(reuse, entries.All(entry => ReferenceEquals(entries[0], entry)));
        }

        [Fact]
        public void NullDependencies_BypassesCompletedCache()
        {
            var framework = NuGetFramework.Parse("net10.0");
            var graph = RestoreTargetGraph.Create(RuntimeGraph.Empty, [], null!, "net10.0", framework, "");
            var cache = new LockFileBuilderCache();
            var expected = new LockFileTargetLibrary();
            Assert.Same(expected, cache.GetLockFileTargetLibrary(graph, framework, null!, null!, LibraryIncludeFlags.All,
                null!, false, () => (expected, false, framework, framework)).Item1);
        }

        [Fact]
        public void NullDependencies_UsesNuspecFallback()
        {
            var framework = NuGetFramework.Parse("net10.0");
            var graph = RestoreTargetGraph.Create(RuntimeGraph.Empty, [], null!, "net10.0", framework, "");
            var nuspec = new NuspecReader(XDocument.Parse("""
                <package><metadata><id>A</id><version>1.0.0</version><dependencies>
                  <group targetFramework="net10.0"><dependency id="B" version="1.0.0" /></group>
                </dependencies></metadata></package>
                """));
            var package = new NuGet.Repositories.LocalPackageInfo("A", NuGetVersion.Parse("1.0.0"), "", "", "", "",
                new Lazy<NuspecReader>(() => nuspec), new Lazy<IReadOnlyList<string>>(() => []),
                new Lazy<string>(() => ""), new Lazy<RuntimeGraph?>(() => RuntimeGraph.Empty));
            var library = new LockFileLibrary { Name = "A", Version = package.Version, Type = "package" };
            var result = LockFileUtils.CreateLockFileTargetLibrary(library, package, graph, LibraryIncludeFlags.All);
            Assert.Equal("B", Assert.Single(result.Dependencies).Id);
        }

        private static Dictionary<string, PrunePackageReference> Rules(string id) =>
            new(StringComparer.OrdinalIgnoreCase) { [id] = PrunePackageReference.Create(id, "1.0.0") };

        private static GraphItem<RemoteResolveResult> CreateItem() =>
            new(new LibraryIdentity("A", NuGetVersion.Parse("1.0.0"), LibraryType.Package))
            {
                Data = new RemoteResolveResult
                {
                    Match = null!,
                    Dependencies = new[] { "B", "C" }.Select(id => new LibraryDependency(
                        new LibraryRange(id, VersionRange.Parse("1.0.0"), LibraryDependencyTarget.Package))).ToList()
                }
            };
    }
}
