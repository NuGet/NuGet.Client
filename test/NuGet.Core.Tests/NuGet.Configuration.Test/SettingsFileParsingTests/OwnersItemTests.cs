// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using FluentAssertions;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Configuration.Test
{
    public class OwnersItemTests
    {
        [Fact]
        public void OwnersItem_WithoutContent_Throws()
        {
            // Arrange
            var config = @"
<configuration>
    <SectionName>
        <owners />
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
                ex.Message.Should().Be(
                    string.Format("Unable to parse config file because: Owners item must only have text content and cannot be empty. Path: '{0}'.", Path.Combine(mockBaseDirectory, nugetConfigPath)));
            }
        }

        [Fact]
        public void OwnersItem_WithItemsAsContent_Throws()
        {
            // Arrange
            var config = @"
<configuration>
    <SectionName>
        <owners>
            <add key=""key"" value=""val"" />
        </owners>
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
                ex.Message.Should().Be(
                    string.Format("Unable to parse config file because: Owners item must only have text content and cannot be empty. Path: '{0}'.", Path.Combine(mockBaseDirectory, nugetConfigPath)));
            }
        }

        [Fact]
        public void OwnersItem_WithItemsAndContentAsChildren_Throws()
        {
            // Arrange
            var config = @"
<configuration>
    <SectionName>
        <owners>
            test;text
            <add key=""key"" value=""val"" />
        </owners>
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
                ex.Message.Should().Be(
                    string.Format("Unable to parse config file because: Owners item must only have text content and cannot be empty. Path: '{0}'.", Path.Combine(mockBaseDirectory, nugetConfigPath)));
            }
        }

        [Fact]
        public void OwnersItem_ParsedCorrectly()
        {
            // Arrange
            var config = @"
<configuration>
    <SectionName>
        <owners>test;text;owner</owners>
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
                var item = section.Items.First() as OwnersItem;

                var expectedItem = new OwnersItem("test;text;owner");
                item.DeepEquals(expectedItem).Should().BeTrue();
            }
        }
    }
}
