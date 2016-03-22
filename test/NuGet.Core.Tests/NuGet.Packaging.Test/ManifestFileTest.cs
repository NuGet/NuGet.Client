using System.IO;
using System.Linq;

using Xunit;

namespace NuGet.Packaging.Test
{
    public class ManifestFileTest
    {
        [Fact]
        public void ManifestFileReturnsNoValidationResultsIfSourceAndTargetPathAreValid()
        {
            // Arrange
            var manifestFile = new ManifestFile { Source = @"bin\release\MyLib.dll".Replace('\\', Path.DirectorySeparatorChar), Target = @"lib" };

            // Act
            var result = manifestFile.Validate();

            // Assert
            Assert.False(result.Any());
        }

        [Fact]
        public void ManifestFileReturnsNoValidationResultIfSourceContainsWildCardCharacters()
        {
            // Arrange
            var manifestFile = new ManifestFile { Source = @"b?n\**\*.dll".Replace('\\', Path.DirectorySeparatorChar), Target = @"lib" };

            // Act
            var result = manifestFile.Validate();

            // Assert
            Assert.False(result.Any());
        }

        [Fact]
        public void ManifestFileReturnsValidationResultIfSourceContainsInvalidCharacters()
        {
            // Arrange
            char badChar = Path.GetInvalidPathChars()[0];
            var manifestFile = new ManifestFile { Source = "bin" + Path.DirectorySeparatorChar + badChar + Path.DirectorySeparatorChar + "*.dll", Target = @"lib" };

            // Act
            var result = manifestFile.Validate().ToList();

            // Assert
            Assert.Equal(1, result.Count);
            Assert.Equal(@"Source path 'bin" + Path.DirectorySeparatorChar + badChar + Path.DirectorySeparatorChar + "*.dll' contains invalid characters.", result.Single());
        }

        [Fact]
        public void ManifestFileReturnsValidationResultIfTargetContainsInvalidCharacters()
        {
            // Arrange
            char badChar = Path.GetInvalidPathChars()[0];
            var manifestFile = new ManifestFile { Source = @"bin" + Path.DirectorySeparatorChar + "**" + Path.DirectorySeparatorChar + "*.dll", Target = @"lib" + Path.DirectorySeparatorChar + badChar + Path.DirectorySeparatorChar + "net40" };

            // Act
            var result = manifestFile.Validate().ToList();

            // Assert
            Assert.Equal(1, result.Count);
            Assert.Equal(@"Target path 'lib" + Path.DirectorySeparatorChar + badChar + Path.DirectorySeparatorChar + "net40' contains invalid characters.", result.Single());
        }

        [Fact]
        public void ManifestFileReturnsValidationResultsIfSourceAndTargetContainsInvalidCharacters()
        {
            // Arrange
            char badChar = Path.GetInvalidPathChars()[0];
            var manifestFile = new ManifestFile { Source = @"bin" + badChar + Path.DirectorySeparatorChar + "**" + Path.DirectorySeparatorChar + "*.dll", Target = @"lib" + Path.DirectorySeparatorChar + badChar + Path.DirectorySeparatorChar + "net40" };

            // Act
            var result = manifestFile.Validate().ToList();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal(@"Source path 'bin" + badChar + Path.DirectorySeparatorChar + "**" + Path.DirectorySeparatorChar + "*.dll' contains invalid characters.", result.First());
            Assert.Equal(@"Target path 'lib" + Path.DirectorySeparatorChar + badChar + Path.DirectorySeparatorChar + "net40' contains invalid characters.", result.Last());
        }

        [Fact]
        public void ManifestFileReturnsValidationResultsIfTargetPathContainsWildCardCharacters()
        {
            // Arrange
            var manifestFile = new ManifestFile { Source = @"bin" + Path.DirectorySeparatorChar + "**" + Path.DirectorySeparatorChar + "*.dll", Target = @"lib" + Path.DirectorySeparatorChar + "**" + Path.DirectorySeparatorChar + "net40" };

            // Act
            var result = manifestFile.Validate().ToList();

            // Assert
            Assert.Equal(1, result.Count);
            Assert.Equal(@"Target path 'lib" + Path.DirectorySeparatorChar + "**" + Path.DirectorySeparatorChar + "net40' contains invalid characters.", result.Single());
        }
    }
}
