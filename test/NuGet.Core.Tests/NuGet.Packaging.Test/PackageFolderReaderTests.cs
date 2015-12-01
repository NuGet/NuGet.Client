// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NuGet.Packaging.Core;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class PackageFolderReaderTests : IDisposable
    {
        [Fact]
        public void PackageFolderReader_NuspecCountOne()
        {
            // Arrange
            var workingDir = GetTempDir();

            var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                zip.AddEntry("lib/net45/a.dll", new byte[0]);
                zip.AddEntry("package.nuspec", new byte[5]);
            }

            stream.Seek(0, SeekOrigin.Begin);

            var zipFile = new ZipArchive(stream, ZipArchiveMode.Read);

            zipFile.ExtractAll(workingDir.FullName);

            var folderReader = new PackageFolderReader(workingDir.FullName);

            // Act
            using (var nuspec = folderReader.GetNuspec())
            {
                // Assert
                Assert.NotNull(nuspec);
                Assert.Equal(5, nuspec.ReadAllBytes().Count());
            }
        }

        [Fact]
        public void PackageFolderReader_NuspecCountNested()
        {
            // Arrange
            var workingDir = GetTempDir();

            var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                zip.AddEntry("lib/net45/a.dll", new byte[0]);
                zip.AddEntry("package.nuspec", new byte[5]);
                zip.AddEntry("content/package.nuspec", new byte[0]);
            }

            stream.Seek(0, SeekOrigin.Begin);

            var zipFile = new ZipArchive(stream, ZipArchiveMode.Read);

            zipFile.ExtractAll(workingDir.FullName);

            var folderReader = new PackageFolderReader(workingDir.FullName);

            // Act
            using (var nuspec = folderReader.GetNuspec())
            {
                // Assert
                Assert.NotNull(nuspec);
                Assert.Equal(5, nuspec.ReadAllBytes().Count());
            }
        }

        [Fact]
        public void PackageFolderReader_NuspecCountNestedOnly()
        {
            // Arrange
            var workingDir = GetTempDir();

            var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                zip.AddEntry("lib/net45/a.dll", new byte[0]);
                zip.AddEntry("content/package.nuspec", new byte[0]);
            }

            stream.Seek(0, SeekOrigin.Begin);

            var zipFile = new ZipArchive(stream, ZipArchiveMode.Read);

            zipFile.ExtractAll(workingDir.FullName);

            var reader = new PackageFolderReader(workingDir.FullName);

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

        [Fact]
        public void PackageFolderReader_NuspecCountMultiple()
        {
            // Arrange
            var workingDir = GetTempDir();

            var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                zip.AddEntry("lib/net45/a.dll", new byte[0]);
                zip.AddEntry("package.NUSPEC", new byte[0]);
                zip.AddEntry("package2.nuspec", new byte[0]);
            }

            stream.Seek(0, SeekOrigin.Begin);

            var zipFile = new ZipArchive(stream, ZipArchiveMode.Read);

            zipFile.ExtractAll(workingDir.FullName);

            var reader = new PackageFolderReader(workingDir.FullName);

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

        [Fact]
        public void PackageFolderReader_NuspecCountNone()
        {
            // Arrange
            var workingDir = GetTempDir();

            var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                zip.AddEntry("lib/net45/a.dll", new byte[0]);
            }

            stream.Seek(0, SeekOrigin.Begin);

            var zipFile = new ZipArchive(stream, ZipArchiveMode.Read);

            zipFile.ExtractAll(workingDir.FullName);

            var reader = new PackageFolderReader(workingDir.FullName);

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

        [Fact]
        public void PackageFolderReader_NuspecCountNoneInvalidEnding()
        {
            // Arrange
            var workingDir = GetTempDir();

            var stream = new MemoryStream();
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

            zipFile.ExtractAll(workingDir.FullName);

            var reader = new PackageFolderReader(workingDir.FullName);

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

        [Fact]
        public void PackageFolderReader_NuspecCountEscapingInName()
        {
            // Arrange
            var workingDir = GetTempDir();

            var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                zip.AddEntry("lib/net45/a.dll", new byte[0]);
                zip.AddEntry("package%20.nuspec", new byte[5]);
            }

            stream.Seek(0, SeekOrigin.Begin);

            var zipFile = new ZipArchive(stream, ZipArchiveMode.Read);

            zipFile.ExtractAll(workingDir.FullName);

            var reader = new PackageFolderReader(workingDir.FullName);

            // Act
            using (var nuspec = reader.GetNuspec())
            {
                // Assert
                Assert.NotNull(nuspec);
                Assert.Equal(5, nuspec.ReadAllBytes().Count());
            }
        }

        // verify a zip package reader, and folder package reader handle reference items the same
        [Fact]
        public void PackageFolderReader_Basic()
        {
            var packageNupkg = TestPackages.GetLegacyTestPackage();
            _path.Add(packageNupkg.FullName);

            var zip = new ZipArchive(packageNupkg.OpenRead());
            PackageReader zipReader = new PackageReader(zip);

            var folder = Path.Combine(packageNupkg.Directory.FullName, Guid.NewGuid().ToString());

            using (var zipFile = new ZipArchive(File.OpenRead(packageNupkg.FullName)))
            {
                zipFile.ExtractAll(folder);

                var folderReader = new PackageFolderReader(folder);

                Assert.Equal(zipReader.GetIdentity(), folderReader.GetIdentity(), new PackageIdentityComparer());

                Assert.Equal(zipReader.GetLibItems().Count(), folderReader.GetLibItems().Count());

                Assert.Equal(zipReader.GetReferenceItems().Count(), folderReader.GetReferenceItems().Count());

                Assert.Equal(zipReader.GetReferenceItems().First().Items.First(), folderReader.GetReferenceItems().First().Items.First());
            }
        }

        private DirectoryInfo GetTempDir()
        {
            var workingDir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + "/"));
            workingDir.Create();
            _path.Add(workingDir.FullName);

            return workingDir;
        }

        private ConcurrentBag<string> _path = new ConcurrentBag<string>();

        public void Dispose()
        {
            foreach (var path in _path)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }

                    if (Directory.Exists(path))
                    {
                        Directory.Delete(path, true);
                    }
                }
                catch
                {

                }
            }
        }
    }
}
