// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Test.Utility;
using NuGet.Versioning;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace NuGet.Protocol.Core.Types.Tests
{
    public class PackageSearchMetadataBuilderTests
    {
        [Fact]        
        public async Task LocalPacakgeSearchMetadata_ClonedPackageSearchMetadata_LocalPackageInfo_NotNull()
        {
            var pkgId = new PackageIdentity("nuget.psm.test", new NuGetVersion(0,0,1));
            var pkg = new SimpleTestPackageContext(pkgId.Id, pkgId.Version.ToNormalizedString());
            pkg.AddFile("lib/net45/a.dll");

            using(var dir = TestDirectory.Create())
            {
                await SimpleTestPackageUtility.CreatePackagesAsync(dir.Path, pkg);
                var pkgPath = Path.Combine(dir.Path, $"{pkgId.Id}.{pkgId.Version.ToNormalizedString()}.nupkg");
                var info = new LocalPackageInfo(
                    identity: pkgId,
                    path: pkgPath,
                    lastWriteTimeUtc: DateTime.UtcNow,
                    nuspec: new Lazy<Packaging.NuspecReader>(() => {
                        var reader = new PackageArchiveReader(pkgPath);
                        return reader.NuspecReader;
                    }),
                    getPackageReader: () => new PackageArchiveReader(pkgPath));
                var meta = new LocalPackageSearchMetadata(info);

                Assert.NotNull(meta.LocalPackageInfo);

                // act 1
                var copy1 = PackageSearchMetadataBuilder
                    .FromMetadata(meta)
                    .Build();
                Assert.True(copy1 is PackageSearchMetadataBuilder.ClonedPackageSearchMetadata);

                var clone1 = copy1 as PackageSearchMetadataBuilder.ClonedPackageSearchMetadata;
                Assert.NotNull(clone1.LocalPackageInfo);

                // Act 2
                var copy2 = PackageSearchMetadataBuilder
                    .FromMetadata(copy1)
                    .Build();
                Assert.True(copy2 is PackageSearchMetadataBuilder.ClonedPackageSearchMetadata);

                var clone2 = copy2 as PackageSearchMetadataBuilder.ClonedPackageSearchMetadata;
                Assert.NotNull(clone2.LocalPackageInfo);
            }
        }
    }
}
