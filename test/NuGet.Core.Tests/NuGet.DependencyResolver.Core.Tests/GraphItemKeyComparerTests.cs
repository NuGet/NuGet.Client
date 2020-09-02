// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.LibraryModel;
using NuGet.Versioning;
using Xunit;

namespace NuGet.DependencyResolver.Core.Tests
{
    public class GraphItemKeyComparerTests
    {
        [Fact]
        public void DistinctListOfObjects()
        {
            // Arrange
            var lib1Version1 = new LibraryIdentity("lib1", NuGetVersion.Parse("1.0.0"), LibraryType.Package);
            var lib1Version1Duplicate = new LibraryIdentity("lib1", NuGetVersion.Parse("1.0.0"), LibraryType.Package);
            var lib2Version1 = new LibraryIdentity("lib2", NuGetVersion.Parse("1.0.0"), LibraryType.Package);
            var lib1Version2 = new LibraryIdentity("lib1", NuGetVersion.Parse("2.0.0"), LibraryType.Package);

            var graphItem1 = new GraphItem<string>(lib1Version1);
            var graphItem2 = new GraphItem<string>(lib1Version1Duplicate);
            var graphItem3 = new GraphItem<string>(lib2Version1);
            var graphItem4 = new GraphItem<string>(lib1Version2);
            var graphItem5 = new GraphItem<string>(lib1Version1) { IsCentralTransitive = true };

            var list = new List<GraphItem<string>>() { graphItem1, graphItem2, graphItem3, graphItem4, graphItem5 };

            // Act
            var distinctElements = list.Distinct(GraphItemKeyComparer<string>.Instance).ToList();

            // Assert
            // There should be three elements distinct
            // These should be graphItem1, graphItem3, graphItem4
            Assert.Equal(3, distinctElements.Count);
            Assert.Equal(graphItem1.Key.Name, distinctElements[0].Key.Name);
            Assert.Equal(graphItem3.Key.Name, distinctElements[1].Key.Name);
            Assert.Equal(graphItem4.Key.Name, distinctElements[2].Key.Name);
        }

        [Fact]
        public void ObjectAreEqualsOnlyWhenGetHashAreEquals()
        {
            // Arrange
            var lib1Version1 = new LibraryIdentity("lib1", NuGetVersion.Parse("1.0.0"), LibraryType.Package);
            var lib1Version1Duplicate = new LibraryIdentity("lib1", NuGetVersion.Parse("1.0.0"), LibraryType.Package);
            var lib2Version1 = new LibraryIdentity("lib2", NuGetVersion.Parse("1.0.0"), LibraryType.Package);
            var lib1Version2 = new LibraryIdentity("lib1", NuGetVersion.Parse("2.0.0"), LibraryType.Package);

            var graphItem1 = new GraphItem<string>(lib1Version1);
            var graphItem2 = new GraphItem<string>(lib1Version1Duplicate);
            var graphItem3 = new GraphItem<string>(lib2Version1);
            var graphItem4 = new GraphItem<string>(lib1Version2);
            var graphItem5 = new GraphItem<string>(lib1Version1) { IsCentralTransitive = true };

            // Assert
            Assert.True(GraphItemKeyComparer<string>.Instance.Equals(graphItem1, graphItem2));
            Assert.True(GraphItemKeyComparer<string>.Instance.Equals(graphItem1, graphItem5));
            Assert.False(GraphItemKeyComparer<string>.Instance.Equals(graphItem1, graphItem3));
            Assert.False(GraphItemKeyComparer<string>.Instance.Equals(graphItem1, graphItem4));

            Assert.Equal(GraphItemKeyComparer<string>.Instance.GetHashCode(graphItem1), GraphItemKeyComparer<string>.Instance.GetHashCode(graphItem2));
            Assert.Equal(GraphItemKeyComparer<string>.Instance.GetHashCode(graphItem1), GraphItemKeyComparer<string>.Instance.GetHashCode(graphItem5));
            Assert.NotEqual(GraphItemKeyComparer<string>.Instance.GetHashCode(graphItem1), GraphItemKeyComparer<string>.Instance.GetHashCode(graphItem3));
            Assert.NotEqual(GraphItemKeyComparer<string>.Instance.GetHashCode(graphItem1), GraphItemKeyComparer<string>.Instance.GetHashCode(graphItem4));
        }
    }
}
