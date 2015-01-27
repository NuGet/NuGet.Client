using NuGet.Frameworks;
using NuGet.Packaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Test.Utility;
using Xunit;

namespace NuGet.Test
{
    public class PackageReaderTests
    {

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

                Assert.Equal("Any", frameworks[0]);
                Assert.Equal(".NETFramework, Version=v4.0", frameworks[1]);
                Assert.Equal(".NETFramework, Version=v4.5", frameworks[2]);
                Assert.Equal(frameworks.Length, 3);
            }
        }

        [Fact]
        public void PackageReader_AgnosticFramework()
        {
            var zip = TestPackages.GetZip(TestPackages.GetLegacyContentPackage());

            using (PackageReader reader = new PackageReader(zip))
            {
                string[] frameworks = reader.GetSupportedFrameworks().Select(f => f.DotNetFrameworkName).ToArray();

                Assert.Equal("Agnostic", frameworks[0]);
                Assert.Equal(frameworks.Length, 1);
            }
        }

        //[Fact]
        //public void PackageReader_ContentFilesInSubFolder()
        //{
        //    var zip = TestPackages.GetZip(TestPackages.GetLegacyContentPackage());

        //    using (PackageReader reader = new PackageReader(zip))
        //    {
        //        var groups = reader.GetComponentTree().GetPaths();
        //        var group = groups.Single();
        //        var prop = group.Properties.Single() as KeyValueTreeProperty;

        //        Assert.Equal("any", prop.Value);
        //        Assert.Equal(group.Items.Count(), 3);
        //    }
        //}

        //// TODO: This behavior might be a breaking change from NuGet legacy
        //[Fact]
        //public void PackageReader_IgnoreSubFolders()
        //{
        //    var zip = TestPackages.GetZip(TestPackages.GetLibSubFolderPackage());

        //    using (PackageReader reader = new PackageReader(zip))
        //    {
        //        var groups = reader.GetComponentTree().GetPaths();
        //        var group = groups.First();
        //        var prop = group.Properties.Single() as KeyValueTreeProperty;

        //        Assert.Equal("net40", prop.Value);
        //        Assert.Equal(group.Items.Count(), 1);
        //    }
        //}
    }
}
