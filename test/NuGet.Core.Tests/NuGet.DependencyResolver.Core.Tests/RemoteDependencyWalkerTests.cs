// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Xunit;

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
            var range = new LibraryRange
            {
                Name = name,
                VersionRange = new VersionRange(new NuGetVersion("1.0"))
            };

            return walker.WalkAsync(range, NuGetFramework.Parse("net45"), runtimeIdentifier: null, runtimeGraph: null, recursive: true);

        }

        private class DependencyProvider : IRemoteDependencyProvider
        {
            private readonly Dictionary<LibraryIdentity, List<LibraryDependency>> _graph = new Dictionary<LibraryIdentity, List<LibraryDependency>>();

            public bool IsHttp
            {
                get
                {
                    return false;
                }
            }

            public PackageSource Source => new PackageSource("Test");

            public Task CopyToAsync(
                LibraryIdentity match,
                Stream stream,
                SourceCacheContext cacheContext,
                ILogger logger,
                CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task<LibraryIdentity> FindLibraryAsync(
                LibraryRange libraryRange,
                NuGetFramework targetFramework,
                SourceCacheContext cacheContext,
                ILogger logger,
                CancellationToken cancellationToken)
            {
                var packages = _graph.Keys.Where(p => p.Name == libraryRange.Name);

                return Task.FromResult(packages.FindBestMatch(libraryRange.VersionRange, i => i?.Version));
            }

            public Task<LibraryDependencyInfo> GetDependenciesAsync(
                LibraryIdentity match,
                NuGetFramework targetFramework,
                SourceCacheContext cacheContext,
                ILogger logger,
                CancellationToken cancellationToken)
            {
                List<LibraryDependency> dependencies;
                if (_graph.TryGetValue(match, out dependencies))
                {
                    return Task.FromResult(LibraryDependencyInfo.Create(match, targetFramework, dependencies));
                }

                return Task.FromResult(LibraryDependencyInfo.Create(match, targetFramework, Enumerable.Empty<LibraryDependency>()));
            }

            public TestPackage Package(string id, string version)
            {
                return Package(id, NuGetVersion.Parse(version));
            }

            public TestPackage Package(string id, NuGetVersion version)
            {
                var libraryIdentity = new LibraryIdentity { Name = id, Version = version, Type = LibraryType.Package };

                List<LibraryDependency> dependencies;
                if (!_graph.TryGetValue(libraryIdentity, out dependencies))
                {
                    dependencies = new List<LibraryDependency>();
                    _graph[libraryIdentity] = dependencies;
                }

                return new TestPackage(dependencies);
            }

            public class TestPackage
            {
                private List<LibraryDependency> _dependencies;

                public TestPackage(List<LibraryDependency> dependencies)
                {
                    _dependencies = dependencies;
                }

                public TestPackage DependsOn(string id, LibraryDependencyTarget target = LibraryDependencyTarget.All)
                {
                    _dependencies.Add(new LibraryDependency
                    {
                        LibraryRange = new LibraryRange
                        {
                            Name = id,
                            TypeConstraint = target
                        }
                    });

                    return this;
                }

                public TestPackage DependsOn(string id, string version, LibraryDependencyTarget target = LibraryDependencyTarget.All)
                {
                    _dependencies.Add(new LibraryDependency
                    {
                        LibraryRange = new LibraryRange
                        {
                            Name = id,
                            VersionRange = VersionRange.Parse(version),
                            TypeConstraint = target
                        }
                    });

                    return this;
                }
            }
        }
    }
}
