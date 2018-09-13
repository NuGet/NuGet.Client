// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using FluentAssertions;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Configuration.Test
{
    public class CertificateItemTests
    {
        [Fact]
        public void CertificateItem_WithoutFingerprint_Throws()
        {
            // Arrange
            var config = @"
<configuration>
    <SectionName>
        <certificate hashAlgorithm=""Sha256"" allowUntrustedRoot=""true""  />
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
                ex.Message.Should().Be(string.Format("Unable to parse config file because: Missing required attribute 'fingerprint' in element 'certificate'. Path: '{0}'.", Path.Combine(mockBaseDirectory, nugetConfigPath)));
            }
        }

        [Fact]
        public void CertificateItem_WithoutHashAlgorithm_Throws()
        {
            // Arrange
            var config = @"
<configuration>
    <SectionName>
        <certificate fingerprint=""abcdefg"" allowUntrustedRoot=""true""  />
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
                ex.Message.Should().Be(string.Format("Unable to parse config file because: Missing required attribute 'hashAlgorithm' in element 'certificate'. Path: '{0}'.", Path.Combine(mockBaseDirectory, nugetConfigPath)));
            }
        }

        [Fact]
        public void CertificateItem_WithUnsupportedHashAlgorithm_Throws()
        {
            // Arrange
            var config = @"
<configuration>
    <SectionName>
        <certificate fingerprint=""abcdefg"" hashAlgorithm=""sha32"" allowUntrustedRoot=""true""  />
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
                ex.Message.Should().Be(string.Format("Unable to parse config file because: Certificate entry has an unsupported hash algorithm: 'sha32'. Path: '{0}'.", Path.Combine(mockBaseDirectory, nugetConfigPath)));
            }
        }

        [Fact]
        public void CertificateItem_WithoutAllowUntrustedRoot_Throws()
        {
            // Arrange
            var config = @"
<configuration>
    <SectionName>
        <certificate fingerprint=""abcdefg"" hashAlgorithm=""Sha256"" />
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
                ex.Message.Should().Be(string.Format("Unable to parse config file because: Missing required attribute 'allowUntrustedRoot' in element 'certificate'. Path: '{0}'.", Path.Combine(mockBaseDirectory, nugetConfigPath)));
            }
        }

        [Fact]
        public void CertificateItem_WithChildren_Throws()
        {
            // Arrange
            var config = @"
<configuration>
    <SectionName>
        <certificate fingerprint=""abcdefg"" hashAlgorithm=""Sha256"" allowUntrustedRoot=""true"">
            <add key=""key"" value=""val"" />
        </certificate>
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
                ex.Message.Should().Be(string.Format("Error parsing NuGet.Config. Element 'certificate' cannot have descendant elements. Path: '{0}'.", Path.Combine(mockBaseDirectory, nugetConfigPath)));
            }
        }

        [Fact]
        public void CertificateItem_ParsedCorrectly()
        {
            // Arrange
            var config = @"
<configuration>
    <SectionName>
        <certificate fingerprint=""abcdefg"" hashAlgorithm=""Sha256"" allowUntrustedRoot=""true"" />
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
                var item = section.Items.First() as CertificateItem;

                var expectedItem = new CertificateItem("abcdefg", Common.HashAlgorithmName.SHA256, allowUntrustedRoot: true);
                item.DeepEquals(expectedItem).Should().BeTrue();
            }
        }
    }
}
