// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using System.Xml.Linq;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class PackagesConfigWriterTests
    {
        [Fact]
        public void PackagesConfigWriter_Basic()
        {
            var stream = new MemoryStream();

            using (PackagesConfigWriter writer = new PackagesConfigWriter(stream))
            {
                writer.WriteMinClientVersion(NuGetVersion.Parse("3.0.1"));

                writer.WritePackageEntry("packageB", NuGetVersion.Parse("2.0.0"), NuGetFramework.Parse("portable-net45+win8"));

                writer.WritePackageEntry("packageA", NuGetVersion.Parse("1.0.1"), NuGetFramework.Parse("net45"));
            }

            stream.Seek(0, SeekOrigin.Begin);

            var xml = XDocument.Load(stream);

            Assert.Equal("utf-8", xml.Declaration.Encoding);

            PackagesConfigReader reader = new PackagesConfigReader(xml);

            Assert.Equal("3.0.1", reader.GetMinClientVersion().ToNormalizedString());

            var packages = reader.GetPackages().ToArray();
            Assert.Equal("packageA", packages[0].PackageIdentity.Id);
            Assert.Equal("packageB", packages[1].PackageIdentity.Id);

            Assert.Equal("1.0.1", packages[0].PackageIdentity.Version.ToNormalizedString());
            Assert.Equal("2.0.0", packages[1].PackageIdentity.Version.ToNormalizedString());

            Assert.Equal("net45", packages[0].TargetFramework.GetShortFolderName());
            Assert.Equal("portable-net45+win8", packages[1].TargetFramework.GetShortFolderName());
        }

        [Fact]
        public void PackagesConfigWriter_Duplicate()
        {
            var stream = new MemoryStream();

            using (PackagesConfigWriter writer = new PackagesConfigWriter(stream))
            {
                writer.WritePackageEntry("packageA", NuGetVersion.Parse("1.0.1"), NuGetFramework.Parse("net45"));

                Assert.Throws<PackagingException>(() => writer.WritePackageEntry("packageA", NuGetVersion.Parse("2.0.1"), NuGetFramework.Parse("net4")));
            }
        }
    }
}
