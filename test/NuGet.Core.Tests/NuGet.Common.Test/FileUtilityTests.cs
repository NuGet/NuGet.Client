using NuGet.Test.Utility;
using System.IO;
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
                Assert.True(File.Exists(dest));
                Assert.False(File.Exists(orig));
            }
        }

        [Fact]
        public void FileUtility_MoveBasicFail()
        {
            using (var testDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var orig = Path.Combine(testDirectory, "a");
                var dest = Path.Combine(testDirectory, "b");

                File.WriteAllText(orig, "a");
                File.WriteAllText(dest, "a");

                using (var stream = File.OpenWrite(dest))
                {
                    // Act & Assert
                    Assert.Throws(typeof(IOException), () =>
                        FileUtility.Move(orig, dest));
                }
            }
        }

        [Fact]
        public void FileUtility_DeleteBasicSuccess()
        {
            using (var testDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var path = Path.Combine(testDirectory, "a");

                File.WriteAllText(path, "a");

                // Act
                FileUtility.Delete(path);

                // Assert
                Assert.False(File.Exists(path));
            }
        }

        [Fact]
        public void FileUtility_DeleteBasicFail()
        {
            using (var testDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var path = Path.Combine(testDirectory, "a");

                File.WriteAllText(path, "a");

                using (var stream = File.OpenWrite(path))
                {
                    // Act & Assert
                    if (RuntimeEnvironmentHelper.IsWindows)
                    {
                        Assert.Throws(typeof(IOException), () =>
                            FileUtility.Delete(path));
                    }
                    else
                    {
                        // Linux and OSX will delete the file without an error
                        FileUtility.Delete(path);
                        Assert.False(File.Exists(path));
                    }
                }
            }
        }
    }
}
