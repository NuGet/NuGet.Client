using NuGet.Versioning;
using Xunit;

namespace NuGet.DependencyResolver.Tests
{
    public class RemoteDependencyWalkerTests
    {
        [Fact]
        public void IsGreaterThanEqualTo_ReturnsTrue_IfLeftVersionIsUnbound()
        {
            // Arrange
            var leftVersion = VersionRange.All;
            var rightVersion = VersionRange.Parse("1.0.0");

            // Act
            var isGreater = RemoteDependencyWalker.IsGreaterThanOrEqualTo(leftVersion, rightVersion);

            // Assert
            Assert.True(isGreater);
        }

        [Fact]
        public void IsGreaterThanEqualTo_ReturnsFalse_IfRightVersionIsUnbound()
        {
            // Arrange
            var leftVersion = VersionRange.Parse("3.1.0-*");
            var rightVersion = VersionRange.All;

            // Act
            var isGreater = RemoteDependencyWalker.IsGreaterThanOrEqualTo(leftVersion, rightVersion);

            // Assert
            Assert.False(isGreater);
        }

        [Theory]
        [InlineData("3.0", "3.0")]
        [InlineData("3.0", "3.0.0")]
        [InlineData("3.1", "3.0.0")]
        [InlineData("3.1.2", "3.1.1")]
        [InlineData("3.1.2-beta", "3.1.2-alpha")]
        [InlineData("[3.1.2-beta, 4.0)", "[3.1.1, 4.3)")]
        [InlineData("[3.1.2-*, 4.0)", "3.1.2-alpha-1002")]
        [InlineData("3.1.2-prerelease", "3.1.2-alpha-*")]
        [InlineData("3.1.2-beta-*", "3.1.2-alpha-*")]
        [InlineData("3.1.*", "3.1.2-alpha-*")]
        [InlineData("*", "3.1.2-alpha-*")]
        [InlineData("*", "*")]
        [InlineData("1.*", "1.1.*")]
        [InlineData("1.*", "1.3.*")]
        [InlineData("1.8.*", "1.8.3.*")]
        [InlineData("1.8.3.5*", "1.8.3.4-*")]
        [InlineData("1.8.3.*", "1.8.3.4-*")]
        [InlineData("1.8.3.4-alphabeta-*", "1.8.3.4-alpha*")]
        [InlineData("1.8.5.4-alpha-*", "1.8.3.4-gamma*")]
        [InlineData("1.8.3.6-alpha-*", "1.8.3.4-gamma*")]
        [InlineData("1.8.3-*", "1.8.3-alpha*")]
        [InlineData("1.8.3-*", "1.8.3-*")]
        [InlineData("1.8.4-*", "1.8.3-*")]
        [InlineData("2.8.1-*", "1.8.3-*")]
        [InlineData("3.2.0-*", "3.1.0-beta-234")]
        [InlineData("3.*", "3.1.*")]
        public void IsGreaterThanEqualTo_ReturnsTrue_IfRightVersionIsSmallerThanLeft(string leftVersionString, string rightVersionString)
        {
            // Arrange
            var leftVersion = VersionRange.Parse(leftVersionString);
            var rightVersion = VersionRange.Parse(rightVersionString);

            // Act
            var isGreater = RemoteDependencyWalker.IsGreaterThanOrEqualTo(leftVersion, rightVersion);

            // Assert
            Assert.True(isGreater);
        }

        [Theory]
        [InlineData("1.3.4", "1.4.3")]
        [InlineData("3.0", "3.1")]
        [InlineData("3.0", "*")]
        [InlineData("3.0-*", "*")]
        [InlineData("3.2.4", "3.2.7")]
        [InlineData("3.2.4-alpha", "[3.2.4-beta, 4.0)")]
        [InlineData("2.2.4-alpha", "2.2.4-beta-*")]
        [InlineData("2.2.4-beta-1", "2.2.4-beta1*")]
        [InlineData("2.2.1.*", "2.3.*")]
        [InlineData("2.*", "3.1.*")]
        [InlineData("3.4.6.*", "3.6.*")]
        [InlineData("3.4.6-alpha*", "3.4.6-beta*")]
        [InlineData("3.4.6-beta*", "3.4.6-betb*")]
        [InlineData("3.1.0-beta-234", "3.2.0-*")]
        [InlineData("3.0.0-*", "3.1.0-beta-234")]
        public void IsGreaterThanEqualTo_ReturnsFalse_IfRightVersionIsLargerThanLeft(string leftVersionString, string rightVersionString)
        {
            // Arrange
            var leftVersion = VersionRange.Parse(leftVersionString);
            var rightVersion = VersionRange.Parse(rightVersionString);

            // Act
            var isGreater = RemoteDependencyWalker.IsGreaterThanOrEqualTo(leftVersion, rightVersion);

            // Assert
            Assert.False(isGreater);
        }
    }
}
