// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class PackageArchiveReaderTests
    {
        [Fact]
        public void GetReferenceItems_RespectsReferencesAccordingToDifferentFrameworks()
        {
            // Copy of the InstallPackageRespectReferencesAccordingToDifferentFrameworks functional test

            // Arrange
            using (var path = TestPackagesCore.GetNearestReferenceFilteringPackage())
            using (var zip = TestPackagesCore.GetZip(path.File))
            using (var reader = new PackageArchiveReader(zip))
            {
                // Act
                var references = reader.GetReferenceItems();
                var netResult = NuGetFrameworkUtility.GetNearest(references, NuGetFramework.Parse("net45"));
                var slResult = NuGetFrameworkUtility.GetNearest(references, NuGetFramework.Parse("sl5"));

                // Assert
                Assert.Equal(2, netResult.Items.Count());
                Assert.Equal(1, slResult.Items.Count());
                Assert.Equal("lib/sl40/a.dll", slResult.Items.First());
                Assert.Equal("lib/net40/one.dll", netResult.Items.First());
                Assert.Equal("lib/net40/three.dll", netResult.Items.Skip(1).First());
            }
        }

        [Fact]
        public void GetReferenceItems_ReturnsLegacyFolders()
        {
            // Verify legacy folder names such as 40 and 35 parse to frameworks
            using (var packageFile = TestPackagesCore.GetLegacyFolderPackage())
            {
                var zip = TestPackagesCore.GetZip(packageFile);

                using (var reader = new PackageArchiveReader(zip))
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
        }

        [Fact]
        public void GetReferenceItems_ReturnsNestedReferenceItemsMixed()
        {
            using (var packageFile = TestPackagesCore.GetLibEmptyFolderPackage())
            {
                var zip = TestPackagesCore.GetZip(packageFile);

                using (var reader = new PackageArchiveReader(zip))
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
        }

        // Verify empty target framework folders under lib are returned
        [Fact]
        public void GetReferenceItems_ReturnsEmptyLibFolder()
        {
            using (var packageFile = TestPackagesCore.GetLibEmptyFolderPackage())
            {
                var zip = TestPackagesCore.GetZip(packageFile);

                using (var reader = new PackageArchiveReader(zip))
                {
                    var groups = reader.GetReferenceItems().ToArray();

                    var emptyGroup = groups.Where(g => g.TargetFramework == NuGetFramework.ParseFolder("net45")).Single();

                    Assert.Equal(0, emptyGroup.Items.Count());
                }
            }
        }

        [Fact]
        public void GetReferenceItems_ReturnsNestedReferenceItems()
        {
            using (var packageFile = TestPackagesCore.GetLibSubFolderPackage())
            {
                var zip = TestPackagesCore.GetZip(packageFile);

                using (var reader = new PackageArchiveReader(zip))
                {
                    var groups = reader.GetReferenceItems().ToArray();

                    Assert.Equal(1, groups.Count());

                    Assert.Equal(NuGetFramework.Parse("net40"), groups[0].TargetFramework);
                    Assert.Equal(2, groups[0].Items.Count());
                    Assert.Equal("lib/net40/test40.dll", groups[0].Items.ToArray()[0]);
                    Assert.Equal("lib/net40/x86/testx86.dll", groups[0].Items.ToArray()[1]);
                }
            }
        }

        [Theory]
        [InlineData("3.0.5-beta", "3.0.5-beta")]
        [InlineData("2.5", "2.5.0")]
        [InlineData("2.5-beta", "2.5.0-beta")]
        public void GetMinClientVersion_ReturnsNormalizedString(string minClientVersion, string expected)
        {
            using (var packageFile = TestPackagesCore.GetLegacyTestPackageMinClient(minClientVersion))
            {
                var zip = TestPackagesCore.GetZip(packageFile);

                using (var reader = new PackageArchiveReader(zip))
                {
                    var version = reader.GetMinClientVersion();

                    Assert.Equal(expected, version.ToNormalizedString());
                }
            }
        }

        [Fact]
        public void GetContentItems_ReturnsContentWithMixedFrameworks()
        {
            using (var packageFile = TestPackagesCore.GetLegacyContentPackageMixed())
            {
                var zip = TestPackagesCore.GetZip(packageFile);

                using (var reader = new PackageArchiveReader(zip))
                {
                    var groups = reader.GetContentItems().ToArray();

                    Assert.Equal(3, groups.Count());
                }
            }
        }

        [Fact]
        public void GetContentItems_ReturnsContentWithFrameworks()
        {
            using (var packageFile = TestPackagesCore.GetLegacyContentPackageWithFrameworks())
            {
                var zip = TestPackagesCore.GetZip(packageFile);

                using (var reader = new PackageArchiveReader(zip))
                {
                    var groups = reader.GetContentItems().ToArray();

                    Assert.Equal(3, groups.Count());
                }
            }
        }

        [Fact]
        public void GetContentItems_ReturnsContentWithNoFrameworks()
        {
            using (var packageFile = TestPackagesCore.GetLegacyContentPackage())
            {
                var zip = TestPackagesCore.GetZip(packageFile);

                using (var reader = new PackageArchiveReader(zip))
                {
                    var groups = reader.GetContentItems().ToArray();

                    Assert.Equal(1, groups.Count());

                    Assert.Equal(NuGetFramework.AnyFramework, groups.Single().TargetFramework);

                    Assert.Equal(3, groups.Single().Items.Count());
                }
            }
        }

        // get reference items without any nuspec entries
        [Fact]
        public void GetReferenceItems_ReturnsItemsWithNoNuspecEntries()
        {
            using (var packageFile = TestPackagesCore.GetLegacyTestPackage())
            {
                var zip = TestPackagesCore.GetZip(packageFile);

                using (var reader = new PackageArchiveReader(zip))
                {
                    var groups = reader.GetReferenceItems().ToArray();

                    Assert.Equal(3, groups.Count());

                    Assert.Equal(4, groups.SelectMany(e => e.Items).Count());
                }
            }
        }

        // normal reference group filtering
        [Fact]
        public void GetReferenceItems_ReturnsReferencesWithGroups()
        {
            using (var packageFile = TestPackagesCore.GetLegacyTestPackageWithReferenceGroups())
            {
                var zip = TestPackagesCore.GetZip(packageFile);

                using (var reader = new PackageArchiveReader(zip))
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
        }

        // v1.5 reference flat list applied to a 2.5+ nupkg with frameworks
        [Fact]
        public void GetReferenceItems_ReturnsReferencesWithoutGroups()
        {
            using (var packageFile = TestPackagesCore.GetLegacyTestPackageWithPre25References())
            {
                var zip = TestPackagesCore.GetZip(packageFile);

                using (var reader = new PackageArchiveReader(zip))
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
        }

        [Fact]
        public void GetSupportedFrameworks_ReturnsSupportedFrameworks()
        {
            using (var packageFile = TestPackagesCore.GetLegacyTestPackage())
            {
                var zip = TestPackagesCore.GetZip(packageFile);

                using (var reader = new PackageArchiveReader(zip))
                {
                    var frameworks = reader.GetSupportedFrameworks().Select(f => f.DotNetFrameworkName).ToArray();

                    Assert.Equal("Any,Version=v0.0", frameworks[0]);
                    Assert.Equal(".NETFramework,Version=v4.0", frameworks[1]);
                    Assert.Equal(".NETFramework,Version=v4.5", frameworks[2]);
                    Assert.Equal(3, frameworks.Length);
                }
            }
        }

        [Fact]
        public void IsServiceable_ReturnsTrueForServiceablePackage()
        {
            // Arrange
            using (var packageFile = TestPackagesCore.GetServiceablePackage())
            {
                var zip = TestPackagesCore.GetZip(packageFile);

                using (var reader = new PackageArchiveReader(zip))
                {
                    // Act
                    var actual = reader.IsServiceable();

                    // Assert
                    Assert.True(actual);
                }
            }
        }

        [Fact]
        public void GetPackageTypes_ReturnsMultiplePackageTypes()
        {
            // Arrange
            using (var packageFile = TestPackagesCore.GetPackageWithPackageTypes())
            {
                var zip = TestPackagesCore.GetZip(packageFile);

                using (var reader = new PackageArchiveReader(zip))
                {
                    // Act
                    var actual = reader.GetPackageTypes();

                    // Assert
                    Assert.Equal(2, actual.Count);
                    Assert.Equal("foo", actual[0].Name);
                    Assert.Equal(new Version(0, 0), actual[0].Version);
                    Assert.Equal("bar", actual[1].Name);
                    Assert.Equal(new Version(2, 0, 0), actual[1].Version);
                }
            }
        }

        [Fact]
        public void GetSupportedFrameworks_ThrowsForSupportedFrameworksForInvalidPortableFramework()
        {
            using (var packageFile = TestPackagesCore.GetLegacyTestPackageWithInvalidPortableFrameworkFolderName())
            {
                var zip = TestPackagesCore.GetZip(packageFile);

                using (var reader = new PackageArchiveReader(zip))
                {
                    var ex = Assert.Throws<PackagingException>(
                        () => reader.GetSupportedFrameworks());
                    Assert.Equal(
                        "The framework in the folder name of '" +
                        "lib/portable-net+win+wpa+wp+sl+net-cf+netmf+MonoAndroid+MonoTouch+Xamarin.iOS/test.dll" +
                        "' in package 'packageA.2.0.3' could not be parsed.",
                        ex.Message);
                    Assert.NotNull(ex.InnerException);
                    Assert.IsType<ArgumentException>(ex.InnerException);
                    Assert.Equal(
                        "Invalid portable frameworks '" +
                        "net+win+wpa+wp+sl+net-cf+netmf+MonoAndroid+MonoTouch+Xamarin.iOS" +
                        "'. A hyphen may not be in any of the portable framework names.",
                        ex.InnerException.Message);
                }
            }
        }

        [Fact]
        public void GetIdentity_ReturnsPackageIdentity()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreReaderTestPackage()))
            {
                var identity = test.Reader.GetIdentity();

                Assert.NotNull(identity);
                Assert.Equal("Aa", identity.Id);
                Assert.Equal("4.5.6", identity.Version.ToNormalizedString());
            }
        }

        [Fact]
        public async Task GetIdentityAsync_ReturnsPackageIdentity()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreReaderTestPackage()))
            {
                var identity = await test.Reader.GetIdentityAsync(CancellationToken.None);

                Assert.NotNull(identity);
                Assert.Equal("Aa", identity.Id);
                Assert.Equal("4.5.6", identity.Version.ToNormalizedString());
            }
        }

        [Fact]
        public void GetMinClientVersion_ReturnsNullIfNoMinClientVersion()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreAndContentReaderMinimalTestPackage()))
            {
                var version = test.Reader.GetMinClientVersion();

                Assert.Null(version);
            }
        }

        [Fact]
        public async Task GetMinClientVersionAsync_ReturnsNullIfNoMinClientVersion()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreAndContentReaderMinimalTestPackage()))
            {
                var version = await test.Reader.GetMinClientVersionAsync(CancellationToken.None);

                Assert.Null(version);
            }
        }

        [Fact]
        public void GetMinClientVersion_ReturnsMinClientVersion()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreReaderTestPackage()))
            {
                var version = test.Reader.GetMinClientVersion();

                Assert.NotNull(version);
                Assert.Equal("1.2.3", version.ToNormalizedString());
            }
        }

        [Fact]
        public async Task GetMinClientVersionAsync_ReturnsMinClientVersion()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreReaderTestPackage()))
            {
                var version = await test.Reader.GetMinClientVersionAsync(CancellationToken.None);

                Assert.NotNull(version);
                Assert.Equal("1.2.3", version.ToNormalizedString());
            }
        }

        [Fact]
        public void GetPackageTypes_ReturnsEmptyEnumerableIfNoPackageTypes()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreAndContentReaderMinimalTestPackage()))
            {
                var packageTypes = test.Reader.GetPackageTypes();

                Assert.Empty(packageTypes);
            }
        }

        [Fact]
        public async Task GetPackageTypesAsync_ReturnsEmptyEnumerableIfNoPackageTypes()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreAndContentReaderMinimalTestPackage()))
            {
                var packageTypes = await test.Reader.GetPackageTypesAsync(CancellationToken.None);

                Assert.Empty(packageTypes);
            }
        }

        [Fact]
        public void GetPackageTypes_ReturnsPackageTypes()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreReaderTestPackage()))
            {
                var packageTypes = test.Reader.GetPackageTypes();

                Assert.NotEmpty(packageTypes);
                Assert.Equal(2, packageTypes.Count);
                Assert.Equal("Bb", packageTypes[0].Name);
                Assert.Equal("0.0", packageTypes[0].Version.ToString());
                Assert.Equal("Cc", packageTypes[1].Name);
                Assert.Equal("7.8.9", packageTypes[1].Version.ToString());
            }
        }

        [Fact]
        public async Task GetPackageTypesAsync_ReturnsPackageTypes()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreReaderTestPackage()))
            {
                var packageTypes = await test.Reader.GetPackageTypesAsync(CancellationToken.None);

                Assert.NotEmpty(packageTypes);
                Assert.Equal(2, packageTypes.Count);
                Assert.Equal("Bb", packageTypes[0].Name);
                Assert.Equal("0.0", packageTypes[0].Version.ToString());
                Assert.Equal("Cc", packageTypes[1].Name);
                Assert.Equal("7.8.9", packageTypes[1].Version.ToString());
            }
        }

        [Fact]
        public void GetStream_ReturnsReadableStream()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreReaderTestPackage()))
            {
                var stream = test.Reader.GetStream("Aa.nuspec");

                Assert.NotNull(stream);
                Assert.True(stream.CanRead);
            }
        }

        [Fact]
        public async Task GetStreamAsync_ReturnsReadableStream()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreReaderTestPackage()))
            {
                var stream = await test.Reader.GetStreamAsync("Aa.nuspec", CancellationToken.None);

                Assert.NotNull(stream);
                Assert.True(stream.CanRead);
            }
        }

        [Fact]
        public void GetFiles_ReturnsFiles()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreReaderTestPackage()))
            {
                var files = test.Reader.GetFiles();

                Assert.NotNull(files);
                Assert.Equal(3, files.Count());
            }
        }

        [Fact]
        public async Task GetFilesAsync_ReturnsFiles()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreReaderTestPackage()))
            {
                var files = (await test.Reader.GetFilesAsync(CancellationToken.None))
                    .OrderBy(file => file)
                    .ToArray();

                Assert.NotNull(files);
                Assert.Equal(3, files.Length);
                Assert.Equal("Aa.nuspec", files[0]);
                Assert.Equal("lib/net45/a.dll", files[1]);
                Assert.Equal("lib/net45/b.dll", files[2]);
            }
        }

        [Fact]
        public void GetFiles_WithFolder_ReturnsEmptyEnumerableIfNoFiles()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreAndContentReaderMinimalTestPackage()))
            {
                var files = test.Reader.GetFiles("lib");

                Assert.Empty(files);
            }
        }

        [Fact]
        public async Task GetFilesAsync_WithFolder_ReturnsEmptyEnumerableIfNoFiles()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreAndContentReaderMinimalTestPackage()))
            {
                var files = await test.Reader.GetFilesAsync("lib", CancellationToken.None);

                Assert.Empty(files);
            }
        }

        [Fact]
        public void GetFiles_WithFolder_ReturnsFiles()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreReaderTestPackage()))
            {
                var files = test.Reader.GetFiles("lib");

                Assert.NotNull(files);
                Assert.Equal(2, files.Count());
            }
        }

        [Fact]
        public async Task GetFilesAsync_WithFolder_ReturnsFiles()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreReaderTestPackage()))
            {
                var files = (await test.Reader.GetFilesAsync("lib", CancellationToken.None))
                    .OrderBy(file => file)
                    .ToArray();

                Assert.NotNull(files);
                Assert.Equal(2, files.Length);
                Assert.Equal("lib/net45/a.dll", files[0]);
                Assert.Equal("lib/net45/b.dll", files[1]);
            }
        }

        [Fact]
        public void GetNuspec_ReturnsReadableStream()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreReaderTestPackage()))
            {
                var stream = test.Reader.GetNuspec();

                Assert.NotNull(stream);
                Assert.True(stream.CanRead);
            }
        }

        [Fact]
        public async Task GetNuspecAsync_ReturnsReadableStream()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreReaderTestPackage()))
            {
                var stream = await test.Reader.GetNuspecAsync(CancellationToken.None);

                Assert.NotNull(stream);
                Assert.True(stream.CanRead);
            }
        }

        [Fact]
        public void GetNuspec_ReturnsStream()
        {
            // Arrange
            using (var stream = new MemoryStream())
            {
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    zip.AddEntry("lib/net45/a.dll", new byte[0]);
                    zip.AddEntry("package.nuspec", new byte[5]);
                }

                stream.Seek(0, SeekOrigin.Begin);

                using (var reader = new PackageArchiveReader(stream))
                {
                    // Act
                    using (var nuspec = new BinaryReader(reader.GetNuspec()))
                    {
                        // Assert
                        Assert.NotNull(nuspec);
                        Assert.Equal(5, nuspec.ReadBytes(4096).Length);
                    }
                }
            }
        }

        [Fact]
        public void GetNuspec_ReturnsStreamForRootNuspec()
        {
            // Arrange
            using (var stream = new MemoryStream())
            {
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    zip.AddEntry("lib/net45/a.dll", new byte[0]);
                    zip.AddEntry("package.nuspec", new byte[5]);
                    zip.AddEntry("content/package.nuspec", new byte[0]);
                }

                // Act
                using (var reader = new PackageArchiveReader(stream))
                using (var nuspec = new BinaryReader(reader.GetNuspec()))
                {
                    // Assert
                    Assert.NotNull(nuspec);
                    Assert.Equal(5, nuspec.ReadBytes(4096).Length);
                }
            }
        }

        [Fact]
        public void GetNuspec_ThrowsForNoRootNuspec()
        {
            // Arrange
            using (var stream = new MemoryStream())
            {
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    zip.AddEntry("lib/net45/a.dll", new byte[0]);
                    zip.AddEntry("content/package.nuspec", new byte[0]);
                }

                using (var reader = new PackageArchiveReader(stream))
                {
                    // Act
                    var exception = Assert.Throws<PackagingException>(() => reader.GetNuspec());

                    // Assert
                    Assert.Equal("Nuspec file does not exist in package.", exception.Message);
                }
            }
        }

        [Fact]
        public void GetNuspec_ThrowsForMultipleRootNuspecs()
        {
            // Arrange
            using (var stream = new MemoryStream())
            {
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    zip.AddEntry("lib/net45/a.dll", new byte[0]);
                    zip.AddEntry("package.NUSPEC", new byte[0]);
                    zip.AddEntry("package2.nuspec", new byte[0]);
                }

                using (var reader = new PackageArchiveReader(stream))
                {
                    // Act
                    var exception = Assert.Throws<PackagingException>(() => reader.GetNuspec());

                    // Assert
                    Assert.Equal("Package contains multiple nuspec files.", exception.Message);
                }
            }
        }

        [Fact]
        public void GetNuspec_ThrowsForNoNuspec()
        {
            // Arrange
            using (var stream = new MemoryStream())
            {
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    zip.AddEntry("lib/net45/a.dll", new byte[0]);
                }

                using (var reader = new PackageArchiveReader(stream))
                {
                    // Act
                    var exception = Assert.Throws<PackagingException>(() => reader.GetNuspec());

                    // Assert
                    Assert.Equal("Nuspec file does not exist in package.", exception.Message);
                }
            }
        }

        [Fact]
        public void GetNuspec_ThrowsForNoNuspecWithCorrectExtension()
        {
            // Arrange
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

                using (var reader = new PackageArchiveReader(stream))
                {
                    // Act
                    var exception = Assert.Throws<PackagingException>(() => reader.GetNuspec());

                    // Assert
                    Assert.Equal("Nuspec file does not exist in package.", exception.Message);
                }
            }
        }

        [Fact]
        public void GetNuspec_SupportsEscapingInFileName()
        {
            // Arrange
            using (var stream = new MemoryStream())
            {
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    zip.AddEntry("lib/net45/a.dll", new byte[0]);
                    zip.AddEntry("package%20.nuspec", new byte[5]);
                }

                using (var reader = new PackageArchiveReader(stream))
                {
                    // Act
                    var nuspec = new BinaryReader(reader.GetNuspec());

                    // Assert
                    Assert.NotNull(nuspec);
                    Assert.Equal(5, nuspec.ReadBytes(4096).Length);
                }
            }
        }

        [Fact]
        public void GetNuspecFile_ReturnsNuspecPathInPackage()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreReaderTestPackage()))
            {
                var filePath = test.Reader.GetNuspecFile();

                Assert.Equal("Aa.nuspec", filePath);
            }
        }

        [Fact]
        public async Task GetNuspecAsyncFile_ReturnsNuspecPathInPackage()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreReaderTestPackage()))
            {
                var filePath = await test.Reader.GetNuspecFileAsync(CancellationToken.None);

                Assert.Equal("Aa.nuspec", filePath);
            }
        }

        [Fact]
        public void CopyFiles_ReturnsCopiedFilePaths()
        {
            using (var testDirectory = TestDirectory.Create())
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreReaderTestPackage()))
            {
                var files = test.Reader.CopyFiles(
                    testDirectory.Path,
                    new[] { "Aa.nuspec" },
                    ExtractFile,
                    NullLogger.Instance,
                    CancellationToken.None);

                var expectedFilePath = Path.Combine(testDirectory.Path, "Aa.nuspec");

                Assert.Equal(1, files.Count());
                Assert.Equal(expectedFilePath, files.Single());
                Assert.True(File.Exists(expectedFilePath));
            }
        }

        [Fact]
        public async Task CopyFilesAsync_ReturnsCopiedFilePaths()
        {
            using (var testDirectory = TestDirectory.Create())
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreReaderTestPackage()))
            {
                var files = await test.Reader.CopyFilesAsync(
                    testDirectory.Path,
                    new[] { "Aa.nuspec" },
                    ExtractFile,
                    NullLogger.Instance,
                    CancellationToken.None);

                var expectedFilePath = Path.Combine(testDirectory.Path, "Aa.nuspec");

                Assert.Equal(1, files.Count());
                Assert.Equal(expectedFilePath, files.Single());
                Assert.True(File.Exists(expectedFilePath));
            }
        }

        [Fact]
        public void GetFrameworkItems_ReturnsEmptyEnumerableIfNoFrameworkItems()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreAndContentReaderMinimalTestPackage()))
            {
                var groups = test.Reader.GetFrameworkItems();

                Assert.Empty(groups);
            }
        }

        [Fact]
        public void GetFrameworkItems_ReturnsFrameworkItems()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageContentReaderTestPackage()))
            {
                var groups = test.Reader.GetFrameworkItems().ToArray();

                Assert.Equal(2, groups.Length);
                Assert.Equal(new[] { "Z" }, groups[0].Items);
                Assert.Equal(".NETFramework,Version=v4.0", groups[0].TargetFramework.DotNetFrameworkName);
                Assert.Equal(new[] { "Y" }, groups[1].Items);
                Assert.Equal("Silverlight,Version=v3.0", groups[1].TargetFramework.DotNetFrameworkName);
            }
        }

        [Fact]
        public async Task GetFrameworkItemsAsync_ReturnsEmptyEnumerableIfNoFrameworkItems()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreAndContentReaderMinimalTestPackage()))
            {
                var groups = await test.Reader.GetFrameworkItemsAsync(CancellationToken.None);

                Assert.Empty(groups);
            }
        }

        [Fact]
        public async Task GetFrameworkItemsAsync_ReturnsFrameworkItems()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageContentReaderTestPackage()))
            {
                var groups = (await test.Reader.GetFrameworkItemsAsync(CancellationToken.None)).ToArray();

                Assert.Equal(2, groups.Length);
                Assert.Equal(new[] { "Z" }, groups[0].Items);
                Assert.Equal(".NETFramework,Version=v4.0", groups[0].TargetFramework.DotNetFrameworkName);
                Assert.Equal(new[] { "Y" }, groups[1].Items);
                Assert.Equal("Silverlight,Version=v3.0", groups[1].TargetFramework.DotNetFrameworkName);
            }
        }

        [Fact]
        public void GetBuildItems_ReturnsEmptyEnumerableIfNoBuildItems()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreAndContentReaderMinimalTestPackage()))
            {
                var groups = test.Reader.GetBuildItems();

                Assert.Empty(groups);
            }
        }

        [Fact]
        public void GetBuildItems_ReturnsBuildItems()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageContentReaderTestPackage()))
            {
                var groups = test.Reader.GetBuildItems().ToArray();

                Assert.Equal(1, groups.Length);
                Assert.Equal(new[] { "build/net45/a.props", "build/net45/a.targets" }, groups[0].Items);
                Assert.Equal(".NETFramework,Version=v4.5", groups[0].TargetFramework.DotNetFrameworkName);
            }
        }

        [Fact]
        public async Task GetBuildItemsAsync_EmptyEnumerableIfNoBuildItems()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreAndContentReaderMinimalTestPackage()))
            {
                var groups = await test.Reader.GetBuildItemsAsync(CancellationToken.None);

                Assert.Empty(groups);
            }
        }

        [Fact]
        public async Task GetBuildItemsAsync_ReturnsBuildItems()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageContentReaderTestPackage()))
            {
                var groups = (await test.Reader.GetBuildItemsAsync(CancellationToken.None)).ToArray();

                Assert.Equal(1, groups.Length);
                Assert.Equal(new[] { "build/net45/a.props", "build/net45/a.targets" }, groups[0].Items);
                Assert.Equal(".NETFramework,Version=v4.5", groups[0].TargetFramework.DotNetFrameworkName);
            }
        }

        [Fact]
        public void GetToolItems_ReturnsEmptyEnumerableIfNoToolItems()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreAndContentReaderMinimalTestPackage()))
            {
                var groups = test.Reader.GetToolItems();

                Assert.Empty(groups);
            }
        }

        [Fact]
        public void GetToolItems_ReturnsToolItems()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageContentReaderTestPackage()))
            {
                var groups = test.Reader.GetToolItems().ToArray();

                Assert.Equal(1, groups.Length);
                Assert.Equal(new[] { "tools/net45/j", "tools/net45/k" }, groups[0].Items);
                Assert.Equal(".NETFramework,Version=v4.5", groups[0].TargetFramework.DotNetFrameworkName);
            }
        }

        [Fact]
        public async Task GetToolItemsAsync_EmptyEnumerableIfNoToolItems()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreAndContentReaderMinimalTestPackage()))
            {
                var groups = await test.Reader.GetToolItemsAsync(CancellationToken.None);

                Assert.Empty(groups);
            }
        }

        [Fact]
        public async Task GetToolItemsAsync_ReturnsToolItems()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageContentReaderTestPackage()))
            {
                var groups = (await test.Reader.GetToolItemsAsync(CancellationToken.None)).ToArray();

                Assert.Equal(1, groups.Length);
                Assert.Equal(new[] { "tools/net45/j", "tools/net45/k" }, groups[0].Items);
                Assert.Equal(".NETFramework,Version=v4.5", groups[0].TargetFramework.DotNetFrameworkName);
            }
        }

        [Fact]
        public void GetContentItems_ReturnsEmptyEnumerableIfNoContentItems()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreAndContentReaderMinimalTestPackage()))
            {
                var groups = test.Reader.GetContentItems();

                Assert.Empty(groups);
            }
        }

        [Fact]
        public void GetContentItems_ReturnsContentItems()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageContentReaderTestPackage()))
            {
                var groups = test.Reader.GetContentItems().ToArray();

                Assert.Equal(1, groups.Length);
                Assert.Equal(new[] { "content/net45/b", "content/net45/c" }, groups[0].Items);
                Assert.Equal(".NETFramework,Version=v4.5", groups[0].TargetFramework.DotNetFrameworkName);
            }
        }

        [Fact]
        public async Task GetContentItemsAsync_ReturnsEmptyEnumerableIfNoContentItems()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreAndContentReaderMinimalTestPackage()))
            {
                var groups = await test.Reader.GetContentItemsAsync(CancellationToken.None);

                Assert.Empty(groups);
            }
        }

        [Fact]
        public async Task GetContentItemsAsync_ReturnsContentItems()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageContentReaderTestPackage()))
            {
                var groups = (await test.Reader.GetContentItemsAsync(CancellationToken.None)).ToArray();

                Assert.Equal(1, groups.Length);
                Assert.Equal(new[] { "content/net45/b", "content/net45/c" }, groups[0].Items);
                Assert.Equal(".NETFramework,Version=v4.5", groups[0].TargetFramework.DotNetFrameworkName);
            }
        }

        [Fact]
        public void GetLibItems_ReturnsEmptyEnumerableIfNoLibItems()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreAndContentReaderMinimalTestPackage()))
            {
                var groups = test.Reader.GetLibItems();

                Assert.Empty(groups);
            }
        }

        [Fact]
        public void GetLibItems_ReturnsLibItems()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageContentReaderTestPackage()))
            {
                var groups = test.Reader.GetLibItems().ToArray();

                Assert.Equal(1, groups.Length);
                Assert.Equal(new[] { "lib/net45/d", "lib/net45/e", "lib/net45/f.dll", "lib/net45/g.dll" }, groups[0].Items);
                Assert.Equal(".NETFramework,Version=v4.5", groups[0].TargetFramework.DotNetFrameworkName);
            }
        }

        [Fact]
        public async Task GetLibItemsAsync_ReturnsEmptyEnumerableIfNoLibItems()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreAndContentReaderMinimalTestPackage()))
            {
                var groups = await test.Reader.GetLibItemsAsync(CancellationToken.None);

                Assert.Empty(groups);
            }
        }

        [Fact]
        public async Task GetLibItemsAsync_ReturnsLibItems()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageContentReaderTestPackage()))
            {
                var groups = (await test.Reader.GetLibItemsAsync(CancellationToken.None)).ToArray();

                Assert.Equal(1, groups.Length);
                Assert.Equal(new[] { "lib/net45/d", "lib/net45/e", "lib/net45/f.dll", "lib/net45/g.dll" }, groups[0].Items);
                Assert.Equal(".NETFramework,Version=v4.5", groups[0].TargetFramework.DotNetFrameworkName);
            }
        }

        [Fact]
        public void GetReferenceItems_ReturnsEmptyEnumerableIfNoReferenceItems()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreAndContentReaderMinimalTestPackage()))
            {
                var groups = test.Reader.GetReferenceItems();

                Assert.Empty(groups);
            }
        }

        [Fact]
        public void GetReferenceItems_ReturnsReferenceItems()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageContentReaderTestPackage()))
            {
                var groups = test.Reader.GetReferenceItems().ToArray();

                Assert.Equal(1, groups.Length);
                Assert.Equal(new[] { "lib/net45/f.dll", "lib/net45/g.dll" }, groups[0].Items);
                Assert.Equal(".NETFramework,Version=v4.5", groups[0].TargetFramework.DotNetFrameworkName);
            }
        }

        [Fact]
        public async Task GetReferenceItemsAsync_ReturnsEmptyEnumerableIfNoReferenceItems()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreAndContentReaderMinimalTestPackage()))
            {
                var groups = await test.Reader.GetReferenceItemsAsync(CancellationToken.None);

                Assert.Empty(groups);
            }
        }

        [Fact]
        public async Task GetReferenceItemsAsync_ReturnsReferenceItems()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageContentReaderTestPackage()))
            {
                var groups = (await test.Reader.GetReferenceItemsAsync(CancellationToken.None)).ToArray();

                Assert.Equal(1, groups.Length);
                Assert.Equal(new[] { "lib/net45/f.dll", "lib/net45/g.dll" }, groups[0].Items);
                Assert.Equal(".NETFramework,Version=v4.5", groups[0].TargetFramework.DotNetFrameworkName);
            }
        }

        [Fact]
        public void GetPackageDependencies_ReturnsEmptyEnumerableIfNoPackageDependencies()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreAndContentReaderMinimalTestPackage()))
            {
                var groups = test.Reader.GetPackageDependencies();

                Assert.Empty(groups);
            }
        }

        [Fact]
        public void GetPackageDependencies_ReturnsPackageDependencies()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageContentReaderTestPackage()))
            {
                var groups = test.Reader.GetPackageDependencies().ToArray();

                Assert.Equal(2, groups.Length);
                Assert.Equal(".NETFramework,Version=v4.0", groups[0].TargetFramework.DotNetFrameworkName);

                var packages = groups[0].Packages.ToArray();

                Assert.Equal(2, packages.Length);
                Assert.Equal("l", packages[0].Id);
                Assert.Equal("m", packages[1].Id);

                Assert.Empty(groups[1].Packages);
                Assert.Equal(".NETFramework,Version=v4.5", groups[1].TargetFramework.DotNetFrameworkName);
            }
        }

        [Fact]
        public async Task GetPackageDependenciesAsync_ReturnsEmptyEnumerableIfNoPackageDependencies()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreAndContentReaderMinimalTestPackage()))
            {
                var groups = await test.Reader.GetPackageDependenciesAsync(CancellationToken.None);

                Assert.Empty(groups);
            }
        }

        [Fact]
        public async Task GetPackageDependenciesAsync_ReturnsPackageDependencies()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageContentReaderTestPackage()))
            {
                var groups = (await test.Reader.GetPackageDependenciesAsync(CancellationToken.None)).ToArray();

                Assert.Equal(2, groups.Length);
                Assert.Equal(".NETFramework,Version=v4.0", groups[0].TargetFramework.DotNetFrameworkName);

                var packages = groups[0].Packages.ToArray();

                Assert.Equal(2, packages.Length);
                Assert.Equal("l", packages[0].Id);
                Assert.Equal("m", packages[1].Id);

                Assert.Empty(groups[1].Packages);
                Assert.Equal(".NETFramework,Version=v4.5", groups[1].TargetFramework.DotNetFrameworkName);
            }
        }

        [Fact]
        public void IsServiceable_ReturnsTrueIfServiceable()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetServiceablePackage()))
            {
                var isServiceable = test.Reader.IsServiceable();

                Assert.True(isServiceable);
            }
        }

        [Fact]
        public async Task IsServiceableAsync_ReturnsTrueIfServiceable()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetServiceablePackage()))
            {
                var isServiceable = await test.Reader.IsServiceableAsync(CancellationToken.None);

                Assert.True(isServiceable);
            }
        }

        [Fact]
        public void IsServiceable_ReturnsFalseIfNotServiceable()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreAndContentReaderMinimalTestPackage()))
            {
                var isServiceable = test.Reader.IsServiceable();

                Assert.False(isServiceable);
            }
        }

        [Fact]
        public async Task IsServiceableAsync_ReturnsFalseIfNotServiceable()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreAndContentReaderMinimalTestPackage()))
            {
                var isServiceable = await test.Reader.IsServiceableAsync(CancellationToken.None);

                Assert.False(isServiceable);
            }
        }

        [Fact]
        public void GetItems_ReturnsEmptyEnumerableIfNoItems()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreAndContentReaderMinimalTestPackage()))
            {
                var groups = test.Reader.GetItems("other");

                Assert.Empty(groups);
            }
        }

        [Fact]
        public async Task GetItemsAsync_ReturnsEmptyEnumerableIfNoItems()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreAndContentReaderMinimalTestPackage()))
            {
                var groups = await test.Reader.GetItemsAsync("other", CancellationToken.None);

                Assert.Empty(groups);
            }
        }

        [Fact]
        public void GetItems_ReturnsItems()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageContentReaderTestPackage()))
            {
                var groups = test.Reader.GetItems("other").ToArray();

                Assert.Equal(1, groups.Length);
                Assert.Equal(new[] { "other/net45/h", "other/net45/i" }, groups[0].Items);
                Assert.Equal(".NETFramework,Version=v4.5", groups[0].TargetFramework.DotNetFrameworkName);
            }
        }

        [Fact]
        public async Task GetItemsAsync_ReturnsItems()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageContentReaderTestPackage()))
            {
                var groups = (await test.Reader.GetItemsAsync("other", CancellationToken.None)).ToArray();

                Assert.Equal(1, groups.Length);
                Assert.Equal(new[] { "other/net45/h", "other/net45/i" }, groups[0].Items);
                Assert.Equal(".NETFramework,Version=v4.5", groups[0].TargetFramework.DotNetFrameworkName);
            }
        }

        [Fact]
        public void GetDevelopmentDependency_ReturnsTrueIfDevelopmentDependency()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetDevelopmentDependencyPackage()))
            {
                var isDevelopmentDependency = test.Reader.GetDevelopmentDependency();

                Assert.True(isDevelopmentDependency);
            }
        }

        [Fact]
        public async Task GetDevelopmentDependencyAsync_ReturnsTrueIfDevelopmentDependency()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetDevelopmentDependencyPackage()))
            {
                var isDevelopmentDependency = await test.Reader.GetDevelopmentDependencyAsync(CancellationToken.None);

                Assert.True(isDevelopmentDependency);
            }
        }

        [Fact]
        public void GetDevelopmentDependency_ReturnsFalseIfNotDevelopmentDependency()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreAndContentReaderMinimalTestPackage()))
            {
                var isDevelopmentDependency = test.Reader.GetDevelopmentDependency();

                Assert.False(isDevelopmentDependency);
            }
        }

        [Fact]
        public async Task GetDevelopmentDependencyAsync_ReturnsFalseIfNotDevelopmentDependency()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreAndContentReaderMinimalTestPackage()))
            {
                var isDevelopmentDependency = await test.Reader.GetDevelopmentDependencyAsync(CancellationToken.None);

                Assert.False(isDevelopmentDependency);
            }
        }

        [Fact]
        public void NuspecReader_ReturnsNuspecReader()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreReaderTestPackage()))
            {
                var nuspecReader = test.Reader.NuspecReader;

                Assert.NotNull(nuspecReader);
            }
        }

        [Fact]
        public async Task GetNuspecReaderAsync_ReturnsNuspecReader()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreReaderTestPackage()))
            {
                var nuspecReader = await test.Reader.GetNuspecReaderAsync(CancellationToken.None);

                Assert.NotNull(nuspecReader);
            }
        }

        [Fact]
        public async Task CopyNupkgAsync_Throws()
        {
            using (var test = PackageReaderTest.Create(TestPackagesCore.GetPackageCoreReaderTestPackage()))
            {
                await Assert.ThrowsAsync<NotImplementedException>(
                    () => test.Reader.CopyNupkgAsync(
                        nupkgFilePath: "a",
                        cancellationToken: CancellationToken.None));
            }
        }

        private static string ExtractFile(string sourcePath, string targetPath, Stream sourceStream)
        {
            using (var targetStream = File.OpenWrite(targetPath))
            {
                sourceStream.CopyTo(targetStream);
            }

            return targetPath;
        }

        // For testing the following implementations:
        //      IPackageCoreReader
        //      IAsyncPackageCoreReader
        //      IPackageContentReader
        //      IAsyncPackageContentReader
        private sealed class PackageReaderTest : IDisposable
        {
            private bool _isDisposed;
            private readonly TestPackagesCore.TempFile _tempFile;

            internal PackageArchiveReader Reader { get; }

            private PackageReaderTest(PackageArchiveReader reader, TestPackagesCore.TempFile tempFile)
            {
                Reader = reader;
                _tempFile = tempFile;
            }

            internal static PackageReaderTest Create(TestPackagesCore.TempFile tempFile)
            {
                var zip = TestPackagesCore.GetZip(tempFile);
                var reader = new PackageArchiveReader(zip);

                return new PackageReaderTest(reader, tempFile);
            }

            public void Dispose()
            {
                if (!_isDisposed)
                {
                    Reader.Dispose();
                    _tempFile.Dispose();

                    GC.SuppressFinalize(this);

                    _isDisposed = true;
                }
            }
        }
    }
}