// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using NuGet.Frameworks;
using NuGet.PackageManagement.VisualStudio.Utility;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using NuGet.VisualStudio.Internal.Contracts;
using Xunit;

namespace NuGet.PackageManagement.VisualStudio.Test
{
    public class GetPackageReferenceUtilityTests
    {
        [Fact]
        public void MergeTransitiveOrigin_DuplicateTransitiveOrigins_Merges()
        {
            // Arrange
            var net472 = NuGetFramework.Parse("net472");
            var pr = new PackageReference(new PackageIdentity("packageA", new NuGetVersion("1.0.0")), net472);
            var te = new Dictionary<FrameworkRuntimePair, IList<PackageReference>>
            {
                {
                    new FrameworkRuntimePair(net472, string.Empty),
                    new List<PackageReference>()
                    {
                        new PackageReference(new PackageIdentity("package1", new NuGetVersion("0.0.1")), net472),
                        new PackageReference(new PackageIdentity("package1", new NuGetVersion("0.0.2")), net472),
                    }
                }
            };

            // Act
            TransitivePackageReference transitivePackageReference = GetPackageReferenceUtility.MergeTransitiveOrigin(pr, te);

            // Assert
            var transitiveOrigin = transitivePackageReference.TransitiveOrigins.Single();
            Assert.Equal(NuGetVersion.Parse("0.0.2"), transitiveOrigin.PackageIdentity.Version);
        }

        [Fact]
        public void MergeTransitiveOrigin_EmptyList_Succeeds()
        {
            // Arrange
            var framework = NuGetFramework.Parse("net6.0");
            var pr = new PackageReference(new PackageIdentity("packageA", new NuGetVersion("1.0.0")), framework);
            var fwRuntimePair = new FrameworkRuntimePair(framework, string.Empty);
            var transitiveEntry = new Dictionary<FrameworkRuntimePair, IList<PackageReference>>
            {
                [fwRuntimePair] = new List<PackageReference>(),
            };

            // Act
            TransitivePackageReference transitivePackageReference = GetPackageReferenceUtility.MergeTransitiveOrigin(pr, transitiveEntry);

            // Assert
            Assert.Equal(pr.PackageIdentity, transitivePackageReference.PackageIdentity);
            Assert.Empty(transitivePackageReference.TransitiveOrigins);
        }

        [Theory]
        [MemberData(nameof(GetDataWithNulls))]
        public void MergeTransitiveOrigin_ListWithNulls_Succeeds(List<PackageReference> transitiveOrigins, int expectedElementCount)
        {
            // Arrange
            var fwRidPair = new FrameworkRuntimePair(NuGetFramework.Parse("net6.0"), string.Empty);
            var pr = new PackageReference(new PackageIdentity("packageA", new NuGetVersion("1.0.0")), fwRidPair.Framework);
            var transitiveEntry = new Dictionary<FrameworkRuntimePair, IList<PackageReference>>
            {
                [fwRidPair] = transitiveOrigins,
            };

            // Act
            TransitivePackageReference transitivePackageReference = GetPackageReferenceUtility.MergeTransitiveOrigin(pr, transitiveEntry);

            // Assert
            Assert.Equal(expectedElementCount, transitivePackageReference.TransitiveOrigins.Count());
        }

        [Fact]
        public void MergeTransitiveOrigin_NullArgument_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => GetPackageReferenceUtility.MergeTransitiveOrigin(null, It.IsAny<IDictionary<FrameworkRuntimePair, IList<PackageReference>>>()));
            Assert.Throws<ArgumentNullException>(() => GetPackageReferenceUtility.MergeTransitiveOrigin(It.IsAny<PackageReference>(), null));
        }

        [Theory]
        [InlineData("a", "1.0", "b", "1.0")]
        [InlineData("a", "1.0", "b", null)]
        [InlineData("09a", "1.0", "b", null)]
        [InlineData("a", "1.0", "a", "2.0")]
        public void PackageReferenceMergeComparer_DifferentPackageIdentities_ReturnLessThanZero(string id1, string version1, string id2, string version2)
        {
            // Arrange
            PackageReference first = CreatePackageReference(id1, version1, "net6.0");
            PackageReference second = CreatePackageReference(id2, version2, null);

            // Act
            int result = GetPackageReferenceUtility.PackageReferenceMergeComparer.Compare(first, second);

            // Assert
            Assert.True(result < 0);
}

        [Fact]
        public void PackageReferenceMergeComparer_EqualPackageIdentities_ReturnsZero()
        {
            // Arrange
            PackageReference first = CreatePackageReference("Abc", "1.0", "net6.0");
            PackageReference second = CreatePackageReference("abc", "1.0", null);

            // Act
            int result = GetPackageReferenceUtility.PackageReferenceMergeComparer.Compare(first, second);

            // Assert
            Assert.Equal(0, result);
        }

        [Theory]
        [InlineData("b", "1.0", "a", "1.0")]
        [InlineData("a", "2.0", "a", "1.0")]
        public void PackageReferenceMergeComparer_DifferentPackageIdentities_ReturnGreaterThanZero(string id1, string version1, string id2, string version2)
        {
            // Arrange
            PackageReference first = CreatePackageReference(id1, version1, "net6.0");
            PackageReference second = CreatePackageReference(id2, version2, null);

            // Act
            int result = GetPackageReferenceUtility.PackageReferenceMergeComparer.Compare(first, second);

            // Assert
            Assert.True(result > 0);
        }

        private static PackageReference CreatePackageReference(string id, string version, string framework)
        {
            NuGetFramework fw = string.IsNullOrEmpty(framework) ? null : NuGetFramework.Parse(framework);
            return new PackageReference(CreatePackageIdentity(id, version), fw);
        }

        private static PackageIdentity CreatePackageIdentity(string id, string version)
        {
            NuGetVersion ver = string.IsNullOrEmpty(version) ? null : NuGetVersion.Parse(version);
            return new PackageIdentity(id, ver);
        }

        [Fact]
        public void MergeTransitiveOrigin_WithNullTransitiveEntryList_ReturnsEmpty()
        {
            // Arrange
            var fwRidNetCore = new FrameworkRuntimePair(NuGetFramework.Parse("net6.0"), string.Empty);
            var fwRidNetFx = new FrameworkRuntimePair(NuGetFramework.Parse("net472"), string.Empty);
            var pr = new PackageReference(new PackageIdentity("packageA", new NuGetVersion("1.0.0")), fwRidNetCore.Framework);
            var transitiveEntry = new Dictionary<FrameworkRuntimePair, IList<PackageReference>>
            {
                [fwRidNetCore] = new List<PackageReference>()
                {
                    new PackageReference(new PackageIdentity("package2", new NuGetVersion("0.0.1")), fwRidNetCore.Framework),
                    new PackageReference(new PackageIdentity("package1", new NuGetVersion("0.0.1")), fwRidNetCore.Framework),
                    null,
                },
                [fwRidNetFx] = new List<PackageReference>()
                {
                    null,
                    new PackageReference(new PackageIdentity("package3", new NuGetVersion("0.0.1")), fwRidNetFx.Framework),
                    new PackageReference(new PackageIdentity("package1", new NuGetVersion("0.0.2")), fwRidNetFx.Framework),
                    null,
                },
            };

            // Act
            TransitivePackageReference transitivePackageReference = GetPackageReferenceUtility.MergeTransitiveOrigin(pr, transitiveEntry);

            // Assert
            // Target framework does not matter because PM UI doesn't support multi-targeting
            Assert.Collection(transitivePackageReference.TransitiveOrigins,
                item => Assert.Equal(CreatePackageIdentity("package1", "0.0.2"), item.PackageIdentity), // highest version found
                item => Assert.Equal(CreatePackageIdentity("package2", "0.0.1"), item.PackageIdentity),
                item => Assert.Equal(CreatePackageIdentity("package3", "0.0.1"), item.PackageIdentity));
        }
        public static IEnumerable<object[]> GetDataWithNulls()
        {
            // return list and expectedResultcount
            yield return new object[]
            {
                new List<PackageReference>() { null, null },
                0,
            };

            yield return new object[]
            {
                new List<PackageReference>()
                {
                    null,
                    new PackageReference(new PackageIdentity("package1", new NuGetVersion("0.0.1")), NuGetFramework.Parse("net6.0")),
                    null,
                },
                1,
            };

            yield return new object[]
            {
                new List<PackageReference>(),
                0,
            };

            yield return new object[]
            {
                null,
                0,
            };
        }
    }
}
