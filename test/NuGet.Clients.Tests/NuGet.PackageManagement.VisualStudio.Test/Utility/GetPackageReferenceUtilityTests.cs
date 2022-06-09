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
        public void MergeTransitiveOrigin_AnyNullArguments_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => GetPackageReferenceUtility.MergeTransitiveOrigin(null, It.IsAny<IDictionary<FrameworkRuntimePair, IList<PackageReference>>>()));
            Assert.Throws<ArgumentNullException>(() => GetPackageReferenceUtility.MergeTransitiveOrigin(It.IsAny<PackageReference>(), null));
        }

        [Fact]
        public void MergeTransitiveOrigin_DuplicateTransitiveOrigins_Merges()
        {
            // Arrange
            PackageReference testPkgReference = CreatePackageReference("packageA", "1.0.0", "net472");
            var transitiveEntry = new Dictionary<FrameworkRuntimePair, IList<PackageReference>>
            {
                {
                    new FrameworkRuntimePair(testPkgReference.TargetFramework, string.Empty),
                    new List<PackageReference>()
                    {
                        CreatePackageReference("package1", "0.0.1", testPkgReference.TargetFramework),
                        CreatePackageReference("package1", "0.0.2", testPkgReference.TargetFramework),
                    }
                }
            };

            // Act
            TransitivePackageReference transitivePackageReference = GetPackageReferenceUtility.MergeTransitiveOrigin(testPkgReference, transitiveEntry);

            // Assert
            var transitiveOrigin = transitivePackageReference.TransitiveOrigins.Single();
            Assert.Equal(NuGetVersion.Parse("0.0.2"), transitiveOrigin.PackageIdentity.Version);
        }

        [Fact]
        public void MergeTransitiveOrigin_EmptyTransitiveOriginsList_ReturnsEmptyMergedTransitiveOriginsList()
        {
            // Arrange
            PackageReference testPkgReference = CreatePackageReference("packageA", "1.0.0", "net6.0");
            var fwRuntimePair = new FrameworkRuntimePair(testPkgReference.TargetFramework, string.Empty);
            var transitiveEntry = new Dictionary<FrameworkRuntimePair, IList<PackageReference>>
            {
                [fwRuntimePair] = new List<PackageReference>(),
            };

            // Act
            TransitivePackageReference transitivePackageReference = GetPackageReferenceUtility.MergeTransitiveOrigin(testPkgReference, transitiveEntry);

            // Assert
            Assert.Equal(testPkgReference.PackageIdentity, transitivePackageReference.PackageIdentity);
            Assert.Empty(transitivePackageReference.TransitiveOrigins);
        }

        [Fact]
        public void MergeTransitiveOrigin_WithNullTransitiveEntryList_ReturnsMergedTransitiveOrigins()
        {
            // Arrange
            var fwRidNetCore = new FrameworkRuntimePair(NuGetFramework.Parse("net6.0"), string.Empty);
            var fwRidNetFx = new FrameworkRuntimePair(NuGetFramework.Parse("net472"), string.Empty);
            PackageReference testPkgReference = CreatePackageReference("packageA", "1.0.0", fwRidNetCore.Framework);
            var transitiveEntry = new Dictionary<FrameworkRuntimePair, IList<PackageReference>>
            {
                [fwRidNetCore] = new List<PackageReference>()
                {
                    CreatePackageReference("package2", "0.0.1", fwRidNetCore.Framework),
                    CreatePackageReference("package1", "0.0.1", fwRidNetCore.Framework),
                    null,
                },
                [fwRidNetFx] = new List<PackageReference>()
                {
                    null,
                    CreatePackageReference("package3", "0.0.1", fwRidNetFx.Framework),
                    CreatePackageReference("package1", "0.0.2", fwRidNetFx.Framework),
                    null,
                },
            };

            // Act
            TransitivePackageReference transitivePackageReference = GetPackageReferenceUtility.MergeTransitiveOrigin(testPkgReference, transitiveEntry);

            // Assert
            // Target framework does not matter because PM UI doesn't support multi-targeting
            Assert.Collection(transitivePackageReference.TransitiveOrigins,
                item => Assert.Equal(CreatePackageIdentity("package1", "0.0.2"), item.PackageIdentity), // Highest version found
                item => Assert.Equal(CreatePackageIdentity("package2", "0.0.1"), item.PackageIdentity),
                item => Assert.Equal(CreatePackageIdentity("package3", "0.0.1"), item.PackageIdentity));
        }

        [Theory]
        [MemberData(nameof(GetTransitiveOriginListsWithNulls))]
        public void MergeTransitiveOrigin_WithTransitiveOriginListWithNulls_ReturnsExpectedTransitiveOriginsElementCount(List<PackageReference> transitiveOrigins, int expectedElementCount)
        {
            // Arrange
            var fwRidPair = new FrameworkRuntimePair(NuGetFramework.Parse("net6.0"), string.Empty);
            PackageReference testPkgReference = CreatePackageReference("packageA", "1.0.0", fwRidPair.Framework);
            var transitiveEntry = new Dictionary<FrameworkRuntimePair, IList<PackageReference>>
            {
                [fwRidPair] = transitiveOrigins,
            };

            // Act
            TransitivePackageReference transitivePackageReference = GetPackageReferenceUtility.MergeTransitiveOrigin(testPkgReference, transitiveEntry);

            // Assert
            Assert.Equal(expectedElementCount, transitivePackageReference.TransitiveOrigins.Count());
        }

        [Theory]
        [InlineData("a", "1.0", "b", "1.0")]
        [InlineData("a", "1.0", "b", null)]
        [InlineData("09a", "1.0", "b", null)]
        [InlineData("a", "1.0", "a", "2.0")]
        public void PackageReferenceMergeComparer_AscendingPackageIdentities_ReturnLessThanZero(string id1, string version1, string id2, string version2)
        {
            // Arrange
            PackageReference first = CreatePackageReference(id1, version1, "net6.0");
            PackageReference second = CreatePackageReference(id2, version2, framework: null);

            // Act
            int result = GetPackageReferenceUtility.PackageReferenceMergeComparer.Compare(first, second);

            // Assert
            Assert.True(result < 0);
        }

        [Theory]
        [InlineData("b", "1.0", "a", "1.0")]
        [InlineData("a", "2.0", "a", "1.0")]
        public void PackageReferenceMergeComparer_DescendingPackageIdentities_ReturnGreaterThanZero(string id1, string version1, string id2, string version2)
        {
            // Arrange
            PackageReference first = CreatePackageReference(id1, version1, "net6.0");
            PackageReference second = CreatePackageReference(id2, version2, fw: null);

            // Act
            int result = GetPackageReferenceUtility.PackageReferenceMergeComparer.Compare(first, second);

            // Assert
            Assert.True(result > 0);
        }

        [Fact]
        public void PackageReferenceMergeComparer_EqualPackageIdentities_ReturnsZero()
        {
            // Arrange
            PackageReference first = CreatePackageReference("Abc", "1.0", "net6.0");
            PackageReference second = CreatePackageReference("abc", "1.0", fw: null);

            // Act
            int result = GetPackageReferenceUtility.PackageReferenceMergeComparer.Compare(first, second);

            // Assert
            Assert.Equal(0, result);
        }

        [Fact]
        public void PackageReferenceMergeComparer_AnyNullArgument_ReturnsExpectedResult()
        {
            // Arrange
            PackageReference testPkgReference = CreatePackageReference("Abc", "1.0", "net6.0");

            // Act and Assert 1: Second is null
            int result1 = GetPackageReferenceUtility.PackageReferenceMergeComparer.Compare(testPkgReference, null);
            Assert.True(result1 > 0);

            // Act and Assert 2: First is null
            int result2 = GetPackageReferenceUtility.PackageReferenceMergeComparer.Compare(null, testPkgReference);
            Assert.True(result2 < 0);

            // Act and Assert 3: Both are null
            int result3 = GetPackageReferenceUtility.PackageReferenceMergeComparer.Compare(null, null);
            Assert.True(result3 == 0);

            var prWithNullIdentity = new PackageReference(null, null);

            // Act and Assert 4: Second with null PackageIdentity
            int result4 = GetPackageReferenceUtility.PackageReferenceMergeComparer.Compare(testPkgReference, prWithNullIdentity);
            Assert.True(result4 > 0);

            // Act and Assert 5: First with null PackageIdentity
            int result5 = GetPackageReferenceUtility.PackageReferenceMergeComparer.Compare(prWithNullIdentity, testPkgReference);
            Assert.True(result5 < 0);

            // Act and Assert 6: First with null PackageIdentity and null
            int result6 = GetPackageReferenceUtility.PackageReferenceMergeComparer.Compare(prWithNullIdentity, null);
            Assert.Equal(0, result6);

            PackageReference pkgRefWithNullVersion1 = CreatePackageReference("nullVersion", null, fw: null);
            PackageReference pkgRefWithNullVersion2 = CreatePackageReference("otherVersion", null, fw: null);

            // Act and Assert 7: First Null PackageIdentity and Second with null version, compare only with package IDs
            int result7 = GetPackageReferenceUtility.PackageReferenceMergeComparer.Compare(prWithNullIdentity, pkgRefWithNullVersion1);
            Assert.True(result7 < 0);

            // Act and Assert 8: Only package ID's on both
            int result8 = GetPackageReferenceUtility.PackageReferenceMergeComparer.Compare(pkgRefWithNullVersion1, pkgRefWithNullVersion2);
            Assert.True(result8 < 0);
        }

        private static PackageReference CreatePackageReference(string id, string version, NuGetFramework fw) => new PackageReference(CreatePackageIdentity(id, version), fw);

        private static PackageReference CreatePackageReference(string id, string version, string framework)
        {
            NuGetFramework fw = string.IsNullOrEmpty(framework) ? null : NuGetFramework.Parse(framework);
            return CreatePackageReference(id, version, fw);
        }

        private static PackageIdentity CreatePackageIdentity(string id, string version)
        {
            NuGetVersion ver = string.IsNullOrEmpty(version) ? null : NuGetVersion.Parse(version);
            return new PackageIdentity(id, ver);
        }

        public static IEnumerable<object[]> GetTransitiveOriginListsWithNulls()
        {
            // Returns list and expectedResultCount
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
                    CreatePackageReference("package1", "0.0.1", "net6.0"),
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
