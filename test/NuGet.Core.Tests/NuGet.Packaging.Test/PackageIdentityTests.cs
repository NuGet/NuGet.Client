// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Packaging.Core.Test
{
    public class PackageIdentityTests
    {
        [Theory]
        [InlineData("Example", "Example", null)]
        [InlineData("Example.Package@1.2.3", "Example.Package", "1.2.3")]
        [InlineData("Example-Package_1@1.2", "Example-Package_1", "1.2.0")]
        [InlineData("Example@1.2.3-beta.1+metadata", "Example", "1.2.3-beta.1+metadata")]
        public void ParseAtSeparated_ValidInput_ReturnsIdentity(string value, string id, string version)
        {
            var identity = PackageIdentity.ParseAtSeparated(value);

            Assert.Equal(id, identity.Id);
            Assert.Equal(version != null, identity.HasVersion);
            if (version != null)
            {
                Assert.Equal(NuGetVersion.Parse(version), identity.Version);
            }
        }

        [Fact]
        public void ParseAtSeparated_Null_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => PackageIdentity.ParseAtSeparated(null!));
        }

        [Theory]
        [InlineData("")]
        [InlineData("@1.2.3")]
        [InlineData("Invalid/Id@1.2.3")]
        [InlineData("Invalid Id")]
        public void ParseAtSeparated_InvalidId_PreservesValidationError(string value)
        {
            var id = value.Split('@')[0];
            var expected = Assert.Throws<ArgumentException>(() => PackageIdValidator.ValidatePackageId(id));

            var actual = Assert.Throws<ArgumentException>(() => PackageIdentity.ParseAtSeparated(value));

            Assert.Equal(expected.Message, actual.Message);
        }

        [Theory]
        [InlineData("")]
        [InlineData("one")]
        [InlineData("1.0@2.0")]
        [InlineData("[1.0,2.0)")]
        public void ParseAtSeparated_InvalidVersion_PreservesInnerException(string version)
        {
            var expected = Assert.Throws<ArgumentException>(() => NuGetVersion.Parse(version));

            var actual = Assert.Throws<ArgumentException>(() => PackageIdentity.ParseAtSeparated("Example@" + version));

            Assert.Equal("value", actual.ParamName);
            Assert.Contains("Example", actual.Message);
            Assert.Contains(version, actual.Message);
            Assert.Equal(expected.Message, Assert.IsType<ArgumentException>(actual.InnerException).Message);
        }

        [Fact]
        public void TestToString()
        {
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            Assert.Equal("packageA.1.0.0", packageIdentity.ToString());

            var packageIdentityNoVersion = new PackageIdentity("packageB", null);
            Assert.Equal("packageB", packageIdentityNoVersion.ToString());

            var formattedString = string.Format("This is package '{0}'", packageIdentity);
            Assert.Equal("This is package 'packageA.1.0.0'", formattedString);
        }
    }
}
