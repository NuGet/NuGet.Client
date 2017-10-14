// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FluentAssertions;
using NuGet.Common;
using NuGet.Packaging.Signing;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Packaging.Test.SigningTests
{
    public class PackageContentManifestTests
    {
        [Fact]
        public void PackageContentManifest_CreateManifestWithNoFiles()
        {
            var version = new SemanticVersion(1, 0, 0);
            var files = new List<PackageContentManifestFileEntry>();

            var manifest = new PackageContentManifest(version, HashAlgorithmName.SHA256, files);

            manifest.Version.ToNormalizedString().Should().Be("1.0.0");
            manifest.HashAlgorithm.Should().Be(HashAlgorithmName.SHA256);
            manifest.PackageEntries.Should().BeEmpty();
        }

        [Fact]
        public void PackageContentManifest_CreateManifestWithNoFilesRoundTripVerifyResult()
        {
            var version = new SemanticVersion(1, 0, 0);
            var files = new List<PackageContentManifestFileEntry>();

            var manifest = new PackageContentManifest(version, HashAlgorithmName.SHA256, files);

            using (var stream = new MemoryStream())
            {
                // Write
                manifest.Save(stream);
                stream.Position = 0;

                // Read
                var manifest2 = PackageContentManifest.Load(stream);

                // Verify
                manifest2.Version.ToNormalizedString().Should().Be("1.0.0");
                manifest2.HashAlgorithm.Should().Be(HashAlgorithmName.SHA256);
                manifest2.PackageEntries.Should().BeEmpty();
            }
        }

        [Fact]
        public void PackageContentManifest_CreateManifestVerifyNoByteOrderMarkingOnFile()
        {
            var version = new SemanticVersion(1, 0, 0);
            var files = new List<PackageContentManifestFileEntry>();

            var manifest = new PackageContentManifest(version, HashAlgorithmName.SHA256, files);

            using (var stream = new MemoryStream())
            {
                manifest.Save(stream);
                stream.Position = 0;

                stream.ReadByte().Should().Be(86, "The file should start with V. If 239 is found then it is a BOM.");
            }
        }

        [Fact]
        public void PackageContentManifest_CreateManifestWithFilesVerifySameResultsAfterRoundTrip()
        {
            var version = new SemanticVersion(1, 0, 0);
            var files = new List<PackageContentManifestFileEntry>
            {
                new PackageContentManifestFileEntry("lib/net45/a.dll", "abc"),
                new PackageContentManifestFileEntry("lib/net46/a.dll", "xyz")
            };

            var manifest = new PackageContentManifest(version, HashAlgorithmName.SHA256, files);

            using (var stream = new MemoryStream())
            {
                // Write
                manifest.Save(stream);
                stream.Position = 0;

                // Read
                var manifest2 = PackageContentManifest.Load(stream);

                // Verify
                manifest2.Version.ToNormalizedString().Should().Be("1.0.0");
                manifest2.HashAlgorithm.Should().Be(HashAlgorithmName.SHA256);
                manifest2.PackageEntries.ShouldBeEquivalentTo(files);
            }
        }

        [Fact]
        public void PackageContentManifest_CreateManifestWithFilesVerifyHashIsConsistent()
        {
            var version = new SemanticVersion(1, 0, 0);
            var files = new List<PackageContentManifestFileEntry>
            {
                new PackageContentManifestFileEntry("lib/net45/a.dll", "abc"),
                new PackageContentManifestFileEntry("lib/net46/a.dll", "xyz")
            };

            var manifest = new PackageContentManifest(version, HashAlgorithmName.SHA256, files);

            using (var stream = new MemoryStream())
            {
                // Write
                manifest.Save(stream);
                stream.Position = 0;

                var hashProvider = new CryptoHashProvider("SHA512");
                var hash = Convert.ToBase64String(hashProvider.CalculateHash(stream));
                hash.Should().Be("0IkgJ0iyymWMdvg3LQPjKlNeh0tWLFpAqfpciaNtdRe6UnWCDtCPFQMFcSfqbKsdA30iJAf3eIKgCrI17Tid9w==");
            }
        }

        [Fact]
        public void PackageContentManifest_CreateManifestWithFilesVerifyFileContent()
        {
            var version = new SemanticVersion(1, 0, 0);
            var files = new List<PackageContentManifestFileEntry>
            {
                new PackageContentManifestFileEntry("lib/net45/a.dll", "abc"),
                new PackageContentManifestFileEntry("lib/net46/a.dll", "xyz")
            };

            var manifest = new PackageContentManifest(version, HashAlgorithmName.SHA256, files);

            using (var stream = new MemoryStream())
            {
                // Write
                manifest.Save(stream);
                stream.Position = 0;

                var content = stream.ReadToEnd();

                var expected = @"Version:1.0.0
Hash-Algorithm:SHA256

Path:lib/net45/a.dll
Hash-Value:abc

Path:lib/net46/a.dll
Hash-Value:xyz
";

                var expectedLines = expected.Split('\n');
                var contentLines = content.Split('\n');

                for(var i=0; i < expectedLines.Length && i < contentLines.Length; i++)
                {
                    contentLines[i].Trim().Should().Be(expectedLines[i].Trim());
                }
            }
        }
    }
}
