﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Protocol.Core.v3.Tests
{
    public class FindLocalPackagesResourceTests
    {
        private static readonly PackageIdentity PackageA1 = new PackageIdentity("a", NuGetVersion.Parse("1.0.0"));
        private static readonly PackageIdentity PackageA2 = new PackageIdentity("a", NuGetVersion.Parse("1.0-beta"));
        private static readonly PackageIdentity PackageA3 = new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.1.2"));
        private static readonly PackageIdentity PackageA4 = new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.1.3+a.b.c"));
        private static readonly PackageIdentity PackageA5 = new PackageIdentity("A", NuGetVersion.Parse("2.0.0.0"));

        private static readonly PackageIdentity PackageB = new PackageIdentity("b", NuGetVersion.Parse("1.0"));
        private static readonly PackageIdentity PackageC = new PackageIdentity("c", NuGetVersion.Parse("0.0.1-alpha.1.2"));

        private readonly static PackageIdentity[] PackageSet1 = new[]
        {
            PackageA1,
            PackageA2,
            PackageA3,
            PackageA4,
            PackageA5,
            PackageB,
            PackageC
        };

        [Fact]
        public async Task FindLocalPackagesResource_GetPackagesBasic()
        {
            using (var rootPackagesConfig = TestFileSystemUtility.CreateRandomTestFolder())
            using (var rootUnzip = TestFileSystemUtility.CreateRandomTestFolder())
            using (var rootV3 = TestFileSystemUtility.CreateRandomTestFolder())
            using (var rootV2 = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var testLogger = new TestLogger();
                await CreateFeeds(rootV2, rootV3, rootUnzip, rootPackagesConfig, PackageSet1);
                var expected = new HashSet<PackageIdentity>(PackageSet1);

                var resources = new FindLocalPackagesResource[]
                {
                    new FindLocalPackagesResourcePackagesConfig(rootPackagesConfig),
                    new FindLocalPackagesResourceUnzipped(rootUnzip),
                    new FindLocalPackagesResourceV2(rootV2),
                    new FindLocalPackagesResourceV3(rootV3)
                };

                foreach (var resource in resources)
                {
                    // Act
                    var result = resource.GetPackages(testLogger, CancellationToken.None).ToList();

                    // Assert
                    Assert.True(expected.SetEquals(result.Select(p => p.Identity)));
                    Assert.True(expected.SetEquals(result.Select(p => p.GetReader().GetIdentity())));
                    Assert.True(expected.SetEquals(result.Select(p => p.Nuspec.GetIdentity())));
                    Assert.True(result.All(p => p.IsNupkg));
                }
            }
        }

        [Fact]
        public void FindLocalPackagesResource_EmptyFolders()
        {
            using (var emptyDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var testLogger = new TestLogger();
                var doesNotExist = Path.Combine(emptyDir, "doesNotExist");

                var resources = new FindLocalPackagesResource[]
                {
                    new FindLocalPackagesResourcePackagesConfig(doesNotExist),
                    new FindLocalPackagesResourcePackagesConfig(emptyDir),
                    new FindLocalPackagesResourceUnzipped(doesNotExist),
                    new FindLocalPackagesResourceV2(doesNotExist),
                    new FindLocalPackagesResourceV3(doesNotExist),
                    new FindLocalPackagesResourceUnzipped(emptyDir),
                    new FindLocalPackagesResourceV2(emptyDir),
                    new FindLocalPackagesResourceV3(emptyDir)
                };

                foreach (var resource in resources)
                {
                    // Act
                    var getPackages = resource.GetPackages(testLogger, CancellationToken.None).ToList();
                    var findPackages = resource.FindPackagesById("a", testLogger, CancellationToken.None).ToList();
                    var package = resource.GetPackage(PackageA1, testLogger, CancellationToken.None);

                    // Assert
                    Assert.Equal(0, getPackages.Count);
                    Assert.Equal(0, findPackages.Count);
                    Assert.Null(package);
                }
            }
        }

        [Fact]
        public async Task FindLocalPackagesResource_FindPackagesByIdBasic()
        {
            using (var rootPackagesConfig = TestFileSystemUtility.CreateRandomTestFolder())
            using (var rootUnzip = TestFileSystemUtility.CreateRandomTestFolder())
            using (var rootV3 = TestFileSystemUtility.CreateRandomTestFolder())
            using (var rootV2 = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var testLogger = new TestLogger();
                await CreateFeeds(rootV2, rootV3, rootUnzip, rootPackagesConfig, PackageSet1);
                var expected = new HashSet<PackageIdentity>(new[] 
                {
                    PackageA1,
                    PackageA2,
                    PackageA3,
                    PackageA4,
                    PackageA5,
                });

                var resources = new FindLocalPackagesResource[]
                {
                    new FindLocalPackagesResourcePackagesConfig(rootPackagesConfig),
                    new FindLocalPackagesResourceUnzipped(rootUnzip),
                    new FindLocalPackagesResourceV2(rootV2),
                    new FindLocalPackagesResourceV3(rootV3)
                };

                foreach (var resource in resources)
                {
                    // Act
                    var result = resource.FindPackagesById("a", testLogger, CancellationToken.None).ToList();

                    // Assert
                    Assert.True(expected.SetEquals(result.Select(p => p.Identity)));
                    Assert.True(expected.SetEquals(result.Select(p => p.GetReader().GetIdentity())));
                    Assert.True(expected.SetEquals(result.Select(p => p.Nuspec.GetIdentity())));
                    Assert.True(result.All(p => p.IsNupkg));
                }
            }
        }

        [Fact]
        public async Task FindLocalPackagesResource_GetPackageBasic()
        {
            using (var rootPackagesConfig = TestFileSystemUtility.CreateRandomTestFolder())
            using (var rootUnzip = TestFileSystemUtility.CreateRandomTestFolder())
            using (var rootV3 = TestFileSystemUtility.CreateRandomTestFolder())
            using (var rootV2 = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var testLogger = new TestLogger();
                await CreateFeeds(rootV2, rootV3, rootUnzip, rootPackagesConfig, PackageSet1);
                var expected = new HashSet<PackageIdentity>(new[]
                {
                    PackageA1,
                    PackageA2,
                    PackageA3,
                    PackageA4,
                    PackageA5,
                });

                var resources = new FindLocalPackagesResource[]
                {
                    new FindLocalPackagesResourcePackagesConfig(rootPackagesConfig),
                    new FindLocalPackagesResourceUnzipped(rootUnzip),
                    new FindLocalPackagesResourceV2(rootV2),
                    new FindLocalPackagesResourceV3(rootV3)
                };

                foreach (var resource in resources)
                {
                    // Act
                    var result = resource.GetPackage(PackageA3, testLogger, CancellationToken.None);
                    var result2 = resource.GetPackage(UriUtility.CreateSourceUri(result.Path), testLogger, CancellationToken.None);

                    // Assert
                    Assert.Equal(PackageA3, result.Identity);
                    Assert.Equal(PackageA3, result2.Identity);
                }
            }
        }

        private async Task CreateFeeds(string rootV2, string rootV3, string rootUnzip, string rootPackagesConfig, params PackageIdentity[] packages)
        {
            foreach (var package in packages)
            {
                SimpleTestPackageUtility.CreateFolderFeedV2(rootV2, package);
                await SimpleTestPackageUtility.CreateFolderFeedV3(rootV3, package);
                SimpleTestPackageUtility.CreateFolderFeedUnzip(rootUnzip, package);
                SimpleTestPackageUtility.CreateFolderFeedPackagesConfig(rootPackagesConfig, package);
            }
        }
    }
}
