// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Versioning;
using Xunit;
using Test.Utility;

namespace NuGet.DependencyResolver.Tests
{
    public class RemoteDependencyWalkerTests
    {
        [Fact]
        public async Task AllowPreleaseVersionNoConflict()
        {
            var context = new TestRemoteWalkContext();
            var provider = new DependencyProvider();
            provider.Package("A", "1.0-beta")
                    .DependsOn("B", "1.0")
                    .DependsOn("C", "1.0-beta")
                    .DependsOn("E", "1.0");

            provider.Package("B", "1.0")
                    .DependsOn("D", "1.0");

            provider.Package("C", "1.0-beta")
                    .DependsOn("D", "1.1-beta");

            provider.Package("E", "1.0")
                    .DependsOn("D", "0.1");

            context.LocalLibraryProviders.Add(provider);
            var walker = new RemoteDependencyWalker(context);
            var node = await DoWalkAsync(walker, "A");

            var result = node.Analyze();

            Assert.Equal(0, result.VersionConflicts.Count);
        }

        [Fact]
        public async Task CyclesAreDetected()
        {
            var context = new TestRemoteWalkContext();
            var provider = new DependencyProvider();
            provider.Package("A", "1.0")
                    .DependsOn("B", "2.0");

            provider.Package("B", "2.0")
                    .DependsOn("A", "1.0");

            context.LocalLibraryProviders.Add(provider);
            var walker = new RemoteDependencyWalker(context);
            var node = await DoWalkAsync(walker, "A");

            var result = node.Analyze();

            Assert.Equal(1, result.Cycles.Count);

            var cycle = result.Cycles[0];

            AssertPath(cycle, "A 1.0", "B 2.0", "A 1.0");
        }

        [Fact]
        public async Task CyclesAreDetectedIf2VersionsOfTheSamePackageId()
        {
            var context = new TestRemoteWalkContext();
            var provider = new DependencyProvider();
            provider.Package("A", "1.0")
                    .DependsOn("B", "2.0");

            provider.Package("B", "2.0")
                    .DependsOn("A", "5.0");

            context.LocalLibraryProviders.Add(provider);
            var walker = new RemoteDependencyWalker(context);
            var node = await DoWalkAsync(walker, "A");

            var result = node.Analyze();

            Assert.Equal(1, result.Cycles.Count);

            var cycle = result.Cycles[0];

            AssertPath(cycle, "A 1.0", "B 2.0", "A 5.0");
        }

        [Fact]
        public async Task DeepCycleCheck()
        {
            var context = new TestRemoteWalkContext();
            var provider = new DependencyProvider();
            provider.Package("A", "1.0")
                    .DependsOn("B", "2.0");

            provider.Package("B", "2.0")
                    .DependsOn("C", "2.0");

            provider.Package("C", "2.0")
                .DependsOn("D", "1.0")
                .DependsOn("E", "1.0");

            provider.Package("D", "1.0");
            provider.Package("E", "1.0")
                .DependsOn("A", "1.0");

            context.LocalLibraryProviders.Add(provider);
            var walker = new RemoteDependencyWalker(context);
            var node = await DoWalkAsync(walker, "A");

            var result = node.Analyze();

            Assert.Equal(1, result.Cycles.Count);

            var cycle = result.Cycles[0];

            AssertPath(cycle, "A 1.0", "B 2.0", "C 2.0", "E 1.0", "A 1.0");
        }

        [Fact]
        public async Task DependencyRangesButNoConflict()
        {
            var context = new TestRemoteWalkContext();
            var provider = new DependencyProvider();
            provider.Package("A", "1.0")
                    .DependsOn("B", "2.0")
                    .DependsOn("C", "2.0");

            provider.Package("B", "2.0")
                    .DependsOn("D", "2.0");

            provider.Package("C", "2.0")
                    .DependsOn("D", "1.0");

            provider.Package("D", "1.0")
                    .DependsOn("E", "[1.0]");

            provider.Package("D", "2.0")
                    .DependsOn("E", "[2.0]");

            provider.Package("E", "1.0");
            provider.Package("E", "2.0");

            context.LocalLibraryProviders.Add(provider);
            var walker = new RemoteDependencyWalker(context);
            var node = await DoWalkAsync(walker, "A");

            var result = node.Analyze();

            Assert.Equal(0, result.VersionConflicts.Count);
        }

        [Fact]
        public async Task AllowProjectOverridePackageNoConflict()
        {
            var context = new TestRemoteWalkContext();
            var packageProvider = new DependencyProvider();
            var projectProvider = new DependencyProvider();
            projectProvider.Package("A", "1.0", LibraryType.Project)
                    .DependsOn("B", "2.0", LibraryDependencyTarget.Project)
                    .DependsOn("C", "2.0");

            projectProvider.Package("B", "2.0", LibraryType.Project)
                    .DependsOn("D", "[2.0]", LibraryDependencyTarget.Project);

            packageProvider.Package("C", "2.0")
                    .DependsOn("D", "[1.0]");

            packageProvider.Package("D", "1.0");
            projectProvider.Package("D", "2.0", LibraryType.Project);

            context.LocalLibraryProviders.Add(packageProvider);
            context.ProjectLibraryProviders.Add(projectProvider);

            var walker = new RemoteDependencyWalker(context);
            var node = await DoWalkAsync(walker, "A");

            var result = node.Analyze();

            Assert.Equal(0, result.VersionConflicts.Count);
        }

        [Fact]
        public async Task SingleConflict()
        {
            var context = new TestRemoteWalkContext();
            var provider = new DependencyProvider();
            provider.Package("A", "1.0")
                    .DependsOn("B", "2.0")
                    .DependsOn("C", "2.0");

            provider.Package("B", "2.0")
                    .DependsOn("D", "[2.0]");

            provider.Package("C", "2.0")
                    .DependsOn("D", "[1.0]");

            provider.Package("D", "1.0");
            provider.Package("D", "2.0");

            context.LocalLibraryProviders.Add(provider);
            var walker = new RemoteDependencyWalker(context);
            var node = await DoWalkAsync(walker, "A");

            var result = node.Analyze();

            Assert.Equal(1, result.VersionConflicts.Count);

            var conflict = result.VersionConflicts[0];
            var c1 = conflict.Selected;
            var c2 = conflict.Conflicting;

            AssertPath(c1, "A 1.0", "B 2.0", "D 2.0");
            AssertPath(c2, "A 1.0", "C 2.0", "D 1.0");
        }

        [Fact]
        public async Task SingleConflictWhereConflictingDependenyIsUnresolved()
        {
            var context = new TestRemoteWalkContext();
            var provider = new DependencyProvider();
            provider.Package("A", "1.0")
                    .DependsOn("B", "2.0")
                    .DependsOn("C", "2.0");

            provider.Package("B", "2.0")
                    .DependsOn("D", "[2.0]");

            provider.Package("C", "2.0")
                    .DependsOn("D", "[1.0]");

            provider.Package("D", "2.0");

            context.LocalLibraryProviders.Add(provider);
            var walker = new RemoteDependencyWalker(context);
            var node = await DoWalkAsync(walker, "A");

            var result = node.Analyze();

            Assert.Equal(1, result.VersionConflicts.Count);

            var conflict = result.VersionConflicts[0];
            var c1 = conflict.Selected;
            var c2 = conflict.Conflicting;

            AssertPath(c1, "A 1.0", "B 2.0", "D 2.0");
            AssertPath(c2, "A 1.0", "C 2.0", "D 1.0");
        }

        [Fact]
        public async Task StrictDependenciesButNoConflict()
        {
            var context = new TestRemoteWalkContext();
            var provider = new DependencyProvider();
            provider.Package("A", "1.0")
                    .DependsOn("B", "2.0")
                    .DependsOn("C", "2.0");

            provider.Package("B", "2.0")
                    .DependsOn("D", "[2.0]");

            provider.Package("C", "2.0")
                    .DependsOn("D", "[1.0, 3.0]");

            provider.Package("D", "1.0");
            provider.Package("D", "2.0");

            context.LocalLibraryProviders.Add(provider);
            var walker = new RemoteDependencyWalker(context);
            var node = await DoWalkAsync(walker, "A");

            var result = node.Analyze();

            Assert.Equal(0, result.VersionConflicts.Count);
        }

        [Fact]
        public async Task ConflictAtDifferentLevel()
        {
            var context = new TestRemoteWalkContext();
            var provider = new DependencyProvider();
            provider.Package("A", "1.0")
                    .DependsOn("B", "2.0")
                    .DependsOn("C", "2.0")
                    .DependsOn("F", "2.0");

            provider.Package("B", "2.0")
                    .DependsOn("D", "2.0");

            provider.Package("C", "2.0")
                    .DependsOn("D", "1.0");

            provider.Package("D", "1.0")
                    .DependsOn("E", "[1.0]");

            provider.Package("D", "2.0")
                    .DependsOn("E", "[2.0]");

            provider.Package("F", "2.0")
                    .DependsOn("E", "[1.0]");

            provider.Package("E", "1.0");
            provider.Package("E", "2.0");

            context.LocalLibraryProviders.Add(provider);
            var walker = new RemoteDependencyWalker(context);
            var node = await DoWalkAsync(walker, "A");

            var result = node.Analyze();

            Assert.Equal(1, result.VersionConflicts.Count);

            var conflict = result.VersionConflicts[0];
            var c1 = conflict.Selected;
            var c2 = conflict.Conflicting;

            AssertPath(c1, "A 1.0", "B 2.0", "D 2.0", "E 2.0");
            AssertPath(c2, "A 1.0", "F 2.0", "E 1.0");
        }

        [Fact]
        public async Task TryResolveConflicts_ThrowsIfPackageConstraintCannotBeResolved()
        {
            var context = new TestRemoteWalkContext();
            var provider = new DependencyProvider();
            provider.Package("Root", "1.0")
                    .DependsOn("A", "1.0")
                    .DependsOn("B", "2.0");

            provider.Package("A", "1.0")
                    .DependsOn("C", "(1.0, 1.4]");

            provider.Package("B", "2.0")
                    .DependsOn("C", "1.8");

            provider.Package("C", "1.3.8");
            provider.Package("C", "1.8");

            context.LocalLibraryProviders.Add(provider);
            var walker = new RemoteDependencyWalker(context);
            var node = await DoWalkAsync(walker, "Root");

            var result = node.Analyze();

            Assert.Equal(1, result.VersionConflicts.Count);

            var conflict = result.VersionConflicts[0];
            var c1 = conflict.Selected;
            var c2 = conflict.Conflicting;

            AssertPath(c1, "Root 1.0", "B 2.0", "C 1.8");
            AssertPath(c2, "Root 1.0", "A 1.0", "C 1.3.8");
        }

        [Fact]
        public async Task TryResolveConflicts_WorksWhenVersionRangeIsNotSpecified()
        {
            var context = new TestRemoteWalkContext();
            var provider = new DependencyProvider();
            provider.Package("Root", "1.0")
                    .DependsOn("A", "1.0")
                    .DependsOn("B", "2.0");

            provider.Package("A", "1.0")
                    .DependsOn("C");

            provider.Package("B", "2.0")
                    .DependsOn("C", "1.8");

            provider.Package("C", "1.8");
            provider.Package("C", "2.0");

            context.LocalLibraryProviders.Add(provider);
            var walker = new RemoteDependencyWalker(context);
            var node = await DoWalkAsync(walker, "Root");

            // Restore doesn't actually support null versions so fake a resolved dependency
            var cNode = node.Path("A", "C");
            cNode.Key.TypeConstraint = LibraryDependencyTarget.Package;
            cNode.Item = new GraphItem<RemoteResolveResult>(new LibraryIdentity
            {
                Name = "C",
                Version = new NuGetVersion("2.0")
            });

            var result = node.Analyze();

            Assert.Empty(result.VersionConflicts);

            Assert.Equal(Disposition.Accepted, cNode.Disposition);
            Assert.Equal(Disposition.Rejected, node.Path("B", "C").Disposition);
        }

        [Fact]
        public async Task NearestWinsOverridesStrictDependency()
        {
            var context = new TestRemoteWalkContext();
            var provider = new DependencyProvider();
            provider.Package("A", "1.0")
                    .DependsOn("B", "2.0")
                    .DependsOn("C", "2.0")
                    .DependsOn("D", "3.0");

            provider.Package("B", "2.0")
                    .DependsOn("D", "[2.0]");

            provider.Package("C", "2.0")
                    .DependsOn("D", "[1.0]");

            provider.Package("D", "1.0");
            provider.Package("D", "2.0");
            provider.Package("D", "3.0");

            context.LocalLibraryProviders.Add(provider);
            var walker = new RemoteDependencyWalker(context);
            var node = await DoWalkAsync(walker, "A");

            var result = node.Analyze();

            Assert.Equal(0, result.VersionConflicts.Count);
            Assert.Equal(0, result.Downgrades.Count);
        }

        [Fact]
        public async Task NearestWinsOverridesStrictDependencyButDowngradeWarns()
        {
            var context = new TestRemoteWalkContext();
            var provider = new DependencyProvider();
            provider.Package("A", "1.0")
                    .DependsOn("B", "2.0")
                    .DependsOn("C", "2.0")
                    .DependsOn("D", "1.0");

            provider.Package("B", "2.0")
                    .DependsOn("D", "[2.0]");

            provider.Package("C", "2.0")
                    .DependsOn("D", "[1.0]");

            provider.Package("D", "1.0");
            provider.Package("D", "2.0");
            provider.Package("D", "3.0");

            context.LocalLibraryProviders.Add(provider);
            var walker = new RemoteDependencyWalker(context);
            var node = await DoWalkAsync(walker, "A");

            var result = node.Analyze();

            Assert.Equal(0, result.VersionConflicts.Count);
            Assert.Equal(1, result.Downgrades.Count);

            var downgraded = result.Downgrades[0].DowngradedFrom;
            var downgradedBy = result.Downgrades[0].DowngradedTo;
            AssertPath(downgraded, "A 1.0", "B 2.0", "D 2.0");
            AssertPath(downgradedBy, "A 1.0", "D 1.0");
        }

        [Fact]
        public async Task DowngradeSkippedIfEqual()
        {
            var context = new TestRemoteWalkContext();
            var provider = new DependencyProvider();
            provider.Package("A", "1.0")
                    .DependsOn("B", "2.0")
                    .DependsOn("C", "2.0");

            provider.Package("B", "2.0")
                    .DependsOn("C", "2.0");

            provider.Package("C", "2.0");

            context.LocalLibraryProviders.Add(provider);
            var walker = new RemoteDependencyWalker(context);
            var node = await DoWalkAsync(walker, "A");

            var result = node.Analyze();

            Assert.Equal(0, result.Downgrades.Count);
        }

        [Fact]
        public async Task DowngradeAtRootIsDetected()
        {
            var context = new TestRemoteWalkContext();
            var provider = new DependencyProvider();
            provider.Package("A", "1.0")
                    .DependsOn("B", "2.0")
                    .DependsOn("C", "1.0");

            provider.Package("B", "2.0")
                    .DependsOn("C", "2.0");

            provider.Package("C", "1.0");
            provider.Package("C", "2.0");

            context.LocalLibraryProviders.Add(provider);
            var walker = new RemoteDependencyWalker(context);
            var node = await DoWalkAsync(walker, "A");

            var result = node.Analyze();

            Assert.Equal(1, result.Downgrades.Count);
            var downgraded = result.Downgrades[0].DowngradedFrom;
            var downgradedBy = result.Downgrades[0].DowngradedTo;

            AssertPath(downgraded, "A 1.0", "B 2.0", "C 2.0");
            AssertPath(downgradedBy, "A 1.0", "C 1.0");
        }

        [Fact]
        public async Task DowngradeNotAtRootIsDetected()
        {
            var context = new TestRemoteWalkContext();
            var provider = new DependencyProvider();
            provider.Package("A", "1.0")
                    .DependsOn("B", "2.0");

            provider.Package("B", "2.0")
                    .DependsOn("C", "2.0")
                    .DependsOn("D", "1.0");

            provider.Package("C", "2.0")
                    .DependsOn("D", "2.0");

            provider.Package("D", "1.0");

            context.LocalLibraryProviders.Add(provider);
            var walker = new RemoteDependencyWalker(context);
            var node = await DoWalkAsync(walker, "A");

            var result = node.Analyze();

            Assert.Equal(1, result.Downgrades.Count);
            var downgraded = result.Downgrades[0].DowngradedFrom;
            var downgradedBy = result.Downgrades[0].DowngradedTo;

            AssertPath(downgraded, "A 1.0", "B 2.0", "C 2.0", "D 2.0");
            AssertPath(downgradedBy, "A 1.0", "B 2.0", "D 1.0");
        }

        [Fact]
        public async Task DowngradeOverddienByCousinCheck()
        {
            var context = new TestRemoteWalkContext();
            var provider = new DependencyProvider();
            provider.Package("A", "1.0")
                    .DependsOn("B", "1.0")
                    .DependsOn("C", "1.0");

            provider.Package("B", "1.0")
                    .DependsOn("E", "1.0")
                    .DependsOn("D", "1.0");

            provider.Package("E", "1.0")
                    .DependsOn("D", "2.0");

            provider.Package("C", "1.0")
                    .DependsOn("D", "3.0");

            provider.Package("D", "1.0");
            provider.Package("D", "2.0");
            provider.Package("D", "3.0");

            context.LocalLibraryProviders.Add(provider);
            var walker = new RemoteDependencyWalker(context);
            var node = await DoWalkAsync(walker, "A");

            var result = node.Analyze();

            Assert.Equal(0, result.Downgrades.Count);
        }

        [Fact]
        public async Task PotentialDowngradeThenUpgrade()
        {
            var context = new TestRemoteWalkContext();
            var provider = new DependencyProvider();
            provider.Package("A", "1.0")
                    .DependsOn("B", "1.2")
                    .DependsOn("C", "1.0");

            provider.Package("B", "1.2");

            provider.Package("C", "1.0")
                    .DependsOn("B", "0.8")
                    .DependsOn("D", "1.0");

            provider.Package("B", "0.8");

            provider.Package("D", "1.0")
                    .DependsOn("B", "1.0");

            provider.Package("B", "1.0");

            context.LocalLibraryProviders.Add(provider);
            var walker = new RemoteDependencyWalker(context);
            var node = await DoWalkAsync(walker, "A");

            var result = node.Analyze();

            Assert.Equal(0, result.Downgrades.Count);
        }

        [Fact]
        public async Task DowngradeThenUpgradeThenDowngrade()
        {
            var context = new TestRemoteWalkContext();
            var provider = new DependencyProvider();
            provider.Package("A", "1.0")
                    .DependsOn("B", "1.0")
                    .DependsOn("C", "1.0");

            provider.Package("B", "1.0");

            provider.Package("C", "1.0")
                    .DependsOn("B", "2.0")
                    .DependsOn("D", "1.0");

            provider.Package("B", "2.0");

            provider.Package("D", "1.0")
                    .DependsOn("B", "1.0");

            context.LocalLibraryProviders.Add(provider);
            var walker = new RemoteDependencyWalker(context);
            var node = await DoWalkAsync(walker, "A");

            var result = node.Analyze();

            Assert.Equal(1, result.Downgrades.Count);
            var downgraded = result.Downgrades[0].DowngradedFrom;
            var downgradedBy = result.Downgrades[0].DowngradedTo;

            AssertPath(downgraded, "A 1.0", "C 1.0", "B 2.0");
            AssertPath(downgradedBy, "A 1.0", "B 1.0");
        }

        [Fact]
        public async Task UpgradeThenDowngradeThenEqual()
        {
            var context = new TestRemoteWalkContext();
            var provider = new DependencyProvider();
            provider.Package("A", "1.0")
                    .DependsOn("B", "2.0")
                    .DependsOn("C", "1.0");

            provider.Package("B", "1.0");

            provider.Package("C", "1.0")
                    .DependsOn("B", "1.0")
                    .DependsOn("D", "1.0");

            provider.Package("B", "2.0");

            provider.Package("D", "1.0")
                    .DependsOn("B", "2.0");

            context.LocalLibraryProviders.Add(provider);
            var walker = new RemoteDependencyWalker(context);
            var node = await DoWalkAsync(walker, "A");

            var result = node.Analyze();

            Assert.Equal(0, result.Downgrades.Count);
        }

        [Fact]
        public async Task DoubleDowngrade()
        {
            var context = new TestRemoteWalkContext();
            var provider = new DependencyProvider();
            provider.Package("A", "1.0")
                    .DependsOn("B", "0.7")
                    .DependsOn("C", "1.0");

            provider.Package("B", "0.7");

            provider.Package("C", "1.0")
                    .DependsOn("B", "0.8")
                    .DependsOn("D", "1.0");

            provider.Package("B", "0.8");

            provider.Package("D", "1.0")
                    .DependsOn("B", "1.0");

            provider.Package("B", "1.0");

            context.LocalLibraryProviders.Add(provider);
            var walker = new RemoteDependencyWalker(context);
            var node = await DoWalkAsync(walker, "A");

            var downgrades = new List<Tuple<GraphNode<RemoteResolveResult>, GraphNode<RemoteResolveResult>>>();
            var cycles = new List<GraphNode<RemoteResolveResult>>();

            var result = node.Analyze();

            Assert.Equal(2, result.Downgrades.Count);


            var d0 = result.Downgrades[0];
            var d0To = d0.DowngradedFrom;
            var d0By = d0.DowngradedTo;

            AssertPath(d0To, "A 1.0", "C 1.0", "B 0.8");
            AssertPath(d0By, "A 1.0", "B 0.7");

            var d1 = result.Downgrades[1];
            var d1To = d1.DowngradedFrom;
            var d1By = d1.DowngradedTo;

            AssertPath(d1To, "A 1.0", "C 1.0", "D 1.0", "B 1.0");
            AssertPath(d1By, "A 1.0", "B 0.7");

        }

        [Fact]
        public void IsGreaterThanEqualTo_ReturnsTrue_IfLeftVersionIsUnbound()
        {
            // Arrange
            var leftVersion = VersionRange.All;
            var rightVersion = VersionRange.Parse("1.0.0");

            // Act
            var isGreater = RemoteDependencyWalker.IsGreaterThanOrEqualTo(leftVersion, rightVersion);

            // Assert
            Assert.True(isGreater);
        }

        [Fact]
        public void IsGreaterThanEqualTo_ReturnsFalse_IfRightVersionIsUnbound()
        {
            // Arrange
            var leftVersion = VersionRange.Parse("3.1.0-*");
            var rightVersion = VersionRange.All;

            // Act
            var isGreater = RemoteDependencyWalker.IsGreaterThanOrEqualTo(leftVersion, rightVersion);

            // Assert
            Assert.False(isGreater);
        }

        [Theory]
        [InlineData("3.0", "3.0")]
        [InlineData("3.0", "3.0.0")]
        [InlineData("3.1", "3.0.0")]
        [InlineData("3.1.2", "3.1.1")]
        [InlineData("3.1.2-beta", "3.1.2-alpha")]
        [InlineData("[3.1.2-beta, 4.0)", "[3.1.1, 4.3)")]
        [InlineData("[3.1.2-*, 4.0)", "3.1.2-alpha-1002")]
        [InlineData("3.1.2-prerelease", "3.1.2-alpha-*")]
        [InlineData("3.1.2-beta-*", "3.1.2-alpha-*")]
        [InlineData("3.1.*", "3.1.2-alpha-*")]
        [InlineData("*", "3.1.2-alpha-*")]
        [InlineData("*", "*")]
        [InlineData("1.*", "1.1.*")]
        [InlineData("1.*", "1.3.*")]
        [InlineData("1.8.*", "1.8.3.*")]
        [InlineData("1.8.3.5*", "1.8.3.4-*")]
        [InlineData("1.8.3.*", "1.8.3.4-*")]
        [InlineData("1.8.3.4-alphabeta-*", "1.8.3.4-alpha*")]
        [InlineData("1.8.5.4-alpha-*", "1.8.3.4-gamma*")]
        [InlineData("1.8.3.6-alpha-*", "1.8.3.4-gamma*")]
        [InlineData("1.8.3-*", "1.8.3-alpha*")]
        [InlineData("1.8.3-*", "1.8.3-*")]
        [InlineData("1.8.4-*", "1.8.3-*")]
        [InlineData("2.8.1-*", "1.8.3-*")]
        [InlineData("3.2.0-*", "3.1.0-beta-234")]
        [InlineData("3.*", "3.1.*")]
        public void IsGreaterThanEqualTo_ReturnsTrue_IfRightVersionIsSmallerThanLeft(string leftVersionString, string rightVersionString)
        {
            // Arrange
            var leftVersion = VersionRange.Parse(leftVersionString);
            var rightVersion = VersionRange.Parse(rightVersionString);

            // Act
            var isGreater = RemoteDependencyWalker.IsGreaterThanOrEqualTo(leftVersion, rightVersion);

            // Assert
            Assert.True(isGreater);
        }

        [Theory]
        [InlineData("1.3.4", "1.4.3")]
        [InlineData("3.0", "3.1")]
        [InlineData("3.0", "*")]
        [InlineData("3.0-*", "*")]
        [InlineData("3.2.4", "3.2.7")]
        [InlineData("3.2.4-alpha", "[3.2.4-beta, 4.0)")]
        [InlineData("2.2.4-alpha", "2.2.4-beta-*")]
        [InlineData("2.2.4-beta-1", "2.2.4-beta1*")]
        [InlineData("2.2.1.*", "2.3.*")]
        [InlineData("2.*", "3.1.*")]
        [InlineData("3.4.6.*", "3.6.*")]
        [InlineData("3.4.6-alpha*", "3.4.6-beta*")]
        [InlineData("3.4.6-beta*", "3.4.6-betb*")]
        [InlineData("3.1.0-beta-234", "3.2.0-*")]
        [InlineData("3.0.0-*", "3.1.0-beta-234")]
        public void IsGreaterThanEqualTo_ReturnsFalse_IfRightVersionIsLargerThanLeft(string leftVersionString, string rightVersionString)
        {
            // Arrange
            var leftVersion = VersionRange.Parse(leftVersionString);
            var rightVersion = VersionRange.Parse(rightVersionString);

            // Act
            var isGreater = RemoteDependencyWalker.IsGreaterThanOrEqualTo(leftVersion, rightVersion);

            // Assert
            Assert.False(isGreater);
        }

        [Fact]
        public void TransitiveCentralPackageVersions_AddAndTake()
        {
            // Arrange
            var transitiveCentralPackageVersions = new RemoteDependencyWalker.TransitiveCentralPackageVersions();
            var centralPackageVersionDependency = new LibraryDependency()
            {
                LibraryRange = new LibraryRange("name1", VersionRange.Parse("1.0.0"), LibraryDependencyTarget.Package),
            };

            bool resultAdd = transitiveCentralPackageVersions.TryAdd(centralPackageVersionDependency);
            bool resultTake1 = transitiveCentralPackageVersions.TryTake(out LibraryDependency centralPackageVersionTake1);
            // nothing more to take 
            bool resultTake2 = transitiveCentralPackageVersions.TryTake(out LibraryDependency centralPackageVersionTake2);

            // Assert
            Assert.True(resultAdd);
            Assert.True(resultTake1);
            Assert.False(resultTake2);

            Assert.Equal(centralPackageVersionDependency, centralPackageVersionTake1);
            Assert.Null(centralPackageVersionTake2);
        }

        [Fact]
        public void TransitiveCentralPackageVersions_TryAdd_DuplicatesAreIgnored()
        {
            // Arrange
            var transitiveCentralPackageVersions = new RemoteDependencyWalker.TransitiveCentralPackageVersions();
            var centralPackageVersionDependency = new LibraryDependency()
            {
                LibraryRange = new LibraryRange("name1", VersionRange.Parse("1.0.0"), LibraryDependencyTarget.Package),
            };
            bool resultAdd1 = transitiveCentralPackageVersions.TryAdd(centralPackageVersionDependency);
            bool resultAdd2 = transitiveCentralPackageVersions.TryAdd(centralPackageVersionDependency);

            // Assert
            Assert.True(resultAdd1);
            Assert.False(resultAdd2);

            // Once the data is added it cannot be re-added even if after TryTake 
            bool resultTake1 = transitiveCentralPackageVersions.TryTake(out LibraryDependency centralPackageVersionTake1);
            bool resultAdd3 = transitiveCentralPackageVersions.TryAdd(centralPackageVersionDependency);

            Assert.True(resultTake1);
            Assert.False(resultAdd3);
        }

        [Theory]
        [InlineData("2.0.0", "2.0.0")]
        [InlineData("2.0.0", "1.0.0")]

        public async Task WalkAsyncAddsTransitiveCentralDependency(string centralPackageVersion, string otherVersion)
        {
            var centralPackageName = "D";
            var framework = NuGetFramework.Parse("net45");

            var context = new TestRemoteWalkContext();
            var provider = new DependencyProvider();
            // D is a transitive dependency for package A through package B -> C -> D
            // D is defined as a Central Package Version
            // In this context Package D with version centralPackageVersion will be added as inner node of Node A, next to B 

            // Input 
            // A -> B (version = otherVersion) -> C (version = otherVersion) -> D (version = otherVersion)
            // A ~> D (the version 2.0.0 or as defined by "centralPackageVersion" argument
            //         the dependency is not direct,
            //         it simulates the fact that there is a centrally defined "D" package
            //         the information is added to the provider)

            // The expected output graph
            //    -> B (version = otherVersion) -> C (version = otherVersion)
            // A
            //    -> D (version = 2.0.0)

            provider.Package("A", otherVersion)
                    .DependsOn("B", otherVersion);

            provider.Package("B", otherVersion)
                   .DependsOn("C", otherVersion);

            provider.Package("C", otherVersion)
                  .DependsOn(centralPackageName, otherVersion);

            // Simulates the existence of a D centrally defined package that is not direct dependency
            provider.Package("A", otherVersion)
                     .DependsOn(centralPackageName, centralPackageVersion, LibraryDependencyTarget.Package, versionCentrallyManaged: true, libraryDependencyReferenceType: LibraryDependencyReferenceType.None);

            // Add central package to the source with multiple versions
            provider.Package(centralPackageName, "1.0.0");
            provider.Package(centralPackageName, centralPackageVersion);
            provider.Package(centralPackageName, "3.0.0");

            context.LocalLibraryProviders.Add(provider);
            var walker = new RemoteDependencyWalker(context);

            // Act
            var rootNode = await DoWalkAsync(walker, "A", framework);

            // Assert
            Assert.Equal(2, rootNode.InnerNodes.Count);
            var centralVersionInGraphNode = rootNode.InnerNodes.Where(n => n.Item.Key.Name == centralPackageName).FirstOrDefault();
            Assert.NotNull(centralVersionInGraphNode);
            Assert.Equal(centralPackageVersion, centralVersionInGraphNode.Item.Key.Version.ToNormalizedString());
            Assert.True(centralVersionInGraphNode.Item.IsCentralTransitive);

            var BNode = rootNode.InnerNodes.Where(n => n.Item.Key.Name == "B").FirstOrDefault();
            Assert.NotNull(BNode);
            Assert.Equal(1, BNode.InnerNodes.Count);
            Assert.Equal(otherVersion, BNode.Item.Key.Version.ToNormalizedString());
            Assert.False(BNode.Item.IsCentralTransitive);

            var CNode = BNode.InnerNodes.Where(n => n.Item.Key.Name == "C").FirstOrDefault();
            Assert.NotNull(CNode);
            Assert.Equal(otherVersion, CNode.Item.Key.Version.ToNormalizedString());
            Assert.Equal(0, CNode.InnerNodes.Count);
            Assert.False(CNode.Item.IsCentralTransitive);
        }

        [Fact]
        public async Task WalkAsyncDowngradesBecauseOfCentralDependecy()
        {
            var centralPackageName = "D";
            var centralPackageVersion = "2.0.0";
            var otherVersion = "3.0.0";
            var framework = NuGetFramework.Parse("net45");

            var context = new TestRemoteWalkContext();
            var provider = new DependencyProvider();
            // D is a transitive dependency for package A through package B -> C -> D
            // D is defined as a Central Package Version
            // In this context Package D with version centralPackageVersion will be added as inner node of Node A, next to B 

            // Picture:
            // A -> B -> C -> D (version 1.0.0 or 2.0.0 as defined by "otherVersion" argument.
            // A ~> D (the version 2.0.0 or as defined by "centralPackageVersion" argument
            //         the dependency is not direct,
            //         it simulates the fact that there is a centrally defined "D" package
            //         the information is added to the provider)

            provider.Package("A", otherVersion)
                    .DependsOn("B", otherVersion);

            provider.Package("B", otherVersion)
                   .DependsOn("C", otherVersion);

            provider.Package("C", otherVersion)
                  .DependsOn(centralPackageName, otherVersion);

            // Simulates the existence of a D centrally defined package that is not direct dependency
            provider.Package("A", otherVersion)
                     .DependsOn(centralPackageName, centralPackageVersion, LibraryDependencyTarget.Package, versionCentrallyManaged: true);

            // Add central package to the source with multiple versions
            provider.Package(centralPackageName, "1.0.0");
            provider.Package(centralPackageName, centralPackageVersion);
            provider.Package(centralPackageName, "3.0.0");

            context.LocalLibraryProviders.Add(provider);
            var walker = new RemoteDependencyWalker(context);

            // Act
            var rootNode = await DoWalkAsync(walker, "A", framework);

            var analyzeResult = rootNode.Analyze();

            // Assert
            Assert.Equal(1, analyzeResult.Downgrades.Count);
            var downgrade = analyzeResult.Downgrades.First();
            Assert.Equal(centralPackageName, downgrade.DowngradedFrom.Key.Name);
            Assert.Equal("[3.0.0, )", downgrade.DowngradedFrom.Key.VersionRange.ToNormalizedString());
            Assert.Equal(centralPackageName, downgrade.DowngradedTo.Key.Name);
            Assert.Equal("[2.0.0, )", downgrade.DowngradedTo.Key.VersionRange.ToNormalizedString());
        }

        [Theory]
        [InlineData(LibraryDependencyReferenceType.Direct, true)]
        [InlineData(LibraryDependencyReferenceType.Transitive, true)]
        [InlineData(LibraryDependencyReferenceType.None, true)]
        [InlineData(LibraryDependencyReferenceType.Direct, false)]
        [InlineData(LibraryDependencyReferenceType.Transitive, false)]
        [InlineData(LibraryDependencyReferenceType.None, false)]
        public void IsDependencyValidForGraphTest(LibraryDependencyReferenceType referenceType, bool versionCentrallyManaged)
        {
            var centralPackageName = "D";
            var context = new TestRemoteWalkContext();
            var centralPackageVersion = new CentralPackageVersion(centralPackageName, VersionRange.Parse("2.0.0"));
            var centralPackageVersionDependecy_VersionCentrallyManaged = new LibraryDependency()
            {
                LibraryRange = new LibraryRange(centralPackageVersion.Name, centralPackageVersion.VersionRange, LibraryDependencyTarget.Package),
                VersionCentrallyManaged = versionCentrallyManaged,
                ReferenceType = referenceType,
            };
            var walker = new RemoteDependencyWalker(context);

            // Act
            var expectedResult = walker.IsDependencyValidForGraph(centralPackageVersionDependecy_VersionCentrallyManaged);

            // Assert
            if (referenceType != LibraryDependencyReferenceType.None)
            {
                Assert.True(expectedResult);
            }
            else
            {
                Assert.False(expectedResult);
            }
        }

        private void AssertPath<TItem>(GraphNode<TItem> node, params string[] items)
        {
            var matches = new List<string>();

            while (node != null)
            {
                matches.Insert(0, $"{node.Key.Name} {node.Item?.Key?.Version ?? node.Key.VersionRange.MinVersion}");
                node = node.OuterNode;
            }

            Assert.Equal(matches, items);
        }

        private Task<GraphNode<RemoteResolveResult>> DoWalkAsync(RemoteDependencyWalker walker, string name)
        {
            return DoWalkAsync(walker, name, NuGetFramework.Parse("net45"));
        }

        private Task<GraphNode<RemoteResolveResult>> DoWalkAsync(RemoteDependencyWalker walker, string name, NuGetFramework framework)
        {
            var range = new LibraryRange
            {
                Name = name,
                VersionRange = new VersionRange(new NuGetVersion("1.0"))
            };

            return walker.WalkAsync(range, framework, runtimeIdentifier: null, runtimeGraph: null, recursive: true);

        }
    }
}
