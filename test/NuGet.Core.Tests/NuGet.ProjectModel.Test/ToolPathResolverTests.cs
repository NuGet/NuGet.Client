using System.IO;
using NuGet.Frameworks;
using NuGet.Versioning;
using Xunit;

namespace NuGet.ProjectModel.Test
{
    public class ToolPathResolverTests
    {
        [Fact]
        public void ToolPathResolver_BuildsLockFilePath()
        {
            // Arrange
            var target = new ToolPathResolver("packages");
            var expected = Path.Combine(
                "packages",
                ".tools",
                "packageA",
                "3.1.4",
                "netstandard1.3",
                "project.lock.json");

            // Act
            var actual = target.GetLockFilePath(
                "packageA",
                NuGetVersion.Parse("3.1.4"),
                FrameworkConstants.CommonFrameworks.NetStandard13);

            // Assert
            Assert.Equal(expected, actual);
        }
    }
}
