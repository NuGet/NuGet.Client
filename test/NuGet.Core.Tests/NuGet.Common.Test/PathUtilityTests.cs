using Xunit;

namespace NuGet.Common.Test
{
    public class PathUtilityTests
    {
        [Fact]
        public void PathUtility_RelativePathDifferenctRootCase()
        {
            if (RuntimeEnvironmentHelper.IsWindows)
            {
                // Arrange & Act
                var path1 = @"C:\foo\";
                var path2 = @"c:\foo\bar";
                var path = PathUtility.GetRelativePath(path1, path2);

                // Assert
                Assert.Equal("bar", path);
            }
        }
    }
}
