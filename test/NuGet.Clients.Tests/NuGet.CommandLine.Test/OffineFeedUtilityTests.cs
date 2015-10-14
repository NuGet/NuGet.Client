using System.IO;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class OffineFeedUtilityTests
    {
        [Theory]
        [InlineData("c:\\foo|<>|bar")]
        [InlineData("c:\\foo|<>|bar.nupkg")]
        public void OfflineFeedUtility_ThrowIfInvalid_Throws_PathInvalid(string path)
        {
            // Act & Assert
            var expectedMessage = string.Format(NuGetResources.Path_Invalid, path);

            var exception
                = Assert.Throws<CommandLineException>(() => OfflineFeedUtility.ThrowIfInvalid(path));

            Assert.Equal(expectedMessage, exception.Message);
        }

        [Theory]
        [InlineData("http://foonugetbar.org")]
        [InlineData("http://foonugetbar.org/A.nupkg")]
        public void OfflineFeedUtility_ThrowIfInvalid_Throws_Path_Invalid_NotFileNotUnc(string path)
        {
            // Act & Assert
            var expectedMessage = string.Format(NuGetResources.Path_Invalid_NotFileNotUnc, path);

            var exception
                = Assert.Throws<CommandLineException>(() => OfflineFeedUtility.ThrowIfInvalid(path));

            Assert.Equal(expectedMessage, exception.Message);
        }

        [Theory]
        [InlineData("foo\\bar")]
        [InlineData("c:\\foo\\bar")]
        [InlineData("\\foouncshare\\bar")]
        public void OfflineFeedUtility_ThrowIfInvalid_DoesNotThrow(string path)
        {
            // Act & Assert that the following call does not throw
            OfflineFeedUtility.ThrowIfInvalid(path);
        }

        [Theory]
        [InlineData("c:\\foobardoesnotexist", true)]
        [InlineData("foobardoesnotexist\\A.nupkg", false)]
        public void OfflineFeedUtility_ThrowIfInvalidOrNotFound_Throws(string path, bool isDirectory)
        {
            // Arrange
            var nameOfNotFoundErrorResource
                = isDirectory
                ? nameof(NuGetResources.InitCommand_FeedIsNotFound) : nameof(NuGetResources.NupkgPath_NotFound);

            // Act & Assert
            var expectedMessage = string.Format(NuGetResources.Path_Invalid_NotFileNotUnc, path);
            var exception
                = Assert.Throws<CommandLineException>(()
                    => OfflineFeedUtility.ThrowIfInvalidOrNotFound(
                        path,
                        isDirectory,
                        nameOfNotFoundErrorResource));
        }
    }
}
