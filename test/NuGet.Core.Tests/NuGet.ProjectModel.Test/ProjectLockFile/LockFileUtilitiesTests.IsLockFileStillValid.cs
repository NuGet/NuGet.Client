// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using Xunit;
using static NuGet.Frameworks.FrameworkConstants;
using PackagesLockFileBuilder = NuGet.ProjectModel.Test.Builders.PackagesLockFileBuilder;

namespace NuGet.ProjectModel.Test.ProjectLockFile
{
    public partial class LockFileUtilitiesTests
    {
        [Fact]
        public void IsLockFileStillValid_DifferentVersions_AreNotEqual()
        {
            var x = new PackagesLockFileBuilder().Build();
            var y = new PackagesLockFileBuilder()
                .WithVersion(2)
                .Build();

            var actual = PackagesLockFileUtilities.IsLockFileStillValid(x, y);
            Assert.False(actual.IsValid);

            actual = PackagesLockFileUtilities.IsLockFileStillValid(y, x);
            Assert.False(actual.IsValid);
        }

        [Fact]
        public void IsLockFileStillValid_DifferentTargetCounts_AreNotEqual()
        {
            var x = new PackagesLockFileBuilder()
                .WithTarget(target => target.WithFramework(CommonFrameworks.NetStandard20))
                .WithTarget(target => target.WithFramework(CommonFrameworks.NetCoreApp22))
                .Build();
            var y = new PackagesLockFileBuilder()
                .WithTarget(target => target.WithFramework(CommonFrameworks.NetStandard20))
                .Build();

            var actual = PackagesLockFileUtilities.IsLockFileStillValid(x, y);
            Assert.False(actual.IsValid);

            actual = PackagesLockFileUtilities.IsLockFileStillValid(y, x);
            Assert.False(actual.IsValid);
        }

        [Fact]
        public void IsLockFileStillValid_DifferentTargets_AreNotEqual()
        {
            var x = new PackagesLockFileBuilder()
                .WithTarget(target => target.WithFramework(CommonFrameworks.NetStandard20))
                .Build();
            var y = new PackagesLockFileBuilder()
                .WithTarget(target => target.WithFramework(CommonFrameworks.NetCoreApp22))
                .Build();

            var actual = PackagesLockFileUtilities.IsLockFileStillValid(x, y);
            Assert.False(actual.IsValid);

            actual = PackagesLockFileUtilities.IsLockFileStillValid(y, x);
            Assert.False(actual.IsValid);
        }

        [Fact]
        public void IsLockFileStillValid_DifferentDependencyCounts_AreNotEqual()
        {
            var x = new PackagesLockFileBuilder()
                .WithTarget(target => target
                    .WithFramework(CommonFrameworks.NetStandard20)
                    .WithDependency(dep => dep.WithId("PackageA")))
                .Build();
            var y = new PackagesLockFileBuilder()
                .WithTarget(target => target
                    .WithFramework(CommonFrameworks.NetStandard20)
                    .WithDependency(dep => dep.WithId("PackageA"))
                    .WithDependency(dep => dep.WithId("PackageB")))
                .Build();

            var actual = PackagesLockFileUtilities.IsLockFileStillValid(x, y);
            Assert.False(actual.IsValid);

            actual = PackagesLockFileUtilities.IsLockFileStillValid(y, x);
            Assert.False(actual.IsValid);
        }

        [Fact]
        public void IsLockFileStillValid_DifferentDependency_AreNotEqual()
        {
            var x = new PackagesLockFileBuilder()
                .WithTarget(target => target
                    .WithFramework(CommonFrameworks.NetStandard20)
                    .WithDependency(dep => dep.WithId("PackageA")))
                .Build();
            var y = new PackagesLockFileBuilder()
                .WithTarget(target => target
                    .WithFramework(CommonFrameworks.NetStandard20)
                    .WithDependency(dep => dep.WithId("PackageB")))
                .Build();

            var actual = PackagesLockFileUtilities.IsLockFileStillValid(x, y);
            Assert.False(actual.IsValid);

            actual = PackagesLockFileUtilities.IsLockFileStillValid(y, x);
            Assert.False(actual.IsValid);
        }

        [Fact]
        public void IsLockFileStillValid_MatchesDependencies_AreEqual()
        {
            var x = new PackagesLockFileBuilder()
                .WithTarget(target => target
                    .WithFramework(CommonFrameworks.NetStandard20)
                    .WithDependency(dep => dep
                        .WithId("PackageA")
                        .WithContentHash("ABC"))
                    .WithDependency(dep => dep
                        .WithId("PackageB")
                        .WithContentHash("123")))
                .Build();
            var y = new PackagesLockFileBuilder()
                .WithTarget(target => target
                    .WithFramework(CommonFrameworks.NetStandard20)
                    .WithDependency(dep => dep
                        .WithId("PackageA")
                        .WithContentHash("XYZ"))
                    .WithDependency(dep => dep
                        .WithId("PackageB")
                        .WithContentHash("890")))
                .Build();

            var actual = PackagesLockFileUtilities.IsLockFileStillValid(x, y);
            Assert.True(actual.IsValid);
            Assert.NotNull(actual.MatchedDependencies);
            Assert.Equal(2, actual.MatchedDependencies.Count);
            var depKvp = actual.MatchedDependencies.Single(d => d.Key.Id == "PackageA");
            Assert.Equal("ABC", depKvp.Key.ContentHash);
            Assert.Equal("XYZ", depKvp.Value.ContentHash);
            depKvp = actual.MatchedDependencies.Single(d => d.Key.Id == "PackageB");
            Assert.Equal("123", depKvp.Key.ContentHash);
            Assert.Equal("890", depKvp.Value.ContentHash);

            actual = PackagesLockFileUtilities.IsLockFileStillValid(y, x);
            Assert.True(actual.IsValid);
            Assert.NotNull(actual.MatchedDependencies);
            Assert.Equal(2, actual.MatchedDependencies.Count);
            depKvp = actual.MatchedDependencies.Single(d => d.Key.Id == "PackageA");
            Assert.Equal("ABC", depKvp.Value.ContentHash);
            Assert.Equal("XYZ", depKvp.Key.ContentHash);
            depKvp = actual.MatchedDependencies.Single(d => d.Key.Id == "PackageB");
            Assert.Equal("123", depKvp.Value.ContentHash);
            Assert.Equal("890", depKvp.Key.ContentHash);
        }
    }
}
