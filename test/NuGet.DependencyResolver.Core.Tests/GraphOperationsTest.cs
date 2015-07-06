// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.LibraryModel;
using NuGet.Versioning;
using Xunit;

namespace NuGet.DependencyResolver
{
    public class GraphOperationsTest
    {
        [Fact]
        public void TryResolveConflicts_ThrowsIfPackageConstraintCannotBeResolved()
        {
            // Arrange
            var root = CreateNode("Root", "1.0.0");
            var nodeA = CreateNode("A", "1.0.0");
            nodeA.InnerNodes.Add(CreateNode("C", "(1.0.0,1.4.0]", "1.3.8"));
            root.InnerNodes.Add(nodeA);

            var nodeB = CreateNode("B", "2.0.0");
            nodeB.InnerNodes.Add(CreateNode("C", "1.8.0"));
            root.InnerNodes.Add(nodeB);

            // Act and Assert
            var ex = Assert.Throws<InvalidOperationException>(() => root.TryResolveConflicts());
            Assert.Equal("Unable to find a version of package 'C' that is compatible with version constraint '(1.0.0, 1.4.0]'.", ex.Message);
        }

        [Fact]
        public void TryResolveConflicts_ResolvesConflictsAmongstOverlappingSiblings()
        {
            // Arrange
            var root = CreateNode("Root", "1.0.0");
            var nodeA = CreateNode("A", "1.0.0");
            var nodeC138 = CreateNode("C", "1.0.0", "1.3.8");
            nodeA.InnerNodes.Add(nodeC138);
            root.InnerNodes.Add(nodeA);

            var nodeB = CreateNode("B", "2.0.0");
            var nodeC180 = CreateNode("C", "1.8.0");
            nodeB.InnerNodes.Add(nodeC180);
            root.InnerNodes.Add(nodeB);

            // Act
            var result = root.TryResolveConflicts();

            // Assert
            Assert.True(result);

            Assert.Equal(Disposition.Rejected, nodeC138.Disposition);
            Assert.Equal(Disposition.Accepted, nodeC180.Disposition);
        }

        private static GraphNode<RemoteResolveResult> CreateNode(
            string id,
            string versionSpec,
            string resolvedVersion = null)
        {
            var node = new GraphNode<RemoteResolveResult>(new LibraryRange
            {
                Name = id,
                VersionRange = VersionRange.Parse(versionSpec)
            });
            node.Item = new GraphItem<RemoteResolveResult>(new LibraryIdentity
            {
                Name = id,
                Version = NuGetVersion.Parse(resolvedVersion ?? versionSpec)
            });

            return node;
        }
    }
}
