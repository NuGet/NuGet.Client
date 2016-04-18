// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using NuGet.Frameworks;
using Xunit;
using static NuGet.Frameworks.FrameworkConstants.FrameworkIdentifiers;

namespace NuGet.Test
{
    public class FrameworkNameProviderTests
    {
        [Fact]
        public void FrameworkNameProvider_NetStandardVersions()
        {
            // Arrange
            var provider = DefaultFrameworkNameProvider.Instance;

            // Act
            var versions = provider
                .GetNetStandardVersions()
                .ToArray();

            // Assert
            Assert.Equal(7, versions.Length);
            Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard10, versions[0]);
            Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard11, versions[1]);
            Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard12, versions[2]);
            Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard13, versions[3]);
            Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard14, versions[4]);
            Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard15, versions[5]);
            Assert.Equal(FrameworkConstants.CommonFrameworks.NetStandard16, versions[6]);
        }

        [Fact]
        public void FrameworkNameProvider_DuplicateFrameworksInPrecedence()
        {
            // Arrange
            var mappingsA = new Mock<IFrameworkMappings>();
            var mappingsB = new Mock<IFrameworkMappings>();
            mappingsA.Setup(x => x.NonPackageBasedFrameworkPrecedence).Returns(new[] { Net, NetCore });
            mappingsB.Setup(x => x.NonPackageBasedFrameworkPrecedence).Returns(new[] { NetCore, Net });

            var provider = new FrameworkNameProvider(new[] { mappingsA.Object, mappingsB.Object }, null);

            // Act
            var lt = provider.CompareFrameworks(FrameworkConstants.CommonFrameworks.Net45, FrameworkConstants.CommonFrameworks.NetCore45);
            var gt = provider.CompareFrameworks(FrameworkConstants.CommonFrameworks.NetCore45, FrameworkConstants.CommonFrameworks.Net45);

            // Assert
            Assert.True(lt < 0, "Net should come before NetCore");
            Assert.True(gt > 0, "NetCore should come after Net");
        }

        [Fact]
        public void FrameworkNameProvider_DistinctFrameworksInPrecedence()
        {
            // Arrange
            var mappingsA = new Mock<IFrameworkMappings>();
            var mappingsB = new Mock<IFrameworkMappings>();
            mappingsA.Setup(x => x.NonPackageBasedFrameworkPrecedence).Returns(new[] { Net });
            mappingsB.Setup(x => x.NonPackageBasedFrameworkPrecedence).Returns(new[] { NetCore });

            var provider = new FrameworkNameProvider(new[] { mappingsA.Object, mappingsB.Object }, null);

            // Act
            var lt = provider.CompareFrameworks(FrameworkConstants.CommonFrameworks.Net45, FrameworkConstants.CommonFrameworks.NetCore45);
            var gt = provider.CompareFrameworks(FrameworkConstants.CommonFrameworks.NetCore45, FrameworkConstants.CommonFrameworks.Net45);

            // Assert
            Assert.True(lt < 0, "Net should come before NetCore");
            Assert.True(gt > 0, "NetCore should come after Net");
        }

        [Fact]
        public void FrameworkNameProvider_MissingFrameworksInPrecedence()
        {
            // Arrange
            var mappingsA = new Mock<IFrameworkMappings>();
            var mappingsB = new Mock<IFrameworkMappings>();
            mappingsA.Setup(x => x.NonPackageBasedFrameworkPrecedence).Returns(new[] { Silverlight });
            mappingsB.Setup(x => x.NonPackageBasedFrameworkPrecedence).Returns(new[] { Net });

            var provider = new FrameworkNameProvider(new[] { mappingsA.Object, mappingsB.Object }, null);

            // Act
            var lt = provider.CompareFrameworks(FrameworkConstants.CommonFrameworks.Net45, FrameworkConstants.CommonFrameworks.NetCore45);
            var gt = provider.CompareFrameworks(FrameworkConstants.CommonFrameworks.NetCore45, FrameworkConstants.CommonFrameworks.Net45);

            // Assert
            Assert.True(lt < 0, "Net should come before NetCore");
            Assert.True(gt > 0, "NetCore should come after Net");
        }

        [Fact]
        public void FrameworkNameProvider_EquivalentFrameworkPrecedence()
        {
            // Arrange
            var mappingsA = new Mock<IFrameworkMappings>();
            var mappingsB = new Mock<IFrameworkMappings>();
            mappingsA.Setup(x => x.EquivalentFrameworkPrecedence).Returns(new[] { Windows });
            mappingsB.Setup(x => x.EquivalentFrameworkPrecedence).Returns(new[] { NetCore });

            var provider = new FrameworkNameProvider(new[] { mappingsA.Object, mappingsB.Object }, null);

            // Act
            var lt = provider.CompareEquivalentFrameworks(
                FrameworkConstants.CommonFrameworks.Win8,
                FrameworkConstants.CommonFrameworks.NetCore45);

            var gt = provider.CompareEquivalentFrameworks(
                FrameworkConstants.CommonFrameworks.NetCore45,
                FrameworkConstants.CommonFrameworks.Win8);

            // Assert
            Assert.True(lt < 0, "Win should come before NetCore");
            Assert.True(gt > 0, "NetCore should come after Win");
        }

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

            var results = frameworks
                .OrderBy(f => f, new NuGetFrameworkSorter())
                .Select(f => f.GetShortFolderName())
                .ToArray();

            Assert.Equal(5, results.Length);
            Assert.Equal("netcore", results[0]);
            Assert.Equal("netcore45", results[1]);
            Assert.Equal("win", results[2]);
            Assert.Equal("winrt", results[3]);
            Assert.Equal("winrt45", results[4]);
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
