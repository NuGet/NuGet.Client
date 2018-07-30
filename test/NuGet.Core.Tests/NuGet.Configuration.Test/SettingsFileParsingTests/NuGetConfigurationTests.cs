// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using FluentAssertions;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Configuration.Test
{
    public class NuGetConfigurationTests
    {
        [Fact]
        public void AsXNode_ReturnsExpectedXNode()
        {
            var configuration = new NuGetConfiguration(
                new SettingsSection("Section",
                    new AddItem("key0", "value0")));

            var add = new XElement("add",
                        new XAttribute("key", "key0"),
                        new XAttribute("value", "value0"));
            var section = new XElement("Section");
            XElementUtility.AddIndented(section, add);
            var expectedXNode = new XElement("configuration");
            XElementUtility.AddIndented(expectedXNode, section);

            var xNode = configuration.AsXNode();

            XNode.DeepEquals(xNode, expectedXNode).Should().BeTrue();
        }

        [Fact]
        public void MergeSectionsInto_WhenSectionDoNotMatch_AllSectionsAreReturned()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <Section1>
        <add key='key1' value='value1' />
    </Section1>
    <Section2>
        <add key='key2' value='value2' />
    </Section2>
    <Section3>
        <add key='key3' value='value3' />
    </Section3>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settingsFile = new SettingsFile(mockBaseDirectory);

                // Act
                var settingsDict = new Dictionary<string, SettingsSection>() {
                    { "Section4", new SettingsSection("Section4", new AddItem("key4", "value4")) }
                };

                settingsFile.RootElement.MergeSectionsInto(settingsDict);

                var expectedSettingsDict = new Dictionary<string, SettingsSection>() {
                    { "Section4", new SettingsSection("Section4", new AddItem("key4", "value4")) },
                    { "Section1", new SettingsSection("Section1", new AddItem("key1", "value1")) },
                    { "Section2", new SettingsSection("Section2", new AddItem("key2", "value2")) },
                    { "Section3", new SettingsSection("Section3", new AddItem("key3", "value3")) }
                };

                foreach (var pair in settingsDict)
                {
                    expectedSettingsDict.TryGetValue(pair.Key, out var expectedSection).Should().BeTrue();
                    pair.Value.DeepEquals(expectedSection).Should().BeTrue();
                    expectedSettingsDict.Remove(pair.Key);
                }
                expectedSettingsDict.Should().BeEmpty();
            }
        }

        [Fact]
        public void MergeSectionsInto_WithSectionsInCommon_ReturnsConfigWithAllSectionsMerged()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <Section1>
        <add key='key1' value='value1' />
    </Section1>
    <Section2>
        <add key='key2' value='value2' />
    </Section2>
    <Section3>
        <add key='key3' value='value3' />
    </Section3>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settingsFile = new SettingsFile(mockBaseDirectory);

                // Act
                var settingsDict = new Dictionary<string, SettingsSection>() {
                    { "Section2", new SettingsSection("Section2", new AddItem("keyX", "valueX")) },
                    { "Section3", new SettingsSection("Section3", new AddItem("key3", "valueY")) },
                    { "Section4", new SettingsSection("Section4", new AddItem("key4", "value4")) }
                };

                settingsFile.RootElement.MergeSectionsInto(settingsDict);

                var expectedSettingsDict = new Dictionary<string, SettingsSection>() {
                    { "Section2", new SettingsSection("Section2", new AddItem("keyX", "valueX"), new AddItem("key2", "value2")) },
                    { "Section3", new SettingsSection("Section3", new AddItem("key3", "value3")) },
                    { "Section4", new SettingsSection("Section4", new AddItem("key4", "value4")) },
                    { "Section1", new SettingsSection("Section1", new AddItem("key1", "value1")) },
                };

                foreach (var pair in settingsDict)
                {
                    expectedSettingsDict.TryGetValue(pair.Key, out var expectedSection).Should().BeTrue();
                    pair.Value.DeepEquals(expectedSection).Should().BeTrue();
                    expectedSettingsDict.Remove(pair.Key);
                }
                expectedSettingsDict.Should().BeEmpty();
            }
        }

        [Fact]
        public void MergeSectionsInto_WithSectionsInCommon_AndClear_ClearsSection()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <Section1>
        <add key='key1' value='value1' />
    </Section1>
    <Section2>
        <clear />
    </Section2>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settingsFile = new SettingsFile(mockBaseDirectory);

                // Act
                var settingsDict = new Dictionary<string, SettingsSection>() {
                    { "Section2", new SettingsSection("Section2", new AddItem("keyX", "valueX")) },
                    { "Section3", new SettingsSection("Section3", new AddItem("key3", "valueY")) },
                };

                settingsFile.RootElement.MergeSectionsInto(settingsDict);

                var expectedSettingsDict = new Dictionary<string, SettingsSection>() {
                    { "Section2", new SettingsSection("Section2", new ClearItem()) },
                    { "Section3", new SettingsSection("Section3", new AddItem("key3", "valueY")) },
                    { "Section1", new SettingsSection("Section1", new AddItem("key1", "value1")) },
                };

                foreach (var pair in settingsDict)
                {
                    expectedSettingsDict.TryGetValue(pair.Key, out var expectedSection).Should().BeTrue();
                    pair.Value.DeepEquals(expectedSection).Should().BeTrue();
                    expectedSettingsDict.Remove(pair.Key);
                }
                expectedSettingsDict.Should().BeEmpty();
            }
        }

        [Fact]
        public void AddChild_OnMachineWideConfig_ReturnsFalse()
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

                var settingsFile = new SettingsFile(mockBaseDirectory, nugetConfigPath, isMachineWide: true);

                // Act
                var section = settingsFile.RootElement.GetSection("Section");
                section.Should().NotBeNull();

                section.AddChild(new AddItem("key2", "value2")).Should().BeFalse();
                section.Children.Count.Should().Be(1);

                var updatedFileHash = ConfigurationFileTestUtility.GetFileHash(Path.Combine(mockBaseDirectory, nugetConfigPath));
                updatedFileHash.Should().BeEquivalentTo(configFileHash);
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
                var section = settingsFile.RootElement.GetSection("Section");
                section.Should().NotBeNull();

                var ex = Record.Exception(() => secondSettingsFile.RootElement.AddChild(section));
                ex.Should().NotBeNull();
                ex.Should().BeOfType<InvalidOperationException>();
                ex.Message.Should().Be(string.Format("Cannot add an element that is already part of another config. Path: '{0}'.", Path.Combine(mockBaseDirectory, nugetConfigPath)));
            }
        }
    }
}
