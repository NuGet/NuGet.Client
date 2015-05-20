// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.PackageManagement;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Resolver;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Test
{
    public class ResolverGatherTests
    {
        // [Fact]
        public void ResolverGather_MissingPrimaryPackage()
        {
            // Arrange
            var target = CreatePackage("a", "2.0.0");
            IEnumerable<PackageIdentity> targets = new[] { target };

            var framework = NuGetFramework.Parse("net451");

            var repoA = new List<SourcePackageDependencyInfo>
                {
                    CreateDependencyInfo("a", "1.0.0"),
                    CreateDependencyInfo("a", "3.0.0")
                };

            var repoInstalled = new List<SourcePackageDependencyInfo>()
                {
                    // missing packages
                };

            var primaryRepo = new List<SourceRepository>();
            primaryRepo.Add(CreateRepo("a", repoA));

            var repos = new List<SourceRepository>();
            repos.Add(CreateRepo("a", repoA));

            var installedPackages = new List<PackageIdentity>
                {
                    CreatePackage("a", "1.0.0")
                };

            // Act and Assert
            Assert.Throws(typeof(InvalidOperationException), () =>
                {
                    try
                    {
                        ResolverGather.GatherPackageDependencyInfo(targets,
                            installedPackages, framework, primaryRepo, repos, CreateRepo("installed", repoInstalled), CancellationToken.None).Wait();
                    }
                    catch (AggregateException ex)
                    {
                        throw ex.InnerException;
                    }
                });
        }

        [Fact]
        public async Task ResolverGather_MissingPackageGatheredFromSource()
        {
            // Arrange

            var target = CreatePackage("a", "2.0.0");
            IEnumerable<PackageIdentity> targets = new[] { target };

            var framework = NuGetFramework.Parse("net451");

            var repoA = new List<SourcePackageDependencyInfo>
                {
                    CreateDependencyInfo("a", "1.0.0"),
                    CreateDependencyInfo("a", "2.0.0"),
                    CreateDependencyInfo("a", "3.0.0"),
                    CreateDependencyInfo("b", "1.0.0"),
                    CreateDependencyInfo("b", "2.0.0")
                };

            var repoInstalled = new List<SourcePackageDependencyInfo>()
                {
                    // missing packages
                };

            var primaryRepo = new List<SourceRepository>();
            primaryRepo.Add(CreateRepo("a", repoA));

            var repos = new List<SourceRepository>();
            repos.Add(CreateRepo("a", repoA));

            var installedPackages = new List<PackageIdentity>
                {
                    CreatePackage("a", "1.0.0"),
                    CreatePackage("b", "1.0.0")
                };

            // Act
            var results = await ResolverGather.GatherPackageDependencyInfo(targets,
                installedPackages, framework, primaryRepo, repos, CreateRepo("installed", repoInstalled), CancellationToken.None);

            var check = results.GroupBy(e => e.Id).OrderBy(e => e.Key).ToList();

            // Assert
            Assert.Equal(2, check.Count);
            Assert.Equal("a", check[0].Key);
            Assert.Equal(1, check[0].Count());
            Assert.Equal("b", check[1].Key);
            Assert.Equal(2, check[1].Count());
        }

        [Fact]
        public async Task ResolverGather_VerifyUnrelatedPackageIsIgnored()
        {
            // Arrange

            var target = CreatePackage("a", "2.0.0");
            IEnumerable<PackageIdentity> targets = new[] { target };

            var framework = NuGetFramework.Parse("net451");

            var repoA = new List<SourcePackageDependencyInfo>
                {
                    CreateDependencyInfo("a", "1.0.0"),
                    CreateDependencyInfo("a", "2.0.0"),
                    CreateDependencyInfo("a", "3.0.0"),
                    CreateDependencyInfo("b", "1.0.0"),
                    CreateDependencyInfo("b", "2.0.0")
                };

            var repoInstalled = new List<SourcePackageDependencyInfo>
                {
                    CreateDependencyInfo("a", "1.0.0"),
                    CreateDependencyInfo("b", "1.0.0")
                };

            var primaryRepo = new List<SourceRepository>();
            primaryRepo.Add(CreateRepo("a", repoA));

            var repos = new List<SourceRepository>();
            repos.Add(CreateRepo("a", repoA));

            var installedPackages = new List<PackageIdentity>
                {
                    CreatePackage("a", "1.0.0"),
                    CreatePackage("b", "1.0.0")
                };

            // Act
            var results = await ResolverGather.GatherPackageDependencyInfo(targets,
                installedPackages, framework, primaryRepo, repos, CreateRepo("installed", repoInstalled), CancellationToken.None);

            var check = results.GroupBy(e => e.Id).OrderBy(e => e.Key).ToList();

            // Assert
            Assert.Equal(2, check.Count);
            Assert.Equal("a", check[0].Key);
            Assert.Equal(2, check[0].Count());
            Assert.Equal("b", check[1].Key);
            Assert.Equal(1, check[1].Count());
        }

        [Fact]
        public async Task ResolverGather_VerifyParentDependencyIsExpanded()
        {
            // Arrange

            var target = CreatePackage("c", "2.0.0");
            IEnumerable<PackageIdentity> targets = new[] { target };

            var framework = NuGetFramework.Parse("net451");

            var repoA = new List<SourcePackageDependencyInfo>
                {
                    CreateDependencyInfo("a", "2.0.0", "d"),
                    CreateDependencyInfo("a", "1.0.0", "b"),
                    CreateDependencyInfo("b", "1.0.0", "c"),
                    CreateDependencyInfo("b", "2.0.0", "c"),
                    CreateDependencyInfo("c", "1.0.0"),
                    CreateDependencyInfo("c", "2.0.0"),
                    CreateDependencyInfo("d", "1.0.0")
                };

            var repoInstalled = new List<SourcePackageDependencyInfo>
                {
                    CreateDependencyInfo("a", "1.0.0", "b"),
                    CreateDependencyInfo("b", "1.0.0", "c"),
                    CreateDependencyInfo("c", "1.0.0")
                };

            var primaryRepo = new List<SourceRepository>();
            primaryRepo.Add(CreateRepo("a", repoA));

            var repos = new List<SourceRepository>();
            repos.Add(CreateRepo("a", repoA));

            var installedPackages = new List<PackageIdentity>
                {
                    CreatePackage("a", "1.0.0"),
                    CreatePackage("b", "1.0.0")
                };

            // Act
            var results = await ResolverGather.GatherPackageDependencyInfo(targets,
                installedPackages, framework, primaryRepo, repos, CreateRepo("installed", repoInstalled), CancellationToken.None);

            var check = results.GroupBy(e => e.Id).OrderBy(e => e.Key).ToList();

            // Assert
            Assert.Equal(4, check.Count);
            Assert.Equal("a", check[0].Key);
            Assert.Equal("b", check[1].Key);
            Assert.Equal("c", check[2].Key);
            Assert.Equal("d", check[3].Key);
        }

        [Fact]
        public async Task ResolverGather_VerifyDependencyIsExpanded()
        {
            // Arrange

            var target = CreatePackage("a", "2.0.0");
            IEnumerable<PackageIdentity> targets = new[] { target };

            var framework = NuGetFramework.Parse("net451");

            var repoA = new List<SourcePackageDependencyInfo>
                {
                    CreateDependencyInfo("a", "2.0.0", "b"),
                    CreateDependencyInfo("a", "1.0.0", "b"),
                    CreateDependencyInfo("b", "1.0.0"),
                    CreateDependencyInfo("b", "2.0.0", "c"),
                    CreateDependencyInfo("c", "2.0.0")
                };

            var repoInstalled = new List<SourcePackageDependencyInfo>
                {
                    CreateDependencyInfo("a", "1.0.0", "b"),
                    CreateDependencyInfo("b", "1.0.0")
                };

            var primaryRepo = new List<SourceRepository>();
            primaryRepo.Add(CreateRepo("a", repoA));

            var repos = new List<SourceRepository>();
            repos.Add(CreateRepo("a", repoA));

            var installedPackages = new List<PackageIdentity>
                {
                    CreatePackage("a", "1.0.0"),
                    CreatePackage("b", "1.0.0")
                };

            // Act
            var results = await ResolverGather.GatherPackageDependencyInfo(targets,
                installedPackages, framework, primaryRepo, repos, CreateRepo("installed", repoInstalled), CancellationToken.None);

            var check = results.GroupBy(e => e.Id).OrderBy(e => e.Key).ToList();

            // Assert
            Assert.Equal(3, check.Count);
            Assert.Equal("a", check[0].Key);
            Assert.Equal("b", check[1].Key);

            // when a is updated, b is expanded and c is found
            Assert.Equal("c", check[2].Key);
        }

        [Fact]
        public async Task ResolverGather_VerifyParentIsExpanded()
        {
            // Arrange

            var target = CreatePackage("b", "2.0.0");
            IEnumerable<PackageIdentity> targets = new[] { target };

            var framework = NuGetFramework.Parse("net451");

            var repoA = new List<SourcePackageDependencyInfo>
                {
                    CreateDependencyInfo("a", "2.0.0", "b", "c"),
                    CreateDependencyInfo("a", "1.0.0", "b"),
                    CreateDependencyInfo("b", "1.0.0"),
                    CreateDependencyInfo("b", "2.0.0"),
                    CreateDependencyInfo("c", "2.0.0")
                };

            var repoInstalled = new List<SourcePackageDependencyInfo>
                {
                    CreateDependencyInfo("a", "1.0.0", "b"),
                    CreateDependencyInfo("b", "1.0.0")
                };

            var primaryRepo = new List<SourceRepository>();
            primaryRepo.Add(CreateRepo("a", repoA));

            var repos = new List<SourceRepository>();
            repos.Add(CreateRepo("a", repoA));

            var installedPackages = new List<PackageIdentity>
                {
                    CreatePackage("a", "1.0.0"),
                    CreatePackage("b", "1.0.0")
                };

            // Act
            var results = await ResolverGather.GatherPackageDependencyInfo(targets,
                installedPackages, framework, primaryRepo, repos, CreateRepo("installed", repoInstalled), CancellationToken.None);

            var check = results.GroupBy(e => e.Id).OrderBy(e => e.Key).ToList();

            // Assert
            Assert.Equal(3, check.Count);
            Assert.Equal("a", check[0].Key);
            Assert.Equal("b", check[1].Key);

            // when update b, a is expanded which then retrieves c
            Assert.Equal("c", check[2].Key);
        }

        [Fact]
        public async Task ResolverGather_ComplexGraphNeedingMultiplePasses()
        {
            // Arrange

            var target = CreatePackage("a", "1.0.0");
            IEnumerable<PackageIdentity> targets = new[] { target };

            var framework = NuGetFramework.Parse("net451");

            var repoA = new List<SourcePackageDependencyInfo>
                {
                    CreateDependencyInfo("a", "1.0.0", "b", "d"),
                    CreateDependencyInfo("a", "2.0.0", "z"),
                    CreateDependencyInfo("b", "1.0.0", "c"),
                    CreateDependencyInfo("b", "2.0.0", "c"),
                    CreateDependencyInfo("c", "1.0.0"),
                    CreateDependencyInfo("d", "1.0.0", "f"),
                    CreateDependencyInfo("f", "1.0.0"),
                    CreateDependencyInfo("g", "1.0.0"),
                    CreateDependencyInfo("g", "2.0.0"),
                    CreateDependencyInfo("c", "2.0.0"),
                    CreateDependencyInfo("j", "2.0.0"),
                    CreateDependencyInfo("z", "1.0.0"),
                    CreateDependencyInfo("g", "1.0.0", "a"),
                    CreateDependencyInfo("h", "1.0.0", "c", "g"),
                    CreateDependencyInfo("h", "2.0.0", "c"),
                    CreateDependencyInfo("h", "3.0.0", "j"),
                    CreateDependencyInfo("i", "1.0.0", "a", "b"),
                    CreateDependencyInfo("y", "1.0.0", "c"),
                    CreateDependencyInfo("y", "2.0.0", "c")
                };

            var repoB = new List<SourcePackageDependencyInfo>
                {
                    CreateDependencyInfo("b", "3.0.0", "c"),
                    CreateDependencyInfo("h", "3.0.0", "c"),
                    CreateDependencyInfo("c", "3.0.0"),
                    CreateDependencyInfo("g", "1.0.0")
                };

            var repoInstalled = new List<SourcePackageDependencyInfo>
                {
                    CreateDependencyInfo("y", "1.0.0", "c"),
                    CreateDependencyInfo("c", "1.0.0"),
                    CreateDependencyInfo("h", "1.0.0", "c", "g"),
                    CreateDependencyInfo("x", "1.0.0"),
                    CreateDependencyInfo("g", "1.0.0")
                };

            var primaryRepo = new List<SourceRepository>();
            primaryRepo.Add(CreateRepo("a", repoA));

            var repos = new List<SourceRepository>();
            repos.Add(CreateRepo("a", repoA));
            repos.Add(CreateRepo("b", repoB));

            var installedPackages = new List<PackageIdentity>
                {
                    CreatePackage("y", "1.0.0"),
                    CreatePackage("c", "1.0.0"),
                    CreatePackage("h", "1.0.0"),
                    CreatePackage("g", "1.0.0")
                };

            // Act
            var results = await ResolverGather.GatherPackageDependencyInfo(targets,
                installedPackages, framework, primaryRepo, repos, CreateRepo("installed", repoInstalled), CancellationToken.None);

            var check = results.GroupBy(e => e.Id).OrderBy(e => e.Key).ToList();

            // Assert
            Assert.Equal(9, check.Count);
            Assert.Equal("a", check[0].Key);
            Assert.Equal(1, check[0].Count());
            Assert.Equal("b", check[1].Key);
            Assert.Equal(3, check[1].Count());
            Assert.Equal("c", check[2].Key);
            Assert.Equal(3, check[2].Count());
            Assert.Equal("d", check[3].Key);
            Assert.Equal(1, check[3].Count());
            Assert.Equal("f", check[4].Key);
            Assert.Equal(1, check[4].Count());
            Assert.Equal("g", check[5].Key);
            Assert.Equal(2, check[5].Count());
            Assert.Equal("h", check[6].Key);
            Assert.Equal(3, check[6].Count());
            Assert.Equal("j", check[7].Key);
            Assert.Equal(1, check[7].Count());
            Assert.Equal("y", check[8].Key);
            Assert.Equal(2, check[8].Count());
        }

        /// <summary>
        /// Verify packages can be found across repos
        /// </summary>
        [Fact]
        public async Task ResolverGather_Basic()
        {
            // Arrange

            var target = new PackageIdentity("a", new NuGetVersion(1, 0, 0));
            IEnumerable<PackageIdentity> targets = new[] { target };

            var framework = NuGetFramework.Parse("net451");

            var packagesA = new List<SourcePackageDependencyInfo>
                {
                    new SourcePackageDependencyInfo("a", new NuGetVersion(1, 0, 0), new[] { new Packaging.Core.PackageDependency("b", new VersionRange(new NuGetVersion(1, 0, 0))) }, true, null),
                    new SourcePackageDependencyInfo("c", new NuGetVersion(1, 0, 0), new[] { new Packaging.Core.PackageDependency("d", new VersionRange(new NuGetVersion(1, 0, 0))) }, true, null),
                    new SourcePackageDependencyInfo("e", new NuGetVersion(1, 0, 0), new Packaging.Core.PackageDependency[] { }, true, null),
                    new SourcePackageDependencyInfo("notpartofthis", new NuGetVersion(1, 0, 0), new Packaging.Core.PackageDependency[] { }, true, null)
                };

            var packagesB = new List<SourcePackageDependencyInfo>
                {
                    new SourcePackageDependencyInfo("b", new NuGetVersion(1, 0, 0), new[] { new Packaging.Core.PackageDependency("c", new VersionRange(new NuGetVersion(1, 0, 0))) }, true, null),
                    new SourcePackageDependencyInfo("d", new NuGetVersion(1, 0, 0), new[] { new Packaging.Core.PackageDependency("e", new VersionRange(new NuGetVersion(1, 0, 0))) }, true, null),
                    new SourcePackageDependencyInfo("notpartofthis2", new NuGetVersion(1, 0, 0), new Packaging.Core.PackageDependency[] { }, true, null)
                };

            var providersA = new List<Lazy<INuGetResourceProvider>>();
            providersA.Add(new Lazy<INuGetResourceProvider>(() => new TestDependencyInfoProvider(packagesA)));

            var providersB = new List<Lazy<INuGetResourceProvider>>();
            providersB.Add(new Lazy<INuGetResourceProvider>(() => new TestDependencyInfoProvider(packagesB)));

            var providersC = new List<Lazy<INuGetResourceProvider>>();
            providersC.Add(new Lazy<INuGetResourceProvider>(() => new TestDependencyInfoProvider(new List<SourcePackageDependencyInfo>())));

            var repos = new List<SourceRepository>();
            repos.Add(new SourceRepository(new Configuration.PackageSource("http://a"), providersA));
            repos.Add(new SourceRepository(new Configuration.PackageSource("http://b"), providersB));
            repos.Add(new SourceRepository(new Configuration.PackageSource("http://c"), providersC));

            // Act
            var results = await ResolverGather.GatherPackageDependencyInfo(targets,
                Enumerable.Empty<PackageIdentity>(), framework, repos, repos, repos[2], CancellationToken.None);

            var check = results.OrderBy(e => e.Id).ToList();

            // Assert
            Assert.Equal(5, check.Count);
            Assert.Equal("a", check[0].Id);
            Assert.Equal("b", check[1].Id);
            Assert.Equal("c", check[2].Id);
            Assert.Equal("d", check[3].Id);
            Assert.Equal("e", check[4].Id);
        }

        /// <summary>
        /// Verify packages can be found across repos
        /// </summary>
        [Fact]
        public async Task ResolverGather_BasicGatherWithExtraPackages()
        {
            // Arrange

            var target = new PackageIdentity("a", new NuGetVersion(1, 0, 0));
            IEnumerable<PackageIdentity> targets = new[] { target };

            var framework = NuGetFramework.Parse("net451");

            var packagesA = new List<SourcePackageDependencyInfo>
                {
                    new SourcePackageDependencyInfo("a", new NuGetVersion(1, 0, 0), new[] { new Packaging.Core.PackageDependency("b", new VersionRange(new NuGetVersion(1, 0, 0))) }, true, null),
                    new SourcePackageDependencyInfo("c", new NuGetVersion(1, 0, 0), new[] { new Packaging.Core.PackageDependency("d", new VersionRange(new NuGetVersion(1, 0, 0))) }, true, null),
                    new SourcePackageDependencyInfo("e", new NuGetVersion(1, 0, 0), new Packaging.Core.PackageDependency[] { }, true, null),
                    new SourcePackageDependencyInfo("d", new NuGetVersion(1, 0, 0), new[] { new Packaging.Core.PackageDependency("e", new VersionRange(new NuGetVersion(1, 0, 0))) }, true, null),
                    new SourcePackageDependencyInfo("notpartofthis", new NuGetVersion(1, 0, 0), new Packaging.Core.PackageDependency[] { }, true, null)
                };

            var packagesB = new List<SourcePackageDependencyInfo>
                {
                    new SourcePackageDependencyInfo("b", new NuGetVersion(1, 0, 0), new[] { new Packaging.Core.PackageDependency("c", new VersionRange(new NuGetVersion(1, 0, 0))) }, true, null),
                    new SourcePackageDependencyInfo("notpartofthis2", new NuGetVersion(1, 0, 0), new Packaging.Core.PackageDependency[] { }, true, null)
                };

            var providersC = new List<Lazy<INuGetResourceProvider>>();
            providersC.Add(new Lazy<INuGetResourceProvider>(() => new TestDependencyInfoProvider(new List<SourcePackageDependencyInfo>())));

            var providersA = new List<Lazy<INuGetResourceProvider>>();
            providersA.Add(new Lazy<INuGetResourceProvider>(() => new TestDependencyInfoProvider(packagesA)));

            var providersB = new List<Lazy<INuGetResourceProvider>>();
            providersB.Add(new Lazy<INuGetResourceProvider>(() => new TestDependencyInfoProvider(packagesB)));

            var repos = new List<SourceRepository>();
            repos.Add(new SourceRepository(new Configuration.PackageSource("http://a"), providersA));
            repos.Add(new SourceRepository(new Configuration.PackageSource("http://b"), providersB));
            repos.Add(new SourceRepository(new Configuration.PackageSource("http://c"), providersC));

            // Act
            var results = await ResolverGather.GatherPackageDependencyInfo(targets, Enumerable.Empty<PackageIdentity>(),
                framework, repos, repos, repos[2], CancellationToken.None);

            var check = results.OrderBy(e => e.Id).ToList();

            // Assert
            Assert.Equal(5, check.Count);
            Assert.Equal("a", check[0].Id);
            Assert.Equal("b", check[1].Id);
            Assert.Equal("c", check[2].Id);
            Assert.Equal("d", check[3].Id);
            Assert.Equal("e", check[4].Id);
        }

        /// <summary>
        /// Verify packages can be found across repos
        /// </summary>
        [Fact]
        public async Task ResolverGather_GatherWithNotFoundPackages()
        {
            // Arrange

            var target = new PackageIdentity("a", new NuGetVersion(1, 0, 0));
            IEnumerable<PackageIdentity> targets = new[] { target };

            var framework = NuGetFramework.Parse("net451");

            var packagesA = new List<SourcePackageDependencyInfo>
                {
                    new SourcePackageDependencyInfo("a", new NuGetVersion(1, 0, 0), new[] { new Packaging.Core.PackageDependency("b", new VersionRange(new NuGetVersion(1, 0, 0))) }, true, null),
                    new SourcePackageDependencyInfo("c", new NuGetVersion(1, 0, 0), new[] { new Packaging.Core.PackageDependency("d", new VersionRange(new NuGetVersion(1, 0, 0))) }, true, null),
                    new SourcePackageDependencyInfo("e", new NuGetVersion(1, 0, 0), new Packaging.Core.PackageDependency[] { }, true, null),
                    new SourcePackageDependencyInfo("notpartofthis", new NuGetVersion(1, 0, 0), new Packaging.Core.PackageDependency[] { }, true, null)
                };

            var packagesB = new List<SourcePackageDependencyInfo>
                {
                    new SourcePackageDependencyInfo("b", new NuGetVersion(1, 0, 0), new[] { new Packaging.Core.PackageDependency("c", new VersionRange(new NuGetVersion(1, 0, 0))) }, true, null),
                    new SourcePackageDependencyInfo("notpartofthis2", new NuGetVersion(1, 0, 0), new Packaging.Core.PackageDependency[] { }, true, null)
                };

            var providersA = new List<Lazy<INuGetResourceProvider>>();
            providersA.Add(new Lazy<INuGetResourceProvider>(() => new TestDependencyInfoProvider(packagesA)));

            var providersB = new List<Lazy<INuGetResourceProvider>>();
            providersB.Add(new Lazy<INuGetResourceProvider>(() => new TestDependencyInfoProvider(packagesB)));

            var providersC = new List<Lazy<INuGetResourceProvider>>();
            providersC.Add(new Lazy<INuGetResourceProvider>(() => new TestDependencyInfoProvider(new List<SourcePackageDependencyInfo>())));

            var repos = new List<SourceRepository>();
            repos.Add(new SourceRepository(new Configuration.PackageSource("http://a"), providersA));
            repos.Add(new SourceRepository(new Configuration.PackageSource("http://b"), providersB));
            repos.Add(new SourceRepository(new Configuration.PackageSource("http://c"), providersC));

            // Act
            var results = await ResolverGather.GatherPackageDependencyInfo(targets,
                Enumerable.Empty<PackageIdentity>(), framework, repos, repos, repos[2], CancellationToken.None);

            var check = results.OrderBy(e => e.Id).ToList();

            // Assert
            Assert.Equal(3, check.Count);
            Assert.Equal("a", check[0].Id);
            Assert.Equal("b", check[1].Id);
            Assert.Equal("c", check[2].Id);
        }

        /// <summary>
        /// Verify packages can be found across repos
        /// </summary>
        [Fact]
        public async Task ResolverGather_DependenciesSpreadAcrossRepos()
        {
            // Arrange
            var target = new PackageIdentity("a", new NuGetVersion(1, 0, 0));
            IEnumerable<PackageIdentity> targets = new[] { target };

            var framework = NuGetFramework.Parse("net451");

            var packages1 = new List<SourcePackageDependencyInfo>
                {
                    new SourcePackageDependencyInfo("c", new NuGetVersion(1, 0, 0), new Packaging.Core.PackageDependency[] { }, true, null)
                };

            var packages2 = new List<SourcePackageDependencyInfo>
                {
                    new SourcePackageDependencyInfo("b", new NuGetVersion(1, 0, 0), new[] { new Packaging.Core.PackageDependency("c", new VersionRange(new NuGetVersion(1, 0, 0))) }, true, null)
                };

            var packages3 = new List<SourcePackageDependencyInfo>
                {
                    new SourcePackageDependencyInfo("a", new NuGetVersion(1, 0, 0), new[] { new Packaging.Core.PackageDependency("b", new VersionRange(new NuGetVersion(1, 0, 0))) }, true, null)
                };

            var providers1 = new List<Lazy<INuGetResourceProvider>>();
            providers1.Add(new Lazy<INuGetResourceProvider>(() => new TestDependencyInfoProvider(packages1)));

            var providers2 = new List<Lazy<INuGetResourceProvider>>();
            providers2.Add(new Lazy<INuGetResourceProvider>(() => new TestDependencyInfoProvider(packages2)));

            var providers3 = new List<Lazy<INuGetResourceProvider>>();
            providers3.Add(new Lazy<INuGetResourceProvider>(() => new TestDependencyInfoProvider(packages3)));

            var providersPackagesFolder = new List<Lazy<INuGetResourceProvider>>();
            providersPackagesFolder.Add(new Lazy<INuGetResourceProvider>(() => new TestDependencyInfoProvider(new List<SourcePackageDependencyInfo>())));

            var repos = new List<SourceRepository>();
            repos.Add(new SourceRepository(new Configuration.PackageSource("http://1"), providers1));
            repos.Add(new SourceRepository(new Configuration.PackageSource("http://2"), providers2));
            repos.Add(new SourceRepository(new Configuration.PackageSource("http://3"), providers3));
            repos.Add(new SourceRepository(new Configuration.PackageSource("http://4"), providersPackagesFolder));

            // Act
            var results = await ResolverGather.GatherPackageDependencyInfo(targets, Enumerable.Empty<PackageIdentity>(),
                framework, repos, repos, repos[3], CancellationToken.None);

            var check = results.OrderBy(e => e.Id).ToList();

            // Assert
            Assert.Equal(3, check.Count);
            Assert.Equal("a", check[0].Id);
            Assert.Equal("b", check[1].Id);
            Assert.Equal("c", check[2].Id);
        }

        private static SourceRepository CreateRepo(string source, List<SourcePackageDependencyInfo> packages)
        {
            var providers = new List<Lazy<INuGetResourceProvider>>();
            providers.Add(new Lazy<INuGetResourceProvider>(() => new TestDependencyInfoProvider(packages)));

            return new SourceRepository(new Configuration.PackageSource(source), providers);
        }

        private static SourcePackageDependencyInfo CreateDependencyInfo(string id, string version, params string[] dependencyIds)
        {
            return new SourcePackageDependencyInfo(CreatePackage(id, version),
                dependencyIds.Select(depId => new Packaging.Core.PackageDependency(depId, new VersionRange(NuGetVersion.Parse("1.0.0")))), true, null);
        }

        private static PackageIdentity CreatePackage(string id, string version)
        {
            return new PackageIdentity(id, NuGetVersion.Parse(version));
        }
    }

    internal class TestDependencyInfoProvider : ResourceProvider
    {
        public List<SourcePackageDependencyInfo> Packages { get; set; }

        public TestDependencyInfoProvider(List<SourcePackageDependencyInfo> packages)
            : base(typeof(DependencyInfoResource))
        {
            Packages = packages;
        }

        public override Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            var nuGetResource = new TestDependencyInfo(source, Packages);
            return Task.FromResult(new Tuple<bool, INuGetResource>(true, nuGetResource));
        }
    }

    /// <summary>
    /// Resolves against a local set of packages
    /// </summary>
    internal class TestDependencyInfo : DependencyInfoResource
    {
        public SourceRepository Source { get; set; }

        public List<SourcePackageDependencyInfo> Packages { get; set; }

        public TestDependencyInfo(SourceRepository source, List<SourcePackageDependencyInfo> packages)
        {
            Source = source;
            Packages = packages;
        }

        public override Task<SourcePackageDependencyInfo> ResolvePackage(PackageIdentity package, NuGetFramework projectFramework, CancellationToken token)
        {
            var matchingPackage = Packages.FirstOrDefault(e => PackageIdentity.Comparer.Equals(e, package));
            return Task.FromResult<SourcePackageDependencyInfo>(ApplySource(matchingPackage));
        }

        public override Task<IEnumerable<SourcePackageDependencyInfo>> ResolvePackages(string packageId, NuGetFramework projectFramework, CancellationToken token)
        {
            var results = new HashSet<SourcePackageDependencyInfo>(
                Packages.Where(e => StringComparer.OrdinalIgnoreCase.Equals(packageId, e.Id)),
                PackageIdentity.Comparer);

            return Task.FromResult<IEnumerable<SourcePackageDependencyInfo>>(results.Select(p => ApplySource(p)));
        }

        SourcePackageDependencyInfo ApplySource(SourcePackageDependencyInfo original)
        {
            if (original == null)
            {
                return null;
            }

            return new SourcePackageDependencyInfo(
                original.Id,
                original.Version,
                original.Dependencies,
                original.Listed,
                Source);
        }
    }

    /// <summary>
    /// Test MetadataProvider for unit tests
    /// </summary>
    internal class TestMetadataProvider : ResourceProvider
    {
        public List<SourcePackageDependencyInfo> Packages { get; set; }

        public TestMetadataProvider(List<SourcePackageDependencyInfo> packages)
            : base(typeof(MetadataResource))
        {
            Packages = packages;
        }

        public override Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            var nuGetResource = new TestMetadataResource(Packages);
            return Task.FromResult(new Tuple<bool, INuGetResource>(true, nuGetResource));
        }
    }

    /// <summary>
    /// Retrieve versions from a local set of packages
    /// </summary>
    internal class TestMetadataResource : MetadataResource
    {
        public List<SourcePackageDependencyInfo> Packages { get; set; }

        public TestMetadataResource(List<SourcePackageDependencyInfo> packages)
        {
            Packages = packages;
        }

        public override Task<IEnumerable<NuGetVersion>> GetVersions(string packageId, bool includePrerelease, bool includeUnlisted, CancellationToken token)
        {
            return Task.FromResult(Packages
                .Where(p =>
                    p.Id.Equals(packageId, StringComparison.InvariantCultureIgnoreCase)
                    && (!p.Version.IsPrerelease || includePrerelease)
                    && (p.Listed || includeUnlisted))
                .Select(p => p.Version)
            );
        }

        public override Task<bool> Exists(PackageIdentity identity, bool includeUnlisted, CancellationToken token)
        {
            return Task.FromResult(Packages
                .Exists(p => 
                    ((PackageIdentity)p).Equals(identity) 
                    && (p.Listed || includeUnlisted))
            );
        }

        public override Task<bool> Exists(string packageId, bool includePrerelease, bool includeUnlisted, CancellationToken token)
        {
            return Task.FromResult(Packages
                .Exists((p) => 
                    p.Id.Equals(packageId, StringComparison.InvariantCultureIgnoreCase)
                    && (!p.Version.IsPrerelease || includePrerelease)
                    && (p.Listed || includeUnlisted))
            );
        }

        public async override Task<IEnumerable<KeyValuePair<string, NuGetVersion>>> GetLatestVersions(IEnumerable<string> packageIds, bool includePrerelease, bool includeUnlisted, CancellationToken token)
        {
            var results = new List<KeyValuePair<string, NuGetVersion>>();

            foreach (var id in packageIds)
            {
                var versions = await GetVersions(id, token);
                var latest = versions.OrderByDescending(p => p, VersionComparer.VersionRelease).FirstOrDefault();

                if (latest != null)
                {
                    results.Add(new KeyValuePair<string, NuGetVersion>(id, latest));
                }
            }

            return results;
        }
    }
}
