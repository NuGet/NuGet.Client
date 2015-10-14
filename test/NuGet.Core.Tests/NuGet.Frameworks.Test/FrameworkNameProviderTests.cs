// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;
using Xunit;

namespace NuGet.Test
{
    public class FrameworkNameProviderTests
    {
        [Fact]
        public void FrameworkNameProvider_EqualFrameworksWithoutCurrent()
        {
            var provider = DefaultFrameworkNameProvider.Instance;

            NuGetFramework input = new NuGetFramework("Windows", new Version(8, 0));
            IEnumerable<NuGetFramework> frameworks = null;
            provider.TryGetEquivalentFrameworks(input, out frameworks);

            var set = new HashSet<NuGetFramework>(frameworks, NuGetFramework.Comparer);

            Assert.False(set.Contains(input));
        }

        [Fact]
        public void FrameworkNameProvider_EqualFrameworks()
        {
            var provider = DefaultFrameworkNameProvider.Instance;

            NuGetFramework input = new NuGetFramework("Windows", new Version(8, 0));
            IEnumerable<NuGetFramework> frameworks = null;
            provider.TryGetEquivalentFrameworks(input, out frameworks);

            var results = frameworks.ToArray();

            Assert.Equal(2, results.Length);
        }

        [Fact]
        public void FrameworkNameProvider_EqualFrameworksNotFound()
        {
            var provider = DefaultFrameworkNameProvider.Instance;

            NuGetFramework input = new NuGetFramework("MyFramework", new Version(9, 0));
            IEnumerable<NuGetFramework> frameworks = null;
            bool found = provider.TryGetEquivalentFrameworks(input, out frameworks);

            Assert.False(found);
        }

        [Theory]
        [InlineData("unknown")]
        [InlineData("")]
        public void FrameworkNameProvider_GetIdentifierError(string input)
        {
            var provider = DefaultFrameworkNameProvider.Instance;

            string identifier = null;
            bool found = provider.TryGetIdentifier(input, out identifier);

            Assert.False(found);
        }

        [Theory]
        [InlineData("net", ".NETFramework")]
        [InlineData(".NETFramework", ".NETFramework")]
        [InlineData("NETFramework", ".NETFramework")]
        public void FrameworkNameProvider_GetIdentifier(string input, string expected)
        {
            var provider = DefaultFrameworkNameProvider.Instance;

            string identifier = null;
            provider.TryGetIdentifier(input, out identifier);

            Assert.Equal(expected, identifier);
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("1", "1")]
        [InlineData("10", "1")]
        [InlineData("100", "1")]
        [InlineData("101", "101")]
        [InlineData("1010", "101")]
        [InlineData("1001", "1001")]
        [InlineData("1.0", "1")]
        [InlineData("1.0.0", "1")]
        [InlineData("1.0.1", "101")]
        [InlineData("1.0.1.0", "101")]
        [InlineData("1.0.0.1", "1001")]
        [InlineData("10.0", "10.0")]
        [InlineData("10.1", "10.1")]
        [InlineData("10.1.0.1", "10.1.0.1")]
        [InlineData("1.1.10", "1.1.10")]
        [InlineData("1.10.1", "1.10.1")]
        public void FrameworkNameProvider_VersionRoundTrip(string versionString, string expected)
        {
            var provider = DefaultFrameworkNameProvider.Instance;

            Version version = null;
            provider.TryGetVersion(versionString, out version);

            string actual = provider.GetVersionString("Windows", version);

            Assert.Equal(expected, actual);
        }
    }
}
