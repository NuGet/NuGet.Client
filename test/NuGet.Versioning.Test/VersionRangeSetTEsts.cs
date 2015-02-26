using System;
using Xunit;

namespace NuGet.Versioning.Test
{
    public class VersionRangeSetTests
    {

        [Theory]
        [InlineData("[1.0.0, )", "[1.0.0, )")]
        [InlineData("[1.0.0, )", "[1.0.1, )")]
        [InlineData("[1.0.0-alpha, )", "[1.0.0, )")]
        [InlineData("[1.0.0]", "[1.0.0]")]
        [InlineData("[1.0.0, 2.0.0]", "(1.1.0, 1.5.0)")]
        public void VersionRangeSet_SubSetTest(string superSet, string subSet)
        {
            var superSetRange = VersionRange.Parse(superSet);
            var subSetRange = VersionRange.Parse(subSet);

            Assert.True(subSetRange.IsSubSetOrEqualTo(superSetRange));
        }

        [Theory]
        [InlineData("[1.0.1, )", "[1.0.0, )")]
        [InlineData("[1.0.1, )", "[1.0.1-alpha, )")]
        [InlineData("[1.0.0, 2.0.0)", "[1.0.0, 2.0.0]")]
        public void VersionRangeSet_SubSetTestNeg(string superSet, string subSet)
        {
            var superSetRange = VersionRange.Parse(superSet);
            var subSetRange = VersionRange.Parse(subSet);

            Assert.False(subSetRange.IsSubSetOrEqualTo(superSetRange));
        }
    }
}