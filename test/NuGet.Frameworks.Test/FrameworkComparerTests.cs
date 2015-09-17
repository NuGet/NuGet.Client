// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;
using Xunit;

namespace NuGet.Test
{
    public class FrameworkComparerTests
    {
        [Fact]
        public void FrameworkComparer_FrameworkOrderingWithPreferredAndNormalSorting()
        {
            // Arrange
            var fw1 = NuGetFramework.Parse("net45");
            var fw2 = NuGetFramework.Parse("net40");
            var fw3 = NuGetFramework.Parse("netcore451");
            var fw4 = NuGetFramework.Parse("netcore45");
            var fw5 = NuGetFramework.Parse("win81");
            var fw6 = NuGetFramework.Parse("win8");
            var fw7 = NuGetFramework.Parse("wpa81");
            var fw8 = NuGetFramework.Parse("sl5");
            var fw9 = NuGetFramework.Parse("sl4");
            var fw10 = NuGetFramework.Parse("sl3");

            var list = new List<NuGetFramework>()
            {
                fw1,
                fw3,
                fw5,
                fw2,
                fw4,
                fw6,
                fw7,
                fw8,
                fw9,
                fw10,
            };

            // Act
            list = list.OrderBy(f => f, new FrameworkPrecedenceSorter(DefaultFrameworkNameProvider.Instance))
                       .ThenByDescending(f => f, new NuGetFrameworkSorter())
                       .ToList();

            // Assert
            Assert.Equal(fw1, list[0]);
            Assert.Equal(fw2, list[1]);
            Assert.Equal(fw3, list[2]);
            Assert.Equal(fw4, list[3]);
            Assert.Equal(fw5, list[4]);
            Assert.Equal(fw6, list[5]);
            Assert.Equal(fw7, list[6]);
            Assert.Equal(fw8, list[7]);
            Assert.Equal(fw9, list[8]);
            Assert.Equal(fw10, list[9]);
        }

        [Fact]
        public void FrameworkComparer_PreferredFrameworkOrdering()
        {
            // Arrange
            var fw1 = NuGetFramework.Parse("net45");
            var fw2 = NuGetFramework.Parse("netcore45");
            var fw3 = NuGetFramework.Parse("win81");
            var fw4 = NuGetFramework.Parse("wpa81");
            var fw5 = NuGetFramework.Parse("sl5");

            var list = new List<NuGetFramework>()
            {
                fw1, 
                fw3,
                fw5,
                fw2, 
                fw4,
            };

            var comparer = new FrameworkPrecedenceSorter(DefaultFrameworkNameProvider.Instance);

            // Act
            list.Sort(comparer);

            // Assert
            Assert.Equal(fw1, list[0]);
            Assert.Equal(fw2, list[1]);
            Assert.Equal(fw3, list[2]);
            Assert.Equal(fw4, list[3]);
            Assert.Equal(fw5, list[4]);
        }

        [Fact]
        public void FrameworkComparer_VersionNormalize()
        {
            var fw1 = NuGetFramework.Parse("net45");
            var fw2 = NuGetFramework.Parse("net4.5");
            var fw3 = NuGetFramework.Parse("net4.5.0");
            var fw4 = NuGetFramework.Parse("net450");
            var fw5 = NuGetFramework.Parse(".NETFramework45");

            var comparer = new NuGetFrameworkFullComparer();

            Assert.True(comparer.Equals(fw1, fw2));
            Assert.True(comparer.Equals(fw1, fw3));
            Assert.True(comparer.Equals(fw1, fw4));
            Assert.True(comparer.Equals(fw1, fw5));
        }

        [Fact]
        public void FrameworkComparer_PCLNormalize()
        {
            var fw1 = NuGetFramework.Parse("portable-net45+win8+wp8+wpa81+monotouch+monoandroid");
            var fw2 = NuGetFramework.Parse("portable-net45+win8+wp8+wpa81");
            var fw3 = NuGetFramework.Parse(".NETPortable, Version=v0.0, Profile=Profile259");

            var comparer = new NuGetFrameworkFullComparer();

            Assert.True(comparer.Equals(fw1, fw2), "2");
            Assert.True(comparer.Equals(fw1, fw3), "3");
        }
    }
}
