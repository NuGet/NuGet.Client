// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Xunit;

namespace NuGet.Versioning.Test
{
    public class FloatingRangeTests
    {
        [Fact]
        public void FloatRange_OutsideOfRange()
        {
            var range = VersionRange.Parse("[1.0.*, 2.0.0)");

            var versions = new List<NuGetVersion>()
                {
                    NuGetVersion.Parse("0.1.0"),
                    NuGetVersion.Parse("1.0.0-alpha.2"),
                    NuGetVersion.Parse("2.0.0"),
                    NuGetVersion.Parse("2.2.0"),
                    NuGetVersion.Parse("3.0.0"),
                };

            Assert.Null(range.FindBestMatch(versions));
        }

        [Fact]
        public void FloatRange_OutsideOfRangeLower()
        {
            var range = VersionRange.Parse("[1.0.*, 2.0.0)");

            var versions = new List<NuGetVersion>()
                {
                    NuGetVersion.Parse("0.1.0"),
                    NuGetVersion.Parse("0.2.0"),
                    NuGetVersion.Parse("1.0.0-alpha.2")
                };

            Assert.Null(range.FindBestMatch(versions));
        }

        [Fact]
        public void FloatRange_OutsideOfRangeHigher()
        {
            var range = VersionRange.Parse("[1.0.*, 2.0.0)");

            var versions = new List<NuGetVersion>()
                {
                    NuGetVersion.Parse("2.0.0"),
                    NuGetVersion.Parse("2.0.0-alpha.2"),
                    NuGetVersion.Parse("3.1.0"),
                };

            Assert.Null(range.FindBestMatch(versions));
        }

        [Fact]
        public void FloatRange_OutsideOfRangeOpen()
        {
            var range = VersionRange.Parse("[1.0.*, )");

            var versions = new List<NuGetVersion>()
                {
                    NuGetVersion.Parse("0.1.0"),
                    NuGetVersion.Parse("0.2.0"),
                    NuGetVersion.Parse("1.0.0-alpha.2")
                };

            Assert.Null(range.FindBestMatch(versions));
        }

        [Fact]
        public void FloatRange_RangeOpen()
        {
            var range = VersionRange.Parse("[1.0.*, )");

            var versions = new List<NuGetVersion>()
                {
                    NuGetVersion.Parse("0.1.0"),
                    NuGetVersion.Parse("0.2.0"),
                    NuGetVersion.Parse("1.0.0-alpha.2"),
                    NuGetVersion.Parse("101.0.0")
                };

            Assert.Equal("101.0.0", range.FindBestMatch(versions).ToNormalizedString());
        }

        [Theory]
        [InlineData("1.0.0")]
        public void FloatRange_ParseBasic(string version)
        {
            var range = FloatRange.Parse(version);

            Assert.Equal(range.MinVersion, range.MinVersion);
            Assert.Equal(range.FloatBehavior, NuGetVersionFloatBehavior.None);
        }

        [Fact]
        public void FloatRange_ParsePrerelease()
        {
            var range = FloatRange.Parse("1.0.0-*");

            Assert.True(range.Satisfies(NuGetVersion.Parse("1.0.0-alpha")));
            Assert.True(range.Satisfies(NuGetVersion.Parse("1.0.0-beta")));
            Assert.True(range.Satisfies(NuGetVersion.Parse("1.0.0")));

            Assert.False(range.Satisfies(NuGetVersion.Parse("1.0.1-alpha")));
            Assert.False(range.Satisfies(NuGetVersion.Parse("1.0.1")));
        }

        [Fact]
        public void FloatingRange_FloatNone()
        {
            var range = FloatRange.Parse("1.0.0");

            Assert.Equal("1.0.0", range.MinVersion.ToNormalizedString());
            Assert.Equal(NuGetVersionFloatBehavior.None, range.FloatBehavior);
        }

        [Fact]
        public void FloatingRange_FloatPre()
        {
            var range = FloatRange.Parse("1.0.0-*");

            Assert.Equal("1.0.0--", range.MinVersion.ToNormalizedString());
            Assert.Equal(NuGetVersionFloatBehavior.Prerelease, range.FloatBehavior);
        }

        [Fact]
        public void FloatingRange_FloatPrePrefix()
        {
            var range = FloatRange.Parse("1.0.0-alpha-*");

            Assert.Equal("1.0.0-alpha-", range.MinVersion.ToNormalizedString());
            Assert.Equal(NuGetVersionFloatBehavior.Prerelease, range.FloatBehavior);
        }

        [Fact]
        public void FloatingRange_FloatRev()
        {
            var range = FloatRange.Parse("1.0.0.*");

            Assert.Equal("1.0.0", range.MinVersion.ToNormalizedString());
            Assert.Equal(NuGetVersionFloatBehavior.Revision, range.FloatBehavior);
        }

        [Fact]
        public void FloatingRange_FloatPatch()
        {
            var range = FloatRange.Parse("1.0.*");

            Assert.Equal("1.0.0", range.MinVersion.ToNormalizedString());
            Assert.Equal(NuGetVersionFloatBehavior.Patch, range.FloatBehavior);
        }

        [Fact]
        public void FloatingRange_FloatMinor()
        {
            var range = FloatRange.Parse("1.*");

            Assert.Equal("1.0.0", range.MinVersion.ToNormalizedString());
            Assert.Equal(NuGetVersionFloatBehavior.Minor, range.FloatBehavior);
        }

        [Fact]
        public void FloatingRange_FloatMajor()
        {
            var range = FloatRange.Parse("*");

            Assert.Equal("0.0.0", range.MinVersion.ToNormalizedString());
            Assert.Equal(NuGetVersionFloatBehavior.Major, range.FloatBehavior);
        }

        [Fact]
        public void FloatingRange_FloatNoneBest()
        {
            var range = VersionRange.Parse("1.0.0");

            var versions = new List<NuGetVersion>()
                {
                    NuGetVersion.Parse("1.0.0"),
                    NuGetVersion.Parse("1.0.1"),
                    NuGetVersion.Parse("2.0.0"),
                };

            Assert.Equal("1.0.0", range.FindBestMatch(versions).ToNormalizedString());
        }

        [Fact]
        public void FloatingRange_FloatMinorBest()
        {
            var range = VersionRange.Parse("1.*");

            var versions = new List<NuGetVersion>()
                {
                    NuGetVersion.Parse("0.1.0"),
                    NuGetVersion.Parse("1.0.0"),
                    NuGetVersion.Parse("1.2.0"),
                    NuGetVersion.Parse("2.0.0"),
                };

            Assert.Equal("1.2.0", range.FindBestMatch(versions).ToNormalizedString());
        }

        [Fact]
        public void FloatingRange_FloatMinorPrefixNotFoundBest()
        {
            var range = VersionRange.Parse("1.*");

            var versions = new List<NuGetVersion>()
                {
                    NuGetVersion.Parse("0.1.0"),
                    NuGetVersion.Parse("2.0.0"),
                    NuGetVersion.Parse("2.5.0"),
                    NuGetVersion.Parse("3.3.0"),
                };

            // take the nearest when the prefix is not matched
            Assert.Equal("2.0.0", range.FindBestMatch(versions).ToNormalizedString());
        }

        [Fact]
        public void FloatingRange_FloatAllBest()
        {
            var range = VersionRange.Parse("*");

            var versions = new List<NuGetVersion>()
                {
                    NuGetVersion.Parse("0.1.0"),
                    NuGetVersion.Parse("2.0.0"),
                    NuGetVersion.Parse("2.5.0"),
                    NuGetVersion.Parse("3.3.0"),
                };

            Assert.Equal("3.3.0", range.FindBestMatch(versions).ToNormalizedString());
        }

        [Fact]
        public void FloatingRange_FloatPrereleaseBest()
        {
            var range = VersionRange.Parse("1.0.0-*");

            var versions = new List<NuGetVersion>()
                {
                    NuGetVersion.Parse("0.1.0-alpha"),
                    NuGetVersion.Parse("1.0.0-alpha01"),
                    NuGetVersion.Parse("1.0.0-alpha02"),
                    NuGetVersion.Parse("2.0.0-beta"),
                    NuGetVersion.Parse("2.0.1"),
                };

            Assert.Equal("1.0.0-alpha02", range.FindBestMatch(versions).ToNormalizedString());
        }

        [Fact]
        public void FloatingRange_FloatPrereleaseNotFoundBest()
        {
            // "1.0.0-*"
            var range = VersionRange.Parse("1.0.0-*");

            var versions = new List<NuGetVersion>()
                {
                    NuGetVersion.Parse("0.1.0-alpha"),
                    NuGetVersion.Parse("1.0.1-alpha01"),
                    NuGetVersion.Parse("1.0.1-alpha02"),
                    NuGetVersion.Parse("2.0.0-beta"),
                    NuGetVersion.Parse("2.0.1"),
                };

            Assert.Equal("1.0.1-alpha01", range.FindBestMatch(versions).ToNormalizedString());
        }

        [Fact]
        public void FloatingRange_FloatPrereleasePartialBest()
        {
            var range = VersionRange.Parse("1.0.0-alpha*");

            var versions = new List<NuGetVersion>()
                {
                    NuGetVersion.Parse("0.1.0-alpha"),
                    NuGetVersion.Parse("1.0.0-alpha01"),
                    NuGetVersion.Parse("1.0.0-alpha02"),
                    NuGetVersion.Parse("2.0.0-beta"),
                    NuGetVersion.Parse("2.0.1"),
                };

            Assert.Equal("1.0.0-alpha02", range.FindBestMatch(versions).ToNormalizedString());
        }

        [Fact]
        public void FloatingRange_ToStringPre()
        {
            var range = VersionRange.Parse("1.0.0-*");

            Assert.Equal("[1.0.0-*, )", range.ToNormalizedString());
        }

        [Fact]
        public void FloatingRange_ToStringPrePrefix()
        {
            var range = VersionRange.Parse("1.0.0-alpha.*");

            Assert.Equal("[1.0.0-alpha.*, )", range.ToNormalizedString());
        }

        [Fact]
        public void FloatingRange_ToStringRev()
        {
            var range = VersionRange.Parse("1.0.0.*");

            Assert.Equal("[1.0.0.*, )", range.ToNormalizedString());
        }

        [Fact]
        public void FloatingRange_ToStringPatch()
        {
            var range = VersionRange.Parse("1.0.*");

            Assert.Equal("[1.0.*, )", range.ToNormalizedString());
        }

        [Fact]
        public void FloatingRange_ToStringMinor()
        {
            var range = VersionRange.Parse("1.*");

            Assert.Equal("[1.*, )", range.ToNormalizedString());
        }

        // TODO: fix this one the proper syntax is determined
        //[Fact]
        //public void FloatingRange_ToStringMajor()
        //{
        //    VersionRange range = VersionRange.Parse("*");

        //    Assert.Equal("[*, )", range.ToNormalizedString());
        //}
    }
}
