// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Frameworks;
using Xunit;

namespace NuGet.Test
{
    public class FrameworkComparerTests
    {
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
