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
        [Fact]
        public void PackageFolderReader_NuspecCountOne()
        {
            // Arrange
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var stream = new MemoryStream())
            {
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    zip.AddEntry("lib/net45/a.dll", new byte[0]);
                    zip.AddEntry("package.nuspec", new byte[5]);
                }

                stream.Seek(0, SeekOrigin.Begin);

                var zipFile = new ZipArchive(stream, ZipArchiveMode.Read);

                zipFile.ExtractAll(workingDir);

                var folderReader = new PackageFolderReader(workingDir);

                // Act
                using (var nuspec = folderReader.GetNuspec())
                {
                    // Assert
                    Assert.NotNull(nuspec);
                    Assert.Equal(5, nuspec.Length);
                }
            }
        }

        [Fact]
        public void PackageFolderReader_NuspecCountNested()
        {
            // Arrange
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var stream = new MemoryStream())
            {
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    zip.AddEntry("lib/net45/a.dll", new byte[0]);
                    zip.AddEntry("package.nuspec", new byte[5]);
                    zip.AddEntry("content/package.nuspec", new byte[0]);
                }

                stream.Seek(0, SeekOrigin.Begin);

                var zipFile = new ZipArchive(stream, ZipArchiveMode.Read);

                zipFile.ExtractAll(workingDir);

                var folderReader = new PackageFolderReader(workingDir);

                // Act
                using (var nuspec = folderReader.GetNuspec())
                {
                    // Assert
                    Assert.NotNull(nuspec);
                    Assert.Equal(5, nuspec.Length);
                }
            }
        }

        [Fact]
        public void PackageFolderReader_NuspecCountNestedOnly()
        {
            // Arrange
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var stream = new MemoryStream())
            {
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    zip.AddEntry("lib/net45/a.dll", new byte[0]);
                    zip.AddEntry("content/package.nuspec", new byte[0]);
                }

                stream.Seek(0, SeekOrigin.Begin);

                var zipFile = new ZipArchive(stream, ZipArchiveMode.Read);

                zipFile.ExtractAll(workingDir);

                var reader = new PackageFolderReader(workingDir);

                var threwPackagingException = false;

                // Act
                try
                {
                    var nuspec = reader.GetNuspec();
                }
                catch (PackagingException)
                {
                    threwPackagingException = true;
                }

                // Assert
                Assert.True(threwPackagingException);
            }
        }

        [Fact]
        public void PackageFolderReader_NuspecCountMultiple()
        {
            // Arrange
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var stream = new MemoryStream())
            {
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    zip.AddEntry("lib/net45/a.dll", new byte[0]);
                    zip.AddEntry("package.NUSPEC", new byte[0]);
                    zip.AddEntry("package2.nuspec", new byte[0]);
                }

                stream.Seek(0, SeekOrigin.Begin);

                var zipFile = new ZipArchive(stream, ZipArchiveMode.Read);

                zipFile.ExtractAll(workingDir);

                var reader = new PackageFolderReader(workingDir);

                var threwPackagingException = false;

                // Act
                try
                {
                    var nuspec = reader.GetNuspec();
                }
                catch (PackagingException)
                {
                    threwPackagingException = true;
                }

                // Assert
                Assert.True(threwPackagingException);
            }
        }

        [Fact]
        public void PackageFolderReader_NuspecCountNone()
        {
            // Arrange
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var stream = new MemoryStream())
            {
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    zip.AddEntry("lib/net45/a.dll", new byte[0]);
                }

                stream.Seek(0, SeekOrigin.Begin);

                var zipFile = new ZipArchive(stream, ZipArchiveMode.Read);

                zipFile.ExtractAll(workingDir);

                var reader = new PackageFolderReader(workingDir);

                var threwPackagingException = false;

                // Act
                try
                {
                    var nuspec = reader.GetNuspec();
                }
                catch (PackagingException)
                {
                    threwPackagingException = true;
                }

                // Assert
                Assert.True(threwPackagingException);
            }
        }

        [Fact]
        public void PackageFolderReader_NuspecCountNoneInvalidEnding()
        {
            // Arrange
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var stream = new MemoryStream())
            {
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    zip.AddEntry("lib/net45/a.dll", new byte[0]);
                    zip.AddEntry("nuspec.blah", new byte[0]);
                    zip.AddEntry("blahnuspec", new byte[0]);
                    zip.AddEntry("blah/nuspec", new byte[0]);
                    zip.AddEntry("blah-nuspec", new byte[0]);
                    zip.AddEntry("blah.nuspecc", new byte[0]);
                }

                stream.Seek(0, SeekOrigin.Begin);

                var zipFile = new ZipArchive(stream, ZipArchiveMode.Read);

                zipFile.ExtractAll(workingDir);

                var reader = new PackageFolderReader(workingDir);

                var threwPackagingException = false;

                // Act
                try
                {
                    var nuspec = reader.GetNuspec();
                }
                catch (PackagingException)
                {
                    threwPackagingException = true;
                }

                // Assert
                Assert.True(threwPackagingException);
            }
        }

        [Fact]
        public void PackageFolderReader_NuspecCountEscapingInName()
        {
            // Arrange
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var stream = new MemoryStream())
            {
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    zip.AddEntry("lib/net45/a.dll", new byte[0]);
                    zip.AddEntry("package%20.nuspec", new byte[5]);
                }

                stream.Seek(0, SeekOrigin.Begin);

                var zipFile = new ZipArchive(stream, ZipArchiveMode.Read);

                zipFile.ExtractAll(workingDir);

                var reader = new PackageFolderReader(workingDir);

                // Act
                using (var nuspec = reader.GetNuspec())
                {
                    // Assert
                    Assert.NotNull(nuspec);
                    Assert.Equal(5, nuspec.Length);
                }
            }
        }

        // verify a zip package reader, and folder package reader handle reference items the same
        [Fact]
        public void PackageFolderReader_Basic()
        {
            using (var packageFile = TestPackages.GetLegacyTestPackage())
            {
                using (var zip = new ZipArchive(File.OpenRead(packageFile)))
                using (var zipReader = new PackageArchiveReader(zip))
                {

                    var folder = Path.Combine(Path.GetDirectoryName(packageFile), Guid.NewGuid().ToString());

                    using (var zipFile = new ZipArchive(File.OpenRead(packageFile)))
                    {
                        zipFile.ExtractAll(folder);

                        var folderReader = new PackageFolderReader(folder);

                        Assert.Equal(zipReader.GetIdentity(), folderReader.GetIdentity(), new PackageIdentityComparer());

                        Assert.Equal(zipReader.GetLibItems().Count(), folderReader.GetLibItems().Count());

                        Assert.Equal(zipReader.GetReferenceItems().Count(), folderReader.GetReferenceItems().Count());

                        Assert.Equal(zipReader.GetReferenceItems().First().Items.First(), folderReader.GetReferenceItems().First().Items.First());
                    }
                }
            }
        }
    }
}
