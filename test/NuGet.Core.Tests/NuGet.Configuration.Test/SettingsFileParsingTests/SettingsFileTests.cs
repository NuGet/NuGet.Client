// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Configuration.Test
{
    public class SettingsFileTests
    {
        [Fact]
        public void Constructor_WithNullRoot_Throws()
        {
            // Act & Assert
            var ex = Record.Exception(() => new SettingsFile(null));
            Assert.NotNull(ex);
            Assert.IsAssignableFrom<ArgumentNullException>(ex);
        }

        [Fact]
        public void Constructor_WithMalformedConfig_Throws()
        {
            // Arrange
            var configFile = "NuGet.Config";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration><sectionName></configuration>");

                // Act & Assert
                var ex = Record.Exception(() => new SettingsFile(mockBaseDirectory));
                ex.Should().NotBeNull();
                ex.Should().BeOfType<NuGetConfigurationException>();
                ex.Message.Should().Be(string.Format("NuGet.Config is not valid XML. Path: '{0}'.", Path.Combine(mockBaseDirectory, configFile)));
            }
        }


        [Fact]
        public void Constructor_InvalidXml_Throws()
        {
            // Arrange
            var config = @"boo>";

            var nugetConfigPath = "NuGet.Config";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);

                // Act
                var ex = Record.Exception(() => new SettingsFile(mockBaseDirectory));

                // Assert
                ex.Should().NotBeNull();
                ex.Should().BeOfType<NuGetConfigurationException>();
                ex.Message.Should().Be(string.Format("NuGet.Config is not valid XML. Path: '{0}'.", Path.Combine(mockBaseDirectory, nugetConfigPath)));
            }
        }

        [Fact]
        public void Constructor_WithInvalidRootElement_Throws()
        {
            // Arrange
            var configFile = "NuGet.Config";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var config = @"
<notvalid>
    <SectionName>
        <add key='key1' value='value1' />
        <add key='key2' value='value2' />
    </SectionName>
</notvalid>";

                ConfigurationFileTestUtility.CreateConfigurationFile(configFile, mockBaseDirectory, config);

                // Act & Assert
                var ex = Record.Exception(() => new SettingsFile(mockBaseDirectory));
                ex.Should().NotBeNull();
                ex.Should().BeOfType<NuGetConfigurationException>();
                ex.Message.Should().Be(string.Format("NuGet.Config does not contain the expected root element: 'configuration'. Path: '{0}'.", Path.Combine(mockBaseDirectory, configFile)));
            }
        }

        [Fact]
        public void Constructor_ConfigurationPath_Succeds()
        {
            // Arrange
            var configFile = "NuGet.Config";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var config = @"
<configuration>
    <SectionName>
        <add key='key1' value='value1' />
        <add key='key2' value='value2' />
    </SectionName>
</configuration>";
                ConfigurationFileTestUtility.CreateConfigurationFile(configFile, mockBaseDirectory, config);

                // Act
                var settingsFile = new SettingsFile(mockBaseDirectory);

                // Assert
                var section = settingsFile.RootElement.GetSection("SectionName");
                section.Should().NotBeNull();

                var key1Element = section.GetFirstItemWithAttribute<AddItem>("key", "key1");
                var key2Element = section.GetFirstItemWithAttribute<AddItem>("key", "key2");
                key1Element.Should().NotBeNull();
                key2Element.Should().NotBeNull();

                key1Element.Value.Should().Be("value1");
                key2Element.Value.Should().Be("value2");
            }
        }

        [Fact]
        public void Constructor_ConfigurationPath_AndFilename_Succeds()
        {
            // Arrange
            var configFile = "NuGet.Config";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var config = @"
<configuration>
    <SectionName>
        <add key='key1' value='value1' />
        <add key='key2' value='value2' />
    </SectionName>
</configuration>";
                ConfigurationFileTestUtility.CreateConfigurationFile(configFile, mockBaseDirectory, config);

                // Act
                var settingsFile = new SettingsFile(mockBaseDirectory, configFile);

                // Assert
                var section = settingsFile.RootElement.GetSection("SectionName");
                section.Should().NotBeNull();

                var key1Element = section.GetFirstItemWithAttribute<AddItem>("key", "key1");
                var key2Element = section.GetFirstItemWithAttribute<AddItem>("key", "key2");
                key1Element.Should().NotBeNull();
                key2Element.Should().NotBeNull();

                key1Element.Value.Should().Be("value1");
                key2Element.Value.Should().Be("value2");
            }
        }

        [Fact]
        public void Constructor_CreateDefaultConfigFileIfNoConfig()
        {
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                // Act
                var settingsFile = new SettingsFile(mockBaseDirectory);

                // Assert
                var text = ConfigurationFileTestUtility.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory, "NuGet.Config")));

                var expectedResult = ConfigurationFileTestUtility.RemoveWhitespace(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
    <add key=""nuget.org"" value=""https://api.nuget.org/v3/index.json"" protocolVersion=""3"" />
    </packageSources>
</configuration>");

                text.Should().Be(expectedResult);
            }
        }

        [Fact]
        public void GetSections_WithNonExistantSection_ReturnsNull()
        {
            // Arrange
            var configFile = "NuGet.Config";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
                var settingsFile = new SettingsFile(mockBaseDirectory);

                // Act & Assert
                var section = settingsFile.RootElement.GetSection("DoesNotExist");
                section.Should().BeNull();
            }
        }

        [Fact]
        public void GetValues_DuplicatedSections_TakesFirstAndIgnoresRest()
        {
            // Arrange
            var config = @"
<configuration>
    <SectionName>
        <add key='key1' value='value1' />
    </SectionName>
    <SectionName>
        <add key='key2' value='value2' />
    </SectionName>
</configuration>";
            var nugetConfigPath = "NuGet.Config";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);

                // Act and Assert
                var settingsFile = new SettingsFile(mockBaseDirectory);
                settingsFile.Should().NotBeNull();

                settingsFile.RootElement.Children.Count.Should().Be(1);
                var section = settingsFile.RootElement.GetSection("SectionName");
                section.Should().NotBeNull();

                var addItem = section.Children.FirstOrDefault() as AddItem;
                addItem.Should().NotBeNull();

                addItem.Key.Should().Be("key1");
                addItem.Value.Should().Be("value1");
            }
        }

        [Fact]
        public void IsEmpty_WithEmptyValidConfig_ReturnsTrue()
        {
            // Arrange
            var configFile = "NuGet.Config";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
                var settingsFile = new SettingsFile(mockBaseDirectory);

                // Act & Assert
                settingsFile.IsEmpty().Should().BeTrue();
            }
        }

        [Fact]
        public void IsEmpty_WithNonemptyNuGetConfig_ReturnsFalse()
        {
            // Arrange
            var config = @"
<configuration>
    <SectionName>
        <add key='key1' value='value1' />
    </SectionName>
</configuration>";
            var configFile = "NuGet.Config";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(configFile, mockBaseDirectory, config);
                var settingsFile = new SettingsFile(mockBaseDirectory);

                // Act & Assert
                settingsFile.IsEmpty().Should().BeFalse();
            }
        }

        [Fact]
        public void ConnectSettingsFilesLinkedList_ConnectsConfigsCorrectly()
        {
            // Arrange
            var configFile = "NuGet.Config";
            using (var mockBaseDirectory = TestDirectory.Create())
            using (var mockSubDirectory = TestDirectory.Create(mockBaseDirectory))
            using (var mockSubSubDirectory = TestDirectory.Create(mockSubDirectory))
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
                ConfigurationFileTestUtility.CreateConfigurationFile(configFile, mockSubDirectory, @"<configuration></configuration>");
                ConfigurationFileTestUtility.CreateConfigurationFile(configFile, mockSubSubDirectory, @"<configuration></configuration>");

                var baseSettingsFile = new SettingsFile(mockBaseDirectory);
                var subSettingsFile = new SettingsFile(mockSubDirectory);
                var subSubSettingsFile = new SettingsFile(mockSubSubDirectory);

                // Act & Assert
                baseSettingsFile.Should().NotBeNull();
                subSettingsFile.Should().NotBeNull();
                subSubSettingsFile.Should().NotBeNull();

                SettingsFile.ConnectSettingsFilesLinkedList(new List<SettingsFile>() { baseSettingsFile, subSettingsFile, subSubSettingsFile });

                subSubSettingsFile.Next.Should().BeSameAs(subSettingsFile);
                subSettingsFile.Next.Should().BeSameAs(baseSettingsFile);
                baseSettingsFile.Next.Should().BeNull();
            }
        }
    }
}
