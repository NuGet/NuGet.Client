﻿using Xunit;

namespace NuGet.Packaging.Test
{
    public class PackageIdValidatorTest
    {
        [Fact]
        public void ValidatePackageIdInvalidIdThrows()
        {
            // Arrange
            string packageId = "  Invalid  . Woo   .";

            // Act & Assert
            ExceptionAssert.ThrowsArgumentException(() => PackageIdValidator.ValidatePackageId(packageId), "The package ID '  Invalid  . Woo   .' contains invalid characters. Examples of valid package IDs include 'MyPackage' and 'MyPackage.Sample'.");
        }

        [Fact]
        public void EmptyIsNotValid()
        {
            // Arrange
            string packageId = "";

            // Act
            bool isValid = PackageIdValidator.IsValidPackageId(packageId);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void NullThrowsException()
        {
            // Arrange
            string packageId = null;

            // Act & Assert
            ExceptionAssert.ThrowsArgumentException(() => PackageIdValidator.IsValidPackageId(packageId), "packageId");
        }

        [Fact]
        public void AlphaNumericIsValid()
        {
            // Arrange
            string packageId = "42This1Is4You";

            // Act
            bool isValid = PackageIdValidator.IsValidPackageId(packageId);

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public void MultipleDotSeparatorsAllowed()
        {
            // Arrange
            string packageId = "I.Like.Writing.Unit.Tests";

            // Act
            bool isValid = PackageIdValidator.IsValidPackageId(packageId);

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public void NumbersAndWordsDotSeparatedAllowd()
        {
            // Arrange
            string packageId = "1.2.3.4.Uno.Dos.Tres.Cuatro";

            // Act
            bool isValid = PackageIdValidator.IsValidPackageId(packageId);

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public void UnderscoreDotAndDashSeparatorsAreValid()
        {
            // Arrange
            string packageId = "Nu_Get.Core-IsCool";

            // Act
            bool isValid = PackageIdValidator.IsValidPackageId(packageId);

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public void NonAlphaNumericUnderscoreDotDashIsInvalid()
        {
            // Arrange
            string packageId = "ILike*Asterisks";

            // Act
            bool isValid = PackageIdValidator.IsValidPackageId(packageId);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void ConsecutiveSeparatorsNotAllowed()
        {
            // Arrange
            string packageId = "I_.Like.-Separators";

            // Act
            bool isValid = PackageIdValidator.IsValidPackageId(packageId);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void StartingWithSeparatorsNotAllowed()
        {
            // Arrange
            string packageId = "-StartWithSeparator";

            // Act
            bool isValid = PackageIdValidator.IsValidPackageId(packageId);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void EndingWithSeparatorsNotAllowed()
        {
            // Arrange
            string packageId = "StartWithSeparator.";

            // Act
            bool isValid = PackageIdValidator.IsValidPackageId(packageId);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void DotToolsIsNotAllowed()
        {
            // Arrange
            string packageId = ".tools";

            // Act
            bool isValid = PackageIdValidator.IsValidPackageId(packageId);

            // Assert
            Assert.False(isValid);
        }

        [Theory]
        [InlineData(101)]
        [InlineData(102)]
        [InlineData(200)]
        public void IdExceedingMaxLengthThrows(int idTestLength)
        {
            // Arrange
            string packageId = new string('d', idTestLength);

            // Act && Assert
            ExceptionAssert.ThrowsArgumentException(
                () => PackageIdValidator.ValidatePackageId(packageId),
                "Id must not exceed 100 characters.");
        }
    }
}
