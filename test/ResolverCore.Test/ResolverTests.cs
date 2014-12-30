using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using NuGet.Resolver;
using NuGet.PackagingCore;
using NuGet.Versioning;

namespace NuGet.ResolverTest
{
    public class ResolverTests
    {
        [Fact]
        public void Resolver_Basic()
        {
            ResolverPackage target = new ResolverPackage("a", new NuGetVersion(1, 0, 0), new PackageDependency[] { new PackageDependency("b", null) });

            var dep1 = new ResolverPackage("b");
            dep1.Absent = true;

            List<ResolverPackage> possible = new List<ResolverPackage>();
            possible.Add(dep1);

            var solution = NuGet.Resolver.Resolver.Resolve(target, possible);

            Assert.Equal(2, solution.Count());
        }

        [Fact]
        public void Resolver_NoSolution()
        {
            ResolverPackage target = new ResolverPackage("a", new NuGetVersion(1, 0, 0), new PackageDependency[] { new PackageDependency("b", null) });

            List<ResolverPackage> possible = new List<ResolverPackage>();

            var solution = NuGet.Resolver.Resolver.Resolve(target, possible);

            Assert.Equal(0, solution.Count());
        }
    }
}
