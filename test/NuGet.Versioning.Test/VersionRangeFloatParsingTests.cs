// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Xunit;

namespace NuGet.Versioning.Test
{
    public class VersionRangeFloatParsingTests
    {
        [Fact]
        public void VersionRangeFloatParsing_Prerelease()
        {
            var range = VersionRange.Parse("1.0.0-*");

            Assert.True(range.IncludePrerelease);
            Assert.True(range.MinVersion.IsPrerelease);
        }

        [Theory]
        [InlineData("1.0.0")]
        [InlineData("[1.0.0]")]
        [InlineData("(0.0.0, )")]
        [InlineData("[1.0.0, )")]
        [InlineData("[1.0.0, 2.0.0)")]
        public void VersionRangeFloatParsing_NoFloat(string rangeString)
        {
            var range = VersionRange.Parse(rangeString);

            var versions = new List<NuGetVersion>()
                {
                    NuGetVersion.Parse("1.0.0"),
                    NuGetVersion.Parse("1.1.0")
                };

            Assert.Equal("1.0.0", range.FindBestMatch(versions).ToNormalizedString());
        }

        [Fact]
        public void VersionRangeFloatParsing_FloatPrerelease()
        {
            var range = VersionRange.Parse("1.0.0-*");

            var versions = new List<NuGetVersion>()
                {
                    NuGetVersion.Parse("1.0.0-alpha"),
                    NuGetVersion.Parse("1.0.0-beta")
                };

            Assert.Equal("1.0.0-beta", range.FindBestMatch(versions).ToNormalizedString());
        }

        [Fact]
        public void VersionRangeFloatParsing_FloatPrereleaseMatchVersion()
        {
            var range = VersionRange.Parse("1.0.0-*");

            var versions = new List<NuGetVersion>()
                {
                    NuGetVersion.Parse("1.0.0-beta"),
                    NuGetVersion.Parse("1.0.1-omega"),
                };

            Assert.Equal("1.0.0-beta", range.FindBestMatch(versions).ToNormalizedString());
        }

        [Fact]
        public void VersionRangeFloatParsing_FloatPrereleasePrefix()
        {
            var range = VersionRange.Parse("1.0.0-beta.*");

            var versions = new List<NuGetVersion>()
                {
                    NuGetVersion.Parse("1.0.0-beta.1"),
                    NuGetVersion.Parse("1.0.0-beta.2"),
                    NuGetVersion.Parse("1.0.0-omega.3"),
                };

            Assert.Equal("1.0.0-beta.2", range.FindBestMatch(versions).ToNormalizedString());
        }

        [Fact]
        public void VersionRangeFloatParsing_FloatPrereleasePrefixSemVerLabelMix()
        {
            var range = VersionRange.Parse("1.0.0-beta.*");

            var versions = new List<NuGetVersion>()
                {
                    NuGetVersion.Parse("1.0.0-beta.1"),
                    NuGetVersion.Parse("1.0.0-beta.2"),
                    NuGetVersion.Parse("1.0.0-beta.a"),
                };

            Assert.Equal("1.0.0-beta.a", range.FindBestMatch(versions).ToNormalizedString());
        }

        [Theory]
        [InlineData("[]")]
        [InlineData("[*]")]
        [InlineData("[1.0.0, 1.1.*)")]
        [InlineData("[1.0.0, 2.0.*)")]
        [InlineData("(, 2.*.*)")]
        [InlineData("<1.0.*")]
        [InlineData("<=1.0.*")]
        [InlineData("1.0.0<")]
        [InlineData("1.0.0~")]
        [InlineData("~1.*.*")]
        [InlineData("~*")]
        [InlineData("~")]
        [InlineData("^")]
        [InlineData("^*")]
        [InlineData(">=*")]
        [InlineData("1.*.0")]
        [InlineData("1.*.0-beta-*")]
        [InlineData("1.*.0-beta")]
        [InlineData("1.0.0.0.*")]
        //[InlineData("1.0.0*")]
        [InlineData("=1.0.*")]
        public void VersionRangeFloatParsing_Invalid(string rangeString)
        {
            VersionRange range = null;
            Assert.False(VersionRange.TryParse(rangeString, out range));
        }

        [Theory]
        [InlineData("*")]
        [InlineData("1.*")]
        [InlineData("1.0.*")]
        [InlineData("1.0.0.*")]
        [InlineData("1.0.0.0-beta")]
        [InlineData("1.0.0.0-beta*")]
        [InlineData("1.0.0")]
        [InlineData("1.0")]
        [InlineData("[1.0.*, )")]
        [InlineData("[1.0.0-beta.*, 2.0.0)")]
        [InlineData("1.0.0-beta.*")]
        [InlineData("1.0.0-beta-*")]
        public void VersionRangeFloatParsing_Valid(string rangeString)
        {
            VersionRange range = null;
            Assert.True(VersionRange.TryParse(rangeString, out range));
        }

        [Theory]
        [InlineData("1.0.0", "[1.0.0, )")]
        [InlineData("1.0.*", "[1.0.0, )")]
        [InlineData("[1.0.*, )", "[1.0.0, )")]
        [InlineData("[1.*, )", "[1.0.0, )")]
        [InlineData("[1.*, 2.0)", "[1.0.0, 2.0.0)")]
        [InlineData("*", "(, )")]
        public void VersionRangeFloatParsing_LegacyEquivalent(string rangeString, string legacyString)
        {
            VersionRange range = null;
            Assert.True(VersionRange.TryParse(rangeString, out range));

            Assert.Equal(legacyString, range.ToLegacyString());
        }

        [Theory]
        [InlineData("1.0.0-beta*")]
        [InlineData("1.0.0-beta.*")]
        [InlineData("1.0.0-beta-*")]
        public void VersionRangeFloatParsing_CorrectFloatRange(string rangeString)
        {
            VersionRange range = null;
            Assert.True(VersionRange.TryParse(rangeString, out range));

            Assert.Equal(rangeString, range.Float.ToString());
        }
    }
}
