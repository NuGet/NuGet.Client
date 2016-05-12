// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class PackageReaderTests
    {
        [Fact]
        public void PackageReader_NuspecCountOne()
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
                    var nuspec = new BinaryReader(reader.GetNuspec());

                    // Assert
                    Assert.NotNull(nuspec);
                    Assert.Equal(5, nuspec.ReadBytes(4096).Length);
                }
            }
        }

        [Fact]
        public void PackageReader_NuspecCountNested()
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
        public void PackageReader_NuspecCountNestedOnly()
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
        public void PackageReader_NuspecCountMultiple()
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
        public void PackageReader_NuspecCountNone()
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
        public void PackageReader_NuspecCountNoneInvalidEnding()
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
        public void PackageReader_NuspecCountEscapingInName()
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
        public void PackageReader_RespectReferencesAccordingToDifferentFrameworks()
        {
            // Copy of the InstallPackageRespectReferencesAccordingToDifferentFrameworks functional test

            // Arrange
            using (var path = TestPackages.GetNearestReferenceFilteringPackage())
            {
                using (var zip = TestPackages.GetZip(path.File))
                using (var reader = new PackageArchiveReader(zip))
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
            using (var packageFile = TestPackages.GetLegacyFolderPackage())
            {
                var zip = TestPackages.GetZip(packageFile);

                using (PackageArchiveReader reader = new PackageArchiveReader(zip))
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
        public void PackageReader_NestedReferenceItemsMixed()
        {
            using (var packageFile = TestPackages.GetLibEmptyFolderPackage())
            {
                var zip = TestPackages.GetZip(packageFile);

                using (PackageArchiveReader reader = new PackageArchiveReader(zip))
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
        public void PackageReader_EmptyLibFolder()
        {
            using (var packageFile = TestPackages.GetLibEmptyFolderPackage())
            {
                var zip = TestPackages.GetZip(packageFile);

                using (PackageArchiveReader reader = new PackageArchiveReader(zip))
                {
                    var groups = reader.GetReferenceItems().ToArray();

                    var emptyGroup = groups.Where(g => g.TargetFramework == NuGetFramework.ParseFolder("net45")).Single();

                    Assert.Equal(0, emptyGroup.Items.Count());
                }
            }
        }

        [Fact]
        public void PackageReader_NestedReferenceItems()
        {
            using (var packageFile = TestPackages.GetLibSubFolderPackage())
            {
                var zip = TestPackages.GetZip(packageFile);

                using (PackageArchiveReader reader = new PackageArchiveReader(zip))
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
        public void PackageReader_MinClientVersion(string minClientVersion, string expected)
        {
            using (var packageFile = TestPackages.GetLegacyTestPackageMinClient(minClientVersion))
            {
                var zip = TestPackages.GetZip(packageFile);

                using (PackageArchiveReader reader = new PackageArchiveReader(zip))
                {
                    var version = reader.GetMinClientVersion();

                    Assert.Equal(expected, version.ToNormalizedString());
                }
            }
        }

        [Fact]
        public void PackageReader_ContentWithMixedFrameworks()
        {
            using (var packageFile = TestPackages.GetLegacyContentPackageMixed())
            {
                var zip = TestPackages.GetZip(packageFile);

                using (PackageArchiveReader reader = new PackageArchiveReader(zip))
                {
                    var groups = reader.GetContentItems().ToArray();

                    Assert.Equal(3, groups.Count());
                }
            }
        }

        [Fact]
        public void PackageReader_ContentWithFrameworks()
        {
            using (var packageFile = TestPackages.GetLegacyContentPackageWithFrameworks())
            {
                var zip = TestPackages.GetZip(packageFile);

                using (PackageArchiveReader reader = new PackageArchiveReader(zip))
                {
                    var groups = reader.GetContentItems().ToArray();

                    Assert.Equal(3, groups.Count());
                }
            }
        }

        [Fact]
        public void PackageReader_ContentNoFrameworks()
        {
            using (var packageFile = TestPackages.GetLegacyContentPackage())
            {
                var zip = TestPackages.GetZip(packageFile);

                using (PackageArchiveReader reader = new PackageArchiveReader(zip))
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
        public void PackageReader_NoReferences()
        {
            using (var packageFile = TestPackages.GetLegacyTestPackage())
            {
                var zip = TestPackages.GetZip(packageFile);

                using (PackageArchiveReader reader = new PackageArchiveReader(zip))
                {
                    var groups = reader.GetReferenceItems().ToArray();

                    Assert.Equal(3, groups.Count());

                    Assert.Equal(4, groups.SelectMany(e => e.Items).Count());
                }
            }
        }

        // normal reference group filtering
        [Fact]
        public void PackageReader_ReferencesWithGroups()
        {
            using (var packageFile = TestPackages.GetLegacyTestPackageWithReferenceGroups())
            {
                var zip = TestPackages.GetZip(packageFile);

                using (PackageArchiveReader reader = new PackageArchiveReader(zip))
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
        public void PackageReader_ReferencesWithoutGroups()
        {
            using (var packageFile = TestPackages.GetLegacyTestPackageWithPre25References())
            {
                var zip = TestPackages.GetZip(packageFile);

                using (PackageArchiveReader reader = new PackageArchiveReader(zip))
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
        public void PackageReader_SupportedFrameworks()
        {
            using (var packageFile = TestPackages.GetLegacyTestPackage())
            {
                var zip = TestPackages.GetZip(packageFile);

                using (PackageArchiveReader reader = new PackageArchiveReader(zip))
                {
                    string[] frameworks = reader.GetSupportedFrameworks().Select(f => f.DotNetFrameworkName).ToArray();

                    Assert.Equal("Any,Version=v0.0", frameworks[0]);
                    Assert.Equal(".NETFramework,Version=v4.0", frameworks[1]);
                    Assert.Equal(".NETFramework,Version=v4.5", frameworks[2]);
                    Assert.Equal(3, frameworks.Length);
                }
            }
        }

        [Fact]
        public void PackageReader_PackageTypes()
        {
            // Arrange
            using (var packageFile = TestPackages.GetPackageWithPackageTypes())
            {
                var zip = TestPackages.GetZip(packageFile);

                using (PackageArchiveReader reader = new PackageArchiveReader(zip))
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
        public void PackageReader_SupportedFrameworksForInvalidPortableFrameworkThrows()
        {
            using (var packageFile = TestPackages.GetLegacyTestPackageWithInvalidPortableFrameworkFolderName())
            {
                var zip = TestPackages.GetZip(packageFile);

                using (PackageArchiveReader reader = new PackageArchiveReader(zip))
                {
                    Assert.Throws<ArgumentException>(
                        () => reader.GetSupportedFrameworks());
                }
            }
        }
    }
}
