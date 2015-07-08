using NuGet.Versioning;
using Xunit;

namespace NuGet.DependencyResolver.Tests
{
    public class RemoteDependencyWalkerTests
    {
        [Fact]
        public void IsPotentialDowngrade_ReturnsFalse_IfLeftVersionIsUnbound()
        {
            // Arrange
            var leftVersion = VersionRange.All;
            var rightVersion = VersionRange.Parse("1.0.0");

            // Act
            var isDowngrade = RemoteDependencyWalker.IsPotentialDowngrade(leftVersion, rightVersion);

            // Assert
            Assert.False(isDowngrade);
        }

        [Fact]
        public void IsPotentialDowngrade_ReturnsTrue_IfRightVersionIsUnbound()
        {
            // Arrange
            var leftVersion = VersionRange.Parse("3.1.0-*");
            var rightVersion = VersionRange.All;

            // Act
            var isDowngrade = RemoteDependencyWalker.IsPotentialDowngrade(leftVersion, rightVersion);

            // Assert
            Assert.True(isDowngrade);
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
        [InlineData("1.8.3.4-*", "1.8.3.*")]
        [InlineData("1.8.3.4-alphabeta-*", "1.8.3.4-alpha*")]
        [InlineData("1.8.3-*", "1.8.3-alpha*")]
        [InlineData("1.8.3-*", "1.8.3-*")]
        [InlineData("1.8.4-*", "1.8.3-*")]
        [InlineData("2.8.1-*", "1.8.3-*")]
        public void IsPotentialDowngrade_ReturnsFalse_IfRightVersionIsSmallerThanLeft(string leftVersionString, string rightVersionString)
        {
            // Arrange
            var leftVersion = VersionRange.Parse(leftVersionString);
            var rightVersion = VersionRange.Parse(rightVersionString);

            // Act
            var isDowngrade = RemoteDependencyWalker.IsPotentialDowngrade(leftVersion, rightVersion);

            // Assert
            Assert.False(isDowngrade);
        }

        [Theory]
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
        public void IsPotentialDowngrade_ReturnsTrue_IfRightVersionIsLargerThanLeft(string leftVersionString, string rightVersionString)
        {
            // Arrange
            var leftVersion = VersionRange.Parse(leftVersionString);
            var rightVersion = VersionRange.Parse(rightVersionString);

            // Act
            var isDowngrade = RemoteDependencyWalker.IsPotentialDowngrade(leftVersion, rightVersion);

            // Assert
            Assert.True(isDowngrade);
        }
    }
}
