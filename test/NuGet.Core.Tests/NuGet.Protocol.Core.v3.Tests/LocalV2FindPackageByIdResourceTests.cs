using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Protocol.Core.v3.Tests
{
    public class LocalV2FindPackageByIdResourceTests
    {
        [Fact]
        public async Task LocalV2FindPackageByIdResource_LocalSource()
        {
            // Arrange
            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var fileInfo = TestPackages.GetPackageWithNupkgCopy();
                FileInfo info = fileInfo.File;
                File.Move(info.FullName, Path.Combine(workingDirectory, fileInfo.Id + ".nupkg"));

                var repo = Repository.Factory.GetCoreV3(workingDirectory);
                var findPackageByIdResource = await repo.GetResourceAsync<FindPackageByIdResource>();
                var context = new SourceCacheContext();
                context.NoCache = true;
                findPackageByIdResource.CacheContext = context;

                // Act
                var packages = await findPackageByIdResource.GetAllVersionsAsync(fileInfo.Id, CancellationToken.None);

                // Assert
                Assert.Equal(1, packages.Count());
                Assert.Equal("1.0.0", packages.FirstOrDefault().ToString());
            }
        }

        [Fact]
        public async Task LocalV2FindPackageByIdResource_LocalSourceInSubdirectory()
        {
            // Arrange
            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var fileInfo = TestPackages.GetPackageWithNupkgCopy();
                FileInfo info = fileInfo.File;
                var folderName = Path.Combine(workingDirectory, $"{fileInfo.Id}.{fileInfo.Version}");
                Directory.CreateDirectory(folderName);

                File.Move(info.FullName, Path.Combine(folderName, fileInfo.Id + ".nupkg"));

                var repo = Repository.Factory.GetCoreV3(workingDirectory);
                var findPackageByIdResource = await repo.GetResourceAsync<FindPackageByIdResource>();
                var context = new SourceCacheContext();
                context.NoCache = true;
                findPackageByIdResource.CacheContext = context;

                // Act
                var packages = await findPackageByIdResource.GetAllVersionsAsync(fileInfo.Id, CancellationToken.None);

                // Assert
                Assert.Equal(1, packages.Count());
                Assert.Equal("1.0.0", packages.FirstOrDefault().ToString());
            }
        }
    }
}