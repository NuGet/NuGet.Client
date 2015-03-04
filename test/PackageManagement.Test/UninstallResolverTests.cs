using NuGet.PackageManagement;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NuGet.Test
{
    public class UninstallResolverTests
    {
        private static PackageDependencyInfo CreatePackageDependencyInfo(PackageIdentity packageIdentity, params string[] dependencies)
        {
            if(dependencies != null && dependencies.Length % 2 != 0)
            {
                throw new ArgumentException("dependencies array length should be even");
            }

            List<PackageDependency> dependencyList = new List<PackageDependency>();
            if(dependencies != null)
            {
                for (int i = 0; i < dependencies.Length; i += 2)
                {
                    var packageDependency = new PackageDependency(dependencies[i], VersionRange.Parse(dependencies[i+1]));
                    dependencyList.Add(packageDependency);
                }
            }

            return new PackageDependencyInfo(packageIdentity, dependencyList);
        }

        private static PackageIdentity A1 = new PackageIdentity("A", new NuGetVersion("1.0"));
        private static PackageIdentity A2 = new PackageIdentity("A", new NuGetVersion("2.0"));
        private static PackageIdentity B1 = new PackageIdentity("B", new NuGetVersion("1.0"));
        private static PackageIdentity B2 = new PackageIdentity("B", new NuGetVersion("2.0"));
        private static PackageIdentity C1 = new PackageIdentity("C", new NuGetVersion("1.0"));
        private static PackageIdentity D1 = new PackageIdentity("D", new NuGetVersion("1.0"));
        private static PackageIdentity E1 = new PackageIdentity("E", new NuGetVersion("1.0"));
        private static PackageIdentity F1 = new PackageIdentity("F", new NuGetVersion("1.0"));

        private static List<PackageDependencyInfo> PackageDependencyInfo1 = new List<PackageDependencyInfo>()
        {
            CreatePackageDependencyInfo(A1, B1.Id, "1.0", C1.Id, "1.0"),
            CreatePackageDependencyInfo(C1, D1.Id, "1.0", E1.Id, "1.0"),
        };

        private static IEnumerable<PackageIdentity> InstalledPackages1 = new List<PackageIdentity>()
        {
            A1,
            B1,
            C1,
            D1,
            E1
        };

        private static List<PackageDependencyInfo> DiamondDependencyInfo = new List<PackageDependencyInfo>()
        {
            CreatePackageDependencyInfo(A1, B1.Id, "1.0", C1.Id, "1.0"),
            CreatePackageDependencyInfo(B1, D1.Id, "1.0"),
            CreatePackageDependencyInfo(C1, D1.Id, "1.0"),
        };

        private static IEnumerable<PackageIdentity> DiamondDependencyInstalledPackages = new List<PackageIdentity>()
        {
            A1,
            B1,
            C1,
            D1,
        };

        private static List<PackageDependencyInfo> DeepDiamondDependencyInfo = new List<PackageDependencyInfo>()
        {
            CreatePackageDependencyInfo(A1, B1.Id, "1.0", C1.Id, "1.0"),
            CreatePackageDependencyInfo(B1, D1.Id, "1.0"),
            CreatePackageDependencyInfo(C1, E1.Id, "1.0"),
            CreatePackageDependencyInfo(E1, D1.Id, "1.0"),
        };

        private static IEnumerable<PackageIdentity> DeepDiamondDependencyInstalledPackages = new List<PackageIdentity>()
        {
            A1,
            B1,
            C1,
            D1,
            E1,
        };

        [Fact]
        public void TestUninstallResolverGetDependentsDict()
        {
            IDictionary<PackageIdentity, HashSet<PackageIdentity>> dependenciesDict;
            var dependentsDict = UninstallResolver.GetPackageDependents(PackageDependencyInfo1,
                InstalledPackages1,
                out dependenciesDict);

            var keysList = dependentsDict.Keys.ToList();
            var valuesList = dependentsDict.Values.ToList();

            Assert.Equal(4, dependentsDict.Count);
            Assert.Equal(B1, keysList[0]);
            Assert.True(valuesList[0].Contains(A1));
            Assert.Equal(C1, keysList[1]);
            Assert.True(valuesList[1].Contains(A1));
            Assert.Equal(D1, keysList[2]);
            Assert.True(valuesList[2].Contains(C1));
            Assert.Equal(E1, keysList[3]);
            Assert.True(valuesList[3].Contains(C1));
        }

        [Fact]
        public void TestUninstallResolverDependenciesDict()
        {
            IDictionary<PackageIdentity, HashSet<PackageIdentity>> dependenciesDict;
            var dependentsDict = UninstallResolver.GetPackageDependents(PackageDependencyInfo1,
                InstalledPackages1,
                out dependenciesDict);

            var keysList = dependenciesDict.Keys.ToList();
            var valuesList = dependenciesDict.Values.ToList();

            Assert.Equal(2, dependenciesDict.Count);
            Assert.Equal(A1, keysList[0]);
            Assert.True(valuesList[0].Contains(B1));
            Assert.True(valuesList[0].Contains(C1));
            Assert.Equal(C1, keysList[1]);
            Assert.True(valuesList[1].Contains(D1));
            Assert.True(valuesList[1].Contains(E1));
        }

        [Fact]
        public void TestUninstallResolverSimplePass()
        {
            // Act
            var result = UninstallResolver.GetPackagesToBeUninstalled(A1,
                PackageDependencyInfo1,
                InstalledPackages1,
                new UninstallationContext(removeDependencies: true)).ToList();

            // Assert
            Assert.Equal(5, result.Count);
            Assert.True(result[0].Equals(A1));
            Assert.True(result[1].Equals(B1));
            Assert.True(result[2].Equals(C1));
            Assert.True(result[3].Equals(D1));
            Assert.True(result[4].Equals(E1));
        }

        [Fact]
        public void TestUninstallResolverSimpleFail()
        {
            // Act
            Exception exception = null;
            try
            {
                var result = UninstallResolver.GetPackagesToBeUninstalled(E1,
                                                PackageDependencyInfo1,
                                                InstalledPackages1,
                                                new UninstallationContext(removeDependencies: true)).ToList();
            }
            catch (InvalidOperationException ex)
            {
                exception = ex;
            }
            catch (AggregateException ex)
            {
                exception = ExceptionUtility.Unwrap(ex);
            }

            // Assert
            Assert.NotNull(exception);
            Assert.True(exception is InvalidOperationException);
            Assert.Equal("Unable to uninstall 'E.1.0.0' because 'C.1.0.0' depends on it.",
                exception.Message);
        }

        [Fact]
        public void DiamondDependencyUninstall()
        {
            // Act
            var result = UninstallResolver.GetPackagesToBeUninstalled(A1,
                DiamondDependencyInfo,
                DiamondDependencyInstalledPackages,
                new UninstallationContext(removeDependencies: true)).ToList();

            // Assert
            Assert.Equal(4, result.Count);
            Assert.True(result[0].Equals(A1));
            Assert.True(result[1].Equals(B1));
            Assert.True(result[2].Equals(C1));
            Assert.True(result[3].Equals(D1));
        }

        [Fact]
        public void DeepDiamondDependencyUninstall()
        {
            // Act
            var result = UninstallResolver.GetPackagesToBeUninstalled(A1,
                DeepDiamondDependencyInfo,
                DeepDiamondDependencyInstalledPackages,
                new UninstallationContext(removeDependencies: true)).ToList();

            // Assert
            Assert.Equal(5, result.Count);
            Assert.True(result[0].Equals(A1));
            Assert.True(result[1].Equals(B1));
            Assert.True(result[2].Equals(C1));
            Assert.True(result[3].Equals(E1));
            Assert.True(result[4].Equals(D1));
        }
    }
}
