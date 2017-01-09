﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGet.Versioning.Test
{
    public class SemanticVersionTests
    {
        [Theory]
        [InlineData("1.0.0")]
        [InlineData("0.0.1")]
        [InlineData("1.2.3")]
        [InlineData("1.2.3-alpha")]
        [InlineData("1.2.3-X.yZ.3.234.243.32423423.4.23423.4324.234.234.3242")]
        [InlineData("1.2.3-X.yZ.3.234.243.32423423.4.23423+METADATA")]
        [InlineData("1.2.3-X.y3+0")]
        [InlineData("1.2.3-X+0")]
        [InlineData("1.2.3+0")]
        [InlineData("1.2.3-0")]
        public void ParseSemanticVersionStrict(string versionString)
        {
            // Act
            SemanticVersion semVer = null;
            SemanticVersion.TryParse(versionString, out semVer);

            // Assert
            Assert.Equal<string>(versionString, semVer.ToFullString());
            Assert.Equal<string>(semVer.ToNormalizedString(), semVer.ToString());
        }

        [Theory]
        [InlineData("1.2.3")]
        [InlineData("1.2.3+0")]
        [InlineData("1.2.3+321")]
        [InlineData("1.2.3+XYZ")]
        public void SemanticVersionStrictEquality(string versionString)
        {
            // Act
            SemanticVersion main = null;
            SemanticVersion.TryParse("1.2.3", out main);

            SemanticVersion semVer = null;
            SemanticVersion.TryParse(versionString, out semVer);

            // Assert
            Assert.True(main.Equals(semVer));
            Assert.True(semVer.Equals(main));

            Assert.True(main.GetHashCode() == semVer.GetHashCode());
        }

        [Theory]
        [InlineData("1.2.3-alpha")]
        [InlineData("1.2.3-alpha+0")]
        [InlineData("1.2.3-alpha+10")]
        [InlineData("1.2.3-alpha+beta")]
        public void SemanticVersionStrictEqualityPreRelease(string versionString)
        {
            // Act
            SemanticVersion main = null;
            SemanticVersion.TryParse("1.2.3-alpha", out main);

            SemanticVersion semVer = null;
            SemanticVersion.TryParse(versionString, out semVer);

            // Assert
            Assert.True(main.Equals(semVer));
            Assert.True(semVer.Equals(main));

            Assert.True(main.GetHashCode() == semVer.GetHashCode());
        }

        [Theory]
        [InlineData("2.7")]
        [InlineData("1.3.4.5")]
        [InlineData("1.3-alpha")]
        [InlineData("1.3 .4")]
        [InlineData("2.3.18.2-a")]
        [InlineData("1.2.3-A..B")]
        [InlineData("01.2.3")]
        [InlineData("1.02.3")]
        [InlineData("1.2.03")]
        [InlineData(".2.03")]
        [InlineData("1.2.")]
        [InlineData("1.2.3-a$b")]
        [InlineData("a.b.c")]
        [InlineData("1.2.3-00")]
        [InlineData("1.2.3-A.00.B")]
        public void TryParseStrictReturnsFalseIfVersionIsNotStrictSemVer(string version)
        {
            // Act 
            SemanticVersion semanticVersion;
            var result = SemanticVersion.TryParse(version, out semanticVersion);

            // Assert
            Assert.False(result);
            Assert.Null(semanticVersion);
        }
    }
}
