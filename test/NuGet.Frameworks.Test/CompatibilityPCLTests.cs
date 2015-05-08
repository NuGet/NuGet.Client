// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Frameworks;
using Xunit;

namespace NuGet.Test
{
    public class CompatibilityPCLTests
    {
        [Fact]
        public void CompatibilityPCL_NetNeg()
        {
            var framework1 = NuGetFramework.Parse("net40");
            var framework2 = NuGetFramework.Parse("portable-win8+net45");

            var compat = DefaultCompatibilityProvider.Instance;

            Assert.False(compat.IsCompatible(framework1, framework2));
            Assert.False(compat.IsCompatible(framework2, framework1));
        }

        [Fact]
        public void CompatibilityPCL_Net()
        {
            var framework1 = NuGetFramework.Parse("net45");
            var framework2 = NuGetFramework.Parse("portable-win8+net45");

            var compat = DefaultCompatibilityProvider.Instance;

            Assert.True(compat.IsCompatible(framework1, framework2));
            Assert.False(compat.IsCompatible(framework2, framework1));
        }

        [Theory]
        [InlineData("portable-net45+win8+monoandroid", "portable-net45+win8+unk8+monoandroid")]
        [InlineData("portable-net45+win8", "portable-net45+win8+unk8+monoandroid+monotouch")]
        [InlineData("portable-net45+win8", "portable-net45+win8+unk8+monoandroid1+monotouch1")]
        [InlineData("portable-net45+win8+monoandroid+monotouch", "portable-net45+win8+unk8")]
        [InlineData("portable-net45+win8+monoandroid1+monotouch1", "portable-net45+win8+unk8")]
        public void CompatibilityPCL_OptionalUnk(string fw1, string fw2)
        {
            var framework1 = NuGetFramework.Parse(fw1);
            var framework2 = NuGetFramework.Parse(fw2);

            var compat = DefaultCompatibilityProvider.Instance;

            // verify that compatibility is inferred across all the mappings
            Assert.True(compat.IsCompatible(framework1, framework2));

            // verify that this was a one way mapping
            Assert.False(compat.IsCompatible(framework2, framework1));
        }

        [Theory]
        [InlineData("portable-net45+win8+monoandroid", "portable-net45+win8+wp8+monoandroid")]
        [InlineData("portable-net45+win8", "portable-net45+win8+wp8+monoandroid+monotouch")]
        [InlineData("portable-net45+win8", "portable-net45+win8+wp8+monoandroid1+monotouch1")]
        [InlineData("portable-net45+win8+monoandroid+monotouch", "portable-net45+win8+wp8")]
        [InlineData("portable-net45+win8+monoandroid1+monotouch1", "portable-net45+win8+wp8")]
        public void CompatibilityPCL_Optional(string fw1, string fw2)
        {
            var framework1 = NuGetFramework.Parse(fw1);
            var framework2 = NuGetFramework.Parse(fw2);

            var compat = DefaultCompatibilityProvider.Instance;

            // verify that compatibility is inferred across all the mappings
            Assert.True(compat.IsCompatible(framework1, framework2));

            // verify that this was a one way mapping
            Assert.False(compat.IsCompatible(framework2, framework1));
        }

        [Fact]
        public void CompatibilityPCL_Basic()
        {
            // win9 -> win8 -> netcore45, win8 -> netcore45
            var framework1 = NuGetFramework.Parse("portable-net451+win81");
            var framework2 = NuGetFramework.Parse("portable-win8+net45");

            var compat = DefaultCompatibilityProvider.Instance;

            // verify that compatibility is inferred across all the mappings
            Assert.True(compat.IsCompatible(framework1, framework2));

            // verify that this was a one way mapping
            Assert.False(compat.IsCompatible(framework2, framework1));
        }

        [Fact]
        public void CompatibilityPCL_Same()
        {
            // win9 -> win8 -> netcore45, win8 -> netcore45
            var framework1 = NuGetFramework.Parse("portable-net45+win8");
            var framework2 = NuGetFramework.Parse("portable-win8+net45");

            var compat = DefaultCompatibilityProvider.Instance;

            // verify that compatibility is inferred across all the mappings
            Assert.True(compat.IsCompatible(framework1, framework2));

            // verify that this was a one way mapping
            Assert.True(compat.IsCompatible(framework2, framework1));
        }
    }
}
