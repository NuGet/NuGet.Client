using NuGet.Protocol.Core.v3.DependencyInfo;
using NuGet.Versioning;
using Xunit;
using Xunit.Extensions;

namespace Client.V3Test
{
    public class ResolverMetadataClientTests
    {
        [InlineData("[1.0]")]
        [InlineData("[2.0]")]
        [InlineData("1.0")]
        [InlineData("2.0")]
        [InlineData("(,1.0]")]
        [InlineData("(,2.0]")]
        [InlineData("(1.0,)")]
        [InlineData("(,2.0)")]
        [InlineData("(1.0,2.0)")]
        [InlineData("[1.0,2.0]")]
        // [InlineData("")] # Need to handle any version range
        [Theory]
        public void TestIsItemRangeRequiredTrue(string dependencyRangeString)
        {
            // Arrange
            NuGetVersion catalogItemLower = new NuGetVersion("1.0.0");
            NuGetVersion catalogItemUpper = new NuGetVersion("2.0.0");
            VersionRange dependencyRange = VersionRange.Parse(dependencyRangeString);

            // Act and Assert
            Assert.True(ResolverMetadataClientUtility.IsItemRangeRequired(dependencyRange, catalogItemLower, catalogItemUpper));
        }

        [InlineData("(2.0,)")]
        [InlineData("(,1.0)")]
        [Theory]
        public void TestIsItemRangeRequiredFalse(string preFilterRangeString)
        {
            // Arrange
            NuGetVersion catalogItemLower = new NuGetVersion("1.0.0");
            NuGetVersion catalogItemUpper = new NuGetVersion("2.0.0");
            VersionRange preFilterRange = VersionRange.Parse(preFilterRangeString);

            // Act and Assert
            Assert.False(ResolverMetadataClientUtility.IsItemRangeRequired(preFilterRange, catalogItemLower, catalogItemUpper));
        }
    }
}
