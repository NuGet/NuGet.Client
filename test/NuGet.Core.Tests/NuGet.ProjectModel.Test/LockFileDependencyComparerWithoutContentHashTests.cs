// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.ProjectModel.ProjectLockFile;
using NuGet.ProjectModel.Test.Builders;
using NuGet.Versioning;
using Xunit;

namespace NuGet.ProjectModel.Test
{
    public class LockFileDependencyComparerWithoutContentHashTests
    {
        [Fact]
        public void NullReferencesAreEqual()
        {
            LockFileDependency x = null;
            LockFileDependency y = null;

            var actual = LockFileDependencyComparerWithoutContentHash.Default.Equals(x, y);

            Assert.True(actual);
        }

        [Fact]
        public void OneNullReferenceIsNotEqual()
        {
            LockFileDependency isNull = null;
            var notNull = new LockFileDependencyBuilder().Build();

            var actual = LockFileDependencyComparerWithoutContentHash.Default.Equals(isNull, notNull);
            Assert.False(actual);

            actual = LockFileDependencyComparerWithoutContentHash.Default.Equals(notNull, isNull);
            Assert.False(actual);
        }

        [Fact]
        public void SameReferenceIsEqual()
        {
            var dep = new LockFileDependencyBuilder().Build();

            var actual = LockFileDependencyComparerWithoutContentHash.Default.Equals(dep, dep);

            Assert.True(actual);
        }

        [Fact]
        public void DependenciesWithSameValuesAreEqual()
        {
            var x = new LockFileDependencyBuilder().Build();
            var y = new LockFileDependencyBuilder().Build();

            var actual = LockFileDependencyComparerWithoutContentHash.Default.Equals(x, y);
            Assert.True(actual);

            actual = LockFileDependencyComparerWithoutContentHash.Default.Equals(y, x);
            Assert.True(actual);
        }

        [Fact]
        public void DependenciesWithDifferentContentHashValuesAreEqual()
        {
            var x = new LockFileDependencyBuilder().Build();
            var y = new LockFileDependencyBuilder()
                .WithContentHash("XYZ")
                .Build();

            Assert.NotEqual(x.ContentHash, y.ContentHash);

            var actual = LockFileDependencyComparerWithoutContentHash.Default.Equals(x, y);
            Assert.True(actual);

            actual = LockFileDependencyComparerWithoutContentHash.Default.Equals(y, x);
            Assert.True(actual);
        }

        [Fact]
        public void DependenciesWithDifferentIdsAreNotEqual()
        {
            var x = new LockFileDependencyBuilder().Build();
            var y = new LockFileDependencyBuilder()
                .WithId("PackageB")
                .Build();

            Assert.NotEqual(x.Id, y.Id);

            var actual = LockFileDependencyComparerWithoutContentHash.Default.Equals(x, y);
            Assert.False(actual);

            actual = LockFileDependencyComparerWithoutContentHash.Default.Equals(y, x);
            Assert.False(actual);
        }

        [Fact]
        public void DependenciesWithDifferentResolvedVersionsAreNotEqual()
        {
            var x = new LockFileDependencyBuilder().Build();
            var y = new LockFileDependencyBuilder()
                .WithResolvedVersion(new NuGetVersion(1, 0, 1))
                .Build();

            Assert.NotEqual(x.ResolvedVersion, y.ResolvedVersion);

            var actual = LockFileDependencyComparerWithoutContentHash.Default.Equals(x, y);
            Assert.False(actual);

            actual = LockFileDependencyComparerWithoutContentHash.Default.Equals(y, x);
            Assert.False(actual);
        }

        [Fact]
        public void DependenciesWithDifferentRequestedRangesAreNotEqual()
        {
            var x = new LockFileDependencyBuilder().Build();
            var y = new LockFileDependencyBuilder()
                .WithRequestedVersion(new VersionRange(new NuGetVersion(1, 0, 1)))
                .Build();

            Assert.NotEqual(x.RequestedVersion, y.RequestedVersion);

            var actual = LockFileDependencyComparerWithoutContentHash.Default.Equals(x, y);
            Assert.False(actual);

            actual = LockFileDependencyComparerWithoutContentHash.Default.Equals(y, x);
            Assert.False(actual);
        }

        [Fact]
        public void DependenciesWithDifferentTypesAreNotEqual()
        {
            var x = new LockFileDependencyBuilder().Build();
            var y = new LockFileDependencyBuilder()
                .WithType(PackageDependencyType.Transitive)
                .Build();

            Assert.NotEqual(x.Type, y.Type);

            var actual = LockFileDependencyComparerWithoutContentHash.Default.Equals(x, y);
            Assert.False(actual);

            actual = LockFileDependencyComparerWithoutContentHash.Default.Equals(y, x);
            Assert.False(actual);
        }
    }
}
