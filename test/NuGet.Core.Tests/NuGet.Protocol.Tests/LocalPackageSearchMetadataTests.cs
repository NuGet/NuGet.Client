// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using System.IO;
using System.Xml.Linq;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public class LocalPackageSearchMetadataTests : IDisposable
    {
        private readonly LocalPackageSearchMetadata _testInstance;
        private readonly TestDirectory _packageDir;

        public LocalPackageSearchMetadataTests()
        {
            var pkgId = new PackageIdentity("nuget.lpsm.test", new NuGetVersion(0, 0, 1));

            var nuspecBuilder = NuspecBuilder.Create()
                .WithPackageId(pkgId.Id)
                .WithPackageVersion(pkgId.Version.ToNormalizedString())
                .WithIcon("icon.jpg");

            var pkgCtx = new SimpleTestPackageContext(pkgId.Id, pkgId.Version.ToNormalizedString());
            pkgCtx.Nuspec = XDocument.Parse(nuspecBuilder.Build().ToString());

            _packageDir = TestDirectory.Create();
            
            SimpleTestPackageUtility.CreatePackagesAsync(_packageDir.Path, pkgCtx).Wait();

            var pkgPath = Path.Combine(_packageDir.Path, $"{pkgId.Id}.{pkgId.Version.ToNormalizedString()}.nupkg");
            var info = new LocalPackageInfo(
                identity: pkgId,
                path: pkgPath,
                lastWriteTimeUtc: DateTime.UtcNow,
                nuspec: new Lazy<Packaging.NuspecReader>(() =>
                {
                    var reader = new PackageArchiveReader(pkgPath);
                    return reader.NuspecReader;
                }),
                getPackageReader: () => new PackageArchiveReader(pkgPath));

            _testInstance = new LocalPackageSearchMetadata(info);            
        }

        public void Dispose()
        {
            _packageDir.Dispose();
        }

        [Fact]
        public async Task DeprecationMetadataIsNull()
        {
            var localPackageInfo = new LocalPackageInfo(
                new PackageIdentity("id", NuGetVersion.Parse("1.0.0")),
                "path",
                new DateTime(2019, 8, 19),
                new Lazy<NuspecReader>(() => null),
                () => null);

            var localPackageSearchMetadata = new LocalPackageSearchMetadata(localPackageInfo);

            Assert.Null(await localPackageSearchMetadata.GetDeprecationMetadataAsync());
        }

        [Fact]
        public void LocalPackageInfo_NotNull()
        {
            Assert.NotNull(_testInstance.LocalPackageInfo);
        }

        [Fact]
        public void EmbeddedIcon_IconUrl_ReturnsFile()
        {
            Assert.NotNull(_testInstance.IconUrl);
            Assert.True(_testInstance.IconUrl.IsFile);
            Assert.True(_testInstance.IconUrl.IsAbsoluteUri);
            Assert.NotNull(_testInstance.IconUrl.Fragment);
        }
    }
}
