using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using NuGet.PackageManagement;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using NuGet.Frameworks;
using System.Threading;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;

namespace NuGet.Test
{
    public class ResolverGatherTests
    {
        /// <summary>
        /// Verify packages can be found across repos
        /// </summary>
        [Fact]
        public async Task ResolverGather_Basic()
        {
            ResolutionContext context = new ResolutionContext(Resolver.DependencyBehavior.Lowest, true);

            PackageIdentity target = new PackageIdentity("a", new NuGetVersion(1, 0, 0));
            IEnumerable<PackageIdentity> targets = new PackageIdentity[] { target };

            NuGetFramework framework = NuGetFramework.Parse("net451");

            List<PackageDependencyInfo> packagesA = new List<PackageDependencyInfo>()
            {
                new PackageDependencyInfo("a", new NuGetVersion(1, 0, 0), new PackageDependency[] { new PackageDependency("b", new VersionRange(new NuGetVersion(1, 0, 0))) }),
                new PackageDependencyInfo("c", new NuGetVersion(1, 0, 0), new PackageDependency[] { new PackageDependency("d", new VersionRange(new NuGetVersion(1, 0, 0))) }),
                new PackageDependencyInfo("e", new NuGetVersion(1, 0, 0), new PackageDependency[] { }),
                new PackageDependencyInfo("notpartofthis", new NuGetVersion(1, 0, 0), new PackageDependency[] { })
            };

            List<PackageDependencyInfo> packagesB = new List<PackageDependencyInfo>()
            {
                new PackageDependencyInfo("b", new NuGetVersion(1, 0, 0), new PackageDependency[] { new PackageDependency("c", new VersionRange(new NuGetVersion(1, 0, 0))) }),
                new PackageDependencyInfo("d", new NuGetVersion(1, 0, 0), new PackageDependency[] { new PackageDependency("e", new VersionRange(new NuGetVersion(1, 0, 0))) }),
                new PackageDependencyInfo("notpartofthis2", new NuGetVersion(1, 0, 0), new PackageDependency[] { })
            };

            List<Lazy<INuGetResourceProvider>> providersA = new List<Lazy<INuGetResourceProvider>>();
            providersA.Add(new Lazy<INuGetResourceProvider>(() => new TestDependencyInfoProvider(packagesA)));

            List<Lazy<INuGetResourceProvider>> providersB = new List<Lazy<INuGetResourceProvider>>();
            providersB.Add(new Lazy<INuGetResourceProvider>(() => new TestDependencyInfoProvider(packagesB)));

            List<SourceRepository> repos = new List<SourceRepository>();
            repos.Add(new SourceRepository(new PackageSource("http://a"), providersA));
            repos.Add(new SourceRepository(new PackageSource("http://b"), providersB));

            var results = await ResolverGather.GatherPackageDependencyInfo(context, targets, Enumerable.Empty<PackageIdentity>(),
                framework, repos, repos, CancellationToken.None);

            var check = results.OrderBy(e => e.Id).ToList();

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
        public async Task ResolverGather_Basic2()
        {
            ResolutionContext context = new ResolutionContext(Resolver.DependencyBehavior.Lowest, true);

            PackageIdentity target = new PackageIdentity("a", new NuGetVersion(1, 0, 0));
            IEnumerable<PackageIdentity> targets = new PackageIdentity[] { target };

            NuGetFramework framework = NuGetFramework.Parse("net451");

            List<PackageDependencyInfo> packagesA = new List<PackageDependencyInfo>()
            {
                new PackageDependencyInfo("a", new NuGetVersion(1, 0, 0), new PackageDependency[] { new PackageDependency("b", new VersionRange(new NuGetVersion(1, 0, 0))) }),
                new PackageDependencyInfo("c", new NuGetVersion(1, 0, 0), new PackageDependency[] { new PackageDependency("d", new VersionRange(new NuGetVersion(1, 0, 0))) }),
                new PackageDependencyInfo("e", new NuGetVersion(1, 0, 0), new PackageDependency[] { }),
                new PackageDependencyInfo("d", new NuGetVersion(1, 0, 0), new PackageDependency[] { new PackageDependency("e", new VersionRange(new NuGetVersion(1, 0, 0))) }),
                new PackageDependencyInfo("notpartofthis", new NuGetVersion(1, 0, 0), new PackageDependency[] { })
            };

            List<PackageDependencyInfo> packagesB = new List<PackageDependencyInfo>()
            {
                new PackageDependencyInfo("b", new NuGetVersion(1, 0, 0), new PackageDependency[] { new PackageDependency("c", new VersionRange(new NuGetVersion(1, 0, 0))) }),
                new PackageDependencyInfo("notpartofthis2", new NuGetVersion(1, 0, 0), new PackageDependency[] { })
            };

            List<Lazy<INuGetResourceProvider>> providersA = new List<Lazy<INuGetResourceProvider>>();
            providersA.Add(new Lazy<INuGetResourceProvider>(() => new TestDependencyInfoProvider(packagesA)));

            List<Lazy<INuGetResourceProvider>> providersB = new List<Lazy<INuGetResourceProvider>>();
            providersB.Add(new Lazy<INuGetResourceProvider>(() => new TestDependencyInfoProvider(packagesB)));

            List<SourceRepository> repos = new List<SourceRepository>();
            repos.Add(new SourceRepository(new PackageSource("http://a"), providersA));
            repos.Add(new SourceRepository(new PackageSource("http://b"), providersB));

            var results = await ResolverGather.GatherPackageDependencyInfo(context, targets, Enumerable.Empty<PackageIdentity>(),
                framework, repos, repos, CancellationToken.None);

            var check = results.OrderBy(e => e.Id).ToList();

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
        public async Task ResolverGather_Missing()
        {
            ResolutionContext context = new ResolutionContext(Resolver.DependencyBehavior.Lowest, true);

            PackageIdentity target = new PackageIdentity("a", new NuGetVersion(1, 0, 0));
            IEnumerable<PackageIdentity> targets = new PackageIdentity[] { target };

            NuGetFramework framework = NuGetFramework.Parse("net451");

            List<PackageDependencyInfo> packagesA = new List<PackageDependencyInfo>()
            {
                new PackageDependencyInfo("a", new NuGetVersion(1, 0, 0), new PackageDependency[] { new PackageDependency("b", new VersionRange(new NuGetVersion(1, 0, 0))) }),
                new PackageDependencyInfo("c", new NuGetVersion(1, 0, 0), new PackageDependency[] { new PackageDependency("d", new VersionRange(new NuGetVersion(1, 0, 0))) }),
                new PackageDependencyInfo("e", new NuGetVersion(1, 0, 0), new PackageDependency[] { }),
                new PackageDependencyInfo("notpartofthis", new NuGetVersion(1, 0, 0), new PackageDependency[] { })
            };

            List<PackageDependencyInfo> packagesB = new List<PackageDependencyInfo>()
            {
                new PackageDependencyInfo("b", new NuGetVersion(1, 0, 0), new PackageDependency[] { new PackageDependency("c", new VersionRange(new NuGetVersion(1, 0, 0))) }),
                new PackageDependencyInfo("notpartofthis2", new NuGetVersion(1, 0, 0), new PackageDependency[] { })
            };

            List<Lazy<INuGetResourceProvider>> providersA = new List<Lazy<INuGetResourceProvider>>();
            providersA.Add(new Lazy<INuGetResourceProvider>(() => new TestDependencyInfoProvider(packagesA)));

            List<Lazy<INuGetResourceProvider>> providersB = new List<Lazy<INuGetResourceProvider>>();
            providersB.Add(new Lazy<INuGetResourceProvider>(() => new TestDependencyInfoProvider(packagesB)));

            List<SourceRepository> repos = new List<SourceRepository>();
            repos.Add(new SourceRepository(new PackageSource("http://a"), providersA));
            repos.Add(new SourceRepository(new PackageSource("http://b"), providersB));

            var results = await ResolverGather.GatherPackageDependencyInfo(context, targets, Enumerable.Empty<PackageIdentity>(),
                framework, repos, repos, CancellationToken.None);

            var check = results.OrderBy(e => e.Id).ToList();

            Assert.Equal(3, check.Count);
            Assert.Equal("a", check[0].Id);
            Assert.Equal("b", check[1].Id);
            Assert.Equal("c", check[2].Id);
        }

        /// <summary>
        /// Verify packages can be found across repos
        /// </summary>
        [Fact]
        public async Task ResolverGather_Complex()
        {
            ResolutionContext context = new ResolutionContext(Resolver.DependencyBehavior.Lowest, true);

            PackageIdentity target = new PackageIdentity("a", new NuGetVersion(1, 0, 0));
            IEnumerable<PackageIdentity> targets = new PackageIdentity[] { target };

            NuGetFramework framework = NuGetFramework.Parse("net451");

            List<PackageDependencyInfo> packages1 = new List<PackageDependencyInfo>()
            {
                new PackageDependencyInfo("c", new NuGetVersion(1, 0, 0), new PackageDependency[] { }),
            };

            List<PackageDependencyInfo> packages2 = new List<PackageDependencyInfo>()
            {
                new PackageDependencyInfo("b", new NuGetVersion(1, 0, 0), new PackageDependency[] { new PackageDependency("c", new VersionRange(new NuGetVersion(1, 0, 0))) }),
            };

            List<PackageDependencyInfo> packages3 = new List<PackageDependencyInfo>()
            {
                new PackageDependencyInfo("a", new NuGetVersion(1, 0, 0), new PackageDependency[] { new PackageDependency("b", new VersionRange(new NuGetVersion(1, 0, 0))) }),
            };

            List<Lazy<INuGetResourceProvider>> providers1 = new List<Lazy<INuGetResourceProvider>>();
            providers1.Add(new Lazy<INuGetResourceProvider>(() => new TestDependencyInfoProvider(packages1)));

            List<Lazy<INuGetResourceProvider>> providers2 = new List<Lazy<INuGetResourceProvider>>();
            providers2.Add(new Lazy<INuGetResourceProvider>(() => new TestDependencyInfoProvider(packages2)));

            List<Lazy<INuGetResourceProvider>> providers3 = new List<Lazy<INuGetResourceProvider>>();
            providers3.Add(new Lazy<INuGetResourceProvider>(() => new TestDependencyInfoProvider(packages3)));

            List<SourceRepository> repos = new List<SourceRepository>();
            repos.Add(new SourceRepository(new PackageSource("http://1"), providers1));
            repos.Add(new SourceRepository(new PackageSource("http://2"), providers2));
            repos.Add(new SourceRepository(new PackageSource("http://3"), providers3));

            var results = await ResolverGather.GatherPackageDependencyInfo(context, targets, Enumerable.Empty<PackageIdentity>(),
                framework, repos, repos, CancellationToken.None);

            var check = results.OrderBy(e => e.Id).ToList();

            Assert.Equal(3, check.Count);
            Assert.Equal("a", check[0].Id);
            Assert.Equal("b", check[1].Id);
            Assert.Equal("c", check[2].Id);
        }
    }

    internal class TestDependencyInfoProvider : ResourceProvider
    {
        public List<PackageDependencyInfo> Packages { get; set; }

        public TestDependencyInfoProvider(List<PackageDependencyInfo> packages) 
            : base(typeof(DepedencyInfoResource))
        {
            Packages = packages;
        }

        public override Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            var nuGetResource = new TestDependencyInfo(Packages);
            return Task.FromResult(new Tuple<bool, INuGetResource>(true, nuGetResource));
        }
    }

    /// <summary>
    /// Resolves against a local set of packages
    /// </summary>
    internal class TestDependencyInfo : DepedencyInfoResource
    {
        public List<PackageDependencyInfo> Packages { get; set; }

        public TestDependencyInfo(List<PackageDependencyInfo> packages)
        {
            Packages = packages;
        }

        public override async Task<IEnumerable<PackageDependencyInfo>> ResolvePackages(IEnumerable<string> packageIds, NuGetFramework projectFramework, bool includePrerelease, CancellationToken token)
        {
            List<PackageDependencyInfo> results = new List<PackageDependencyInfo>();

            foreach (var packageId in packageIds)
            {
                results.AddRange(await ResolvePackages(Packages.Where(e => StringComparer.OrdinalIgnoreCase.Equals(e.Id, packageId)), projectFramework, includePrerelease, token));
            }

            return results;
        }

        public override async Task<IEnumerable<PackageDependencyInfo>> ResolvePackages(IEnumerable<PackageIdentity> packages, NuGetFramework projectFramework, bool includePrerelease, CancellationToken token)
        {
            var results = new HashSet<PackageDependencyInfo>(
                Packages.Where(e => packages.Contains(e, PackageIdentity.Comparer)),
                PackageIdentity.Comparer);

            bool complete = false;

            while (!complete)
            {
                var dependencies = results.SelectMany(e => e.Dependencies).Select(e => e.Id).Distinct(StringComparer.OrdinalIgnoreCase);

                int before = results.Count;

                results.UnionWith(Packages.Where(e => dependencies.Contains(e.Id, StringComparer.OrdinalIgnoreCase)));

                complete = before == results.Count;
            }

            return results;
        }
    }
}
