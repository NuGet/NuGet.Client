// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using Xunit;

namespace NuGet.Packaging.Core.Test
{
    public class NuSpecCoreReaderTests
    {
        [Fact]
        public void GetPackageType_ReturnsDefaultIfPackageTypeIsNotSpecifiedInManfiest()
        {
            // Arrange
            var contents =
@"<?xml version=""1.0""?>
<package>
<metadata/>
</package>";

            var reader = new TestNuSpecCoreReader(contents);

            // Act
            var packageType = reader.GetPackageType();

            // Assert
            Assert.Same(PackageType.Default, packageType);
        }

        [Theory]
        [InlineData(@"<?xml version=""1.0""?>
<package xmlns=""a-random-xsd"">
  <metadata>
   <id>Test</id>
   <somestuff>some-value</somestuff>
   <ver>123</ver>
  </metadata>
</package>")]
        [InlineData(@"<?xml version=""1.0""?>
<package xmlns=""a-random-xsd"">
  <metadata>
   <packageType version=""2.0"">Managed</packageType>
   <id>Test</id>
   <somestuff>some-value</somestuff>
   <ver>123</ver>
  </metadata>
</package>")]
        public void GetMetadata_SkipsPackageTypeElement(string contents)
        {
            // Arrange
            var reader = new TestNuSpecCoreReader(contents);

            // Act
            var metadata = reader.GetMetadata();

            // Assert
            Assert.Collection(metadata,
                item =>
                {
                    Assert.Equal("id", item.Key);
                    Assert.Equal("Test", item.Value);
                },
                item =>
                {
                    Assert.Equal("somestuff", item.Key);
                    Assert.Equal("some-value", item.Value);
                },
                item =>
                {
                    Assert.Equal("ver", item.Key);
                    Assert.Equal("123", item.Value);
                });
        }

        [Theory]
        [InlineData(
@"<?xml version=""1.0""?>
<package>
  <metadata>
   <packageType version=""2.0"">Managed</packageType>
  </metadata>
</package>", "Managed", "2.0")]
        [InlineData(
@"<?xml version=""1.0""?>
<package>
  <metadata>
   <packageType version=""3.5"">SomeFormat</packageType>
  </metadata>
</package>", "SomeFormat", "3.5")]
        [InlineData(
@"<?xml version=""1.0""?>
<package>
  <metadata>
   <packageType>RandomFormat123</packageType>
  </metadata>
</package>", "RandomFormat123", "0.0")]
        public void GetPackageType_ReadsPackageTypeFromManifest(string contents, string expectedType, string expectedVersion)
        {
            // Arrange
            var reader = new TestNuSpecCoreReader(contents);

            // Act
            var packageType = reader.GetPackageType();

            // Assert
            Assert.Equal(expectedType, packageType.Name);
            Assert.Equal(expectedVersion, packageType.Version.ToString());
        }

        [Fact]
        public void GetPackageType_ThrowsIfPackageTypeVersionCannotBeRead()
        {
            // Arrange
            var contents =
@"<?xml version=""1.0""?>
<package>
  <metadata>
   <packageType version=""3.5-alpha"">SomeFormat</packageType>
  </metadata>
</package>";
            var reader = new TestNuSpecCoreReader(contents);

            // Act and Assert
            Assert.Throws<FormatException>(() => reader.GetPackageType());
        }

        public class TestNuSpecCoreReader : NuspecCoreReaderBase
        {
            public TestNuSpecCoreReader(string content)
                : base(new MemoryStream(Encoding.UTF8.GetBytes(content)))
            {
            }
        }
    }
}
