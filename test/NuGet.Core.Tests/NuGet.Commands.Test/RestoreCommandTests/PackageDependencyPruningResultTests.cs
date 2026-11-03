// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Xunit;

namespace NuGet.Commands.Test.RestoreCommandTests
{
    public class PackageDependencyPruningResultTests
    {
        [Theory]
        [InlineData(0UL)]
        [InlineData(1UL)]
        [InlineData(5UL)]
        [InlineData(1UL << 63)]
        public void Equals_IdenticalMasks_ReturnsTrue(ulong mask)
        {
            // Arrange
            var first = new PackageDependencyPruningResult(mask);
            var second = new PackageDependencyPruningResult(mask);

            // Act
            bool actual = first.Equals(second);

            // Assert
            Assert.True(actual);
        }

        [Theory]
        [InlineData(0UL, 1UL)]
        [InlineData(1UL, 2UL)]
        [InlineData(0UL, 1UL << 63)]
        [InlineData(1UL, 1UL << 32)] // Same hash, different removed positions.
        public void Equals_DifferentMasks_ReturnsFalse(ulong firstMask, ulong secondMask)
        {
            // Arrange
            var first = new PackageDependencyPruningResult(firstMask);
            var second = new PackageDependencyPruningResult(secondMask);

            // Act
            bool actual = first.Equals(second);

            // Assert
            Assert.False(actual);
        }

        [Theory]
        [InlineData(new int[] { })]
        [InlineData(new int[] { 64 })]
        [InlineData(new int[] { 0, 64 })]
        public void Equals_IdenticalOverflowPositions_ReturnsTrue(int[] removedPositions)
        {
            // Arrange
            var first = new PackageDependencyPruningResult(new List<int>(removedPositions));
            var second = new PackageDependencyPruningResult(new List<int>(removedPositions));

            // Act
            bool actual = first.Equals(second);

            // Assert
            Assert.True(actual);
        }

        [Theory]
        [InlineData(new int[] { }, new int[] { 64 })]
        [InlineData(new int[] { 64 }, new int[] { })]
        [InlineData(new int[] { 0, 64 }, new int[] { 0, 65 })]
        [InlineData(new int[] { 0, 64 }, new int[] { 0, 64, 99 })]
        [InlineData(new int[] { 0, 99 }, new int[] { 1, 2 })] // Same hash.
        public void Equals_DifferentOverflowPositions_ReturnsFalse(int[] firstPositions, int[] secondPositions)
        {
            // Arrange
            var first = new PackageDependencyPruningResult(new List<int>(firstPositions));
            var second = new PackageDependencyPruningResult(new List<int>(secondPositions));

            // Act
            bool actual = first.Equals(second);

            // Assert
            Assert.False(actual);
        }
    }
}
