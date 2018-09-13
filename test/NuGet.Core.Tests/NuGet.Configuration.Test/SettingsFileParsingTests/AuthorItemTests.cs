// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using FluentAssertions;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Configuration.Test
{
    public class AuthorItemTests
    {
        [Fact]
        public void AuthorItem_WithoutName_Throws()
        {
             // Arrange
            var config = @"
<configuration>
    <SectionName>
        <author>
            <certificate fingerprint=""abcdefg"" hashAlgorithm=""Sha256"" allowUntrustedRoot=""true""  />
        </author>
    </SectionName>
</configuration>";
            var nugetConfigPath = "NuGet.Config";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);

                // Act and Assert
                var ex = Record.Exception(() => new SettingsFile(mockBaseDirectory));

                ex.Should().NotBeNull();
                ex.Should().BeOfType<NuGetConfigurationException>();
                ex.Message.Should().Be(string.Format("Unable to parse config file because: Missing required attribute 'name' in element 'author'. Path: '{0}'.", Path.Combine(mockBaseDirectory, nugetConfigPath)));
            }
        }

        [Fact]
        public void AuthorItem_WithoutCertificates_Throws()
        {
            // Arrange
            var config = @"
<configuration>
    <SectionName>
        <author name=""authorname"" />
    </SectionName>
</configuration>";
            var nugetConfigPath = "NuGet.Config";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);

                // Act and Assert
                var ex = Record.Exception(() => new SettingsFile(mockBaseDirectory));

                ex.Should().NotBeNull();
                ex.Should().BeOfType<NuGetConfigurationException>();
                ex.Message.Should().Be(string.Format("Unable to parse config file because: A trusted signer entry must have at least one certificate entry. Path: '{0}'.", Path.Combine(mockBaseDirectory, nugetConfigPath)));
            }
        }

        [Fact]
        public void AuthorItem_WithCertificates_ParsedCorrectly()
        {
            // Arrange
            var config = @"
<configuration>
    <SectionName>
        <author name=""authorName"">
            <certificate fingerprint=""abcdefg"" hashAlgorithm=""Sha256"" allowUntrustedRoot=""true""  />
        </author>
    </SectionName>
</configuration>";
            var nugetConfigPath = "NuGet.Config";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);

                // Act and Assert
                var settingsFile = new SettingsFile(mockBaseDirectory);
                var section = settingsFile.GetSection("SectionName");
                section.Should().NotBeNull();

                section.Items.Count.Should().Be(1);
                var item = section.Items.First() as AuthorItem;

                var expectedItem = new AuthorItem("authorName",
                    new CertificateItem("abcdefg", Common.HashAlgorithmName.SHA256, allowUntrustedRoot: true));
                item.DeepEquals(expectedItem).Should().BeTrue();
            }
        }
    }
}
