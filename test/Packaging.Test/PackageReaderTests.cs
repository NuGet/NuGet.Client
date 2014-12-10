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

        [Fact]
        public void PackageReader_ContentFilesInSubFolder()
        {
            var zip = TestPackages.GetZip(TestPackages.GetLegacyContentPackage());

            using (PackageReader reader = new PackageReader(zip))
            {
                var groups = reader.GetComponentTree().GetPaths();
                var group = groups.Single();
                var prop = group.Properties.Single() as KeyValueTreeProperty;

                Assert.Equal("any", prop.Value);
                Assert.Equal(group.Items.Count(), 3);
            }
        }

        // TODO: This behavior might be a breaking change from NuGet legacy
        [Fact]
        public void PackageReader_IgnoreSubFolders()
        {
            var zip = TestPackages.GetZip(TestPackages.GetLibSubFolderPackage());

            using (PackageReader reader = new PackageReader(zip))
            {
                var groups = reader.GetComponentTree().GetPaths();
                var group = groups.First();
                var prop = group.Properties.Single() as KeyValueTreeProperty;

                Assert.Equal("net40", prop.Value);
                Assert.Equal(group.Items.Count(), 1);
            }
        }
    }
}
