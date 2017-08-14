// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public class LocalNuspecCacheTests
    {
        [Fact]
        public async Task LocalNuspecCache_GetNuspecTwiceVerifySameInstance()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var cache = new LocalNuspecCache();
                var pathResolver = new VersionFolderPathResolver(pathContext.PackageSource);

                var identity = new PackageIdentity("X", NuGetVersion.Parse("1.0.0"));

                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    identity);

                var nuspec = pathResolver.GetManifestFilePath(identity.Id, identity.Version);
                var expanded = pathResolver.GetInstallPath(identity.Id, identity.Version);

                var result1 = cache.GetOrAdd(nuspec, expanded);
                var result2 = cache.GetOrAdd(nuspec, expanded);

                Assert.Same(result1.Value, result2.Value);
                result1.Value.GetIdentity().Should().Be(identity);
            }
        }

        [Fact]
        public async Task LocalNuspecCache_FallbackToFolderReaderVerifyResult()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var cache = new LocalNuspecCache();
                var pathResolver = new VersionFolderPathResolver(pathContext.PackageSource);

                var identity = new PackageIdentity("X", NuGetVersion.Parse("1.0.0"));

                await SimpleTestPackageUtility.CreateFolderFeedV3(
                    pathContext.PackageSource,
                    identity);

                var nuspec = pathResolver.GetManifestFilePath(identity.Id, identity.Version) + "invalid.nuspec";
                var expanded = pathResolver.GetInstallPath(identity.Id, identity.Version);

                var result = cache.GetOrAdd(nuspec, expanded);

                result.Value.GetIdentity().Should().Be(identity);
            }
        }

        [Fact]
        public void LocalNuspecCache_NuspecNotFoundVerifyFailure()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var cache = new LocalNuspecCache();
                var pathResolver = new VersionFolderPathResolver(pathContext.PackageSource);

                var identity = new PackageIdentity("X", NuGetVersion.Parse("1.0.0"));

                var nuspec = pathResolver.GetManifestFilePath(identity.Id, identity.Version);
                var expanded = pathResolver.GetInstallPath(identity.Id, identity.Version);
                Directory.CreateDirectory(expanded);

                // Verify does not throw
                var result = cache.GetOrAdd(nuspec, expanded);

                // This should throw
                Assert.Throws<PackagingException>(() => result.Value);
            }
        }

        [Fact]
        public void LocalNuspecCache_DirNotFoundVerifyFailure()
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                var cache = new LocalNuspecCache();
                var pathResolver = new VersionFolderPathResolver(pathContext.PackageSource);

                var identity = new PackageIdentity("X", NuGetVersion.Parse("1.0.0"));

                var nuspec = pathResolver.GetManifestFilePath(identity.Id, identity.Version);
                var expanded = pathResolver.GetInstallPath(identity.Id, identity.Version);

                // Verify does not throw
                var result = cache.GetOrAdd(nuspec, expanded);

                // This should throw
                Assert.Throws<DirectoryNotFoundException>(() => result.Value);
            }
        }
    }
}
