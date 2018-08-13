// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using NuGet.Common;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Configuration.Test
{
    public class AddItemTests
    {
        [Theory]
        [InlineData(null, "value")]
        [InlineData("", "value")]
        public void Constructor_WithEmptyOrNullKey_Throws(string key, string value)
        {
            var ex = Record.Exception(() => new AddItem(key: key, value: value));
            ex.Should().NotBeNull();
            ex.Should().BeOfType<ArgumentNullException>();
        }


        [Fact]
        public void WithoutRequiredAttributes_Throws()
        {
            // Arrange
            var config = @"
<configuration>
    <SectionName>
        <add Key='key2' Value='value2' />
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
                ex.Message.Should().Be(string.Format("Unable to parse config file '{0}'.", Path.Combine(mockBaseDirectory, nugetConfigPath)));
            }
        }

        [Fact]
        public void SingleTag_WithOnlyKeyAndValue_ParsedSuccessfully()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <Section>
        <add key='key1' value='value1' />
    </Section>
</configuration>";

            var expectedSetting = new AddItem("key1", "value1");

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settingsFile = new SettingsFile(mockBaseDirectory);

                // Act
                var section = settingsFile.GetSection("Section");
                section.Should().NotBeNull();

                var element = section.Children.FirstOrDefault();
                element.Should().NotBeNull();

                // Assert
                element.DeepEquals(expectedSetting).Should().BeTrue();
            }
        }

        [Fact]
        public void MultiTag_WithOnlyKeyAndValue_ParsedSuccessfully()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <Section>
        <add key='key1' value='value1'></add>
    </Section>
</configuration>";

            var expectedSetting = new AddItem("key1", "value1");

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settingsFile = new SettingsFile(mockBaseDirectory);

                // Act
                var section = settingsFile.GetSection("Section");
                section.Should().NotBeNull();

                var element = section.Children.FirstOrDefault();
                element.Should().NotBeNull();

                // Assert
                element.DeepEquals(expectedSetting).Should().BeTrue();
            }
        }

        [Fact]
        public void WithAdditionalMetada_ParsedSuccessfully()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <Section>
        <add key='key1' value='value1' meta1='data1' meta2='data2'/>
    </Section>
</configuration>";

            var expectedSetting = new AddItem("key1", "value1",
                new Dictionary<string, string>{
                    { "meta1", "data1" },
                    { "meta2", "data2" }
                });

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settingsFile = new SettingsFile(mockBaseDirectory);

                // Act
                var section = settingsFile.GetSection("Section");
                section.Should().NotBeNull();

                var value = section.Children.FirstOrDefault();
                value.Should().NotBeNull();

                // Assert
                value.DeepEquals(expectedSetting).Should().BeTrue();
            }
        }

        [Fact]
        public void Parsing_ElementWithChildren_Throws()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <Section>
        <add key='key1' value='value1'>
            <add key='key2' value='value2' />
        </add>
    </Section>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);

                // Act and Assert
                var ex = Record.Exception(() => new SettingsFile(mockBaseDirectory));

                ex.Should().NotBeNull();
                ex.Should().BeOfType<NuGetConfigurationException>();
                ex.Message.Should().Be(string.Format("Error parsing NuGet.Config. Element '{0}' cannot have descendant elements. Path: '{1}'.", "add", Path.Combine(mockBaseDirectory, nugetConfigPath)));
            }
        }

        [Fact]
        public void UpdatingAttribute_InMachineWide_ReturnsFalse()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <Section>
        <add key='key1' value='value1' />
    </Section>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var configFileHash = ConfigurationFileTestUtility.GetFileHash(Path.Combine(mockBaseDirectory, nugetConfigPath));

                // Act and Assert
                var settingsFile = new SettingsFile(mockBaseDirectory, nugetConfigPath, isMachineWide: true);

                // Act
                var section = settingsFile.GetSection("Section");
                section.Should().NotBeNull();

                var element = section.Children.FirstOrDefault() as AddItem;
                element.Should().NotBeNull();

                element.UpdateAttributeValue("value", "newValue").Should().BeFalse();
                element.Value.Should().Be("value1");

                var updatedFileHash = ConfigurationFileTestUtility.GetFileHash(Path.Combine(mockBaseDirectory, nugetConfigPath));
                updatedFileHash.Should().BeEquivalentTo(configFileHash);
            }
        }

        [Fact]
        public void UpdatingAttribute_SuccessfullyUpdatesConfigFile()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <Section>
        <add key='key1' value='value1' />
    </Section>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var configFileHash = ConfigurationFileTestUtility.GetFileHash(Path.Combine(mockBaseDirectory, nugetConfigPath));

                // Act and Assert
                var settingsFile = new SettingsFile(mockBaseDirectory);

                // Act
                var section = settingsFile.GetSection("Section");
                section.Should().NotBeNull();

                var element = section.Children.FirstOrDefault() as AddItem;
                element.Should().NotBeNull();

                element.UpdateAttributeValue("value", "newValue").Should().BeTrue();
                element.Value.Should().Be("newValue");

                var updatedFileHash = ConfigurationFileTestUtility.GetFileHash(Path.Combine(mockBaseDirectory, nugetConfigPath));
                updatedFileHash.Should().NotBeEquivalentTo(configFileHash);
            }
        }

        [Fact]
        public void GetValueAsPath_ResolvesPathsCorrectly()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <!-- values that are relative paths -->
        <add key=""key1"" value=""..\value1"" />
        <add key=""key2"" value=""a\b\c"" />
        <add key=""key3"" value="".\a\b\c"" />

        <!-- values that are not relative paths -->
        <add key=""key4"" value=""c:\value2"" />
        <add key=""key5"" value=""http://value3"" />
        <add key=""key6"" value=""\\a\b\c"" />
        <add key=""key7"" value=""\a\b\c"" />
    </SectionName>
</configuration>";

            if (!RuntimeEnvironmentHelper.IsWindows)
            {
                config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <!-- values that are relative paths -->
        <add key=""key1"" value=""../value1"" />
        <add key=""key2"" value=""a/b/c"" />
        <add key=""key3"" value=""./a/b/c"" />

        <!-- values that are not relative paths -->
        <add key=""key5"" value=""http://value3"" />
        <add key=""key7"" value=""/a/b/c"" />
    </SectionName>
</configuration>";
            }

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                var section = settings.GetSection("SectionName");

                // Assert
                section.Should().NotBeNull();
                section.Children.Should().NotBeEmpty();
                section.Children.Should().AllBeOfType<AddItem>();

                if (RuntimeEnvironmentHelper.IsWindows)
                {
                    section.Children.Count.Should().Be(7);

                    var item = section.GetFirstItemWithAttribute<AddItem>("key", "key1");
                    item.Should().NotBeNull();
                    item.GetValueAsPath().Should().Be(Path.Combine(mockBaseDirectory, @"..\value1"));
                    item = section.GetFirstItemWithAttribute<AddItem>("key", "key2");
                    item.Should().NotBeNull();
                    item.GetValueAsPath().Should().Be(Path.Combine(mockBaseDirectory, @"a\b\c"));
                    item = section.GetFirstItemWithAttribute<AddItem>("key", "key3");
                    item.Should().NotBeNull();
                    item.GetValueAsPath().Should().Be(Path.Combine(mockBaseDirectory, @".\a\b\c"));
                    item = section.GetFirstItemWithAttribute<AddItem>("key", "key4");
                    item.Should().NotBeNull();
                    item.GetValueAsPath().Should().Be(@"c:\value2");
                    item = section.GetFirstItemWithAttribute<AddItem>("key", "key5");
                    item.Should().NotBeNull();
                    item.GetValueAsPath().Should().Be(@"http://value3");
                    item = section.GetFirstItemWithAttribute<AddItem>("key", "key6");
                    item.Should().NotBeNull();
                    item.GetValueAsPath().Should().Be(@"\\a\b\c");
                    item = section.GetFirstItemWithAttribute<AddItem>("key", "key7");
                    item.Should().NotBeNull();
                    item.GetValueAsPath().Should().Be(Path.Combine(Path.GetPathRoot(mockBaseDirectory), @"a\b\c"));
                }
                else
                {
                    section.Children.Count.Should().Be(5);

                    var item = section.GetFirstItemWithAttribute<AddItem>("key", "key1");
                    item.Should().NotBeNull();
                    item.GetValueAsPath().Should().Be(Path.Combine(mockBaseDirectory, @"../value1"));
                    item = section.GetFirstItemWithAttribute<AddItem>("key", "key2");
                    item.Should().NotBeNull();
                    item.GetValueAsPath().Should().Be(Path.Combine(mockBaseDirectory, @"a/b/c"));
                    item = section.GetFirstItemWithAttribute<AddItem>("key", "key3");
                    item.Should().NotBeNull();
                    item.GetValueAsPath().Should().Be(Path.Combine(mockBaseDirectory, @"./a/b/c"));
                    item = section.GetFirstItemWithAttribute<AddItem>("key", "key5");
                    item.Should().NotBeNull();
                    item.GetValueAsPath().Should().Be(@"http://value3");
                    item = section.GetFirstItemWithAttribute<AddItem>("key", "key7");
                    item.Should().NotBeNull();
                    item.GetValueAsPath().Should().Be(@"/a/b/c");
                }
            }
        }
    }
}
