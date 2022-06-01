// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;
using NuGet.PackageManagement.VisualStudio.Utility;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using NuGet.VisualStudio.Internal.Contracts;
using Xunit;
using Moq;
using TransitiveEntry = System.Collections.Generic.IDictionary<NuGet.Frameworks.FrameworkRuntimePair, System.Collections.Generic.IList<NuGet.Packaging.PackageReference>>;

namespace NuGet.PackageManagement.VisualStudio.Test
{
    public class GetPackageReferenceUtilityTests
    {
        [Fact]
        public void MergeTransitiveOrigin_DuplicateTransitiveOrigins_Merges()
        {
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

            TransitivePackageReference transitivePackageReference = GetPackageReferenceUtility.MergeTransitiveOrigin(pr, te);

            var transitiveOrigin = transitivePackageReference.TransitiveOrigins.Single();

            Assert.Equal(NuGetVersion.Parse("0.0.2"), transitiveOrigin.PackageIdentity.Version);
        }

        [Fact]
        public void MergeTransitiveOrigin_EmptyList_Succeeds()
        {
            var framework = NuGetFramework.Parse("net6.0");
            var pr = new PackageReference(new PackageIdentity("packageA", new NuGetVersion("1.0.0")), framework);
            var fwRuntimePair = new FrameworkRuntimePair(framework, string.Empty);
            var transitiveEntry = new Dictionary<FrameworkRuntimePair, IList<PackageReference>>
            {
                [fwRuntimePair] = new List<PackageReference>(),
            };

            TransitivePackageReference transitivePackageReference = GetPackageReferenceUtility.MergeTransitiveOrigin(pr, transitiveEntry);
            Assert.Equal(pr.PackageIdentity, transitivePackageReference.PackageIdentity);
            Assert.Empty(transitivePackageReference.TransitiveOrigins);
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

        [Theory]
        [MemberData(nameof(GetDataWithNulls))]
        public void MergeTransitiveOrigin_ListWithNulls_Succeeds(List<PackageReference> transitiveOrigins, int expectedElementCount)
        {
            var fwRidPair = new FrameworkRuntimePair(NuGetFramework.Parse("net6.0"), string.Empty);
            var pr = new PackageReference(new PackageIdentity("packageA", new NuGetVersion("1.0.0")), fwRidPair.Framework);
            var transitiveEntry = new Dictionary<FrameworkRuntimePair, IList<PackageReference>>
            {
                [fwRidPair] = transitiveOrigins,
            };

            TransitivePackageReference transitivePackageReference = GetPackageReferenceUtility.MergeTransitiveOrigin(pr, transitiveEntry);
            Assert.Equal(expectedElementCount, transitivePackageReference.TransitiveOrigins.Count());
        }

        [Fact]
        public void MergeTransitiveOrigin_NullArgument_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => GetPackageReferenceUtility.MergeTransitiveOrigin(null, It.IsAny<TransitiveEntry>()));
            Assert.Throws<ArgumentNullException>(() => GetPackageReferenceUtility.MergeTransitiveOrigin(It.IsAny<PackageReference>(), null));
        }

        [Fact]
        public void MergeTransitiveOrigin_WithNullTransitiveEntryList_ReturnsEmpty()
        {
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

            TransitivePackageReference transitivePackageReference = GetPackageReferenceUtility.MergeTransitiveOrigin(pr, transitiveEntry);
            Assert.Collection(transitivePackageReference.TransitiveOrigins,
                item => Assert.Equal(CreatePackageReference("package1", "0.0.1", "net472"), item), // sorted
                item => Assert.Equal(CreatePackageReference("package2", "0.0.1", "net6.0"), item),
                item => Assert.Equal(CreatePackageReference("package3", "0.0.1", "net6.0"), item));
        }

        private static PackageReference CreatePackageReference(string id, string version, string framework)
        {
            return new PackageReference(new PackageIdentity(id, NuGetVersion.Parse(version)), NuGetFramework.Parse(framework));
        }
    }
}
