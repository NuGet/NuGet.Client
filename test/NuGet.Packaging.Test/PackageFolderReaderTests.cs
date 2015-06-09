// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NuGet.Packaging.Core;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class PackageFolderReaderTests
    {
        // verify a zip package reader, and folder package reader handle reference items the same
        [Fact]
        public void PackageFolderReader_Basic()
        {
            var packageNupkg = TestPackages.GetLegacyTestPackage();
            var zip = new ZipArchive(packageNupkg.OpenRead());
            PackageReader zipReader = new PackageReader(zip);

            var folder = Path.Combine(packageNupkg.Directory.FullName, Guid.NewGuid().ToString());

            var zipFile = new ZipArchive(File.OpenRead(packageNupkg.FullName));

            zipFile.ExtractAll(folder);

            var folderReader = new PackageFolderReader(folder);

            Assert.Equal(zipReader.GetIdentity(), folderReader.GetIdentity(), new PackageIdentityComparer());

            Assert.Equal(zipReader.GetLibItems().Count(), folderReader.GetLibItems().Count());

            Assert.Equal(zipReader.GetReferenceItems().Count(), folderReader.GetReferenceItems().Count());

            Assert.Equal(zipReader.GetReferenceItems().First().Items.First(), folderReader.GetReferenceItems().First().Items.First());
        }

        [Fact]
        public void PackageFolderReader_IgnoresPackageFile()
        {
            // Arrange
            var packageNupkg = TestPackages.GetLegacyTestPackage();
            var zip = new ZipArchive(packageNupkg.OpenRead());
            var zipReader = new PackageReader(zip);
            var folder = Path.Combine(packageNupkg.Directory.FullName, Guid.NewGuid().ToString());
            var zipFile = new ZipArchive(File.OpenRead(packageNupkg.FullName));
            zipFile.ExtractAll(folder);
            packageNupkg.CopyTo(Path.Combine(folder, packageNupkg.Name));

            // Act
            var folderReader = new PackageFolderReader(folder);

            // Assert
            Assert.Equal(zipReader.GetIdentity(), folderReader.GetIdentity(), new PackageIdentityComparer());
            Assert.Equal(zipReader.GetFiles().OrderBy(f => f, StringComparer.Ordinal),
                folderReader.GetFiles().OrderBy(f => f, StringComparer.Ordinal));
            Assert.Equal(zipReader.GetLibItems().Count(), folderReader.GetLibItems().Count());
            Assert.Equal(zipReader.GetReferenceItems().Count(), folderReader.GetReferenceItems().Count());
            Assert.Equal(zipReader.GetReferenceItems().First().Items.First(), folderReader.GetReferenceItems().First().Items.First());
        }
    }
}
