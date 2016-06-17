using NuGet.Versioning;
using Xunit;

namespace NuGet.ProjectModel.Test
{
    public class LockFileLibraryTests
    {
        [Fact]
        public void LockFileLibraryTests_ComparesEqualPaths()
        {
            // Arrange
            var libraryA = new LockFileLibrary
            {
                Name = "SomeLibrary",
                Version = new NuGetVersion("1.0.0"),
                Path = "SomeLibrary/1.0.0"
            };

            // same thing
            var libraryB = new LockFileLibrary
            {
                Name = "SomeLibrary",
                Version = new NuGetVersion("1.0.0"),
                Path = "SomeLibrary/1.0.0"
            };

            // Act & Assert
            Assert.True(libraryA.Equals(libraryB), "The two libraries should be equal.");
        }

        [Fact]
        public void LockFileLibraryTests_ComparesDifferentPaths()
        {
            // Arrange
            var libraryA = new LockFileLibrary
            {
                Name = "SomeLibrary",
                Version = new NuGetVersion("1.0.0"),
                Path = "SomeLibrary/1.0.0"
            };

            // different thing
            var libraryB = new LockFileLibrary
            {
                Name = "SomeLibrary",
                Version = new NuGetVersion("1.0.0"),
                Path = null
            };

            // Act & Assert
            Assert.False(libraryA.Equals(libraryB), "The two libraries should not be equal.");
        }
    }
}
