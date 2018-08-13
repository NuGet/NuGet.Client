// Copyright(c) .NET Foundation.All rights reserved.
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
    public class SettingSectionTests
    {
        [Fact]
        public void GetValues_UnexistantChild_ReturnsNull()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <SectionName>
        <add key='key1' value='value1' />
        <add key='key2' value='value2' />
    </SectionName>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settingsFile = new SettingsFile(mockBaseDirectory);

                // Act
                var section = settingsFile.GetSection("SectionName");
                section.Should().NotBeNull();

                var key3Element = section.GetFirstItemWithAttribute<AddItem>("key", "key3");

                // Assert
                key3Element.Should().BeNull();
            }
        }

        [Fact]
        public void GetValues_ReturnsAllChildElements()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <SectionName>
        <add key='key1' value='value1' />
        <add key='key2' value='value2' />
    </SectionName>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settingsFile = new SettingsFile(mockBaseDirectory);

                // Act
                var section = settingsFile.GetSection("SectionName");
                section.Should().NotBeNull();

                var children = section.Children;

                // Assert
                children.Should().NotBeEmpty();
                children.Count.Should().Be(2);
                children.Should().AllBeOfType<AddItem>();
            }
        }

        [Fact]
        public void WithAClear_ParseClearCorrectly()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <config>
        <add key='key0' value='value0' />
        <add key='key1' value='value1' meta1='data1' meta2='data2'/>
        <clear />
        <add key='key2' value='value2' meta3='data3'/>
    </config>
</configuration>";

            var expectedItem = new AddItem("key2", "value2", new Dictionary<string, string> {
                    { "meta3", "data3" }
                });

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settingsFile = new SettingsFile(mockBaseDirectory);

                // Act
                var section = settingsFile.GetSection("config");
                section.Should().NotBeNull();

                var children = section.Children.ToList();

                // Assert
                children.Should().NotBeEmpty();
                children.Count.Should().Be(2);
                children.FirstOrDefault().Should().BeOfType<ClearItem>();
                children[1].DeepEquals(expectedItem).Should().BeTrue();
            }
        }

        [Fact]
        public void AddChild_WhenChildHasOrigin_Throws()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <Section>
        <add key='key0' value='value0' />
        <add key='key1' value='value1' meta1='data1' meta2='data2'/>
    </Section>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            using (var mockRandomDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockRandomDirectory, config);

                var settingsFile = new SettingsFile(mockBaseDirectory);
                var secondSettingsFile = new SettingsFile(mockRandomDirectory);

                // Act
                var section = settingsFile.GetSection("Section");
                section.Should().NotBeNull();

                var secondSection = secondSettingsFile.GetSection("Section");
                secondSection.Should().NotBeNull();

                var addElement = section.GetFirstItemWithAttribute<AddItem>("key", "key0");
                addElement.Should().NotBeNull();

                var ex = Record.Exception(() => secondSection.AddChild(addElement));
                ex.Should().NotBeNull();
                ex.Should().BeOfType<InvalidOperationException>();
                ex.Message.Should().Be(string.Format("Cannot add an element that is already part of another config. Path: '{0}'.", Path.Combine(mockBaseDirectory, nugetConfigPath)));
            }
        }

        [Fact]
        public void AddChild_Succeds()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <Section>
        <add key='key0' value='value0' />
        <add key='key1' value='value1' meta1='data1' meta2='data2'/>
    </Section>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var configFileHash = ConfigurationFileTestUtility.GetFileHash(Path.Combine(mockBaseDirectory, nugetConfigPath));

                var settingsFile = new SettingsFile(mockBaseDirectory);

                // Act
                var section = settingsFile.GetSection("Section");
                section.Should().NotBeNull();

                section.AddChild(new AddItem("key2", "value2")).Should().BeTrue();
                section.Children.Count.Should().Be(3);

                var updatedFileHash = ConfigurationFileTestUtility.GetFileHash(Path.Combine(mockBaseDirectory, nugetConfigPath));
                updatedFileHash.Should().NotBeEquivalentTo(configFileHash);
            }
        }

        [Fact]
        public void AddChild_ToMachineWide_ReturnsFalse()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <Section>
        <add key='key0' value='value0' />
        <add key='key1' value='value1' meta1='data1' meta2='data2'/>
    </Section>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var configFileHash = ConfigurationFileTestUtility.GetFileHash(Path.Combine(mockBaseDirectory, nugetConfigPath));

                var settingsFile = new SettingsFile(mockBaseDirectory, nugetConfigPath, isMachineWide: true);

                // Act
                var section = settingsFile.GetSection("Section");
                section.Should().NotBeNull();

                section.AddChild(new AddItem("key2", "value2")).Should().BeFalse();
                section.Children.Count.Should().Be(2);

                var updatedFileHash = ConfigurationFileTestUtility.GetFileHash(Path.Combine(mockBaseDirectory, nugetConfigPath));
                updatedFileHash.Should().BeEquivalentTo(configFileHash);
            }
        }

        [Fact]
        public void RemoveChild_Succeeds()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <Section>
        <add key='key0' value='value0' />
        <add key='key1' value='value1' meta1='data1' meta2='data2'/>
    </Section>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var configFileHash = ConfigurationFileTestUtility.GetFileHash(Path.Combine(mockBaseDirectory, nugetConfigPath));

                var settingsFile = new SettingsFile(mockBaseDirectory);

                // Act
                var section = settingsFile.GetSection("Section");
                section.Should().NotBeNull();

                var child = section.GetFirstItemWithAttribute<AddItem>("key", "key0");
                child.Should().NotBeNull();

                section.RemoveChild(child).Should().BeTrue();

                var updatedFileHash = ConfigurationFileTestUtility.GetFileHash(Path.Combine(mockBaseDirectory, nugetConfigPath));
                updatedFileHash.Should().NotBeEquivalentTo(configFileHash);

                section.Children.Count.Should().Be(1);
                var deletedChild = section.GetFirstItemWithAttribute<AddItem>("key", "key0");
                deletedChild.Should().BeNull();
            }
        }

        [Fact]
        public void RemoveChild_ToMachineWide_ReturnsFalse()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <Section>
        <add key='key0' value='value0' />
        <add key='key1' value='value1' meta1='data1' meta2='data2'/>
    </Section>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var configFileHash = ConfigurationFileTestUtility.GetFileHash(Path.Combine(mockBaseDirectory, nugetConfigPath));

                var settingsFile = new SettingsFile(mockBaseDirectory, nugetConfigPath, isMachineWide: true);

                // Act
                var section = settingsFile.GetSection("Section");
                section.Should().NotBeNull();

                var child = section.GetFirstItemWithAttribute<AddItem>("key", "key0");
                child.Should().NotBeNull();

                section.RemoveChild(child).Should().BeFalse();

                var updatedFileHash = ConfigurationFileTestUtility.GetFileHash(Path.Combine(mockBaseDirectory, nugetConfigPath));
                updatedFileHash.Should().BeEquivalentTo(configFileHash);
            }
        }


        [Fact]
        public void RemoveChild_OnlyOneChild_SucceedsAndRemovesSection()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <Section>
        <add key='key0' value='value0' />
    </Section>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var configFileHash = ConfigurationFileTestUtility.GetFileHash(Path.Combine(mockBaseDirectory, nugetConfigPath));

                var settingsFile = new SettingsFile(mockBaseDirectory);

                // Act
                var section = settingsFile.GetSection("Section");
                section.Should().NotBeNull();

                var child = section.GetFirstItemWithAttribute<AddItem>("key", "key0");
                child.Should().NotBeNull();

                section.RemoveChild(child).Should().BeTrue();

                section.Origin.Should().BeNull();
                section.Parent.Should().BeNull();

                var updatedFileHash = ConfigurationFileTestUtility.GetFileHash(Path.Combine(mockBaseDirectory, nugetConfigPath));
                updatedFileHash.Should().NotBeEquivalentTo(configFileHash);

                section = settingsFile.GetSection("Section");
                section.Should().BeNull();
            }
        }

        [Fact]
        public void RemoveChild_UnexistantChild_ReturnsFalse()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <Section>
        <add key='key0' value='value0' />
        <add key='key1' value='value1' meta1='data1' meta2='data2'/>
    </Section>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var configFileHash = ConfigurationFileTestUtility.GetFileHash(Path.Combine(mockBaseDirectory, nugetConfigPath));

                var settingsFile = new SettingsFile(mockBaseDirectory);

                // Act
                var section = settingsFile.GetSection("Section");
                section.Should().NotBeNull();

                section.RemoveChild(new AddItem("key7", "value7")).Should().BeFalse();

                var updatedFileHash = ConfigurationFileTestUtility.GetFileHash(Path.Combine(mockBaseDirectory, nugetConfigPath));
                updatedFileHash.Should().BeEquivalentTo(configFileHash);

                section.Children.Count.Should().Be(2);
            }
        }

        [Fact]
        public void Merge_JoinsTwoSectionsCorrectly()
        {
            var firstSection = new SettingsSection("Section", new AddItem("key1", "value1"), new AddItem("key2", "value2"));
            var secondSection = new SettingsSection("Section", new AddItem("key2", "valueX"), new AddItem("key3", "value3"));

            var expectedSection = new SettingsSection("Section", new AddItem("key1", "value1"), new AddItem("key2", "valueX"), new AddItem("key3", "value3"));

            firstSection.Merge(secondSection);

            firstSection.DeepEquals(expectedSection).Should().BeTrue();
        }

        [Fact]
        public void Merge_WithTwoDifferentSections_Throws()
        {
            var firstSection = new SettingsSection("Section", new AddItem("key1", "value1"), new AddItem("key2", "value2"));
            var secondSection = new SettingsSection("SectionName", new AddItem("key2", "valueX"), new AddItem("key3", "value3"));

            var ex = Record.Exception(() => firstSection.Merge(secondSection));
            ex.Should().NotBeNull();
            ex.Should().BeOfType<ArgumentException>();
            ex.Message.Should().Be("Cannot merge two different sections.");
        }

        [Fact]
        public void RemovingFromCollection_WithValidSection_DeletesTheSectionAndReturnsTrue()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""DeleteMe"" value=""value"" />
        <add key=""keyNotToDelete"" value=""value"" />
    </SectionName>
    <SectionName2>
        <add key=""key"" value=""value"" />
    </SectionName2>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settingsFile = new SettingsFile(mockBaseDirectory);

                // Act & Assert
                var section = settingsFile.GetSection("SectionName");
                section.Should().NotBeNull();

                section.RemoveFromCollection().Should().BeTrue();

                var result = ConfigurationFileTestUtility.RemoveWhitespace(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName2>
        <add key=""key"" value=""value"" />
    </SectionName2>
</configuration>");

                ConfigurationFileTestUtility.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath))).Should().Be(result);
                section = settingsFile.GetSection("SectionName");
                section.Should().BeNull();
            }
        }
    }
}
