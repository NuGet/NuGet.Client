// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using NuGet.VisualStudio.Internal.Contracts;
using Xunit;

namespace NuGet.PackageManagement.VisualStudio.Test
{
    public class PackageCollectionExtensionsTests
    {
        public static IEnumerable<PackageCollectionItem> PackageTestData = new[]
        {
            new PackageCollectionItem("packageA", NuGetVersion.Parse("1.0.0"), new PackageReferenceContextInfo[]
            {
                new PackageReferenceContextInfo(new PackageIdentity("packageA", NuGetVersion.Parse("1.0.0")), NuGetFramework.Parse("net6.0"))
            }),
            new PackageCollectionItem("packageA", NuGetVersion.Parse("3.0.0"), new PackageReferenceContextInfo[]
            {
                new PackageReferenceContextInfo(new PackageIdentity("packageA", NuGetVersion.Parse("0.0.3")), NuGetFramework.Parse("net6.0"))
            }),
            new PackageCollectionItem("packageC", NuGetVersion.Parse("2.0.0"), new PackageReferenceContextInfo[0]),
        };

        public static IEnumerable<PackageIdentity> PackageIdentityData = new[]
        {
            new PackageIdentity("packageA", new NuGetVersion("1.0.0")),
            new PackageIdentity("packageA", new NuGetVersion("3.0.0")),
            new PackageIdentity("packageC", new NuGetVersion("2.0.0")),
        };

        public static IEnumerable<object[]> GetData()
        {
            yield return new object[]
            {
                PackageTestData, typeof(PackageCollectionItem)
            };

            yield return new object[]
            {
                PackageIdentityData, typeof(PackageIdentity)
            };
        }

        [Theory]
        [MemberData(nameof(GetData))]
        public void GetEarliest_ReturnsFirstOfSameTypeAsInput<T>(IEnumerable<T> inputData, Type expectedType) where T : PackageIdentity
        {
            // Act
            T[] earliestPackages = inputData.GetEarliest();

            // Assert
            Assert.Collection(earliestPackages,
                e =>
                {
                    Assert.IsType(expectedType, e);
                    Assert.Equal(e.Id, "packageA");
                    Assert.Equal(e.Version.ToNormalizedString(), "1.0.0");
                },
                e =>
                {
                    Assert.IsType(expectedType, e);
                    Assert.Equal(e.Id, "packageC");
                    Assert.Equal(e.Version.ToNormalizedString(), "2.0.0");
                }
            );
        }

        [Theory]
        [MemberData(nameof(GetData))]
        public void GetLatest_ReturnstLastOfSameTypeAsInput<T>(IEnumerable<T> inputData, Type expectedType) where T : PackageIdentity
        {
            // Act
            T[] latestPackages = inputData.GetLatest();

            // Assert
            Assert.Collection(latestPackages,
                e =>
                {
                    Assert.IsType(expectedType, e);
                    Assert.Equal(e.Id, "packageA");
                    Assert.Equal(e.Version.ToNormalizedString(), "3.0.0");
                },
                e =>
                {
                    Assert.IsType(expectedType, e);
                    Assert.Equal(e.Id, "packageC");
                    Assert.Equal(e.Version.ToNormalizedString(), "2.0.0");
                }
            );
        }

        [Theory]
        [MemberData(nameof(GetData))]
        public void GroupById_ReturnsGroup<T>(IEnumerable<T> inputData, Type expectedType) where T : PackageIdentity
        {
            // Act
            IEnumerable<IGrouping<string, T>> grouped = inputData.GroupById();

            // Assert
            Assert.Collection(grouped,
                group =>
                {
                    Assert.Equal(2, group.Count());
                    Assert.All<object>(group, groupElem => Assert.IsType(expectedType, groupElem));
                },
                group =>
                {
                    Assert.Equal(1, group.Count());
                    Assert.All<object>(group, groupElem => Assert.IsType(expectedType, groupElem));
                });
        }
    }
}
