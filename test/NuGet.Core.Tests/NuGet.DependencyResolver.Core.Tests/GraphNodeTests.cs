// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using FluentAssertions;
using NuGet.LibraryModel;
using NuGet.Versioning;
using Xunit;

namespace NuGet.DependencyResolver.Core.Tests
{
    public class GraphNodeTests
    {
        [Theory]
        [InlineData(null, true)]
        [InlineData(false, false)]
        [InlineData(true, true)]
        public void GraphNode_WithDifferentHasEmptyParentNodes_CreateParentNodesCorrectly(bool? hasParentNodes, bool parentNodesHasNode)
        {
            LibraryRange libraryRangeA = GetLibraryRange("A", "1.0.0", "1.0.0");
            LibraryRange libraryRangeB = GetLibraryRange("B", "1.0.0", "1.0.0");

            GraphNode<RemoteResolveResult> nodeA, nodeB;

            if (!hasParentNodes.HasValue)
            {
                nodeA = new GraphNode<RemoteResolveResult>(libraryRangeA);
                nodeB = new GraphNode<RemoteResolveResult>(libraryRangeB);
            }
            else
            {
                nodeA = new GraphNode<RemoteResolveResult>(libraryRangeA, hasInnerNodes:true, hasParentNodes.Value);
                nodeB = new GraphNode<RemoteResolveResult>(libraryRangeB, hasInnerNodes:true, hasParentNodes.Value);
            }

            bool sameParentNodes = (nodeA.ParentNodes == nodeB.ParentNodes);
            if (!parentNodesHasNode)
            {
                //The ParentNodes should be pointing to the static EmptyList.
                sameParentNodes.Should().BeTrue(because: "nodeA.ParentNodes and nodeB.ParentNodes should both point to the static EmptyList");

                //EmptyList is immutable.
                var exception = Assert.ThrowsAny<NotSupportedException>(
                () => nodeA.ParentNodes.Add(nodeB));
                Assert.Contains("of a fixed size.", exception.Message);
            }
            else
            {
                sameParentNodes.Should().BeFalse(because: "nodeA.ParentNodes and nodeB.ParentNodes should not point to the static EmptyList");
            }
        }

        [Theory]
        [InlineData(null, true)]
        [InlineData(false, false)]
        [InlineData(true, true)]
        public void GraphNode_WithDifferentHasEmptyInnerNodes_CreateInnerNodesCorrectly(bool? hasInnerNodes, bool innerNodesHasNode)
        {
            LibraryRange libraryRangeA = GetLibraryRange("A", "1.0.0", "1.0.0");
            LibraryRange libraryRangeB = GetLibraryRange("B", "1.0.0", "1.0.0");

            GraphNode<RemoteResolveResult> nodeA, nodeB;

            if (!hasInnerNodes.HasValue)
            {
                nodeA = new GraphNode<RemoteResolveResult>(libraryRangeA);
                nodeB = new GraphNode<RemoteResolveResult>(libraryRangeB);
            }
            else
            {
                nodeA = new GraphNode<RemoteResolveResult>(libraryRangeA, hasInnerNodes.Value, hasParentNodes : true);
                nodeB = new GraphNode<RemoteResolveResult>(libraryRangeB, hasInnerNodes.Value, hasParentNodes : true);
            }

            bool sameInnerNodes = (nodeA.InnerNodes == nodeB.InnerNodes);
            if (!innerNodesHasNode)
            {
                //The InnerNodes should be pointing to the static EmptyList.
                sameInnerNodes.Should().BeTrue(because: "nodeA.InnerNodes and nodeB.InnerNodes should be the same as they both point to the static EmptyList");

                //EmptyList is immutable.
                var exception = Assert.ThrowsAny<NotSupportedException>(
                () => nodeA.InnerNodes.Add(nodeB));
                Assert.Contains("of a fixed size.", exception.Message);
            }
            else
            {
                sameInnerNodes.Should().BeFalse(because: "nodeA.InnerNodes and nodeB.InnerNodes should not be the same");
            }
        }

        public LibraryRange GetLibraryRange(string id, string range, string version)
        {
            return new LibraryRange(id.ToUpperInvariant(), VersionRange.Parse(range), LibraryDependencyTarget.Package);
        }
    }
}
