﻿using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Protocol.Core.v3.Tests
{
    public class FindPackageByIdResourceTests
    {
        [Fact]
        public async Task FindPackageByIdResource_V2V3Compare()
        {
            using (var rootV3 = TestFileSystemUtility.CreateRandomTestFolder())
            using (var rootV2 = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var testLogger = new TestLogger();

                var a1 = new PackageIdentity("a", NuGetVersion.Parse("1.0.0-alpha.1.2"));
                var a2 = new PackageIdentity("a", NuGetVersion.Parse("1.0.0+server.2"));
                var b = new PackageIdentity("b", NuGetVersion.Parse("1.0.0.0"));

                SimpleTestPackageUtility.CreateFolderFeedV2(rootV2, a1, a2, b);
                await SimpleTestPackageUtility.CreateFolderFeedV3(rootV3, a1, a2, b);

                var resourceV2 = new LocalV2FindPackageByIdResource(new PackageSource(rootV2));
                var resourceV3 = new LocalV3FindPackageByIdResource(new PackageSource(rootV3));

                resourceV2.Logger = testLogger;
                resourceV3.Logger = testLogger;
                resourceV2.CacheContext = new SourceCacheContext();
                resourceV3.CacheContext = new SourceCacheContext();

                var bNonNorm = new PackageIdentity("B", NuGetVersion.Parse("1.0"));

                // Act
                var versionsV2 = new HashSet<NuGetVersion>(await resourceV2.GetAllVersionsAsync("A", CancellationToken.None));
                var versionsV3 = new HashSet<NuGetVersion>(await resourceV3.GetAllVersionsAsync("A", CancellationToken.None));

                var emptyV2 = (await resourceV2.GetAllVersionsAsync("c", CancellationToken.None))
                    .ToList();

                var emptyV3 = (await resourceV3.GetAllVersionsAsync("c", CancellationToken.None))
                    .ToList();

                var v2CanSeek = false;
                var v3CanSeek = false;

                using (var stream = await resourceV2.GetNupkgStreamAsync(bNonNorm.Id, bNonNorm.Version, CancellationToken.None))
                {
                    v2CanSeek = stream.CanSeek;
                }

                using (var stream = await resourceV3.GetNupkgStreamAsync(bNonNorm.Id, bNonNorm.Version, CancellationToken.None))
                {
                    v3CanSeek = stream.CanSeek;
                }

                var depV2 = await resourceV2.GetDependencyInfoAsync(bNonNorm.Id, bNonNorm.Version, CancellationToken.None);
                var depV3 = await resourceV3.GetDependencyInfoAsync(bNonNorm.Id, bNonNorm.Version, CancellationToken.None);

                var depEmptyV2 = await resourceV2.GetDependencyInfoAsync(bNonNorm.Id, NuGetVersion.Parse("2.9"), CancellationToken.None);
                var depEmptyV3 = await resourceV3.GetDependencyInfoAsync(bNonNorm.Id, NuGetVersion.Parse("2.9"), CancellationToken.None);

                // Assert
                Assert.True(versionsV2.SetEquals(versionsV3));
                Assert.Equal(0, emptyV2.Count);
                Assert.Equal(0, emptyV3.Count);
                Assert.True(v2CanSeek);
                Assert.True(v3CanSeek);
                Assert.Equal(0, depV2.DependencyGroups.Count);
                Assert.Equal(0, depV3.DependencyGroups.Count);
                Assert.Null(depEmptyV2);
                Assert.Null(depEmptyV3);
            }
        }
    }
}
