using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Protocol.Core.v3.Tests
{
    public class FeedTypeUtilityTests
    {
        [Theory]
        [InlineData("https://www.nuget.org/api/v2/")]
        [InlineData("https://www.nuget.org/api/v2")]
        [InlineData("http://www.nuget.org/api/v2/")]
        [InlineData("http://nuget.org")]
        [InlineData("http://")]
        [InlineData("http://nuget.org/index.xml")]
        [InlineData("https://www.myget.org/F/nuget-volatile/api/v2")]
        [InlineData("http://nuget.org/index.json.html")]
        [InlineData("http://tempuri.org/api/v2/")]
        public void FeedTypeUtility_HttpSourcesV2(string source)
        {
            Assert.Equal(FeedType.HttpV2, FeedTypeUtility.GetFeedType(new PackageSource(source)));
        }

        [Theory]
        [InlineData("https://api.nuget.org/v3/index.json")]
        [InlineData("http://api.nuget.org/v3/index.json")]
        [InlineData("https://api.nuget.org/v3/INDEX.JSON")]
        [InlineData("https://www.myget.org/F/nuget-volatile/api/v3/index.json")]
        public void FeedTypeUtility_HttpSourcesV3(string source)
        {
            Assert.Equal(FeedType.HttpV3, FeedTypeUtility.GetFeedType(new PackageSource(source)));
        }

        [Fact]
        public void FeedTypeUtility_VerifyBadSourceIsUnknown()
        {
            // Arrange & Act
            var type = FeedTypeUtility.GetFeedType(new PackageSource("\\blah"));

            // Assert
            Assert.Equal(FeedType.FileSystemUnknown, type);
        }

        [Theory]
        [InlineData("../foo/packages")]
        [InlineData(@"..\foo\packages")]
        [InlineData(@"packages")]
        public void FeedTypeUtility_VerifyRelativePathIsUnknown(string source)
        {
            // Arrange & Act
            var type = FeedTypeUtility.GetFeedType(new PackageSource(source));

            // Assert
            Assert.Equal(FeedType.FileSystemUnknown, type);
        }

        [Fact]
        public void FeedTypeUtility_VerifyBadSourceIsUnknown2()
        {
            // Arrange & Act
            var type = FeedTypeUtility.GetFeedType(new PackageSource("$|. \n\t"));

            // Assert
            Assert.Equal(FeedType.FileSystemUnknown, type);
        }

        [Fact]
        public void FeedTypeUtility_EmptyDirectoryIsUnknownType()
        {
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange & Act
                var type = FeedTypeUtility.GetFeedType(new PackageSource(root));

                // Assert
                Assert.Equal(FeedType.FileSystemUnknown, type);
            }
        }

        [Fact]
        public void FeedTypeUtility_RandomFilesInRootIsUnknownType()
        {
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                CreateFile(Path.Combine(root, "a.txt"));
                CreateFile(Path.Combine(root, "a", "a.txt"));
                CreateFile(Path.Combine(root, "a", "b", "a.txt"));

                // Act
                var type = FeedTypeUtility.GetFeedType(new PackageSource(root));

                // Assert
                Assert.Equal(FeedType.FileSystemUnknown, type);
            }
        }

        [Fact]
        public void FeedTypeUtility_NupkgAtInvalidLocationIsUnknown()
        {
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                CreateFile(Path.Combine(root, "a", "b", "a.1.0.0.nupkg"));

                // Act
                var type = FeedTypeUtility.GetFeedType(new PackageSource(root));

                // Assert
                Assert.Equal(FeedType.FileSystemUnknown, type);
            }
        }

        [Fact]
        public void FeedTypeUtility_NupkgAtRootIsV2()
        {
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                CreateFile(Path.Combine(root, "a.1.0.0.nupkg"));

                // Act
                var type = FeedTypeUtility.GetFeedType(new PackageSource(root));

                // Assert
                Assert.Equal(FeedType.FileSystemV2, type);
            }
        }

        [Fact]
        public void FeedTypeUtility_NupkgInVersionFolderIsV3()
        {
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                CreateFile(Path.Combine(root, "a", "1.0.0", "a.1.0.0.nupkg.sha512"));
                CreateFile(Path.Combine(root, "a", "1.0.0", "a.nuspec"));
                CreateFile(Path.Combine(root, "a", "1.0.0", "a.1.0.0.nupkg"));

                // Act
                var type = FeedTypeUtility.GetFeedType(new PackageSource(root));

                // Assert
                Assert.Equal(FeedType.FileSystemV3, type);
            }
        }

        [Fact]
        public void FeedTypeUtility_NupkgOnlyInVersionFolderIsUnknown()
        {
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                CreateFile(Path.Combine(root, "a", "1.0.0", "a.1.0.0.nupkg"));

                // Act
                var type = FeedTypeUtility.GetFeedType(new PackageSource(root));

                // Assert
                Assert.Equal(FeedType.FileSystemUnknown, type);
            }
        }

        [Fact]
        public void FeedTypeUtility_V2V3CombinedReturnsV2()
        {
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                CreateFile(Path.Combine(root, "a.1.0.0.nupkg"));
                CreateFile(Path.Combine(root, "a", "a.1.0.0.nupkg"));
                CreateFile(Path.Combine(root, "a", "1.0.0", "a.1.0.0.nupkg"));

                // Act
                var type = FeedTypeUtility.GetFeedType(new PackageSource(root));

                // Assert
                Assert.Equal(FeedType.FileSystemV2, type);
            }
        }

        private void CreateFile(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            File.WriteAllText(path, string.Empty);
        }
    }
}
