// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Xunit;

namespace NuGet.Resolver.Test
{
    /// <summary>
    /// Tests for <see cref="CombinationSolver{T}"/>
    /// </summary>
    public class CombinationSolverTests
    {
        /// <summary>
        /// Test that <see cref="CombinationSolver{T}.FindSolution(IEnumerable{IEnumerable{T}}, IComparer{T}, Func{T, T, bool}, Action{IEnumerable{T}})"/> solves constraint satisfaction problem in trivial case of empty collection.
        /// </summary>
        [Fact]
        public void FindSolution_SolvesTrivialCase()
        {
            var groupedItems = Array.Empty<IEnumerable<int>>(); // empty list of groups
            var itemSorter = Comparer<int>.Default;
            Func<int, int, bool> shouldRejectPairFunc = (a, b) => true;

            Assert.Empty(CombinationSolver<int>.FindSolution(groupedItems, itemSorter, shouldRejectPairFunc, null)); // empty set is unique solution
        }
    }
}
