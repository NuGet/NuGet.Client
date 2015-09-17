// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using Xunit;

namespace NuGet.Versioning.Test
{
    public class VersionRangeTests
    {
        [Theory]
        [InlineData("[1.0.0]")]
        [InlineData("[1.0.0, 2.0.0]")]
        [InlineData("[1.0.0, 2.0.0]")]
        [InlineData("1.0.0")]
        [InlineData("1.0.0-beta")]
        [InlineData("(1.0.0-beta, 2.0.0-alpha)")]
        [InlineData("(1.0.0-beta, 2.0.0)")]
        [InlineData("(1.0.0, 2.0.0-alpha)")]
        [InlineData("1.0.0-beta-*")]
        [InlineData("[1.0.0-beta-*, ]")]
        public void VersionRange_SetIncludePrerelease_True(string s)
        {
            // Arrange
            var range = VersionRange.Parse(s);

            // Act
            var updated = VersionRange.SetIncludePrerelease(range, true);

            // Assert
            Assert.Equal(range.IsFloating, updated.IsFloating);
            Assert.Equal(range.Float, updated.Float);
            Assert.True(updated.IncludePrerelease);
            Assert.Equal(range.ToNormalizedString(), updated.ToNormalizedString());
        }

        [Theory]
        [InlineData("[1.0.0]")]
        [InlineData("[1.0.0, 2.0.0]")]
        [InlineData("[1.0.0, 2.0.0]")]
        [InlineData("1.0.0")]
        [InlineData("1.0.0-beta")]
        [InlineData("(1.0.0-beta, 2.0.0-alpha)")]
        [InlineData("(1.0.0-beta, 2.0.0)")]
        [InlineData("(1.0.0, 2.0.0-alpha)")]
        [InlineData("1.0.0-beta-*")]
        [InlineData("[1.0.0-beta-*, ]")]
        public void VersionRange_SetIncludePrerelease_False(string s)
        {
            // Arrange
            var range = VersionRange.Parse(s);

            // Act
            var updated = VersionRange.SetIncludePrerelease(range, false);

            // Assert
            Assert.Equal(range.IsFloating, updated.IsFloating);
            Assert.Equal(range.Float, updated.Float);
            Assert.False(updated.IncludePrerelease);
            Assert.Equal(range.ToNormalizedString(), updated.ToNormalizedString());
        }

        [Fact]
        public void ParseVersionRangeSingleDigit()
        {
            // Act
            var versionInfo = VersionRange.Parse("[1,3)");
            Assert.Equal("1.0.0", versionInfo.MinVersion.ToNormalizedString());
            Assert.True(versionInfo.IsMinInclusive);
            Assert.Equal("3.0.0", versionInfo.MaxVersion.ToNormalizedString());
            Assert.False(versionInfo.IsMaxInclusive);
        }

        [Fact]
        public void VersionRange_Exact()
        {
            // Act 
            var versionInfo = new VersionRange(new NuGetVersion(4, 3, 0), true, new NuGetVersion(4, 3, 0), true, false);

            // Assert
            Assert.True(versionInfo.Satisfies(NuGetVersion.Parse("4.3.0")));
        }

        [Fact]
        public void ParseVersionRangePrerelease()
        {
            // Act 
            var versionInfo = VersionRange.Parse("(1.2-Alpha, 1.3-Beta)");

            // Assert
            Assert.True(versionInfo.IncludePrerelease);
        }

        [Fact]
        public void ParseVersionRangeNoPrerelease()
        {
            // Act 
            var versionInfo = new VersionRange(minVersion: new NuGetVersion("1.2-Alpha"), includePrerelease: false);

            // Assert
            Assert.False(versionInfo.IncludePrerelease);
        }

        [Theory]
        [InlineData("1.0.0", "0.0.0")]
        [InlineData("[1.0.0, 2.0.0]", "2.0.1")]
        [InlineData("[1.0.0, 2.0.0]", "0.0.0")]
        [InlineData("[1.0.0, 2.0.0]", "3.0.0")]
        [InlineData("[1.0.0-beta+meta, 2.0.0-beta+meta]", "1.0.0-alpha")]
        [InlineData("[1.0.0-beta+meta, 2.0.0-beta+meta]", "1.0.0-alpha+meta")]
        [InlineData("[1.0.0-beta+meta, 2.0.0-beta+meta]", "2.0.0-rc")]
        [InlineData("[1.0.0-beta+meta, 2.0.0-beta+meta]", "2.0.0+meta")]
        [InlineData("(1.0.0-beta+meta, 2.0.0-beta+meta)", "2.0.0-beta+meta")]
        [InlineData("(, 2.0.0-beta+meta)", "2.0.0-beta+meta")]
        public void ParseVersionRangeDoesNotSatisfy(string spec, string version)
        {
            // Act
            var versionInfo = VersionRange.Parse(spec);
            var middleVersion = NuGetVersion.Parse(version);

            // Assert
            Assert.False(versionInfo.Satisfies(middleVersion));
            Assert.False(versionInfo.Satisfies(middleVersion, VersionComparison.Default));
            Assert.False(versionInfo.Satisfies(middleVersion, VersionComparer.Default));
            Assert.False(versionInfo.Satisfies(middleVersion, VersionComparison.VersionRelease));
            Assert.False(versionInfo.Satisfies(middleVersion, VersionComparer.VersionRelease));
            Assert.False(versionInfo.Satisfies(middleVersion, VersionComparison.VersionReleaseMetadata));
            Assert.False(versionInfo.Satisfies(middleVersion, VersionComparer.VersionReleaseMetadata));
        }

        [Theory]
        [InlineData("1.0.0", "2.0.0")]
        [InlineData("[1.0.0, 2.0.0]", "2.0.0")]
        [InlineData("[1.0.0, 2.0.0]", "1.0.0")]
        [InlineData("[1.0.0-beta+meta, 2.0.0-beta+meta]", "1.0.0")]
        [InlineData("[1.0.0-beta+meta, 2.0.0-beta+meta]", "1.0.0-beta+meta")]
        [InlineData("[1.0.0-beta+meta, 2.0.0-beta+meta]", "2.0.0-beta")]
        [InlineData("[1.0.0-beta+meta, 2.0.0-beta+meta]", "1.0.0+meta")]
        [InlineData("(1.0.0-beta+meta, 2.0.0-beta+meta)", "1.0.0")]
        [InlineData("(1.0.0-beta+meta, 2.0.0-beta+meta)", "2.0.0-alpha+meta")]
        [InlineData("(1.0.0-beta+meta, 2.0.0-beta+meta)", "2.0.0-alpha")]
        [InlineData("(, 2.0.0-beta+meta)", "2.0.0-alpha")]
        public void ParseVersionRangeSatisfies(string spec, string version)
        {
            // Act
            var versionInfo = VersionRange.Parse(spec);
            var middleVersion = NuGetVersion.Parse(version);

            // Assert
            Assert.True(versionInfo.Satisfies(middleVersion));
            Assert.True(versionInfo.Satisfies(middleVersion, VersionComparison.Default));
            Assert.True(versionInfo.Satisfies(middleVersion, VersionComparer.VersionRelease));
        }

        [Theory]
        [InlineData("1.0.0", "2.0.0", true, true)]
        [InlineData("1.0.0", "1.0.1", false, false)]
        [InlineData("1.0.0-beta+0", "2.0.0", false, true)]
        [InlineData("1.0.0-beta+0", "2.0.0+99", false, false)]
        [InlineData("1.0.0-beta+0", "2.0.0+99", true, true)]
        [InlineData("1.0.0", "2.0.0+99", true, true)]
        public void ParseVersionRangeParts(string minString, string maxString, bool minInc, bool maxInc)
        {
            // Arrange
            var min = NuGetVersion.Parse(minString);
            var max = NuGetVersion.Parse(maxString);

            // Act
            var versionInfo = new VersionRange(min, minInc, max, maxInc);

            // Assert
            Assert.Equal(min, versionInfo.MinVersion, VersionComparer.Default);
            Assert.Equal(max, versionInfo.MaxVersion, VersionComparer.Default);
            Assert.Equal(minInc, versionInfo.IsMinInclusive);
            Assert.Equal(maxInc, versionInfo.IsMaxInclusive);
        }

        [Theory]
        [InlineData("1.0.0", "2.0.0", true, true)]
        [InlineData("1.0.0", "1.0.1", false, false)]
        [InlineData("1.0.0-beta+0", "2.0.0", false, true)]
        [InlineData("1.0.0-beta+0", "2.0.0+99", false, false)]
        [InlineData("1.0.0-beta+0", "2.0.0+99", true, true)]
        [InlineData("1.0.0", "2.0.0+99", true, true)]
        public void ParseVersionRangeToStringReParse(string minString, string maxString, bool minInc, bool maxInc)
        {
            // Arrange
            var min = NuGetVersion.Parse(minString);
            var max = NuGetVersion.Parse(maxString);

            // Act
            var original = new VersionRange(min, minInc, max, maxInc);
            var versionInfo = VersionRange.Parse(original.ToString());

            // Assert
            Assert.Equal(min, versionInfo.MinVersion, VersionComparer.Default);
            Assert.Equal(max, versionInfo.MaxVersion, VersionComparer.Default);
            Assert.Equal(minInc, versionInfo.IsMinInclusive);
            Assert.Equal(maxInc, versionInfo.IsMaxInclusive);
        }

        [Theory]
        [InlineData("1.2.0")]
        [InlineData("1.2.3")]
        [InlineData("1.2.3-beta")]
        [InlineData("1.2.3-beta+900")]
        [InlineData("1.2.3-beta.2.4.55.X+900")]
        [InlineData("1.2.3-0+900")]
        [InlineData("[1.2.0]")]
        [InlineData("[1.2.3]")]
        [InlineData("[1.2.3-beta]")]
        [InlineData("[1.2.3-beta+900]")]
        [InlineData("[1.2.3-beta.2.4.55.X+900]")]
        [InlineData("[1.2.3-0+90]")]
        [InlineData("(, 1.2.0]")]
        [InlineData("(, 1.2.3]")]
        [InlineData("(, 1.2.3-beta]")]
        [InlineData("(, 1.2.3-beta+900]")]
        [InlineData("(, 1.2.3-beta.2.4.55.X+900]")]
        [InlineData("(, 1.2.3-0+900]")]
        public void ParseVersionRangeToStringShortHand(string version)
        {
            // Act
            var versionInfo = VersionRange.Parse(version);

            // Assert
            Assert.Equal(version, versionInfo.ToString("S", new VersionRangeFormatter()));
        }

        [Theory]
        [InlineData("1.2.0", "[1.2.0, )")]
        [InlineData("1.2.3-beta.2.4.55.X+900", "[1.2.3-beta.2.4.55.X+900, )")]
        [InlineData("[1.2.0)", "[1.2.0, 1.2.0)")]
        public void ParseVersionRangeToString(string version, string expected)
        {
            // Act
            var versionInfo = VersionRange.Parse(version);

            // Assert
            Assert.Equal(expected, versionInfo.ToString());
        }

        [Fact]
        public void ParseVersionRangeWithNullThrows()
        {
            // Act & Assert
            ExceptionAssert.ThrowsArgNull(() => VersionRange.Parse(null), "value");
        }

        [Fact]
        public void ParseVersionRangeSimpleVersionNoBrackets()
        {
            // Act
            var versionInfo = VersionRange.Parse("1.2");

            // Assert
            Assert.Equal("1.2", versionInfo.MinVersion.ToString());
            Assert.True(versionInfo.IsMinInclusive);
            Assert.Equal(null, versionInfo.MaxVersion);
            Assert.False(versionInfo.IsMaxInclusive);
        }

        [Fact]
        public void ParseVersionRangeSimpleVersionNoBracketsExtraSpaces()
        {
            // Act
            var versionInfo = VersionRange.Parse("  1  .   2  ");

            // Assert
            Assert.Equal("1.2", versionInfo.MinVersion.ToString());
            Assert.True(versionInfo.IsMinInclusive);
            Assert.Equal(null, versionInfo.MaxVersion);
            Assert.False(versionInfo.IsMaxInclusive);
        }

        [Fact]
        public void ParseVersionRangeMaxOnlyInclusive()
        {
            // Act
            var versionInfo = VersionRange.Parse("(,1.2]");

            // Assert
            Assert.Equal(null, versionInfo.MinVersion);
            Assert.False(versionInfo.IsMinInclusive);
            Assert.Equal("1.2", versionInfo.MaxVersion.ToString());
            Assert.True(versionInfo.IsMaxInclusive);
        }

        [Fact]
        public void ParseVersionRangeMaxOnlyExclusive()
        {
            var versionInfo = VersionRange.Parse("(,1.2)");
            Assert.Equal(null, versionInfo.MinVersion);
            Assert.False(versionInfo.IsMinInclusive);
            Assert.Equal("1.2", versionInfo.MaxVersion.ToString());
            Assert.False(versionInfo.IsMaxInclusive);
        }

        [Fact]
        public void ParseVersionRangeExactVersion()
        {
            // Act
            var versionInfo = VersionRange.Parse("[1.2]");

            // Assert
            Assert.Equal("1.2", versionInfo.MinVersion.ToString());
            Assert.True(versionInfo.IsMinInclusive);
            Assert.Equal("1.2", versionInfo.MaxVersion.ToString());
            Assert.True(versionInfo.IsMaxInclusive);
        }

        [Fact]
        public void ParseVersionRangeMinOnlyExclusive()
        {
            // Act
            var versionInfo = VersionRange.Parse("(1.2,)");

            // Assert
            Assert.Equal("1.2", versionInfo.MinVersion.ToString());
            Assert.False(versionInfo.IsMinInclusive);
            Assert.Equal(null, versionInfo.MaxVersion);
            Assert.False(versionInfo.IsMaxInclusive);
        }

        [Fact]
        public void ParseVersionRangeExclusiveExclusive()
        {
            // Act
            var versionInfo = VersionRange.Parse("(1.2,2.3)");

            // Assert
            Assert.Equal("1.2", versionInfo.MinVersion.ToString());
            Assert.False(versionInfo.IsMinInclusive);
            Assert.Equal("2.3", versionInfo.MaxVersion.ToString());
            Assert.False(versionInfo.IsMaxInclusive);
        }

        [Fact]
        public void ParseVersionRangeExclusiveInclusive()
        {
            // Act
            var versionInfo = VersionRange.Parse("(1.2,2.3]");

            // Assert
            Assert.Equal("1.2", versionInfo.MinVersion.ToString());
            Assert.False(versionInfo.IsMinInclusive);
            Assert.Equal("2.3", versionInfo.MaxVersion.ToString());
            Assert.True(versionInfo.IsMaxInclusive);
        }

        [Fact]
        public void ParseVersionRangeInclusiveExclusive()
        {
            // Act
            var versionInfo = VersionRange.Parse("[1.2,2.3)");
            Assert.Equal("1.2", versionInfo.MinVersion.ToString());
            Assert.True(versionInfo.IsMinInclusive);
            Assert.Equal("2.3", versionInfo.MaxVersion.ToString());
            Assert.False(versionInfo.IsMaxInclusive);
        }

        [Fact]
        public void ParseVersionRangeInclusiveInclusive()
        {
            // Act
            var versionInfo = VersionRange.Parse("[1.2,2.3]");

            // Assert
            Assert.Equal("1.2", versionInfo.MinVersion.ToString());
            Assert.True(versionInfo.IsMinInclusive);
            Assert.Equal("2.3", versionInfo.MaxVersion.ToString());
            Assert.True(versionInfo.IsMaxInclusive);
        }

        [Fact]
        public void ParseVersionRangeInclusiveInclusiveExtraSpaces()
        {
            // Act
            var versionInfo = VersionRange.Parse("   [  1 .2   , 2  .3   ]  ");

            // Assert
            Assert.Equal("1.2", versionInfo.MinVersion.ToString());
            Assert.True(versionInfo.IsMinInclusive);
            Assert.Equal("2.3", versionInfo.MaxVersion.ToString());
            Assert.True(versionInfo.IsMaxInclusive);
        }

        [Theory]
        [InlineData("*")]
        [InlineData("1.*")]
        [InlineData("1.0.0")]
        [InlineData(" 1.0.0")]
        [InlineData("[1.0.0]")]
        [InlineData("[1.0.0] ")]
        [InlineData("[1.0.0, 2.0.0)")]
        public void ParsedVersionRangeHasOriginalString(string range)
        {
            // Act
            var versionInfo = VersionRange.Parse(range);

            // Assert
            Assert.Equal(range, versionInfo.OriginalString);
        }

        public void NonParsedVersionRangeHasNullOriginalString(string range)
        {
            // Act
            var versionInfo = new VersionRange(NuGetVersion.Parse("1.0.0"));

            // Assert
            Assert.Null(versionInfo.OriginalString);
        }

        [Fact]
        public void ParseVersionRangeIntegerRanges()
        {
            // Assert
            var exception = Assert.Throws<ArgumentException>(() => VersionRange.Parse("   [-1, 2]  "));
            Assert.Equal("'   [-1, 2]  ' is not a valid version string.", exception.Message);
        }

        [Fact]
        public void ParseVersionRangeNegativeIntegerRanges()
        {
            // Act
            VersionRange versionInfo;
            var parsed = VersionRange.TryParse("   [-1, 2]  ", out versionInfo);

            Assert.False(parsed);
            Assert.Null(versionInfo);
        }

        [Fact]
        public void ParseVersionThrowsIfExclusiveMinAndMaxVersionRangeContainsNoValues()
        {
            // Arrange
            var versionString = "(,)";

            // Assert
            var exception = Assert.Throws<ArgumentException>(() => VersionRange.Parse(versionString));
            Assert.Equal("'(,)' is not a valid version string.", exception.Message);
        }

        [Fact]
        public void ParseVersionThrowsIfInclusiveMinAndMaxVersionRangeContainsNoValues()
        {
            // Arrange
            var versionString = "[,]";

            // Assert
            var exception = Assert.Throws<ArgumentException>(() => VersionRange.Parse(versionString));
            Assert.Equal("'[,]' is not a valid version string.", exception.Message);
        }

        [Fact]
        public void ParseVersionThrowsIfInclusiveMinAndExclusiveMaxVersionRangeContainsNoValues()
        {
            // Arrange
            var versionString = "[,)";

            // Assert
            var exception = Assert.Throws<ArgumentException>(() => VersionRange.Parse(versionString));
            Assert.Equal("'[,)' is not a valid version string.", exception.Message);
        }

        [Fact]
        public void ParseVersionThrowsIfExclusiveMinAndInclusiveMaxVersionRangeContainsNoValues()
        {
            // Arrange
            var versionString = "(,]";

            // Assert
            var exception = Assert.Throws<ArgumentException>(() => VersionRange.Parse(versionString));
            Assert.Equal("'(,]' is not a valid version string.", exception.Message);
        }

        [Fact]
        public void ParseVersionThrowsIfVersionRangeIsMissingVersionComponent()
        {
            // Arrange
            var versionString = "(,1.3..2]";

            // Assert
            var exception = Assert.Throws<ArgumentException>(() => VersionRange.Parse(versionString));
            Assert.Equal("'(,1.3..2]' is not a valid version string.", exception.Message);
        }

        [Fact]
        public void ParseVersionThrowsIfVersionRangeContainsMoreThen4VersionComponents()
        {
            // Arrange
            var versionString = "(1.2.3.4.5,1.2]";

            // Assert
            var exception = Assert.Throws<ArgumentException>(() => VersionRange.Parse(versionString));
            Assert.Equal("'(1.2.3.4.5,1.2]' is not a valid version string.", exception.Message);
        }

        [Fact]
        public void ParseVersionToNormalizedVersion()
        {
            // Arrange
            var versionString = "(1.0,1.2]";

            // Assert
            Assert.Equal("(1.0.0, 1.2.0]", VersionRange.Parse(versionString).ToString());
        }

        [Theory]
        [InlineData("1.2.0")]
        [InlineData("1.2.3")]
        [InlineData("1.2.3-beta")]
        [InlineData("1.2.3-beta+900")]
        [InlineData("1.2.3-beta.2.4.55.X+900")]
        [InlineData("1.2.3-0+900")]
        [InlineData("[1.2.0)")]
        [InlineData("[1.2.3)")]
        [InlineData("[1.2.3-beta)")]
        [InlineData("[1.2.3-beta+900)")]
        [InlineData("[1.2.3-beta.2.4.55.X+900)")]
        [InlineData("[1.2.3-0+900)")]
        [InlineData("(, 1.2.0)")]
        [InlineData("(, 1.2.3)")]
        [InlineData("(, 1.2.3-beta)")]
        [InlineData("(, 1.2.3-beta+900)")]
        [InlineData("(, 1.2.3-beta.2.4.55.X+900)")]
        [InlineData("(, 1.2.3-0+900)")]
        public void StringFormatNullProvider(string range)
        {
            // Arrange
            var versionRange = VersionRange.Parse(range);
            var actual = String.Format("{0}", versionRange);
            var expected = versionRange.ToString();

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("1.2.0")]
        [InlineData("1.2.3")]
        [InlineData("1.2.3-beta")]
        [InlineData("1.2.3-beta+900")]
        [InlineData("1.2.3-beta.2.4.55.X+900")]
        [InlineData("1.2.3-0+900")]
        [InlineData("[1.2.0]")]
        [InlineData("[1.2.3]")]
        [InlineData("[1.2.3-beta]")]
        [InlineData("[1.2.3-beta+900]")]
        [InlineData("[1.2.3-beta.2.4.55.X+900]")]
        [InlineData("[1.2.3-0+90]")]
        [InlineData("(, 1.2.0]")]
        [InlineData("(, 1.2.3]")]
        [InlineData("(, 1.2.3-beta]")]
        [InlineData("(, 1.2.3-beta+900]")]
        [InlineData("(, 1.2.3-beta.2.4.55.X+900]")]
        [InlineData("(, 1.2.3-0+900]")]
        public void StringFormatNullProvider2(string range)
        {
            // Arrange
            var versionRange = VersionRange.Parse(range);
            var actual = String.Format(CultureInfo.InvariantCulture, "{0}", versionRange);
            var expected = versionRange.ToString();

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("(1.2.3.4, 3.2)", "1.2.3.4", false, "3.2", false)]
        [InlineData("(1.2.3.4, 3.2]", "1.2.3.4", false, "3.2", true)]
        [InlineData("[1.2, 3.2.5)", "1.2", true, "3.2.5", false)]
        [InlineData("[2.3.7, 3.2.4.5]", "2.3.7", true, "3.2.4.5", true)]
        [InlineData("(, 3.2.4.5]", null, false, "3.2.4.5", true)]
        [InlineData("(1.6, ]", "1.6", false, null, true)]
        [InlineData("(1.6)", "1.6", false, "1.6", false)]
        [InlineData("[2.7]", "2.7", true, "2.7", true)]
        public void ParseVersionParsesTokensVersionsCorrectly(string versionString, string min, bool incMin, string max, bool incMax)
        {
            // Arrange
            var versionRange = new VersionRange(min == null ? null : NuGetVersion.Parse(min), incMin,
                max == null ? null : NuGetVersion.Parse(max), incMax);

            // Act
            var actual = VersionRange.Parse(versionString);

            // Assert
            Assert.Equal(versionRange.IsMinInclusive, actual.IsMinInclusive);
            Assert.Equal(versionRange.IsMaxInclusive, actual.IsMaxInclusive);
            Assert.Equal(versionRange.MinVersion, actual.MinVersion);
            Assert.Equal(versionRange.MaxVersion, actual.MaxVersion);
        }
    }
}
