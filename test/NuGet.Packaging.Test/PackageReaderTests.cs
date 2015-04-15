using NuGet.Frameworks;
using NuGet.Packaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Test.Utility;
using Xunit;
using Xunit.Extensions;

namespace NuGet.Packaging.Test
{
    public class PackageReaderTests
    {
        [Fact]
        public void PackageReader_LegacyFolders()
        {
            // Verify legacy folder names such as 40 and 35 parse to frameworks
            var zip = TestPackages.GetZip(TestPackages.GetLegacyFolderPackage());

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

        [Fact]
        public void PackageReader_NestedReferenceItemsMixed()
        {
            var zip = TestPackages.GetZip(TestPackages.GetLibEmptyFolderPackage());

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

        // Verify empty target framework folders under lib are returned
        [Fact]
        public void PackageReader_EmptyLibFolder()
        {
            var zip = TestPackages.GetZip(TestPackages.GetLibEmptyFolderPackage());

            using (PackageReader reader = new PackageReader(zip))
            {
                var groups = reader.GetReferenceItems().ToArray();

                var emptyGroup = groups.Where(g => g.TargetFramework == NuGetFramework.ParseFolder("net45")).Single();

                Assert.Equal(0, emptyGroup.Items.Count());
            }
        }

        [Fact]
        public void PackageReader_NestedReferenceItems()
        {
            var zip = TestPackages.GetZip(TestPackages.GetLibSubFolderPackage());

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

        [Theory]
        [InlineData("3.0.5-beta", "3.0.5-beta")]
        [InlineData("2.5", "2.5.0")]
        [InlineData("2.5-beta", "2.5.0-beta")]
        public void PackageReader_MinClientVersion(string minClientVersion, string expected)
        {
            var zip = TestPackages.GetZip(TestPackages.GetLegacyTestPackageMinClient(minClientVersion));

            using (PackageReader reader = new PackageReader(zip))
            {
                var version = reader.GetMinClientVersion();

                Assert.Equal(expected, version.ToNormalizedString());
            }
        }
        

        [Fact]
        public void PackageReader_ContentWithMixedFrameworks()
        {
            var zip = TestPackages.GetZip(TestPackages.GetLegacyContentPackageMixed());

            using (PackageReader reader = new PackageReader(zip))
            {
                var groups = reader.GetContentItems().ToArray();

                Assert.Equal(3, groups.Count());
            }
        }

        [Fact]
        public void PackageReader_ContentWithFrameworks()
        {
            var zip = TestPackages.GetZip(TestPackages.GetLegacyContentPackageWithFrameworks());

            using (PackageReader reader = new PackageReader(zip))
            {
                var groups = reader.GetContentItems().ToArray();

                Assert.Equal(3, groups.Count());
            }
        }

        [Fact]
        public void PackageReader_ContentNoFrameworks()
        {
            var zip = TestPackages.GetZip(TestPackages.GetLegacyContentPackage());

            using (PackageReader reader = new PackageReader(zip))
            {
                var groups = reader.GetContentItems().ToArray();

                Assert.Equal(1, groups.Count());

                Assert.Equal(NuGetFramework.AnyFramework, groups.Single().TargetFramework);

                Assert.Equal(3, groups.Single().Items.Count());
            }
        }

        // get reference items without any nuspec entries
        [Fact]
        public void PackageReader_NoReferences()
        {
            var zip = TestPackages.GetZip(TestPackages.GetLegacyTestPackage());

            using (PackageReader reader = new PackageReader(zip))
            {
                var groups = reader.GetReferenceItems().ToArray();

                Assert.Equal(3, groups.Count());

                Assert.Equal(4, groups.SelectMany(e => e.Items).Count());
            }
        }

        // normal reference group filtering
        [Fact]
        public void PackageReader_ReferencesWithGroups()
        {
            var zip = TestPackages.GetZip(TestPackages.GetLegacyTestPackageWithReferenceGroups());

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

        // v1.5 reference flat list applied to a 2.5+ nupkg with frameworks
        [Fact]
        public void PackageReader_ReferencesWithoutGroups()
        {
            var zip = TestPackages.GetZip(TestPackages.GetLegacyTestPackageWithPre25References());

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

        [Fact]
        public void PackageReader_SupportedFrameworks()
        {
            var zip = TestPackages.GetZip(TestPackages.GetLegacyTestPackage());

            using (PackageReader reader = new PackageReader(zip))
            {
                string[] frameworks = reader.GetSupportedFrameworks().Select(f => f.DotNetFrameworkName).ToArray();

                Assert.Equal("Any,Version=v0.0", frameworks[0]);
                Assert.Equal(".NETFramework,Version=v4.0", frameworks[1]);
                Assert.Equal(".NETFramework,Version=v4.5", frameworks[2]);
                Assert.Equal(3, frameworks.Length);
            }
        }

        //[Fact]
        //public void PackageReader_AgnosticFramework()
        //{
        //    var zip = TestPackages.GetZip(TestPackages.GetLegacyContentPackage());

        //    using (PackageReader reader = new PackageReader(zip))
        //    {
        //        string[] frameworks = reader.GetSupportedFrameworks().Select(f => f.DotNetFrameworkName).ToArray();

        //        Assert.Equal("Agnostic,Version=v0.0", frameworks[0]);
        //        Assert.Equal(frameworks.Length, 1);
        //    }
        //}
    }
}
