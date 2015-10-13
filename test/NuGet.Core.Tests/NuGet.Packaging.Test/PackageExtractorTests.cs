using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class PackageExtractorTests : IDisposable
    {
        [Fact]
        void PackageExtractor_withContentXmlFile()
        {
            // Arrange
            var packageStream = TestPackages.GetTestPackageWithContentXmlFile();
            var root = GetTempDir();
            var packageReader = new PackageReader(packageStream);
            var packagePath = Path.Combine(root.FullName, "packageA.2.0.3");
            
            // Act
            var files = PackageExtractor.ExtractPackageAsync(packageReader, 
                                                             packageStream, 
                                                             new PackagePathResolver(root.FullName), 
                                                             null, 
                                                             PackageSaveModes.Nupkg, 
                                                             CancellationToken.None).Result;
            // Assert
            Assert.False(files.Contains(Path.Combine(packagePath + "[Content_Types].xml")));
            var test = Path.Combine(packagePath, "content/[Content_Types].xml");
            Assert.True(files.Contains(Path.Combine(packagePath,"content/[Content_Types].xml")));
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
