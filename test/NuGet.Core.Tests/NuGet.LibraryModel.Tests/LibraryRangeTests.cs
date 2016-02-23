using NuGet.Versioning;
using Xunit;

namespace NuGet.LibraryModel.Tests
{
    public class LibraryRangeTests
    {

        [Theory]
        [InlineData("1.0.0", "packageA >= 1.0.0")]
        [InlineData("1.0.0-*", "packageA >= 1.0.0-*")]
        [InlineData("[ , ]", "packageA")]
        [InlineData("[ , 1.0.0 ]", "packageA <= 1.0.0")]
        [InlineData("[ , 1.0.0 )", "packageA < 1.0.0")]
        [InlineData("[1.0.0 , 2.0.0]", "packageA >= 1.0.0 <= 2.0.0")]
        [InlineData("(1.0.0 , 2.0.0)", "packageA > 1.0.0 < 2.0.0")]
        [InlineData("(1.0.0 , 2.0.0]", "packageA > 1.0.0 <= 2.0.0")]
        public void LibraryRange_ToLockFileDependencyGroupString(string versionRange, string expected)
        {
            // Arrange
            LibraryRange range = new LibraryRange()
            {
                Name = "packageA",
                VersionRange = VersionRange.Parse(versionRange),
                TypeConstraint = LibraryDependencyTarget.Project
            };

            // Act and Assert
            Assert.Equal(expected, range.ToLockFileDependencyGroupString());
        }
    }
}
