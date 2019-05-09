// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NuGet.LibraryModel
{
    public class FrameworkDependencyTests
    {
        [Fact]
        public void FrameworkDependecy_ConstructorWithNullName_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new FrameworkDependency(null, FrameworkDependencyFlags.All));
        }

        [Fact]
        public void FrameworkDependecy_NamesWithDifferentCasing_AreEqual()
        {
            Assert.Equal(new FrameworkDependency("AAA", FrameworkDependencyFlags.All),
                        new FrameworkDependency("aaa", FrameworkDependencyFlags.All));
        }

        [Fact]
        public void FrameworkDependency_DifferentFlags_AreNotEqual()
        {
            Assert.NotEqual(new FrameworkDependency("AAA", FrameworkDependencyFlags.All),
                        new FrameworkDependency("AAA", FrameworkDependencyFlags.None));
        }

        [Fact]
        public void FrameworkDependecy_NamesWithDifferentCasign_DoNotAffectCompare()
        {
            var frameworkDependencies = new List<FrameworkDependency>();

            frameworkDependencies.Add(new FrameworkDependency("aa", FrameworkDependencyFlags.None));
            frameworkDependencies.Add(new FrameworkDependency("Aa", FrameworkDependencyFlags.None));
            frameworkDependencies.Add(new FrameworkDependency("AA", FrameworkDependencyFlags.None));

            // Act
            frameworkDependencies.Sort();

            Assert.Equal("aa,Aa,AA", string.Join(",", frameworkDependencies.Select(e => e.Name)));
        }

        [Fact]
        public void FrameworkDependecy_DifferentFlags_AffectCompare()
        {
            Assert.Equal(1, new FrameworkDependency("AAA", FrameworkDependencyFlags.All).CompareTo(new FrameworkDependency("AAA", FrameworkDependencyFlags.None)));
        }
    }
}
