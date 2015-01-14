using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using NuGet.PackageManagement;
using NuGet.PackagingCore;
using NuGet.Versioning;
using NuGet.Frameworks;
using NuGet.Client;
using System.Threading;
using NuGet.Configuration;

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

            List<Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>> providersA = new List<Lazy<INuGetResourceProvider,INuGetResourceProviderMetadata>>();
            providersA.Add(new Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>(() => new TestDependencyInfoProvider(packagesA), new TestAttribute()));

            List<Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>> providersB = new List<Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>>();
            providersB.Add(new Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>(() => new TestDependencyInfoProvider(packagesB), new TestAttribute()));

            List<SourceRepository> repos = new List<SourceRepository>();
            repos.Add(new SourceRepository(new PackageSource("http://a"), providersA));
            repos.Add(new SourceRepository(new PackageSource("http://b"), providersB));

            var results = await ResolverGather.GatherPackageDependencyInfo(context, target, framework, repos, CancellationToken.None);

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

            List<Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>> providersA = new List<Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>>();
            providersA.Add(new Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>(() => new TestDependencyInfoProvider(packagesA), new TestAttribute()));

            List<Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>> providersB = new List<Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>>();
            providersB.Add(new Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>(() => new TestDependencyInfoProvider(packagesB), new TestAttribute()));

            List<SourceRepository> repos = new List<SourceRepository>();
            repos.Add(new SourceRepository(new PackageSource("http://a"), providersA));
            repos.Add(new SourceRepository(new PackageSource("http://b"), providersB));

            var results = await ResolverGather.GatherPackageDependencyInfo(context, target, framework, repos, CancellationToken.None);

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

            List<Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>> providersA = new List<Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>>();
            providersA.Add(new Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>(() => new TestDependencyInfoProvider(packagesA), new TestAttribute()));

            List<Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>> providersB = new List<Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>>();
            providersB.Add(new Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>(() => new TestDependencyInfoProvider(packagesB), new TestAttribute()));

            List<SourceRepository> repos = new List<SourceRepository>();
            repos.Add(new SourceRepository(new PackageSource("http://a"), providersA));
            repos.Add(new SourceRepository(new PackageSource("http://b"), providersB));

            var results = await ResolverGather.GatherPackageDependencyInfo(context, target, framework, repos, CancellationToken.None);

            var check = results.OrderBy(e => e.Id).ToList();

            Assert.Equal(3, check.Count);
            Assert.Equal("a", check[0].Id);
            Assert.Equal("b", check[1].Id);
            Assert.Equal("c", check[2].Id);
        }
    }



    internal class TestAttribute : INuGetResourceProviderMetadata
    {
        public IEnumerable<string> After
        {
            get { return new string[0]; }
        }

        public IEnumerable<string> Before
        {
            get { return new string[0]; }
        }

        public string Name
        {
            get { return string.Empty; }
        }

        public Type ResourceType
        {
            get { return typeof(DepedencyInfoResource); }
        }
    }

    internal class TestDependencyInfoProvider : INuGetResourceProvider
    {
        public List<PackageDependencyInfo> Packages { get; set; }

        public TestDependencyInfoProvider(List<PackageDependencyInfo> packages)
        {
            Packages = packages;
        }

        public bool TryCreate(SourceRepository source, out INuGetResource resource)
        {
            resource = new TestDependencyInfo(Packages);
            return true;
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

        public override async Task<IEnumerable<PackageDependencyInfo>> ResolvePackages(string packageId, NuGetFramework projectFramework, bool includePrerelease, CancellationToken token)
        {
            return await ResolvePackages(Packages.Where(e => StringComparer.OrdinalIgnoreCase.Equals(e.Id, packageId)), projectFramework, includePrerelease, token);
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
