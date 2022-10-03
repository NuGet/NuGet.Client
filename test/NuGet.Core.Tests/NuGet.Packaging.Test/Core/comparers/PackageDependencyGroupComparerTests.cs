// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Shared;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class PackageDependencyGroupComparerTests
    {
        private static readonly PackageDependencyGroupComparer Comparer = PackageDependencyGroupComparer.Default;
        private static readonly NuGetFramework TargetFramework = NuGetFramework.Parse("net472");
        private static readonly PackageDependency PackageDependency = new PackageDependency("a", new VersionRange(NuGetVersion.Parse("1.0.0")));
        private static readonly PackageDependencyGroup DependencyGroupA = new PackageDependencyGroup(
            TargetFramework,
            new PackageDependency[] { PackageDependency });

        [Fact]
        public void Default_Always_ReturnsSameInstance()
        {
            PackageDependencyGroupComparer instance0 = PackageDependencyGroupComparer.Default;
            PackageDependencyGroupComparer instance1 = PackageDependencyGroupComparer.Default;

            Assert.Same(instance0, instance1);
        }

        [Fact]
        public void Equals_WhenArgumentsAreNull_ReturnsTrue()
        {
            Assert.True(Comparer.Equals(x: null, y: null));
        }

        [Fact]
        public void Equals_WhenArgumentsAreSame_ReturnsTrue()
        {
            Assert.True(Comparer.Equals(DependencyGroupA, DependencyGroupA));
        }

        [Fact]
        public void Equals_WhenArgumentsAreEqual_ReturnsTrue()
        {
            var dependencyGroupA1 = new PackageDependencyGroup(TargetFramework, DependencyGroupA.Packages);

            Assert.True(Comparer.Equals(DependencyGroupA, dependencyGroupA1));
        }

        [Fact]
        public void Equals_WhenOneArgumentIsNull_ReturnsFalse()
        {
            Assert.False(Comparer.Equals(DependencyGroupA, y: null));
            Assert.False(Comparer.Equals(x: null, DependencyGroupA));
        }

        [Fact]
        public void Equals_WhenArgumentsHaveUnequalTargetFramework_ReturnsFalse()
        {
            var dependencyGroupA1 = new PackageDependencyGroup(
                NuGetFramework.Parse("net45"),
                DependencyGroupA.Packages);

            Assert.False(Comparer.Equals(DependencyGroupA, dependencyGroupA1));
        }

        [Fact]
        public void Equals_WhenArgumentsHaveUnequalDependencies_ReturnsFalse()
        {
            var dependencyGroupA1 = new PackageDependencyGroup(
                DependencyGroupA.TargetFramework,
                new PackageDependency[] { new PackageDependency("b", new VersionRange(NuGetVersion.Parse("1.0.0"))) });

            Assert.False(Comparer.Equals(DependencyGroupA, dependencyGroupA1));
        }

        [Fact]
        public void GetHashCode_WhenArgumentIsNull_ReturnsZero()
        {
            int hashCode = Comparer.GetHashCode(obj: null);

            Assert.Equal(0, hashCode);
        }

        [Fact]
        public void GetHashCode_WhenArgumentIsValid_ReturnsHashCode()
        {
            var combiner = new HashCodeCombiner();

            combiner.AddObject(DependencyGroupA.TargetFramework.GetHashCode());
            combiner.AddObject(DependencyGroupA.Packages.First().GetHashCode());

            int expectedResult = combiner.CombinedHash;
            int actualResult = Comparer.GetHashCode(DependencyGroupA);

            Assert.Equal(expectedResult, actualResult);
        }
    }
}
