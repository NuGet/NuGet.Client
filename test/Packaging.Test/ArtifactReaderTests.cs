//using NuGet.Packaging;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using Test.Utility;
//using Xunit;

//namespace NuGet.Test
//{
//    public class ArtifactReaderTests
//    {

//        //[Fact]
//        //public void ArtifactReader_Tree()
//        //{
//        //    var zip = TestPackages.GetLegacyTestPackage();

//        //    using (PackageReader packageReader = new PackageReader(new ZipFileSystem(zip.OpenRead())))
//        //    {
//        //        ArtifactReader reader = new ArtifactReader(packageReader);

//        //        var artifactTree = reader.GetArtifactTree();

//        //        ArtifactGroup[] groups = artifactTree.Groups.ToArray();

//        //        Assert.Equal(1, groups[0].Items.Count());
//        //    }
//        //}

//        [Fact]
//        public void ArtifactReader_SupportedFrameworks()
//        {
//            var zip = TestPackages.GetLegacyTestPackage();

//            using (PackageReader packageReader = new PackageReader(new ZipFileSystem(zip.OpenRead())))
//            {
//                ArtifactReader reader = new ArtifactReader(packageReader);

//                string[] frameworks = reader.GetSupportedFrameworks().ToArray();

//                Assert.Equal("any", frameworks[0]);
//                Assert.Equal("net40", frameworks[1]);
//                Assert.Equal("net45", frameworks[2]);
//                Assert.Equal(frameworks.Length, 3);
//            }
//        }

//        [Fact]
//        public void ArtifactReader_AgnosticFramework()
//        {
//            var zip = TestPackages.GetLegacyContentPackage();

//            using (PackageReader packageReader = new PackageReader(new ZipFileSystem(zip.OpenRead())))
//            {
//                ArtifactReader reader = new ArtifactReader(packageReader);

//                string[] frameworks = reader.GetSupportedFrameworks().ToArray();

//                Assert.Equal("agnostic", frameworks[0]);
//                Assert.Equal(frameworks.Length, 1);
//            }
//        }

//        [Fact]
//        public void ArtifactReader_ContentFilesInSubFolder()
//        {
//            var zip = TestPackages.GetLegacyContentPackage();

//            using (PackageReader packageReader = new PackageReader(new ZipFileSystem(zip.OpenRead())))
//            {
//                ArtifactReader reader = new ArtifactReader(packageReader);

//                var group = reader.GetArtifactGroups().Single();

//                Assert.Equal("any", group.Properties.Single().Value);
//                Assert.Equal(group.Items.Count(), 3);
//            }
//        }

//        [Fact]
//        public void ArtifactReader_IgnoreSubFolders()
//        {
//            var zip = TestPackages.GetLibSubFolderPackage();

//            using (PackageReader packageReader = new PackageReader(new ZipFileSystem(zip.OpenRead())))
//            {
//                ArtifactReader reader = new ArtifactReader(packageReader);

//                var group = reader.GetArtifactGroups().Single();

//                Assert.Equal("net40", group.Properties.Single().Value);
//                Assert.Equal(group.Items.Count(), 1);
//            }
//        }
//    }
//}
