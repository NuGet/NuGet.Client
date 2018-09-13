// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using FluentAssertions;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Configuration.Test
{
    public class RepositoryItemTests
    {
        [Fact]
        public void RepositoryItem_WithoutName_Throws()
        {
            // Arrange
            var config = @"
<configuration>
    <SectionName>
        <repository serviceIndex=""https://api.test/index/"">
            <certificate fingerprint=""abcdefg"" hashAlgorithm=""Sha256"" allowUntrustedRoot=""true""  />
        </repository>
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
                ex.Message.Should().Be(string.Format("Unable to parse config file because: Missing required attribute 'name' in element 'repository'. Path: '{0}'.", Path.Combine(mockBaseDirectory, nugetConfigPath)));
            }
        }

        [Fact]
        public void RepositoryItem_WithoutServiceIndex_Throws()
        {
            // Arrange
            var config = @"
<configuration>
    <SectionName>
        <repository name=""repositoryName"">
            <certificate fingerprint=""abcdefg"" hashAlgorithm=""Sha256"" allowUntrustedRoot=""true""  />
        </repository>
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
                ex.Message.Should().Be(string.Format("Unable to parse config file because: Missing required attribute 'serviceIndex' in element 'repository'. Path: '{0}'.", Path.Combine(mockBaseDirectory, nugetConfigPath)));
            }
        }

        [Fact]
        public void RepositoryItem_WithoutCertificates_Throws()
        {
            // Arrange
            var config = @"
<configuration>
    <SectionName>
        <repository name=""repositoryName"" serviceIndex=""https://api.test/index/"">
        </repository>
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
        public void RepositoryItem_WithEmptyOwners_Throws()
        {
            // Arrange
            var config = @"
<configuration>
    <SectionName>
        <repository name=""repositoryName"" serviceIndex=""https://api.test/index/"">
            <certificate fingerprint=""abcdefg"" hashAlgorithm=""Sha256"" allowUntrustedRoot=""true""  />
            <owners></owners>
        </repository>
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
                ex.Message.Should().Be(string.Format("Unable to parse config file because: Owners item must only have text content and cannot be empty. Path: '{0}'.", Path.Combine(mockBaseDirectory, nugetConfigPath)));
            }
        }

        [Fact]
        public void RepositoryItem_WithMultipleOwners_Throws()
        {
            // Arrange
            var config = @"
<configuration>
    <SectionName>
        <repository name=""repositoryName"" serviceIndex=""https://api.test/index/"">
            <certificate fingerprint=""abcdefg"" hashAlgorithm=""Sha256"" allowUntrustedRoot=""true""  />
            <owners>one,two,three</owners>
            <owners>four,five,six</owners>
        </repository>
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
                ex.Message.Should().Be(string.Format("Unable to parse config file because: A repository item can only have one owners item in it. Path: '{0}'.", Path.Combine(mockBaseDirectory, nugetConfigPath)));
            }
        }

        [Fact]
        public void RepositoryItem_WithCertificatesAndNoOwners_ParsedCorrectly()
        {
            // Arrange
            var config = @"
<configuration>
    <SectionName>
        <repository name=""repositoryName"" serviceIndex=""https://api.test/index/"">
            <certificate fingerprint=""abcdefg"" hashAlgorithm=""Sha256"" allowUntrustedRoot=""true""  />
        </repository>
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
                var item = section.Items.First() as RepositoryItem;

                var expectedItem = new RepositoryItem("repositoryName", "https://api.test/index/",
                    new CertificateItem("abcdefg", Common.HashAlgorithmName.SHA256, allowUntrustedRoot: true));
                item.DeepEquals(expectedItem).Should().BeTrue();
            }
        }

        [Fact]
        public void RepositoryItem_WithCertificatesAndOwners_ParsedCorrectly()
        {
            // Arrange
            var config = @"
<configuration>
    <SectionName>
        <repository name=""repositoryName"" serviceIndex=""https://api.test/index/"">
            <certificate fingerprint=""abcdefg"" hashAlgorithm=""Sha256"" allowUntrustedRoot=""true""  />
            <owners>test;text</owners>
        </repository>
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
                var item = section.Items.First() as RepositoryItem;

                var expectedItem = new RepositoryItem("repositoryName", "https://api.test/index/", "test;text",
                    new CertificateItem("abcdefg", Common.HashAlgorithmName.SHA256, allowUntrustedRoot: true));
                item.DeepEquals(expectedItem).Should().BeTrue();
            }
        }
    }
}
