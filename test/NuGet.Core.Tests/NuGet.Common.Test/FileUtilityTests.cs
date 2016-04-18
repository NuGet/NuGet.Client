using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Common;
using Xunit;

namespace NuGet.Common.Test
{
    public class FileUtilityTests
    {
        [Fact]
        public void FileUtility_MoveBasicSuccess()
        {
            using (var testDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var orig = Path.Combine(testDirectory, "a");
                var dest = Path.Combine(testDirectory, "b");

                File.WriteAllText(orig, "a");

                // Act
                FileUtility.Move(orig, dest);

                // Assert
                Assert.True(File.Exist(dest));
                Assert.False(File.Exist(orig));
            }
        }

        [Fact]
        public void FileUtility_DeleteBasicSuccess()
        {
            using (var testDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var path = Path.Combine(testDirectory, "a");

                File.WriteAllText(orig, "a");

                // Act
                FileUtility.Delete(path);

                // Assert
                Assert.False(File.Exist(path));
            }
        }
    }
}
