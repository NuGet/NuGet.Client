// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class PackageReaderTests : IDisposable
    {
        [Fact]
        public void PackageReader_NuspecCountOne()
        {
            // Arrange
            var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                zip.AddEntry("lib/net45/a.dll", new byte[0]);
                zip.AddEntry("package.nuspec", new byte[5]);
            }

            stream.Seek(0, SeekOrigin.Begin);
            var reader = new PackageReader(stream);

            // Act
            var nuspec = reader.GetNuspec();

            // Assert
            Assert.NotNull(nuspec);
            Assert.Equal(5, nuspec.ReadAllBytes().Count());
        }

        [Fact]
        public void PackageReader_NuspecCountNested()
        {
            // Arrange
            var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                zip.AddEntry("lib/net45/a.dll", new byte[0]);
                zip.AddEntry("package.nuspec", new byte[5]);
                zip.AddEntry("content/package.nuspec", new byte[0]);
            }

            var reader = new PackageReader(stream);

            // Act
            var nuspec = reader.GetNuspec();

            // Assert
            Assert.NotNull(nuspec);
            Assert.Equal(5, nuspec.ReadAllBytes().Count());
        }

        [Fact]
        public void PackageReader_NuspecCountNestedOnly()
        {
            // Arrange
            var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                zip.AddEntry("lib/net45/a.dll", new byte[0]);
                zip.AddEntry("content/package.nuspec", new byte[0]);
            }

            var reader = new PackageReader(stream);
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
        public void PackageReader_NuspecCountMultiple()
        {
            // Arrange
            var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                zip.AddEntry("lib/net45/a.dll", new byte[0]);
                zip.AddEntry("package.NUSPEC", new byte[0]);
                zip.AddEntry("package2.nuspec", new byte[0]);
            }

            var reader = new PackageReader(stream);
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
        public void PackageReader_NuspecCountNone()
        {
            // Arrange
            var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                zip.AddEntry("lib/net45/a.dll", new byte[0]);
            }

            var reader = new PackageReader(stream);
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
        public void PackageReader_NuspecCountNoneInvalidEnding()
        {
            // Arrange
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

            var reader = new PackageReader(stream);
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
        public void PackageReader_NuspecCountEscapingInName()
        {
            // Arrange
            var stream = new MemoryStream();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                zip.AddEntry("lib/net45/a.dll", new byte[0]);
                zip.AddEntry("package%20.nuspec", new byte[5]);
            }

            var reader = new PackageReader(stream);

            // Act
            var nuspec = reader.GetNuspec();

            // Assert
            Assert.NotNull(nuspec);
            Assert.Equal(5, nuspec.ReadAllBytes().Count());
        }

        [Fact]
        public void PackageReader_RespectReferencesAccordingToDifferentFrameworks()
        {
            // Copy of the InstallPackageRespectReferencesAccordingToDifferentFrameworks functional test

            // Arrange
            using (var zipInfo = TestPackages.GetNearestReferenceFilteringPackage())
            {
                var path = zipInfo.File;
                _paths.Add(path.FullName);

                using (var zip = TestPackages.GetZip(path))
                using (var reader = new PackageReader(zip))
                {

                    // Act
                    var references = reader.GetReferenceItems();
                    var netResult = NuGetFrameworkUtility.GetNearest<FrameworkSpecificGroup>(references, NuGetFramework.Parse("net45"));
                    var slResult = NuGetFrameworkUtility.GetNearest<FrameworkSpecificGroup>(references, NuGetFramework.Parse("sl5"));

                    // Assert
                    Assert.Equal(2, netResult.Items.Count());
                    Assert.Equal(1, slResult.Items.Count());
                    Assert.Equal("lib/sl40/a.dll", slResult.Items.First());
                    Assert.Equal("lib/net40/one.dll", netResult.Items.First());
                    Assert.Equal("lib/net40/three.dll", netResult.Items.Skip(1).First());
                }
            }
        }

        [Fact]
        public void PackageReader_LegacyFolders()
        {
            // Verify legacy folder names such as 40 and 35 parse to frameworks
            var packageFile = TestPackages.GetLegacyFolderPackage();

            try
            {
                _paths.Add(packageFile.FullName);
                var zip = TestPackages.GetZip(packageFile);

                using (PackageReader reader = new PackageReader(zip))
                {
                    var groups = reader.GetReferenceItems().ToArray();

                    Assert.Equal(4, groups.Count());

                    Assert.Equal(NuGetFramework.AnyFramework, groups[0].TargetFramework);
                    Assert.Equal("lib/a.dll", groups[0].Items.ToArray()[0]);

                    Assert.Equal(NuGetFramework.Parse("net35"), groups[1].TargetFramework);
                    Assert.Equal("lib/35/b.dll", groups[1].Items.ToArray()[0]);

                    Assert.Equal(NuGetFramework.Parse("net4"), groups[2].TargetFramework);
                    Assert.Equal("lib/40/test40.dll", groups[2].Items.ToArray()[0]);
                    Assert.Equal("lib/40/x86/testx86.dll", groups[2].Items.ToArray()[1]);

                    Assert.Equal(NuGetFramework.Parse("net45"), groups[3].TargetFramework);
                    Assert.Equal("lib/45/a.dll", groups[3].Items.ToArray()[0]);
                }
            }
            finally
            {
                packageFile.Delete();
            }
        }

        [Fact]
        public void PackageReader_NestedReferenceItemsMixed()
        {
            var packageFileInfo = TestPackages.GetLibEmptyFolderPackage();

            try
            {
                _paths.Add(packageFileInfo.FullName);
                var zip = TestPackages.GetZip(packageFileInfo);

                using (PackageReader reader = new PackageReader(zip))
                {
                    var groups = reader.GetReferenceItems().ToArray();

                    Assert.Equal(3, groups.Count());

                    Assert.Equal(NuGetFramework.AnyFramework, groups[0].TargetFramework);
                    Assert.Equal(2, groups[0].Items.Count());
                    Assert.Equal("lib/a.dll", groups[0].Items.ToArray()[0]);
                    Assert.Equal("lib/x86/b.dll", groups[0].Items.ToArray()[1]);

                    Assert.Equal(NuGetFramework.Parse("net40"), groups[1].TargetFramework);
                    Assert.Equal(2, groups[1].Items.Count());
                    Assert.Equal("lib/net40/test40.dll", groups[1].Items.ToArray()[0]);
                    Assert.Equal("lib/net40/x86/testx86.dll", groups[1].Items.ToArray()[1]);

                    Assert.Equal(NuGetFramework.Parse("net45"), groups[2].TargetFramework);
                    Assert.Equal(0, groups[2].Items.Count());
                }
            }
            finally
            {
                packageFileInfo.Delete();
            }
        }

        // Verify empty target framework folders under lib are returned
        [Fact]
        public void PackageReader_EmptyLibFolder()
        {
            var packageFileInfo = TestPackages.GetLibEmptyFolderPackage();

            try
            {
                _paths.Add(packageFileInfo.FullName);
                var zip = TestPackages.GetZip(packageFileInfo);

                using (PackageReader reader = new PackageReader(zip))
                {
                    var groups = reader.GetReferenceItems().ToArray();

                    var emptyGroup = groups.Where(g => g.TargetFramework == NuGetFramework.ParseFolder("net45")).Single();

                    Assert.Equal(0, emptyGroup.Items.Count());
                }
            }
            finally
            {
                packageFileInfo.Delete();
            }
        }

        [Fact]
        public void PackageReader_NestedReferenceItems()
        {
            var packageFileInfo = TestPackages.GetLibSubFolderPackage();

            try
            {
                _paths.Add(packageFileInfo.FullName);
                var zip = TestPackages.GetZip(packageFileInfo);

                using (PackageReader reader = new PackageReader(zip))
                {
                    var groups = reader.GetReferenceItems().ToArray();

                    Assert.Equal(1, groups.Count());

                    Assert.Equal(NuGetFramework.Parse("net40"), groups[0].TargetFramework);
                    Assert.Equal(2, groups[0].Items.Count());
                    Assert.Equal("lib/net40/test40.dll", groups[0].Items.ToArray()[0]);
                    Assert.Equal("lib/net40/x86/testx86.dll", groups[0].Items.ToArray()[1]);
                }
            }
            finally
            {
                packageFileInfo.Delete();
            }
        }

        [Theory]
        [InlineData("3.0.5-beta", "3.0.5-beta")]
        [InlineData("2.5", "2.5.0")]
        [InlineData("2.5-beta", "2.5.0-beta")]
        public void PackageReader_MinClientVersion(string minClientVersion, string expected)
        {
            var packageFileInfo = TestPackages.GetLegacyTestPackageMinClient(minClientVersion);

            try
            {
                _paths.Add(packageFileInfo.FullName);
                var zip = TestPackages.GetZip(packageFileInfo);

                using (PackageReader reader = new PackageReader(zip))
                {
                    var version = reader.GetMinClientVersion();

                    Assert.Equal(expected, version.ToNormalizedString());
                }
            }
            finally
            {
                packageFileInfo.Delete();
            }
        }

        [Fact]
        public void PackageReader_ContentWithMixedFrameworks()
        {
            var packageFileInfo = TestPackages.GetLegacyContentPackageMixed();

            try
            {
                _paths.Add(packageFileInfo.FullName);
                var zip = TestPackages.GetZip(packageFileInfo);

                using (PackageReader reader = new PackageReader(zip))
                {
                    var groups = reader.GetContentItems().ToArray();

                    Assert.Equal(3, groups.Count());
                }
            }
            finally
            {
                packageFileInfo.Delete();
            }
        }

        [Fact]
        public void PackageReader_ContentWithFrameworks()
        {
            var packageFileInfo = TestPackages.GetLegacyContentPackageWithFrameworks();

            try
            {
                _paths.Add(packageFileInfo.FullName);
                var zip = TestPackages.GetZip(packageFileInfo);

                using (PackageReader reader = new PackageReader(zip))
                {
                    var groups = reader.GetContentItems().ToArray();

                    Assert.Equal(3, groups.Count());
                }
            }
            finally
            {
                packageFileInfo.Delete();
            }
        }

        [Fact]
        public void PackageReader_ContentNoFrameworks()
        {
            var packageFileInfo = TestPackages.GetLegacyContentPackage();

            try
            {
                _paths.Add(packageFileInfo.FullName);
                var zip = TestPackages.GetZip(packageFileInfo);

                using (PackageReader reader = new PackageReader(zip))
                {
                    var groups = reader.GetContentItems().ToArray();

                    Assert.Equal(1, groups.Count());

                    Assert.Equal(NuGetFramework.AnyFramework, groups.Single().TargetFramework);

                    Assert.Equal(3, groups.Single().Items.Count());
                }
            }
            finally
            {
                packageFileInfo.Delete();
            }
        }

        // get reference items without any nuspec entries
        [Fact]
        public void PackageReader_NoReferences()
        {
            var packageFileInfo = TestPackages.GetLegacyTestPackage();

            try
            {
                _paths.Add(packageFileInfo.FullName);
                var zip = TestPackages.GetZip(packageFileInfo);

                using (PackageReader reader = new PackageReader(zip))
                {
                    var groups = reader.GetReferenceItems().ToArray();

                    Assert.Equal(3, groups.Count());

                    Assert.Equal(4, groups.SelectMany(e => e.Items).Count());
                }
            }
            finally
            {
                packageFileInfo.Delete();
            }
        }

        // normal reference group filtering
        [Fact]
        public void PackageReader_ReferencesWithGroups()
        {
            var packageFileInfo = TestPackages.GetLegacyTestPackageWithReferenceGroups();

            try
            {
                _paths.Add(packageFileInfo.FullName);
                var zip = TestPackages.GetZip(packageFileInfo);

                using (PackageReader reader = new PackageReader(zip))
                {
                    var groups = reader.GetReferenceItems().ToArray();

                    Assert.Equal(2, groups.Count());

                    Assert.Equal(NuGetFramework.AnyFramework, groups[0].TargetFramework);
                    Assert.Equal(1, groups[0].Items.Count());
                    Assert.Equal("lib/test.dll", groups[0].Items.Single());

                    Assert.Equal(NuGetFramework.Parse("net45"), groups[1].TargetFramework);
                    Assert.Equal(1, groups[1].Items.Count());
                    Assert.Equal("lib/net45/test45.dll", groups[1].Items.Single());
                }
            }
            finally
            {
                packageFileInfo.Delete();
            }
        }

        // v1.5 reference flat list applied to a 2.5+ nupkg with frameworks
        [Fact]
        public void PackageReader_ReferencesWithoutGroups()
        {
            var packageFileInfo = TestPackages.GetLegacyTestPackageWithPre25References();

            try
            {
                _paths.Add(packageFileInfo.FullName);
                var zip = TestPackages.GetZip(packageFileInfo);

                using (PackageReader reader = new PackageReader(zip))
                {
                    var groups = reader.GetReferenceItems().ToArray();

                    Assert.Equal(3, groups.Count());

                    Assert.Equal(NuGetFramework.AnyFramework, groups[0].TargetFramework);
                    Assert.Equal(1, groups[0].Items.Count());
                    Assert.Equal("lib/test.dll", groups[0].Items.Single());

                    Assert.Equal(NuGetFramework.Parse("net40"), groups[1].TargetFramework);
                    Assert.Equal(1, groups[1].Items.Count());
                    Assert.Equal("lib/net40/test.dll", groups[1].Items.Single());

                    Assert.Equal(NuGetFramework.Parse("net451"), groups[2].TargetFramework);
                    Assert.Equal(1, groups[1].Items.Count());
                    Assert.Equal("lib/net451/test.dll", groups[2].Items.Single());
                }
            }
            finally
            {
                packageFileInfo.Delete();
            }
        }

        [Fact]
        public void PackageReader_SupportedFrameworks()
        {
            var packageFileInfo = TestPackages.GetLegacyTestPackage();

            try
            {
                _paths.Add(packageFileInfo.FullName);
                var zip = TestPackages.GetZip(packageFileInfo);

                using (PackageReader reader = new PackageReader(zip))
                {
                    string[] frameworks = reader.GetSupportedFrameworks().Select(f => f.DotNetFrameworkName).ToArray();

                    Assert.Equal("Any,Version=v0.0", frameworks[0]);
                    Assert.Equal(".NETFramework,Version=v4.0", frameworks[1]);
                    Assert.Equal(".NETFramework,Version=v4.5", frameworks[2]);
                    Assert.Equal(3, frameworks.Length);
                }
            }
            finally
            {
                packageFileInfo.Delete();
            }
        }

        private ConcurrentBag<string> _paths = new ConcurrentBag<string>();

        public void Dispose()
        {
            foreach (var path in _paths)
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
                catch (Exception)
                {
                    // Ignore
                }
            }
        }
    }
}
