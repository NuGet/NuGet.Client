// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Moq;
using NuGet.Common;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Configuration.Test
{
    public class SettingsTests
    {
        [Fact]
        public void GetValues_SingleConfigFileWithClear_IgnoresClearedValues()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""key1"" value=""value1"" />
        <add key=""key2"" value=""value2"" />
        <clear />
        <add key=""key3"" value=""value3"" />
        <add key=""key4"" value=""value4"" />
    </SectionName>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                var section = settings.GetSection("SectionName");
                section.Should().NotBeNull();

                // Assert
                var children = section.Children.ToList();
                children.Count.Should().Be(3);
                children[1].DeepEquals(new AddItem("key3", "value3")).Should().BeTrue();
                children[2].DeepEquals(new AddItem("key4", "value4")).Should().BeTrue();
            }
        }

        [Fact]
        public void GetValues_MultipleConfigFilesWithClear_IgnoresClearedValues()
        {
            // Arrange
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var nugetConfigPath = "NuGet.Config";
                var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <clear /> <!-- i.e. ignore values from prior conf files -->
        <add key=""key3"" value=""value3"" />
        <add key=""key4"" value=""value4"" />
    </SectionName>
</configuration>";

                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, @"dir1\dir2"), config);
                config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""key1"" value=""value1"" />
        <add key=""key2"" value=""value2"" />
    </SectionName>
</configuration>";

                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1"), config);

                var settings = Settings.LoadDefaultSettings(
                    root: Path.Combine(mockBaseDirectory, @"dir1\dir2"),
                    configFileName: null,
                    machineWideSettings: null);

                // Act
                var section = settings.GetSection("SectionName");
                section.Should().NotBeNull();

                // Assert
                var children = section.Children.ToList();
                children.Count.Should().Be(3);

                children[0].Should().BeOfType<ClearItem>();
                children[1].DeepEquals(new AddItem("key3", "value3")).Should().BeTrue();
                children[2].DeepEquals(new AddItem("key4", "value4")).Should().BeTrue();
            }
        }

        [Fact]
        public void GetValues_ItemsProvideOriginData()
        {
            // Arrange
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var nugetConfigPath = "NuGet.Config";
                var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""key1"" value=""value3"" />
        <add key=""key3"" value=""value4"" />
    </SectionName>
</configuration>";

                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1", "dir2"), config);

                config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""key1"" value=""value1"" />
        <add key=""key2"" value=""value2"" />
    </SectionName>
</configuration>";
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1"), config);

                // Act
                var settings = Settings.LoadDefaultSettings(
                    root: Path.Combine(mockBaseDirectory, "dir1", "dir2"),
                    configFileName: null,
                    machineWideSettings: null);

                var section = settings.GetSection("SectionName");
                section.Should().NotBeNull();

                var parentConfig = Path.Combine(mockBaseDirectory, "dir1", "NuGet.Config");
                var childConfig = Path.Combine(mockBaseDirectory, "dir1", "dir2", "NuGet.Config");

                var item = section.GetFirstItemWithAttribute<AddItem>("key", "key1");
                item.Origin.ConfigFilePath.Should().Be(childConfig); // key1 was overidden, so it has a new origin!

                item = section.GetFirstItemWithAttribute<AddItem>("key", "key2");
                item.Origin.ConfigFilePath.Should().Be(parentConfig);

                item = section.GetFirstItemWithAttribute<AddItem>("key", "key3");
                item.Origin.ConfigFilePath.Should().Be(childConfig);
            }
        }

        [Fact]
        public void GetValues_MultipleConfigFiles_AllDifferentItems_MergeCorrectly()
        {
            // Arrange
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var nugetConfigPath = "NuGet.Config";
                var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""key3"" value=""value3"" />
        <add key=""key4"" value=""value4"" />
    </SectionName>
</configuration>";
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1", "dir2"), config);

                config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""key1"" value=""value1"" />
        <add key=""key2"" value=""value2"" />
    </SectionName>
</configuration>";
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1"), config);

                var settings = Settings.LoadDefaultSettings(
                    root: Path.Combine(mockBaseDirectory, "dir1", "dir2"),
                    configFileName: null,
                    machineWideSettings: null);

                // Assert
                var section = settings.GetSection("SectionName");
                section.Should().NotBeNull();

                section.Children.Count.Should().Be(4);

                var item = section.GetFirstItemWithAttribute<AddItem>("key", "key1") as AddItem;
                item.Value.Should().Be("value1");

                item = section.GetFirstItemWithAttribute<AddItem>("key", "key2") as AddItem;
                item.Value.Should().Be("value2");

                item = section.GetFirstItemWithAttribute<AddItem>("key", "key3") as AddItem;
                item.Value.Should().Be("value3");

                item = section.GetFirstItemWithAttribute<AddItem>("key", "key4") as AddItem;
                item.Value.Should().Be("value4");
            }
        }

        [Fact]
        public void GetValues_MultipleConfigFiles_WithDupes_MergeCorrectly()
        {
            // Arrange
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var nugetConfigPath = "NuGet.Config";
                var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""key1"" value=""LastOneWins1"" />
        <add key=""key2"" value=""LastOneWins2"" />
    </SectionName>
</configuration>";

                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, @"dir1\dir2"), config);
                config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""key1"" value=""value1"" />
        <add key=""key2"" value=""value2"" />
    </SectionName>
</configuration>";
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1"), config);

                var settings = Settings.LoadDefaultSettings(
                    root:  Path.Combine(mockBaseDirectory, @"dir1\dir2"),
                    configFileName: null,
                    machineWideSettings: null);

                // Assert
                var section = settings.GetSection("SectionName");
                section.Should().NotBeNull();

                var item = section.GetFirstItemWithAttribute<AddItem>("key", "key2");
                item.Value.Should().Be("LastOneWins2");

                item = section.GetFirstItemWithAttribute<AddItem>("key", "key1");
                item.Value.Should().Be("LastOneWins1");
            }
        }

        [Fact]
        public void GetValues_MultipleConfigFiles_WithClear_MergeCorrectly()
        {
            // Arrange
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var nugetConfigPath = "NuGet.Config";
                var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <clear /> <!-- i.e. ignore values from prior conf files -->
        <add key=""key2"" value=""value2"" />
    </SectionName>
</configuration>";

                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, @"dir1\dir2"), config);
                config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""key1"" value=""value1"" />
    </SectionName>
</configuration>";
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1"), config);

                var settings = Settings.LoadDefaultSettings(
                    root: Path.Combine(mockBaseDirectory, @"dir1\dir2"),
                    configFileName: null,
                    machineWideSettings: null);

                // Assert
                var section = settings.GetSection("SectionName");
                section.Should().NotBeNull();

                var item = section.GetFirstItemWithAttribute<AddItem>("key", "key2") as AddItem;
                item.Should().NotBeNull();
                item.Value.Should().Be("value2");

                section.GetFirstItemWithAttribute<AddItem>("key", "key1").Should().BeNull();
            }
        }

        [Theory]
        [InlineData(@"foo\bar")]
        [InlineData(@"..\Blah")]
        public void GetValues_ValueAsPath_ReturnsPathRelativeToConfigWhenPathIsNotRooted(string value)
        {
            // Arrange
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var config = string.Format(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""path-key"" value=""{0}"" />
    </SectionName>
</configuration>", value);

                ConfigurationFileTestUtility.CreateConfigurationFile("nuget.config", mockBaseDirectory, config);

                var settings = new Settings(mockBaseDirectory, "nuget.config");

                // Act
                var section = settings.GetSection("SectionName");
                section.Should().NotBeNull();

                var item = section.GetFirstItemWithAttribute<AddItem>("key", "path-key");
                item.Should().NotBeNull();

                var result = item.GetValueAsPath();

                // Assert
                result.Should().Be(Path.Combine(mockBaseDirectory, value));
            }
        }

        [Theory]
        [InlineData(@"z:\foo", "windows")]
        [InlineData(@"x:\foo\bar\qux", "windows")]
        [InlineData(@"\\share\folder\subfolder", "windows")]
        [InlineData(@"/a/b/c", "linux")]
        public void GetValues_ValueAsPath_ReturnsPathWhenPathIsRooted(string value, string os)
        {
            if ((os == "linux" && RuntimeEnvironmentHelper.IsLinux) || (os == "windows" && RuntimeEnvironmentHelper.IsWindows))
            {
                // Arrange
                using (var mockBaseDirectory = TestDirectory.Create())
                {
                    var config = string.Format(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""path-key"" value=""{0}"" />
    </SectionName>
</configuration>", value);
                    ConfigurationFileTestUtility.CreateConfigurationFile("nuget.config", mockBaseDirectory, config);
                    var settings = new Settings(mockBaseDirectory, "nuget.config");

                    // Act
                    var section = settings.GetSection("SectionName");
                    section.Should().NotBeNull();

                    var addItem = section.GetFirstItemWithAttribute<AddItem>("key", "path-key");
                    addItem.Should().NotBeNull();

                    var result = addItem.GetValueAsPath();
                    // Assert
                    result.Should().Be(value);
                }
            }
        }

        [Fact]
        public void GetValues_WithUserSpecifiedDefaultConfigFile_ParsedCorrectly()
        {
            // Arrange
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""key3"" value=""value3"" />
        <add key=""key4"" value=""value4"" />
    </SectionName>
</configuration>";

                ConfigurationFileTestUtility.CreateConfigurationFile("NuGet.Config", Path.Combine(mockBaseDirectory, "dir1"), config);
                config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""key1"" value=""value1"" />
        <add key=""key2"" value=""value2"" />
    </SectionName>
</configuration>";

                ConfigurationFileTestUtility.CreateConfigurationFile("UserDefinedConfigFile.config", Path.Combine(mockBaseDirectory, "dir1", "dir2"), config);

                var settings = Settings.LoadDefaultSettings(
                    root: Path.Combine(mockBaseDirectory, "dir1", "dir2"),
                    configFileName: "UserDefinedConfigFile.config",
                    machineWideSettings: null);

                // Act
                var section = settings.GetSection("SectionName");
                section.Should().NotBeNull();

                var children = section.Children.ToList();

                // Assert
                children.Count.Should().Be(2);
                children[0].DeepEquals(new AddItem("key1", "value1")).Should().BeTrue();
                children[1].DeepEquals(new AddItem("key2", "value2")).Should().BeTrue();
            }
        }

        [Fact]
        public void GetValues_WithMachineWideConfigFile_ParsedCorrectly()
        {
            // Arrange
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var fileContent1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
    <add key=""key1"" value=""value1"" />
    <add key=""key2"" value=""value2"" />
    </SectionName>
</configuration>";

                ConfigurationFileTestUtility.CreateConfigurationFile("a1.config", Path.Combine(mockBaseDirectory, "nuget", "Config"), fileContent1);
                var fileContent2 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
    <add key=""key2"" value=""value3"" />
    <add key=""key3"" value=""value4"" />
    </SectionName>
</configuration>";

                ConfigurationFileTestUtility.CreateConfigurationFile("a1.config", Path.Combine(mockBaseDirectory, "nuget", "Config", "IDE"), fileContent2);
                var fileContent3 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
    <add key=""key3"" value=""user"" />
    </SectionName>
</configuration>";

                ConfigurationFileTestUtility.CreateConfigurationFile("NuGet.Config", Path.Combine(mockBaseDirectory, "TestingGlobalPath"), fileContent3);

                var m = new Mock<IMachineWideSettings>();
                m.SetupGet(obj => obj.Settings).Returns(
                    Settings.LoadMachineWideSettings(Path.Combine(mockBaseDirectory, "nuget", "Config"), "IDE", "Version", "SKU"));

                // Act
                var settings = Settings.LoadSettings(
                    root: mockBaseDirectory,
                    configFileName: null,
                    machineWideSettings: m.Object,
                    loadUserWideSettings: true,
                    useTestingGlobalPath: true);

                // Assert
                var section = settings.GetSection("SectionName");
                section.Should().NotBeNull();

                var addItem = section.GetFirstItemWithAttribute<AddItem>("key", "key1");
                addItem.Should().NotBeNull();
                addItem.Value.Should().Be("value1");

                // the value in NuGet\Config\IDE\a1.config overrides the value in
                // NuGet\Config\a1.config
                addItem = section.GetFirstItemWithAttribute<AddItem>("key", "key2");
                addItem.Should().NotBeNull();
                addItem.Value.Should().Be("value3");

                // the value in user.config overrides the value in NuGet\Config\IDE\a1.config
                addItem = section.GetFirstItemWithAttribute<AddItem>("key", "key3");
                addItem.Should().NotBeNull();
                addItem.Value.Should().Be("user");
            }
        }

        [Fact]
        public void GetValues_FromTwoUserSettings_WithMachineWideSettings_ParsedCorrectly()
        {
            // Arrange
            using (var mockBaseDirectory = TestDirectory.Create())
            using (var mockBaseDirectory1 = TestDirectory.Create())
            using (var mockBaseDirectory2 = TestDirectory.Create())
            {
                var FileContent1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""key1"" value=""value1"" />
    </SectionName>
</configuration>";

                var FileContent2 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""key3"" value=""user1"" />
    </SectionName>
</configuration>";

                var FileContent3 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""key3"" value=""user2"" />
    </SectionName>
</configuration>";
                ConfigurationFileTestUtility.CreateConfigurationFile("a1.config", Path.Combine(mockBaseDirectory, "nuget", "Config"), FileContent1);
                ConfigurationFileTestUtility.CreateConfigurationFile("NuGet.Config", Path.Combine(mockBaseDirectory1, "TestingGlobalPath"), FileContent2);
                ConfigurationFileTestUtility.CreateConfigurationFile("NuGet.Config", Path.Combine(mockBaseDirectory2, "TestingGlobalPath"), FileContent3);

                var m = new Mock<IMachineWideSettings>();
                m.SetupGet(obj => obj.Settings).Returns(
                    Settings.LoadMachineWideSettings(Path.Combine(mockBaseDirectory, "nuget", "Config"), "IDE", "Version", "SKU"));

                // Act
                var settings1 = Settings.LoadSettings(
                    root: mockBaseDirectory1,
                    configFileName: null,
                    machineWideSettings: m.Object,
                    loadUserWideSettings: true,
                    useTestingGlobalPath: true);

                var settings2 = Settings.LoadSettings(
                    root: mockBaseDirectory2,
                    configFileName: null,
                    machineWideSettings: m.Object,
                    loadUserWideSettings: true,
                    useTestingGlobalPath: true);

                // Assert
                var section = settings1.GetSection("SectionName");
                section.Should().NotBeNull();

                var item = section.GetFirstItemWithAttribute<AddItem>("key", "key3");
                item.Should().NotBeNull();
                item.Value.Should().Be("user1");

                item = section.GetFirstItemWithAttribute<AddItem>("key", "key1");
                item.Should().NotBeNull();
                item.Value.Should().Be("value1");

                section = settings2.GetSection("SectionName");
                section.Should().NotBeNull();

                item = section.GetFirstItemWithAttribute<AddItem>("key", "key3");
                item.Should().NotBeNull();
                item.Value.Should().Be("user2");

                item = section.GetFirstItemWithAttribute<AddItem>("key", "key1");
                item.Should().NotBeNull();
                item.Value.Should().Be("value1");
            }
        }

        [Fact]
        public void SetItemInSection_WithEmptySectionName_Throws()
        {
            // Arrange
            var configFile = "NuGet.Config";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
                var settings = new Settings(mockBaseDirectory);

                // Act & Assert
                var ex = Record.Exception(() => settings.SetItemInSection("", new AddItem("SomeKey", "SomeValue")));
                ex.Should().NotBeNull();
                ex.Should().BeOfType<ArgumentNullException>();
            }
        }

        [Fact]
        public void SetItemInSection_WithNullItem_Throws()
        {
            // Arrange
            var configFile = "NuGet.Config";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
                var settings = new Settings(mockBaseDirectory);

                // Act & Assert
                var ex = Record.Exception(() => settings.SetItemInSection("SomeKey", item: null));
                ex.Should().NotBeNull();
                ex.Should().BeOfType<ArgumentNullException>();
            }
        }

        [Fact]
        public void SetItemInSection_SectionThatDoesntExist_WillAddSection()
        {
            // Arrange
            var configFile = "NuGet.Config";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""key"" value=""value"" />
    </SectionName>
</configuration>";

                ConfigurationFileTestUtility.CreateConfigurationFile(configFile, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                settings.SetItemInSection("NewSectionName", new AddItem("key", "value")).Should().BeTrue();

                // Assert
                var result = ConfigurationFileTestUtility.RemoveWhitespace(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""key"" value=""value"" />
    </SectionName>
    <NewSectionName>
        <add key=""key"" value=""value"" />
    </NewSectionName>
</configuration>");

                ConfigurationFileTestUtility.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory, configFile))).Should().Be(result);
                var section = settings.GetSection("NewSectionName");
                section.Should().NotBeNull();
                section.Children.Count.Should().Be(1);
            }
        }

        [Fact]
        public void SetItemInSection_SectionThatExist_WillAddToSection()
        {
            // Arrange
            var configFile = "NuGet.Config";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""key"" value=""value"" />
    </SectionName>
</configuration>";

                ConfigurationFileTestUtility.CreateConfigurationFile(configFile, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                settings.SetItemInSection("SectionName", new AddItem("keyTwo", "valueTwo")).Should().BeTrue();

                // Assert
                var result = ConfigurationFileTestUtility.RemoveWhitespace(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""key"" value=""value"" />
        <add key=""keyTwo"" value=""valueTwo"" />
    </SectionName>
</configuration>");

                ConfigurationFileTestUtility.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory, configFile))).Should().Be(result);
            }
        }

        [Fact]
        public void SetItemInSection_WhenItemExistsInSection_OverrideItem()
        {
            // Arrange
            var configFile = "NuGet.Config";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""key"" value=""value"" />
    </SectionName>
</configuration>";

                ConfigurationFileTestUtility.CreateConfigurationFile(configFile, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                settings.SetItemInSection("SectionName", new AddItem("key", "NewValue")).Should().BeTrue();

                // Assert
                var result = ConfigurationFileTestUtility.RemoveWhitespace(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""key"" value=""NewValue"" />
    </SectionName>
</configuration>");

                ConfigurationFileTestUtility.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory, configFile))).Should().Be(result);
                var section = settings.GetSection("SectionName");
                section.Should().NotBeNull();

                var item = section.GetFirstItemWithAttribute<AddItem>("key", "key");
                item.Should().NotBeNull();
                item.Value.Should().Be("NewValue");
            }
        }

        [Fact]
        public void SetItemInSection_WithMachineWideSettings_OnlyUpdatesUserSpecific()
        {
            // Arrange
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var a1Config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""key1"" value=""value1"" />
    </SectionName>
</configuration>";

                var a2Config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""key2"" value=""value2"" />
        <add key=""key3"" value=""value3"" />
    </SectionName>
</configuration>";

                var userConfig = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""key3"" value=""user"" />
    </SectionName>
</configuration>";

                ConfigurationFileTestUtility.CreateConfigurationFile("a1.config", Path.Combine(mockBaseDirectory, "nuget", "Config"), a1Config);
                ConfigurationFileTestUtility.CreateConfigurationFile("a2.config", Path.Combine(mockBaseDirectory, "nuget", "Config", "IDE"), a2Config);
                ConfigurationFileTestUtility.CreateConfigurationFile("user.config", mockBaseDirectory, userConfig);

                var m = new Mock<IMachineWideSettings>();
                m.SetupGet(obj => obj.Settings).Returns(
                    Settings.LoadMachineWideSettings(Path.Combine(mockBaseDirectory, "nuget", "Config"), "IDE", "Version", "SKU"));

                var settings = Settings.LoadDefaultSettings(
                    root: mockBaseDirectory,
                    configFileName: "user.config",
                    machineWideSettings: m.Object);

                // Act
                settings.SetItemInSection("SectionName", new AddItem("key1", "newValue")).Should().BeTrue();

                // Assert
                var text = ConfigurationFileTestUtility.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory, "user.config")));
                var expectedResult = ConfigurationFileTestUtility.RemoveWhitespace(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""key3"" value=""user"" />
        <add key=""key1"" value=""newValue"" />
    </SectionName>
</configuration>");

                text.Should().Be(expectedResult);

                text = ConfigurationFileTestUtility.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory, "nuget", "Config", "a1.config")));
                ConfigurationFileTestUtility.RemoveWhitespace(a1Config).Should().Be(text);

                text = ConfigurationFileTestUtility.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory, "nuget", "Config", "IDE", "a2.config")));
                ConfigurationFileTestUtility.RemoveWhitespace(a2Config).Should().Be(text);

                var section = settings.GetSection("SectionName");
                section.Should().NotBeNull();

                var key1 = section.GetFirstItemWithAttribute<AddItem>("key", "key1");
                key1.Should().NotBeNull();

                key1.Value.Should().Be("newValue");
            }
        }

        [Fact]
        public void SetItemInSection_WithMultipleSections_ClearedInDifferentConfigs_AddsItemInFurthestCompatibleConfig()
        {
            var configFile = "NuGet.Config";

            // Arrange
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var userConfigFurthest = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <Section1>
        <clear />
        <add key=""key11"" value=""value11"" />
    </Section1>
    <Section2>
        <add key=""key12"" value=""value12"" />
    </Section2>
    <Section3>
        <add key=""key13"" value=""value13"" />
    </Section3>
</configuration>";

                var userConfigMiddle = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <Section1>
        <add key=""key21"" value=""value21"" />
    </Section1>
    <Section2>
        <clear />
        <add key=""key22"" value=""value22"" />
    </Section2>
    <Section3>
        <add key=""key23"" value=""value23"" />
    </Section3>
</configuration>";

                var userConfigClosest = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <Section1>
        <add key=""key31"" value=""value31"" />
    </Section1>
    <Section2>
        <add key=""key32"" value=""value32"" />
    </Section2>
    <Section3>
        <clear />
        <add key=""key33"" value=""value33"" />
    </Section3>
</configuration>";

                ConfigurationFileTestUtility.CreateConfigurationFile(configFile, Path.Combine(mockBaseDirectory, "d1", "d2"), userConfigClosest);
                ConfigurationFileTestUtility.CreateConfigurationFile(configFile, Path.Combine(mockBaseDirectory, "d1"), userConfigMiddle);
                ConfigurationFileTestUtility.CreateConfigurationFile(configFile, mockBaseDirectory, userConfigFurthest);
                var settings = Settings.LoadSettings(
                    root: Path.Combine(mockBaseDirectory, "d1", "d2"),
                    configFileName: null,
                    machineWideSettings: null,
                    loadUserWideSettings: true,
                    useTestingGlobalPath: true);

                // Act
                settings.SetItemInSection("Section1", new AddItem("newKey1", "newValue")).Should().BeTrue();
                settings.SetItemInSection("Section2", new AddItem("newKey2", "newValue")).Should().BeTrue();
                settings.SetItemInSection("Section3", new AddItem("newKey3", "newValue")).Should().BeTrue();
                settings.SetItemInSection("SectionN", new AddItem("newKeyN", "newValue")).Should().BeTrue();

                // Assert
                var actualFurthestConfig = ConfigurationFileTestUtility.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory, configFile)));
                var actualMiddleConfig = ConfigurationFileTestUtility.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory, "d1", configFile)));
                var actualClosestConfig = ConfigurationFileTestUtility.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory, "d1", "d2", configFile)));
                var actualTestingGlobalConfig = ConfigurationFileTestUtility.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory, "d1", "d2", "TestingGlobalPath", configFile)));

                var expectedFurthestConfig = ConfigurationFileTestUtility.RemoveWhitespace(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <Section1>
        <clear />
        <add key=""key11"" value=""value11"" />
        <add key=""newKey1"" value=""newValue"" />
    </Section1>
    <Section2>
        <add key=""key12"" value=""value12"" />
    </Section2>
    <Section3>
        <add key=""key13"" value=""value13"" />
    </Section3>
</configuration>");

                var expectedMiddleConfig = ConfigurationFileTestUtility.RemoveWhitespace(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <Section1>
        <add key=""key21"" value=""value21"" />
    </Section1>
    <Section2>
        <clear />
        <add key=""key22"" value=""value22"" />
        <add key=""newKey2"" value=""newValue"" />
    </Section2>
    <Section3>
        <add key=""key23"" value=""value23"" />
    </Section3>
</configuration>");

                var expectedClosestConfig = ConfigurationFileTestUtility.RemoveWhitespace(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <Section1>
        <add key=""key31"" value=""value31"" />
    </Section1>
    <Section2>
        <add key=""key32"" value=""value32"" />
    </Section2>
    <Section3>
        <clear />
        <add key=""key33"" value=""value33"" />
        <add key=""newKey3"" value=""newValue"" />
    </Section3>
</configuration>");

                var expectedTestingGlobalConfig = ConfigurationFileTestUtility.RemoveWhitespace(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""nuget.org"" value=""https://api.nuget.org/v3/index.json"" protocolVersion=""3"" />
    </packageSources>
    <SectionN>
        <add key=""newKeyN"" value=""newValue"" />
    </SectionN>
</configuration>");

                actualFurthestConfig.Should().Be(expectedFurthestConfig);
                actualMiddleConfig.Should().Be(expectedMiddleConfig);
                actualClosestConfig.Should().Be(expectedClosestConfig);
                actualTestingGlobalConfig.Should().Be(expectedTestingGlobalConfig);

                var section = settings.GetSection("Section1");
                section.Should().NotBeNull();

                var key1 = section.GetFirstItemWithAttribute<AddItem>("key", "newKey1");
                key1.Should().NotBeNull();
                key1.Value.Should().Be("newValue");

                section = settings.GetSection("Section2");
                section.Should().NotBeNull();

                key1 = section.GetFirstItemWithAttribute<AddItem>("key", "newKey2");
                key1.Should().NotBeNull();
                key1.Value.Should().Be("newValue");

                section = settings.GetSection("Section3");
                section.Should().NotBeNull();

                key1 = section.GetFirstItemWithAttribute<AddItem>("key", "newKey3");
                key1.Should().NotBeNull();
                key1.Value.Should().Be("newValue");

                section = settings.GetSection("SectionN");
                section.Should().NotBeNull();

                key1 = section.GetFirstItemWithAttribute<AddItem>("key", "newKeyN");
                key1.Should().NotBeNull();
                key1.Value.Should().Be("newValue");
            }
        }

        [Fact]
        public void SetItemInSection_PreserveComments()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<!-- Comment in nuget configuration -->
<configuration>
    <!-- Will add an item to this section -->
    <SectionName>
        <add key=""key1"" value=""value"" />
    </SectionName>
    <!-- This section wont have a new item -->
    <SectionName2>
        <add key=""key"" value=""value"" />
    </SectionName2>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act & Assert
                settings.SetItemInSection("SectionName", new AddItem("newKey", "value")).Should().BeTrue();

                var result = ConfigurationFileTestUtility.RemoveWhitespace(@"<?xml version=""1.0"" encoding=""utf-8""?>
<!-- Comment in nuget configuration -->
<configuration>
    <!-- Will add an item to this section -->
    <SectionName>
        <add key=""key1"" value=""value"" />
        <add key=""newKey"" value=""value"" />
    </SectionName>
    <!-- This section wont have a new item -->
    <SectionName2>
        <add key=""key"" value=""value"" />
    </SectionName2>
</configuration>");

                ConfigurationFileTestUtility.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath))).Should().Be(result);

                var section = settings.GetSection("SectionName");
                section.Should().NotBeNull();

                var item = section.GetFirstItemWithAttribute<AddItem>("key", "newKey");
                item.Should().NotBeNull();
                item.Value.Should().Be("value");
            }
        }

        [Fact]
        public void SetItemInSection_PreserveUnknownItems()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""key1"" value=""value"" />
    </SectionName>
    <SectionName2>
        <add key=""key"" value=""value"" />
    </SectionName2>
    <UnknownSection>
        <UnknownItem meta1=""data1"" />
        <OtherUnknownItem>
        </OtherUnknownItem>
    </UnknownSection>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act & Assert
                settings.SetItemInSection("SectionName", new AddItem("newKey", "value")).Should().BeTrue();

                var result = ConfigurationFileTestUtility.RemoveWhitespace(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""key1"" value=""value"" />
        <add key=""newKey"" value=""value"" />
    </SectionName>
    <SectionName2>
        <add key=""key"" value=""value"" />
    </SectionName2>
    <UnknownSection>
        <UnknownItem meta1=""data1"" />
        <OtherUnknownItem>
        </OtherUnknownItem>
    </UnknownSection>
</configuration>");

                ConfigurationFileTestUtility.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath))).Should().Be(result);

                var section = settings.GetSection("SectionName");
                section.Should().NotBeNull();

                var item = section.GetFirstItemWithAttribute<AddItem>("key", "newKey");
                item.Should().NotBeNull();
                item.Value.Should().Be("value");
            }
        }

        [Fact]
        public void RemovingValue_MachineWide_ReturnsFalse()
        {
            // Arrange
            var config1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""DeleteMe"" value=""value1"" />
        <add key=""keyNotToDelete"" value=""value"" />
    </SectionName>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile("a1.config", Path.Combine(mockBaseDirectory, "nuget", "Config"), config1);
               
                var m = new Mock<IMachineWideSettings>();
                m.SetupGet(obj => obj.Settings).Returns(
                    Settings.LoadMachineWideSettings(Path.Combine(mockBaseDirectory, "nuget", "Config")));

                var settings = Settings.LoadDefaultSettings(
                    root: mockBaseDirectory,
                    configFileName: null,
                    machineWideSettings: m.Object);

                // Act & Assert
                var section = settings.GetSection("SectionName");
                section.Should().NotBeNull();

                var item = section.GetFirstItemWithAttribute<AddItem>("key", "DeleteMe");
                item.Should().NotBeNull();
                item.Value.Should().Be("value1");

                item.RemoveFromCollection().Should().BeFalse();

                ConfigurationFileTestUtility.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory, "nuget", "Config", "a1.config"))).Should().Be(ConfigurationFileTestUtility.RemoveWhitespace(config1));

                section = settings.GetSection("SectionName");
                section.Should().NotBeNull();

                item = section.GetFirstItemWithAttribute<AddItem>("key", "DeleteMe");
                item.Should().NotBeNull();
                item.Value.Should().Be("value1");
            }
        }

        [Fact]
        public void RemovingValue_LastValueInSectionOfComputedValues_RemovesSection()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""DeleteMe"" value=""value2"" />
    </SectionName>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);

                var settings = new Settings(root: Path.Combine(mockBaseDirectory));

                // Act & Assert
                var section = settings.GetSection("SectionName");
                section.Should().NotBeNull();

                var item = section.GetFirstItemWithAttribute<AddItem>("key", "DeleteMe");
                item.Should().NotBeNull();
                item.Value.Should().Be("value2");

                item.RemoveFromCollection().Should().BeTrue();

                ConfigurationFileTestUtility.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath))).Should().Be(ConfigurationFileTestUtility.RemoveWhitespace(@"<?xml version=""1.0"" encoding=""utf-8""?><configuration></configuration>"));

                section = settings.GetSection("SectionName");
                section.Should().BeNull();
            }
        }

        [Fact]
        public void RemovingValue_LastValueInOneSpecificConfig_RemovesSectionInThatConfig_DoesNotRemoveSectionInComputedValues()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""DeleteMe"" value=""value1"" />
        <add key=""keyNotToDelete"" value=""value"" />
    </SectionName>
</configuration>";

            var config2 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""DeleteMe"" value=""value2"" />
    </SectionName>
</configuration>";

            var config3 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""DeleteMe"" value=""value3"" />
        <add key=""keyNotToDelete"" value=""value"" />
    </SectionName>
</configuration>";


            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config1);
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1"), config2);
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1", "dir2"), config3);

                var settings = Settings.LoadDefaultSettings(root: Path.Combine(mockBaseDirectory, "dir1", "dir2"));

                // Act & Assert
                var section = settings.GetSection("SectionName");
                section.Should().NotBeNull();

                var item = section.GetFirstItemWithAttribute<AddItem>("key", "DeleteMe");
                item.Should().NotBeNull();
                item.Value.Should().Be("value3");

                item.RemoveFromCollection().Should().BeTrue();

                var result = ConfigurationFileTestUtility.RemoveWhitespace(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""keyNotToDelete"" value=""value"" />
    </SectionName>
</configuration>");

                ConfigurationFileTestUtility.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory, "dir1", "dir2", nugetConfigPath))).Should().Be(result);
                ConfigurationFileTestUtility.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath))).Should().Be(result);

                ConfigurationFileTestUtility.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory, "dir1", nugetConfigPath))).Should().Be(ConfigurationFileTestUtility.RemoveWhitespace(@"<?xml version=""1.0"" encoding=""utf-8""?><configuration></configuration>"));

                section = settings.GetSection("SectionName");
                section.Should().NotBeNull();

                item = section.GetFirstItemWithAttribute<AddItem>("key", "DeleteMe");
                item.Should().BeNull();
            }
        }

        [Fact]
        public void RemovingValue_RemovesAllMergedValuesInAllConfigs_ExceptMachineWide()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""DeleteMe"" value=""value1"" />
        <add key=""keyNotToDelete"" value=""value"" />
    </SectionName>
</configuration>";

            var config2 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""DeleteMe"" value=""value2"" />
        <add key=""keyNotToDelete"" value=""value"" />
    </SectionName>
</configuration>";

            var config3 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""DeleteMe"" value=""value3"" />
        <add key=""keyNotToDelete"" value=""value"" />
    </SectionName>
</configuration>";


            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile("a1.config", Path.Combine(mockBaseDirectory, "nuget", "Config"), config1);
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1"), config2);
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1", "dir2"), config3);

                var m = new Mock<IMachineWideSettings>();
                m.SetupGet(obj => obj.Settings).Returns(
                    Settings.LoadMachineWideSettings(Path.Combine(mockBaseDirectory, "nuget", "Config")));

                var settings = Settings.LoadDefaultSettings(
                    root: Path.Combine(mockBaseDirectory, "dir1", "dir2"),
                    configFileName: null,
                    machineWideSettings: m.Object);

                // Act & Assert
                var section = settings.GetSection("SectionName");
                section.Should().NotBeNull();

                var item = section.GetFirstItemWithAttribute<AddItem>("key", "DeleteMe");
                item.Should().NotBeNull();
                item.Value.Should().Be("value3");

                item.RemoveFromCollection().Should().BeFalse();

                var result = ConfigurationFileTestUtility.RemoveWhitespace(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""keyNotToDelete"" value=""value"" />
    </SectionName>
</configuration>");

                ConfigurationFileTestUtility.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory, "dir1", "dir2", nugetConfigPath))).Should().Be(result);
                ConfigurationFileTestUtility.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory, "dir1", nugetConfigPath))).Should().Be(result);
                ConfigurationFileTestUtility.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory, "nuget", "Config", "a1.config"))).Should().Be(ConfigurationFileTestUtility.RemoveWhitespace(config1));

                section = settings.GetSection("SectionName");
                section.Should().NotBeNull();

                item = section.GetFirstItemWithAttribute<AddItem>("key", "DeleteMe");
                item.Should().NotBeNull();
                item.Value.Should().Be("value1");
            }
        }

        [Fact]
        public void RemovingValue_RemovesAllMergedValuesInAllConfigs()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""DeleteMe"" value=""value1"" />
        <add key=""keyNotToDelete"" value=""value"" />
    </SectionName>
</configuration>";

            var config2 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""DeleteMe"" value=""value2"" />
        <add key=""keyNotToDelete"" value=""value"" />
    </SectionName>
</configuration>";

            var config3 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""DeleteMe"" value=""value3"" />
        <add key=""keyNotToDelete"" value=""value"" />
    </SectionName>
</configuration>";


            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config1);
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1"), config2);
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1", "dir2"), config3);

                var settings = Settings.LoadDefaultSettings(Path.Combine(mockBaseDirectory, "dir1", "dir2"));

                // Act & Assert
                var section = settings.GetSection("SectionName");
                section.Should().NotBeNull();

                var item = section.GetFirstItemWithAttribute<AddItem>("key", "DeleteMe");
                item.Should().NotBeNull();
                item.Value.Should().Be("value3");

                item.RemoveFromCollection().Should().BeTrue();

                var result = ConfigurationFileTestUtility.RemoveWhitespace(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""keyNotToDelete"" value=""value"" />
    </SectionName>
</configuration>");

                ConfigurationFileTestUtility.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory, "dir1", "dir2", nugetConfigPath))).Should().Be(result);
                ConfigurationFileTestUtility.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory, "dir1", nugetConfigPath))).Should().Be(result);
                ConfigurationFileTestUtility.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath))).Should().Be(result);

                section = settings.GetSection("SectionName");
                section.Should().NotBeNull();

                item = section.GetFirstItemWithAttribute<AddItem>("key", "DeleteMe");
                item.Should().BeNull();
            }
        }

        [Fact]
        public void RemovingValue_RemovesAllMergedValuesInAllConfigsAfterClear()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""DeleteMe"" value=""value1"" />
        <add key=""keyNotToDelete"" value=""value"" />
    </SectionName>
</configuration>";

            var config2 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <clear />
        <add key=""DeleteMe"" value=""value2"" />
        <add key=""keyNotToDelete"" value=""value"" />
    </SectionName>
</configuration>";

            var config3 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""DeleteMe"" value=""value3"" />
        <add key=""keyNotToDelete"" value=""value"" />
    </SectionName>
</configuration>";


            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config1);
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1"), config2);
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1", "dir2"), config3);

                var settings = Settings.LoadDefaultSettings(Path.Combine(mockBaseDirectory, "dir1", "dir2"));

                // Act & Assert
                var section = settings.GetSection("SectionName");
                section.Should().NotBeNull();

                var item = section.GetFirstItemWithAttribute<AddItem>("key", "DeleteMe");
                item.Should().NotBeNull();
                item.Value.Should().Be("value3");

                item.RemoveFromCollection().Should().BeTrue();

                var result1 = ConfigurationFileTestUtility.RemoveWhitespace(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""keyNotToDelete"" value=""value"" />
    </SectionName>
</configuration>");

                var result2 = ConfigurationFileTestUtility.RemoveWhitespace(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <clear />
        <add key=""keyNotToDelete"" value=""value"" />
    </SectionName>
</configuration>");

                ConfigurationFileTestUtility.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory, "dir1", "dir2", nugetConfigPath))).Should().Be(result1);
                ConfigurationFileTestUtility.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory, "dir1", nugetConfigPath))).Should().Be(result2);
                ConfigurationFileTestUtility.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath))).Should().Be(ConfigurationFileTestUtility.RemoveWhitespace(config1));

                section = settings.GetSection("SectionName");
                section.Should().NotBeNull();

                item = section.GetFirstItemWithAttribute<AddItem>("key", "DeleteMe");
                item.Should().BeNull();
            }
        }

        [Fact]
        public void RemovingValue_WithValidSectionAndKey_DeletesTheEntryAndReturnsTrue()
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
                var settings = new Settings(mockBaseDirectory);

                // Act & Assert
                var section = settings.GetSection("SectionName");
                section.Should().NotBeNull();

                var item = section.GetFirstItemWithAttribute<SettingsItem>("key", "DeleteMe");
                item.RemoveFromCollection().Should().BeTrue();

                var result = ConfigurationFileTestUtility.RemoveWhitespace(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""keyNotToDelete"" value=""value"" />
    </SectionName>
    <SectionName2>
        <add key=""key"" value=""value"" />
    </SectionName2>
</configuration>");

                ConfigurationFileTestUtility.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath))).Should().Be(result);

                section = settings.GetSection("SectionName");
                section.Should().NotBeNull();

                item = section.GetFirstItemWithAttribute<AddItem>("key", "DeleteMe");
                item.Should().BeNull();
            }
        }

        [Fact]
        public void RemovingValue_PreserveComments()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<!-- Comment in nuget configuration -->
<configuration>
    <!-- This section has the item to delete -->
    <SectionName>
        <add key=""DeleteMe"" value=""value"" />
        <add key=""keyNotToDelete"" value=""value"" />
    </SectionName>
    <!-- This section doesn't have the item to delete -->
    <SectionName2>
        <add key=""key"" value=""value"" />
    </SectionName2>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act & Assert
                var section = settings.GetSection("SectionName");
                section.Should().NotBeNull();

                var item = section.GetFirstItemWithAttribute<SettingsItem>("key", "DeleteMe");
                item.RemoveFromCollection().Should().BeTrue();

                var result = ConfigurationFileTestUtility.RemoveWhitespace(@"<?xml version=""1.0"" encoding=""utf-8""?>
<!-- Comment in nuget configuration -->
<configuration>
    <!-- This section has the item to delete -->
    <SectionName>
        <add key=""keyNotToDelete"" value=""value"" />
    </SectionName>
    <!-- This section doesn't have the item to delete -->
    <SectionName2>
        <add key=""key"" value=""value"" />
    </SectionName2>
</configuration>");

                ConfigurationFileTestUtility.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath))).Should().Be(result);

                section = settings.GetSection("SectionName");
                section.Should().NotBeNull();

                item = section.GetFirstItemWithAttribute<AddItem>("key", "DeleteMe");
                item.Should().BeNull();
            }
        }

        [Fact]
        public void RemovingValue_PreserveUnknownItems()
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
    <UnknownSection>
        <UnknownItem meta1=""data1"" />
        <OtherUnknownItem>
        </OtherUnknownItem>
    </UnknownSection>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act & Assert
                var section = settings.GetSection("SectionName");
                section.Should().NotBeNull();

                var item = section.GetFirstItemWithAttribute<SettingsItem>("key", "DeleteMe");
                item.RemoveFromCollection().Should().BeTrue();

                var result = ConfigurationFileTestUtility.RemoveWhitespace(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""keyNotToDelete"" value=""value"" />
    </SectionName>
    <SectionName2>
        <add key=""key"" value=""value"" />
    </SectionName2>
    <UnknownSection>
        <UnknownItem meta1=""data1"" />
        <OtherUnknownItem>
        </OtherUnknownItem>
    </UnknownSection>
</configuration>");

                ConfigurationFileTestUtility.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath))).Should().Be(result);

                section = settings.GetSection("SectionName");
                section.Should().NotBeNull();

                item = section.GetFirstItemWithAttribute<AddItem>("key", "DeleteMe");
                item.Should().BeNull();
            }
        }

        // Checks that the correct files are read, in the right order,
        // when laoding machine wide settings.
        [Fact]
        public void LoadMachineWideSettings_ReadsFilesCorrectly()
        {
            // Arrange
            var fileContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
        <configuration>
          <SectionName>
            <add key=""key"" value=""value"" />
          </SectionName>
        </configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile("a1.config", Path.Combine(mockBaseDirectory, "nuget"), fileContent);
                ConfigurationFileTestUtility.CreateConfigurationFile("a1.config", Path.Combine(mockBaseDirectory, "nuget", "Config"), fileContent);
                ConfigurationFileTestUtility.CreateConfigurationFile("a2.config", Path.Combine(mockBaseDirectory, "nuget", "Config"), fileContent);
                ConfigurationFileTestUtility.CreateConfigurationFile("a3.xconfig", Path.Combine(mockBaseDirectory, "nuget", "Config"), fileContent);
                ConfigurationFileTestUtility.CreateConfigurationFile("a1.config", Path.Combine(mockBaseDirectory, "nuget", "Config", "IDE"), fileContent);
                ConfigurationFileTestUtility.CreateConfigurationFile("a2.config", Path.Combine(mockBaseDirectory, "nuget", "Config", "IDE"), fileContent);
                ConfigurationFileTestUtility.CreateConfigurationFile("a3.xconfig", Path.Combine(mockBaseDirectory, "nuget", "Config", "IDE"), fileContent);
                ConfigurationFileTestUtility.CreateConfigurationFile("a1.config", Path.Combine(mockBaseDirectory, "nuget", "Config", "IDE", "Version"), fileContent);
                ConfigurationFileTestUtility.CreateConfigurationFile("a2.config", Path.Combine(mockBaseDirectory, "nuget", "Config", "IDE", "Version"), fileContent);
                ConfigurationFileTestUtility.CreateConfigurationFile("a3.xconfig", Path.Combine(mockBaseDirectory, "nuget", "Config", "IDE", "Version"), fileContent);
                ConfigurationFileTestUtility.CreateConfigurationFile("a1.config", Path.Combine(mockBaseDirectory, "nuget", "Config", "IDE", "Version", "SKU"), fileContent);
                ConfigurationFileTestUtility.CreateConfigurationFile("a2.config", Path.Combine(mockBaseDirectory, "nuget", "Config", "IDE", "Version", "SKU"), fileContent);
                ConfigurationFileTestUtility.CreateConfigurationFile("a3.xconfig", Path.Combine(mockBaseDirectory, "nuget", "Config", "IDE", "Version", "SKU"), fileContent);
                ConfigurationFileTestUtility.CreateConfigurationFile("a1.config", Path.Combine(mockBaseDirectory, "nuget", "Config", "IDE", "Version", "SKU", "Dir"), fileContent);
                ConfigurationFileTestUtility.CreateConfigurationFile("a1_uppercase.Config", Path.Combine(mockBaseDirectory, "nuget", "Config"), fileContent);

                // Act
                var settings = Settings.LoadMachineWideSettings(
                    Path.Combine(mockBaseDirectory, "nuget", "Config"), "IDE", "Version", "SKU", "TestDir");

                // Assert
                var files = SettingsUtility.GetConfigFilePaths(settings).ToArray();

                files.Count().Should().Be(9);
                files.Should().Contain(Path.Combine(mockBaseDirectory, "nuget", "Config", "IDE", "Version", "SKU", "a2.config"));
                files.Should().Contain(Path.Combine(mockBaseDirectory, "nuget", "Config", "IDE", "Version", "SKU", "a1.config"));
                files.Should().Contain(Path.Combine(mockBaseDirectory, "nuget", "Config", "IDE", "Version", "a2.config"));
                files.Should().Contain(Path.Combine(mockBaseDirectory, "nuget", "Config", "IDE", "Version", "a1.config"));
                files.Should().Contain(Path.Combine(mockBaseDirectory, "nuget", "Config", "IDE", "a2.config"));
                files.Should().Contain(Path.Combine(mockBaseDirectory, "nuget", "Config", "IDE", "a1.config"));
                files.Should().Contain(Path.Combine(mockBaseDirectory, "nuget", "Config", "a2.config"));
                files.Should().Contain(Path.Combine(mockBaseDirectory, "nuget", "Config", "a1.config"));
                files.Should().Contain(Path.Combine(mockBaseDirectory, "nuget", "Config", "a1_uppercase.Config"));
            }
        }

        // Tests that when configFileName is not null, the specified
        // file must exist.
        [Fact]
        public void LoadDefaultSettings_WithUnexistantUserSpecifiedConfigFile_Throws()
        {
            // Arrange
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                // Act and assert
                var ex = Record.Exception(() => Settings.LoadDefaultSettings(
                    mockBaseDirectory,
                    configFileName: "user.config",
                    machineWideSettings: null));

                ex.Should().NotBeNull();
                ex.Should().BeOfType<InvalidOperationException>();
                ex.Message.Should().Be(string.Format(@"File '{0}' does not exist.", Path.Combine(mockBaseDirectory, "user.config")));
            }
        }

        // Tests that when configFileName is not null, machineWideSettings and
        // NuGet.Config files in base directory ancestors are ignored.
        [Fact]
        public void LoadDefaultSettings_WithUserSpecifiedConfigFile_IgnoresOtherSettings()
        {
            // Arrange
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var environmentFileContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""environment"" value=""true"" />
    </SectionName>
</configuration>";

                var machineConfigFileContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""machine"" value=""true"" />
    </SectionName>
</configuration>";

                var userFileContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <SectionName>
        <add key=""user"" value=""true"" />
    </SectionName>
</configuration>";

                ConfigurationFileTestUtility.CreateConfigurationFile("NuGet.Config", Path.Combine(mockBaseDirectory, "a", "b"), environmentFileContent);
                ConfigurationFileTestUtility.CreateConfigurationFile("NuGet.Config", Path.Combine(mockBaseDirectory, "a"), environmentFileContent);
                ConfigurationFileTestUtility.CreateConfigurationFile("NuGet.Config", Path.Combine(mockBaseDirectory), environmentFileContent);

                ConfigurationFileTestUtility.CreateConfigurationFile("NuGet.Config", Path.Combine(mockBaseDirectory, "machine"), machineConfigFileContent);

                ConfigurationFileTestUtility.CreateConfigurationFile("NuGet.Config", Path.Combine(mockBaseDirectory, "nuget"), userFileContent);

                var m = new Mock<IMachineWideSettings>();
                m.SetupGet(obj => obj.Settings).Returns(
                    Settings.LoadMachineWideSettings(Path.Combine(mockBaseDirectory, "machine")));

                // Act and assert
                var settings = Settings.LoadDefaultSettings(
                    Path.Combine(mockBaseDirectory, "a", "b"),
                    Path.Combine(mockBaseDirectory, "nuget", "NuGet.Config"),
                    m.Object);

                var section = settings.GetSection("SectionName");
                section.Should().NotBeNull();

                var machineValue = section.GetFirstItemWithAttribute<AddItem>("key", "machine");
                machineValue.Should().BeNull();

                var environmentValue = section.GetFirstItemWithAttribute<AddItem>("key", "environment");
                environmentValue.Should().BeNull();

                var userFileValue = section.GetFirstItemWithAttribute<AddItem>("key", "user");
                userFileValue.Should().NotBeNull();
                userFileValue.Value.Should().Be("true");
            }
        }

        [Fact]
        public void LoadSettings_AddsV3ToEmptyConfigFile_OnlyFirstTime()
        {
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                // Arrange
                var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>";

                var nugetConfigPath = "NuGet.Config";
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "TestingGlobalPath"), config);

                // Act
                var settings = Settings.LoadSettings(
                    root: mockBaseDirectory,
                    configFileName: null,
                    machineWideSettings: null,
                    loadUserWideSettings: true,
                    useTestingGlobalPath: true);

                // Assert
                var text = ConfigurationFileTestUtility.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory, "TestingGlobalPath", "NuGet.Config")));
                var result = ConfigurationFileTestUtility.RemoveWhitespace(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSources>
        <add key=""nuget.org"" value=""https://api.nuget.org/v3/index.json"" protocolVersion=""3"" />
    </packageSources>
</configuration>");

                text.Should().Be(result);

                var settingsFile = new SettingsFile(Path.Combine(mockBaseDirectory, "TestingGlobalPath"));

                // Act
                var section = settingsFile.GetSection("packageSources");
                section.Should().NotBeNull();
                section.Children.FirstOrDefault().RemoveFromCollection().Should().BeTrue();

                settings = Settings.LoadSettings(
                                    root: mockBaseDirectory,
                                    configFileName: null,
                                    machineWideSettings: null,
                                    loadUserWideSettings: true,
                                    useTestingGlobalPath: true);
                // Assert
                text = ConfigurationFileTestUtility.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory, "TestingGlobalPath", "NuGet.Config")));
                result = ConfigurationFileTestUtility.RemoveWhitespace(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>");

                text.Should().Be(result);
            }
        }

        [Theory]
        [InlineData(@"D:\", @"C:\Users\SomeUsers\AppData\Roaming\nuget\nuget.config", @"C:\Users\SomeUsers\AppData\Roaming\nuget", @"nuget.config", "windows")]
        [InlineData(@"D:\", (string)null, @"D:\", (string)null, "windows")]
        [InlineData(@"D:\", "nuget.config", @"D:\", "nuget.config", "windows")]
        [InlineData(@"/Root", @"/Home/Users/nuget/nuget.config", @"/Home/Users/nuget", @"nuget.config", "linux")]
        [InlineData(@"/", (string)null, @"/", (string)null, "linux")]
        [InlineData(@"/", "nuget.config", @"/", "nuget.config", "linux")]
        public void GetFileNameAndItsRoot_ParsesPathsCorrectly(string root, string settingsPath, string expectedRoot, string expectedFileName, string os)
        {
            if ((os == "linux" && RuntimeEnvironmentHelper.IsLinux) || (os == "windows" && RuntimeEnvironmentHelper.IsWindows))
            {
                // Act
                var tuple = Settings.GetFileNameAndItsRoot(root, settingsPath);

                // Assert
                tuple.Item1.Should().Be(expectedFileName);
                tuple.Item2.Should().Be(expectedRoot);
            }
        }

        [PlatformFact(Platform.Windows)]
        public void NuGetEnviromentPath_OnWindows_ReturnsCorrectPath()
        {
            // Arrange
#if IS_CORECLR
            var programFilesX86Data = Environment.GetEnvironmentVariable("PROGRAMFILES(X86)");

            if (string.IsNullOrEmpty(programFilesX86Data))
            {
                programFilesX86Data = Environment.GetEnvironmentVariable("PROGRAMFILES");
            }
            var userSetting = Environment.GetEnvironmentVariable("APPDATA");
#else
            var programFilesX86Data = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            if (string.IsNullOrEmpty(programFilesX86Data))
            {
                programFilesX86Data = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            }
            var userSetting = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
#endif

            // Act
            var machineWidePath = Path.Combine(NuGetEnvironment.GetFolderPath(NuGetFolderPath.MachineWideSettingsBaseDirectory), "NuGet.Config");
            var globalConfigPath = Path.Combine(NuGetEnvironment.GetFolderPath(NuGetFolderPath.UserSettingsDirectory), "NuGet.Config");
            var machineWidePathTuple = Settings.GetFileNameAndItsRoot("test root", machineWidePath);
            var globalConfigTuple = Settings.GetFileNameAndItsRoot("test root", globalConfigPath);

            // Assert
            machineWidePathTuple.Item2.Should().Be(Path.Combine(programFilesX86Data, "NuGet"));
            machineWidePathTuple.Item1.Should().Be("NuGet.Config");
            globalConfigTuple.Item2.Should().Be(Path.Combine(userSetting, "NuGet"));
            globalConfigTuple.Item1.Should().Be("NuGet.Config");
        }

        [PlatformFact(Platform.Darwin)]
        public void NuGetEnviromentPath_OnMac_ReturnsCorrectPath()
        {
            // Arrange
#if IS_CORECLR
            var commonApplicationData = @"/Library/Application Support";
            var userSetting = Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".nuget");
#else
            var commonApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var userSetting = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
#endif

            // Act
            var machineWidePath = Path.Combine(NuGetEnvironment.GetFolderPath(NuGetFolderPath.MachineWideSettingsBaseDirectory), "NuGet.Config");
            var globalConfigPath = Path.Combine(NuGetEnvironment.GetFolderPath(NuGetFolderPath.UserSettingsDirectory), "NuGet.Config");
            var machineWidePathTuple = Settings.GetFileNameAndItsRoot("test root", machineWidePath);
            var globalConfigTuple = Settings.GetFileNameAndItsRoot("test root", globalConfigPath);

            // Assert
            machineWidePathTuple.Item2.Should().Be(Path.Combine(commonApplicationData, "NuGet"));
            machineWidePathTuple.Item1.Should().Be("NuGet.Config");
            globalConfigTuple.Item2.Should().Be(Path.Combine(userSetting, "NuGet"));
            globalConfigTuple.Item1.Should().Be("NuGet.Config");
        }

        [PlatformFact(Platform.Linux)]
        public void NuGetEnviromentPath_OnLinux_ReturnsCorrectPath()
        {
            // Arrange
#if IS_CORECLR
            var commonApplicationData = @"/etc/opt";
            var userSetting = Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".nuget");
#else
            var commonApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var userSetting = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
#endif

            // Act
            var machineWidePath = Path.Combine(NuGetEnvironment.GetFolderPath(NuGetFolderPath.MachineWideSettingsBaseDirectory), "NuGet.Config");
            var globalConfigPath = Path.Combine(NuGetEnvironment.GetFolderPath(NuGetFolderPath.UserSettingsDirectory), "NuGet.Config");
            var machineWidePathTuple = Settings.GetFileNameAndItsRoot("test root", machineWidePath);
            var globalConfigTuple = Settings.GetFileNameAndItsRoot("test root", globalConfigPath);

            // Assert
            machineWidePathTuple.Item2.Should().Be(Path.Combine(commonApplicationData, "NuGet"));
            machineWidePathTuple.Item1.Should().Be("NuGet.Config");
            globalConfigTuple.Item2.Should().Be(Path.Combine(userSetting, "NuGet"));
            globalConfigTuple.Item1.Should().Be("NuGet.Config");
        }

        //TODO: remove deprecated APIs
#pragma warning disable CS0618 // Type or member is obsolete
        [Fact]
        public void CallingGetSettingValuesWithNullSectionWillThrowException()
        {
            // Arrange
            var configFile = "NuGet.Config";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
                var settings = new Settings(mockBaseDirectory);

                // Act & Assert
                var ex = Record.Exception(() => settings.GetSettingValues(null));
                Assert.NotNull(ex);
                Assert.IsAssignableFrom<ArgumentException>(ex);
            }
        }

        [Fact]
        public void CallingGetValueWithNullSectionWillThrowException()
        {
            // Arrange
            var configFile = "NuGet.Config";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
                var settings = new Settings(mockBaseDirectory);

                // Act & Assert
                var ex = Record.Exception(() => settings.GetValue(null, "SomeKey"));
                Assert.NotNull(ex);
                Assert.IsAssignableFrom<ArgumentException>(ex);
            }
        }

        [Fact]
        public void CallingGetValueWithNullKeyWillThrowException()
        {
            // Arrange
            var configFile = "NuGet.Config";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
                var settings = new Settings(mockBaseDirectory);

                // Act & Assert
                var ex = Record.Exception(() => settings.GetValue("SomeSection", null));
                Assert.NotNull(ex);
                Assert.IsAssignableFrom<ArgumentException>(ex);
            }
        }

        [Fact]
        public void UserSetting_CallingGetValuesWithNonExistantSectionReturnsEmpty()
        {
            // Arrange
            var configFile = "NuGet.Config";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
                var settings = new Settings(mockBaseDirectory);

                // Act
                var result = settings.GetSettingValues("DoesNotExisit");

                // Assert
                Assert.Empty(result);
            }
        }

        [Fact]
        public void CallingGetValuesWithSectionWithInvalidAddItemsThrows()
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
                var ex = Record.Exception(() => new Settings(mockBaseDirectory));
                Assert.NotNull(ex);
                Assert.IsAssignableFrom<NuGetConfigurationException>(ex);
                Assert.Equal(string.Format("Unable to parse config file '{0}'.", Path.Combine(mockBaseDirectory, nugetConfigPath)), ex.Message);
            }
        }

        [Fact]
        public void GetValuesThrowsIfSettingsIsMissingKeys()
        {
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
<packageSources>
<add key=""    "" value=""C:\Temp\Nuget"" />
</packageSources>
<activePackageSource>
<add key=""test2"" value=""C:\Temp\Nuget"" />
</activePackageSource>
</configuration>";
            var nugetConfigPath = "NuGet.Config";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);

                // Act and Assert
                var ex = Record.Exception(() => new Settings(mockBaseDirectory));
                Assert.NotNull(ex);
                Assert.IsAssignableFrom<NuGetConfigurationException>(ex);
                Assert.Equal(string.Format("Unable to parse config file '{0}'.", Path.Combine(mockBaseDirectory, nugetConfigPath)), ex.Message);
            }
        }

        [Fact]
        public void CallingGetValuesWithoutSectionReturnsEmptyList()
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
                var settings = new Settings(mockBaseDirectory);

                // Act
                var result = settings.GetSettingValues("NotTheSectionName");

                // Arrange
                Assert.Empty(result);
            }
        }

        [Fact]
        public void CallingGetValueWithoutSectionReturnsNull()
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
                var settings = new Settings(mockBaseDirectory);

                // Act
                var result = settings.GetValue("NotTheSectionName", "key1");

                // Arrange
                Assert.Null(result);
            }
        }

        [Fact]
        public void CallingGetValueWithSectionButNoValidKeyReturnsNull()
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
                var settings = new Settings(mockBaseDirectory);

                // Act
                var result = settings.GetValue("SectionName", "key3");

                // Assert
                Assert.Null(result);
            }
        }

        [Fact]
        public void CallingGetValuesWithSectionReturnsDictionary()
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
                var settings = new Settings(mockBaseDirectory);

                // Act
                var result = settings.GetSettingValues("SectionName");

                // Assert
                Assert.NotNull(result);
                Assert.Equal(2, result.Count);
            }
        }

        [Fact]
        public void CallingGetValueWithSectionAndKeyReturnsValue()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <SectionName>
        <add key='key1' value='value1' />
    </SectionName>
    <SectionNameTwo>
        <add key='key2' value='value2' />
    </SectionNameTwo>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                var result1 = settings.GetValue("SectionName", "key1");
                var result2 = settings.GetValue("SectionNameTwo", "key2");

                // Assert
                Assert.Equal("value1", result1);
                Assert.Equal("value2", result2);
            }
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void CallingGetNestedValuesWithoutSectionThrowsException(string section)
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <Section>
        <SubSection>
            <add key='key1' value='value1' />
        </SubSection>
    </Section>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act & Assert
                Action action = () => settings.GetNestedValues(section, "SubSection");
                action.ShouldThrow<ArgumentException>();
            }
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void CallingGetNestedValuesWithoutSubSectionThrowsException(string subSection)
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <Section>
        <SubSection>
            <add key='key1' value='value1' />
        </SubSection>
    </Section>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act & Assert
                Action action = () => settings.GetNestedValues("Section", subSection);
                action.ShouldThrow<ArgumentException>();
            }
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void CallingGetNestedSettingValuesWithoutSectionThrowsException(string section)
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <Section>
        <SubSection>
            <add key='key1' value='value1' />
        </SubSection>
    </Section>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act & Assert
                Action action = () => settings.GetNestedSettingValues(section, "SubSection");
                action.ShouldThrow<ArgumentException>();
            }
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void CallingGetNestedSettingValuesWithoutSubSectionThrowsException(string subSection)
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <Section>
        <SubSection>
            <add key='key1' value='value1' />
        </SubSection>
    </Section>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act & Assert
                Action action = () => settings.GetNestedSettingValues("Section", subSection);
                action.ShouldThrow<ArgumentException>();
            }
        }

        [Fact]
        public void CallingGetNestedSettingValuesGetsValue()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <Section>
        <SubSection>
            <add key='key1' value='value1' />
        </SubSection>
    </Section>
</configuration>";
            var expectedValues = new List<SettingValue>()
            {
                new SettingValue("key1", "value1", false)
            };

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                var values = settings.GetNestedSettingValues("Section", "SubSection");

                // Assert
                values.Should().NotBeNull();
                values.Should().BeEquivalentTo(expectedValues);
            }
        }

        [Fact]
        public void CallingGetNestedSettingValuesGetsMultipleValues()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <Section>
        <SubSection>
            <add key='key0' value='value0' />
            <add key='key1' value='value1' />
            <add key='key2' value='value2' />
        </SubSection>
    </Section>
</configuration>";
            var expectedValues = new List<SettingValue>()
            {
                new SettingValue("key1", "value1", false),
                new SettingValue("key2", "value2", false),
                new SettingValue("key0", "value0", false)
            };

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                var values = settings.GetNestedSettingValues("Section", "SubSection");

                // Assert
                values.Should().NotBeNull();
                values.Should().BeEquivalentTo(expectedValues);
            }
        }

        [Fact]
        public void CallingGetNestedSettingValuesGetsValueWithMetadata()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <Section>
        <SubSection>
            <add key='key1' value='value1' meta1='data1' meta2='data2'/>
        </SubSection>
    </Section>
</configuration>";

            var expectedSetting = new SettingValue("key1", "value1", false);
            expectedSetting.AdditionalData.Add("meta1", "data1");
            expectedSetting.AdditionalData.Add("meta2", "data2");
            var expectedValues = new List<SettingValue>()
            {
                expectedSetting
            };

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                var values = settings.GetNestedSettingValues("Section", "SubSection");

                // Assert
                values.Should().NotBeNull();
                values.Should().BeEquivalentTo(expectedValues);
            }
        }

        [Fact]
        public void CallingGetNestedSettingValuesGetsMultipleValuesWithMetadata()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <Section>
        <SubSection>
            <add key='key0' value='value0' />
            <add key='key1' value='value1' meta1='data1' meta2='data2'/>
            <add key='key2' value='value2' meta3='data3'/>
        </SubSection>
    </Section>
</configuration>";
            var expectedSetting1 = new SettingValue("key0", "value0", false);
            var expectedSetting2 = new SettingValue("key1", "value1", false);
            expectedSetting2.AdditionalData.Add("meta1", "data1");
            expectedSetting2.AdditionalData.Add("meta2", "data2");
            var expectedSetting3 = new SettingValue("key2", "value2", false);
            expectedSetting3.AdditionalData.Add("meta3", "data3");
            var expectedValues = new List<SettingValue>()
            {
                expectedSetting1,
                expectedSetting2,
                expectedSetting3
            };

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                var values = settings.GetNestedSettingValues("Section", "SubSection");

                // Assert
                values.Should().NotBeNull();
                values.Should().BeEquivalentTo(expectedValues);
            }
        }

        [Fact]
        public void CallingGetNestedSettingValuesGetsMultipleValuesWithMetadataIgnoresDuplicates()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <Section>
        <SubSection>
            <add key='key0' value='value0' />
            <add key='key1' value='value1' meta1='data1' meta2='data2'/>
            <add key='key2' value='value2' meta3='data3'/>
        </SubSection>
        <SubSection>
            <add key='key3' value='value3' />
        </SubSection>
    </Section>
</configuration>";
            var expectedSetting1 = new SettingValue("key0", "value0", false);
            var expectedSetting2 = new SettingValue("key1", "value1", false);
            expectedSetting2.AdditionalData.Add("meta1", "data1");
            expectedSetting2.AdditionalData.Add("meta2", "data2");
            var expectedSetting3 = new SettingValue("key2", "value2", false);
            expectedSetting3.AdditionalData.Add("meta3", "data3");
            var expectedValues = new List<SettingValue>()
            {
                expectedSetting1,
                expectedSetting2,
                expectedSetting3
            };

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                var values = settings.GetNestedSettingValues("Section", "SubSection");

                // Assert
                values.Should().NotBeNull();
                values.Should().BeEquivalentTo(expectedValues);
            }
        }

        [Fact]
        public void CallingGetNestedValuesGetsValue()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <Section>
        <SubSection>
            <add key='key1' value='value1' />
        </SubSection>
    </Section>
</configuration>";
            var expectedValues = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>("key1","value1")
            };

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                var values = settings.GetNestedValues("Section", "SubSection");

                // Assert
                values.Should().NotBeNull();
                values.Should().BeEquivalentTo(expectedValues);
            }
        }

        [Fact]
        public void CallingGetNestedValuesGetsMultipleValues()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <Section>
        <SubSection>
            <add key='key0' value='value0' />
            <add key='key1' value='value1' />
            <add key='key2' value='value2' />
        </SubSection>
    </Section>
</configuration>";
            var expectedValues = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>("key0","value0"),
                new KeyValuePair<string, string>("key1","value1"),
                new KeyValuePair<string, string>("key2","value2"),
            };

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                var values = settings.GetNestedValues("Section", "SubSection");

                // Assert
                values.Should().NotBeNull();
                values.Should().BeEquivalentTo(expectedValues);
            }
        }

        [Fact]
        public void CallingGetNestedValuesGetsValueWithMetadata()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <Section>
        <SubSection>
            <add key='key1' value='value1' meta1='data1' meta2='data2'/>
        </SubSection>
    </Section>
</configuration>";
            var expectedValues = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>("key1","value1")
            };

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                var values = settings.GetNestedValues("Section", "SubSection");

                // Assert
                values.Should().NotBeNull();
                values.Should().BeEquivalentTo(expectedValues);
            }
        }

        [Fact]
        public void CallingGetNestedValuesGetsMultipleValuesWithMetadata()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <Section>
        <SubSection>
            <add key='key0' value='value0' />
            <add key='key1' value='value1' meta1='data1' meta2='data2'/>
            <add key='key2' value='value2' meta3='data3'/>
        </SubSection>
    </Section>
</configuration>";
            var expectedValues = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>("key0","value0"),
                new KeyValuePair<string, string>("key1","value1"),
                new KeyValuePair<string, string>("key2","value2"),
            };

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                var values = settings.GetNestedValues("Section", "SubSection");

                // Assert
                values.Should().NotBeNull();
                values.Should().BeEquivalentTo(expectedValues);
            }
        }


        [Fact]
        public void CallingGetNestedValuesGetsMultipleValuesWithMetadataIgnoresDuplicates()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <Section>
        <SubSection>
            <add key='key0' value='value0' />
            <add key='key1' value='value1' meta1='data1' meta2='data2'/>
            <add key='key2' value='value2' meta3='data3'/>
        </SubSection>
        <SubSection>
            <add key='key3' value='value3' />
        </SubSection>
    </Section>
</configuration>";
            var expectedValues = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>("key0","value0"),
                new KeyValuePair<string, string>("key1","value1"),
                new KeyValuePair<string, string>("key2","value2"),
            };

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                var values = settings.GetNestedValues("Section", "SubSection");

                // Assert
                values.Should().NotBeNull();
                values.Should().BeEquivalentTo(expectedValues);
            }
        }

        [Fact]
        public void CallingSetValueWithEmptySectionNameThrowsException()
        {
            // Arrange
            var configFile = "NuGet.Config";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
                var settings = new Settings(mockBaseDirectory);

                // Act & Assert
                var ex = Record.Exception(() => settings.SetValue("", "SomeKey", "SomeValue"));
                Assert.NotNull(ex);
                Assert.IsAssignableFrom<ArgumentException>(ex);
            }
        }

        [Fact]
        public void CallingSetValueWithEmptyKeyThrowsException()
        {
            // Arrange
            var configFile = "NuGet.Config";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
                var settings = new Settings(mockBaseDirectory);

                // Act & Assert
                var ex = Record.Exception(() => settings.SetValue("SomeKey", "", "SomeValue"));
                Assert.NotNull(ex);
                Assert.IsAssignableFrom<ArgumentException>(ex);
            }
        }

        [Fact]
        public void CallingSetValueWillAddSectionIfItDoesNotExist()
        {
            // Arrange
            var configFile = "NuGet.Config";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value=""value"" />
  </SectionName>
</configuration>";
                ConfigurationFileTestUtility.CreateConfigurationFile(configFile, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                settings.SetValue("NewSectionName", "key", "value");

                // Assert
                var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value=""value"" />
  </SectionName>
  <NewSectionName>
    <add key=""key"" value=""value"" />
  </NewSectionName>
</configuration>";
                Assert.Equal(result.Replace("\r\n", "\n"), File.ReadAllText(Path.Combine(mockBaseDirectory, configFile)).Replace("\r\n", "\n"));
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void CallingSetValueWithEmptyOrNullWillDeleteAddItemIFExists(string setValueParam)
        {
            // Arrange
            var configFile = "NuGet.Config";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value=""value"" />
  </SectionName>
</configuration>";
                ConfigurationFileTestUtility.CreateConfigurationFile(configFile, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                settings.SetValue("SectionName", "key", setValueParam);

                // Assert
                var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>";
                Assert.Equal(result.Replace("\r\n", "\n"), File.ReadAllText(Path.Combine(mockBaseDirectory, configFile)).Replace("\r\n", "\n"));
            }
        }

        [Fact]
        public void CallingSetValueWillAddToSectionIfItExist()
        {
            // Arrange
            var configFile = "NuGet.Config";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value=""value"" />
  </SectionName>
</configuration>";
                ConfigurationFileTestUtility.CreateConfigurationFile(configFile, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                settings.SetValue("SectionName", "keyTwo", "valueTwo");

                // Assert
                var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value=""value"" />
    <add key=""keyTwo"" value=""valueTwo"" />
  </SectionName>
</configuration>";
                Assert.Equal(result.Replace("\r\n", "\n"),
                    File.ReadAllText(Path.Combine(mockBaseDirectory, configFile)).Replace("\r\n", "\n"));
            }
        }

        [Fact]
        public void CallingSetValueWillOverrideValueIfKeyExists()
        {
            // Arrange
            var configFile = "NuGet.Config";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value=""value"" />
  </SectionName>
</configuration>";
                ConfigurationFileTestUtility.CreateConfigurationFile(configFile, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                settings.SetValue("SectionName", "key", "NewValue");

                // Assert
                var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value=""NewValue"" />
  </SectionName>
</configuration>";
                Assert.Equal(result.Replace("\r\n", "\n"),
                    File.ReadAllText(Path.Combine(mockBaseDirectory, configFile)).Replace("\r\n", "\n"));
            }
        }

        [Fact]
        public void CallingSetValuesWithEmptySectionThrowsException()
        {
            // Arrange
            var values = new List<SettingValue>() { new SettingValue("key", "value", isMachineWide: false) };
            var configFile = "NuGet.Config";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
                var settings = new Settings(mockBaseDirectory);

                // Act & Assert
                var ex = Record.Exception(() => settings.SetValues("", values));
                Assert.NotNull(ex);
                Assert.IsAssignableFrom<ArgumentException>(ex);
            }
        }

        [Fact]
        public void CallingSetValuesWithNullValuesThrowsException()
        {
            // Arrange
            var configFile = "NuGet.Config";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
                var settings = new Settings(mockBaseDirectory);

                // Act & Assert
                var ex = Record.Exception(() => settings.SetValues("Section", null));
                Assert.NotNull(ex);
                Assert.IsAssignableFrom<ArgumentNullException>(ex);
            }
        }

        [Fact]
        public void CallingSetValuesWithEmptyKeyThrowsException()
        {
            // Arrange
            var configFile = "NuGet.Config";
            var values = new[] { new SettingValue("", "value", isMachineWide: false) };

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
                var settings = new Settings(mockBaseDirectory);

                // Act & Assert
                var ex = Record.Exception(() => settings.SetValues("Section", values));
                Assert.NotNull(ex);
                Assert.IsAssignableFrom<ArgumentException>(ex);
            }
        }

        [Fact]
        public void CallingSetValuseWillAddSectionIfItDoesNotExist()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value=""value"" />
  </SectionName>
</configuration>";
            var values = new[] { new SettingValue("key", "value", isMachineWide: false) };

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                settings.SetValues("NewSectionName", values);

                // Assert
                var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value=""value"" />
  </SectionName>
  <NewSectionName>
    <add key=""key"" value=""value"" />
  </NewSectionName>
</configuration>";
                Assert.Equal(result.Replace("\r\n", "\n"),
                    File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath)).Replace("\r\n", "\n"));
            }
        }

        [Fact]
        public void CallingSetValuesWillAddToSectionIfItExist()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value=""value"" />
  </SectionName>
</configuration>";
            var values = new[] { new SettingValue("keyTwo", "valueTwo", isMachineWide: false) };

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                settings.SetValues("SectionName", values);

                // Assert
                var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value=""value"" />
    <add key=""keyTwo"" value=""valueTwo"" />
  </SectionName>
</configuration>";
                Assert.Equal(result.Replace("\r\n", "\n"),
                    File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath)).Replace("\r\n", "\n"));
            }
        }

        [Fact]
        public void CallingSetValuesWillOverrideValueIfKeyExists()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";

            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value=""value"" />
  </SectionName>
</configuration>";
            var values = new[] { new SettingValue("key", "NewValue", isMachineWide: false) };

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                settings.SetValues("SectionName", values);

                // Assert
                var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value=""NewValue"" />
  </SectionName>
</configuration>";
                Assert.Equal(result.Replace("\r\n", "\n"),
                    File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath)).Replace("\r\n", "\n"));
            }
        }

        [Fact]
        public void CallingSetValuesWilladdValuesInOrder()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";

            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value=""Value"" />
  </SectionName>
</configuration>";
            var values = new[]
                {
                    new SettingValue("key1", "Value1", isMachineWide: false),
                    new SettingValue("key2", "Value2", isMachineWide: false)
                };

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                settings.SetValues("SectionName", values);

                // Assert
                var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value=""Value"" />
    <add key=""key1"" value=""Value1"" />
    <add key=""key2"" value=""Value2"" />
  </SectionName>
</configuration>";
                Assert.Equal(result.Replace("\r\n", "\n"),
                    File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath)).Replace("\r\n", "\n"));
            }
        }

        [Fact]
        public void CallingSetNestedValuesAddsItemsInNestedElement()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>";
            var values = new[]
                {
                    new KeyValuePair<string, string>("key1", "Value1"),
                    new KeyValuePair<string, string>("key2", "Value2")
                };

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                settings.SetNestedValues("SectionName", "MyKey", values);

                // Assert
                var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <MyKey>
      <add key=""key1"" value=""Value1"" />
      <add key=""key2"" value=""Value2"" />
    </MyKey>
  </SectionName>
</configuration>";
                Assert.Equal(ConfigurationFileTestUtility.RemoveWhitespace(result),
                    ConfigurationFileTestUtility.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath))));
            }
        }

        [Fact]
        public void CallingSetNestedValuesPreservesOtherKeys()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <MyKey>
      <add key=""key1"" value=""Value1"" />
      <add key=""key2"" value=""Value2"" />
    </MyKey>
  </SectionName>
</configuration>";
            var values = new[]
                {
                    new KeyValuePair<string, string>("key3", "Value3"),
                    new KeyValuePair<string, string>("key4", "Value4")
                };

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                settings.SetNestedValues("SectionName", "MyKey2", values);

                // Assert
                var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <MyKey>
      <add key=""key1"" value=""Value1"" />
      <add key=""key2"" value=""Value2"" />
    </MyKey>
    <MyKey2>
      <add key=""key3"" value=""Value3"" />
      <add key=""key4"" value=""Value4"" />
    </MyKey2>
  </SectionName>
</configuration>";
                Assert.Equal(result.Replace("\r\n", "\n"),
                    File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath)).Replace("\r\n", "\n"));
            }
        }

        [Fact]
        public void CallingSetNestedAppendsValuesToExistingKeys()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <MyKey>
      <add key=""key1"" value=""Value1"" />
      <add key=""key2"" value=""Value2"" />
    </MyKey>
  </SectionName>
</configuration>";
            var values = new[]
                {
                    new KeyValuePair<string, string>("key3", "Value3"),
                    new KeyValuePair<string, string>("key4", "Value4")
                };

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                settings.SetNestedValues("SectionName", "MyKey", values);

                // Assert
                var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <MyKey>
      <add key=""key1"" value=""Value1"" />
      <add key=""key2"" value=""Value2"" />
      <add key=""key3"" value=""Value3"" />
      <add key=""key4"" value=""Value4"" />
    </MyKey>
  </SectionName>
</configuration>";
                Assert.Equal(result.Replace("\r\n", "\n"),
                    File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath)).Replace("\r\n", "\n"));
            }
        }

        [Fact]
        public void CallingSetNestedSettingValuesAddsSingleSettingValueInNestedElement()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>";
            var settingvalue = new SettingValue("key1", "value1", false);
            settingvalue.AdditionalData.Add("meta1", "data1");
            settingvalue.AdditionalData.Add("meta2", "data2");
            var values = new List<SettingValue>()
            {
                settingvalue
            };

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                settings.SetNestedSettingValues("section", "subsection", values);

                // Assert
                var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <section>
    <subsection>
      <add key=""key1"" value=""value1"" meta1=""data1"" meta2=""data2"" />
    </subsection>
  </section>
</configuration>";
                Assert.Equal(ConfigurationFileTestUtility.RemoveWhitespace(result),
                    ConfigurationFileTestUtility.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath))));
            }
        }

        [Fact]
        public void CallingSetNestedSettingValuesAddsMultipleSettingValueInNestedElement()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>";
            var settingvalue1 = new SettingValue("key1", "value1", false);
            settingvalue1.AdditionalData.Add("meta1", "data1");
            settingvalue1.AdditionalData.Add("meta2", "data2");

            var settingvalue2 = new SettingValue("key2", "value2", false);
            settingvalue2.AdditionalData.Add("meta3", "data3");

            var settingvalue3 = new SettingValue("key3", "value3", false);
            settingvalue3.AdditionalData.Add("meta1", "data1");

            var values = new List<SettingValue>()
            {
                settingvalue1,
                settingvalue2,
                settingvalue3
            };

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                settings.SetNestedSettingValues("section", "subsection", values);

                // Assert
                var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <section>
    <subsection>
      <add key=""key1"" value=""value1"" meta1=""data1"" meta2=""data2"" />
      <add key=""key2"" value=""value2"" meta3=""data3"" />
      <add key=""key3"" value=""value3"" meta1=""data1"" />
    </subsection>
  </section>
</configuration>";
                Assert.Equal(ConfigurationFileTestUtility.RemoveWhitespace(result),
                    ConfigurationFileTestUtility.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath))));
            }
        }

        [Fact]
        public void CallingSetNestedSettingValuesAfterSetNestedSettingValuesReadsValues()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>";
            var settingvalue1 = new SettingValue("key1", "value1", false);
            settingvalue1.AdditionalData.Add("meta1", "data1");
            settingvalue1.AdditionalData.Add("meta2", "data2");

            var settingvalue2 = new SettingValue("key2", "value2", false);
            settingvalue2.AdditionalData.Add("meta3", "data3");

            var settingvalue3 = new SettingValue("key3", "value3", false);
            settingvalue3.AdditionalData.Add("meta1", "data1");

            var values = new List<SettingValue>()
            {
                settingvalue1,
                settingvalue2,
                settingvalue3
            };

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                settings.SetNestedSettingValues("section", "subsection", values);
                var actualValues = settings.GetNestedSettingValues("section", "subsection");

                // Assert
                actualValues.Should().BeEquivalentTo(values);
            }
        }

        public void CallingSetNestedSettingValuesPreservesOtherSections()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <section1>
    <subsection>
      <add key=""key1"" value=""value1"" meta1=""data1"" meta2=""data2"" />
      <add key=""key2"" value=""value2"" />
    </subsection>
  </section1>
</configuration>";
            var settingvalue = new SettingValue("key1", "value1", false);
            settingvalue.AdditionalData.Add("meta1", "data1");
            settingvalue.AdditionalData.Add("meta2", "data2");
            var values = new List<SettingValue>()
            {
                settingvalue
            };

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                settings.SetNestedSettingValues("section", "subsection2", values);

                // Assert
                var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <section1>
    <subsection>
      <add key=""key1"" value=""value1"" meta1=""data1"" meta2=""data2"" />
      <add key=""key2"" value=""value2"" />
    </subsection>
  </section1>
  <section2>
    <subsection>
      <add key=""key1"" value=""value1"" meta1=""data1"" meta2=""data2"" />
    </subsection>
  </sectio2>
</configuration>";

                Assert.Equal(ConfigurationFileTestUtility.RemoveWhitespace(result),
                    ConfigurationFileTestUtility.RemoveWhitespace(File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath))));
            }
        }

        public void CallingSetNestedSettingValuesPreservesOtherSubsections()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <section>
    <subsection1>
      <add key=""key1"" value=""value1"" meta1=""data1"" meta2=""data2"" />
      <add key=""key2"" value=""value2"" />
    </subsection1>
  </section>
</configuration>";
            var settingvalue = new SettingValue("key1", "value1", false);
            settingvalue.AdditionalData.Add("meta1", "data1");
            settingvalue.AdditionalData.Add("meta2", "data2");
            var values = new List<SettingValue>()
            {
                settingvalue
            };

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                settings.SetNestedSettingValues("section", "subsection2", values);

                // Assert
                var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <section>
    <subsection1>
      <add key=""key1"" value=""value1"" meta1=""data1"" meta2=""data2"" />
      <add key=""key2"" value=""value2"" />
    </subsection1>
    <subsection2>
      <add key=""key1"" value=""value1"" meta1=""data1"" meta2=""data2"" />
    </subsection2>
  </section>
</configuration>";

                Assert.Equal(result.Replace("\r\n", "\n"), File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath)).Replace("\r\n", "\n"));
            }
        }

        [Fact]
        public void CallingSetNestedSettingAppendsValuesToExistingSubsection()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <section>
    <subsection>
      <add key=""key1"" value=""value1"" meta1=""data1"" meta2=""data2"" />
      <add key=""key2"" value=""value2"" />
    </subsection>
  </section>
</configuration>";
            var settingvalue = new SettingValue("key3", "value3", false);
            settingvalue.AdditionalData.Add("meta1", "data1");
            settingvalue.AdditionalData.Add("meta2", "data2");
            var values = new List<SettingValue>()
            {
                settingvalue
            };

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                settings.SetNestedSettingValues("section", "subsection", values);

                // Assert
                var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <section>
    <subsection>
      <add key=""key1"" value=""value1"" meta1=""data1"" meta2=""data2"" />
      <add key=""key2"" value=""value2"" />
      <add key=""key3"" value=""value3"" meta1=""data1"" meta2=""data2"" />
    </subsection>
  </section>
</configuration>";

                Assert.Equal(result.Replace("\r\n", "\n"), File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath)).Replace("\r\n", "\n"));
            }
        }

        [Fact]
        public void CallingGetAllSubsectionsReturnsSubsections()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <section>
    <subsection>
      <add key=""key1"" value=""value1"" meta1=""data1"" meta2=""data2"" />
    </subsection>
    <subsection1>
      <add key=""key1"" value=""value1"" meta1=""data1"" meta2=""data2"" />
    </subsection1>
    <subsection2>
      <add key=""key1"" value=""value1"" meta1=""data1"" meta2=""data2"" />
    </subsection2>
    <subsection3>
      <add key=""key1"" value=""value1"" meta1=""data1"" meta2=""data2"" />
    </subsection3>
  </section>
</configuration>";
            var expectedValues = new List<string>()
            {
                "subsection",
                "subsection1",
                "subsection2",
                "subsection3",
            };

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                var subsections = settings.GetAllSubsections("section");

                // Assert
                subsections.Should().NotBeNull();
                subsections.ShouldBeEquivalentTo(expectedValues);
            }
        }

        [Fact]
        public void CallingGetAllSubsectionsWithNonePresentReturnsEmptyList()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                var subsections = settings.GetAllSubsections("section");

                // Assert
                subsections.Should().NotBeNull();
                subsections.Should().BeEmpty();
            }
        }

        [Fact]
        public void CallingGetAllSubsectionsReturnsSubsectionsFromNestedSettings()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <section>
    <subsection>
      <add key=""key1"" value=""value1"" meta1=""data1"" meta2=""data2"" />
    </subsection>
    <subsection1>
      <add key=""key1"" value=""value1"" meta1=""data1"" meta2=""data2"" />
    </subsection1>
    <subsection2>
      <add key=""key1"" value=""value1"" meta1=""data1"" meta2=""data2"" />
    </subsection2>
    <subsection3>
      <add key=""key1"" value=""value1"" meta1=""data1"" meta2=""data2"" />
    </subsection3>
  </section>
</configuration>";

            var config2 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <section>
    <subsection4>
      <add key=""key1"" value=""value1"" meta1=""data1"" meta2=""data2"" />
    </subsection4>
    <subsection5>
      <add key=""key1"" value=""value1"" meta1=""data1"" meta2=""data2"" />
    </subsection5>
  </section>
</configuration>";
            var expectedValues = new List<string>()
            {
                "subsection",
                "subsection1",
                "subsection2",
                "subsection3",
                "subsection4",
                "subsection5",
            };

            using (var mockBaseDirectory = TestDirectory.Create())
            using (var mockChildDirectory = TestDirectory.Create(mockBaseDirectory))
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config1);
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockChildDirectory, config2);

                var configPaths = new List<string> { Path.Combine(mockChildDirectory, nugetConfigPath), Path.Combine(mockBaseDirectory, nugetConfigPath) };
                var settings = Settings.LoadSettingsGivenConfigPaths(configPaths);

                // Act
                var subsections = settings.GetAllSubsections("section");

                // Assert
                subsections.Should().NotBeNull();
                subsections.ShouldBeEquivalentTo(expectedValues);
            }
        }

        [Fact]
        public void CallingGetAllSubsectionsReturnsSubsectionsFromNestedSettingsWhenPresent()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <section>
    <subsection>
      <add key=""key1"" value=""value1"" meta1=""data1"" meta2=""data2"" />
    </subsection>
    <subsection1>
      <add key=""key1"" value=""value1"" meta1=""data1"" meta2=""data2"" />
    </subsection1>
    <subsection2>
      <add key=""key1"" value=""value1"" meta1=""data1"" meta2=""data2"" />
    </subsection2>
    <subsection3>
      <add key=""key1"" value=""value1"" meta1=""data1"" meta2=""data2"" />
    </subsection3>
  </section>
</configuration>";

            var config2 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <section2>
    <subsection4>
      <add key=""key1"" value=""value1"" meta1=""data1"" meta2=""data2"" />
    </subsection4>
  </section2>
</configuration>";
            var expectedValues = new List<string>()
            {
                "subsection",
                "subsection1",
                "subsection2",
                "subsection3"
            };

            using (var mockBaseDirectory = TestDirectory.Create())
            using (var mockChildDirectory = TestDirectory.Create(mockBaseDirectory))
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config1);
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockChildDirectory, config2);

                var configPaths = new List<string> { Path.Combine(mockChildDirectory, nugetConfigPath), Path.Combine(mockBaseDirectory, nugetConfigPath) };
                var settings = Settings.LoadSettingsGivenConfigPaths(configPaths);

                // Act
                var subsections = settings.GetAllSubsections("section");

                // Assert
                subsections.Should().NotBeNull();
                subsections.ShouldBeEquivalentTo(expectedValues);
            }
        }

        [Fact]
        public void CallingUpdateSubsectionsUpdatesSubsectionIfPresent()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <section>
    <subsection>
      <add key=""key1"" value=""value1"" meta1=""data1"" />
    </subsection>
  </section>
</configuration>";
            var valueLookUp = new Dictionary<string, SettingValue>()
            {
                { "key1", new SettingValue("key1", "value1", isMachineWide: false) },
                { "key2", new SettingValue("key2", "value2", isMachineWide: false) },
                { "key3", new SettingValue("key3", "value3", isMachineWide: false) }
            };

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                settings.UpdateSubsections("section", "subsection", valueLookUp.Values.ToList());
                var settingValues = settings.GetNestedSettingValues("section", "subsection");

                // Assert
                settingValues.Should().NotBeNull();
                settingValues.Count.Should().Be(valueLookUp.Count);
                foreach (var settingValue in settingValues)
                {
                    var matchingValue = valueLookUp[settingValue.Key];
                    matchingValue.Should().NotBeNull();
                    settingValue.Value.ShouldBeEquivalentTo(matchingValue.Value);
                    settingValue.AdditionalData.ShouldBeEquivalentTo(matchingValue.AdditionalData);
                }
            }
        }

        [Fact]
        public void CallingUpdateSubsectionsUpdatesSubsectionSettingMetadata()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <section>
    <subsection>
      <add key=""key1"" value=""value1"" meta1=""data1"" />
    </subsection>
  </section>
</configuration>";

            var settingValue = new SettingValue("key2", "value2", isMachineWide: false);
            settingValue.AdditionalData.Add("meta1", "data1");
            settingValue.AdditionalData.Add("meta2", "data2");

            var valueLookUp = new Dictionary<string, SettingValue>()
            {
                { "key2",  settingValue}
            };

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                settings.UpdateSubsections("section", "subsection", valueLookUp.Values.ToList());
                var settingValues = settings.GetNestedSettingValues("section", "subsection");

                // Assert
                settingValues.Should().NotBeNull();
                settingValues.Count.Should().Be(valueLookUp.Count);
                foreach (var value in settingValues)
                {
                    var matchingValue = valueLookUp[value.Key];
                    matchingValue.Should().NotBeNull();
                    value.Value.ShouldBeEquivalentTo(matchingValue.Value);
                    value.AdditionalData.ShouldBeEquivalentTo(matchingValue.AdditionalData);
                }
            }
        }

        [Fact]
        public void CallingUpdateSubsectionsRemovesSubsectionSettingMetadata()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <section>
    <subsection>
      <add key=""key1"" value=""value1"" meta1=""data1"" />
    </subsection>
  </section>
</configuration>";

            var settingValue = new SettingValue("key2", "value2", isMachineWide: false);

            var valueLookUp = new Dictionary<string, SettingValue>()
            {
                { "key2",  settingValue}
            };

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                settings.UpdateSubsections("section", "subsection", valueLookUp.Values.ToList());
                var settingValues = settings.GetNestedSettingValues("section", "subsection");

                // Assert
                settingValues.Should().NotBeNull();
                settingValues.Count.Should().Be(valueLookUp.Count);
                foreach (var value in settingValues)
                {
                    var matchingValue = valueLookUp[value.Key];
                    matchingValue.Should().NotBeNull();
                    value.Value.ShouldBeEquivalentTo(matchingValue.Value);
                    value.AdditionalData.ShouldBeEquivalentTo(matchingValue.AdditionalData);
                }
            }
        }

        [Fact]
        public void CallingUpdateSubsectionsRemovesSubsectionAndSectionIfEmpty()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <section>
    <subsection>
      <add key=""key1"" value=""value1"" meta1=""data1"" />
    </subsection>
  </section>
</configuration>";
            var valueLookUp = new Dictionary<string, SettingValue>();

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                settings.UpdateSubsections("section", "subsection", valueLookUp.Values.ToList());
                var settingValues = settings.GetNestedSettingValues("section", "subsection");

                // Assert
                settingValues.Should().NotBeNull();
                settingValues.Should().BeEmpty();
            }
        }

        [Fact]
        public void CallingUpdateSubsectionsRemovesSubsectionButLeavesOtherSubsections()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <section>
    <subsection>
      <add key=""key1"" value=""value1"" meta1=""data1"" />
    </subsection>
    <subsection2>
      <add key=""key2"" value=""value2"" meta1=""data1"" />
    </subsection2>
  </section>
</configuration>";

            var settingValue = new SettingValue("key2", "value2", isMachineWide: false);
            settingValue.AdditionalData.Add("meta1", "data1");

            var valueLookUp = new Dictionary<string, SettingValue>()
            {
                { "key2",  settingValue}
            };

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                settings.UpdateSubsections("section", "subsection", valueLookUp.Values.ToList());
                var settingValues = settings.GetNestedSettingValues("section", "subsection2");

                // Assert
                settingValues.Should().NotBeNull();
                settingValues.Count.Should().Be(valueLookUp.Count);
                foreach (var value in settingValues)
                {
                    var matchingValue = valueLookUp[value.Key];
                    matchingValue.Should().NotBeNull();
                    value.Value.ShouldBeEquivalentTo(matchingValue.Value);
                    value.AdditionalData.ShouldBeEquivalentTo(matchingValue.AdditionalData);
                }
            }
        }

        [Fact]
        public void CallingUpdateSubsectionsRemovesSubsectionButLeavesOtherSections()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <section>
    <subsection>
      <add key=""key1"" value=""value1"" meta1=""data1"" />
    </subsection>
  </section>
  <section2>
    <subsection2>
      <add key=""key2"" value=""value2"" meta1=""data1"" />
    </subsection2>
  </section2>
</configuration>";

            var settingValue = new SettingValue("key2", "value2", isMachineWide: false);
            settingValue.AdditionalData.Add("meta1", "data1");

            var valueLookUp = new Dictionary<string, SettingValue>()
            {
                { "key2",  settingValue}
            };

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                settings.UpdateSubsections("section", "subsection", valueLookUp.Values.ToList());
                var settingValues = settings.GetNestedSettingValues("section2", "subsection2");

                // Assert
                settingValues.Should().NotBeNull();
                settingValues.Count.Should().Be(valueLookUp.Count);
                foreach (var value in settingValues)
                {
                    var matchingValue = valueLookUp[value.Key];
                    matchingValue.Should().NotBeNull();
                    value.Value.ShouldBeEquivalentTo(matchingValue.Value);
                    value.AdditionalData.ShouldBeEquivalentTo(matchingValue.AdditionalData);
                }
            }
        }

        [Fact]
        public void CallingUpdateSubsectionsRemovesSubsectionAndSectionInConfigFileIfEmpty()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <section>
    <subsection>
      <add key=""key1"" value=""value1"" meta1=""data1"" />
    </subsection>
  </section>
</configuration>";
            var valueLookUp = new Dictionary<string, SettingValue>();

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                settings.UpdateSubsections("section", "subsection", valueLookUp.Values.ToList());
                var settingValues = settings.GetNestedSettingValues("section", "subsection");

                // Assert
                var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>";

                Assert.Equal(result.Replace("\r\n", "\n"), File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath)).Replace("\r\n", "\n"));
            }
        }

        [Fact]
        public void CallingUpdateSubsectionsRemovesSubsectionAndSectionInConfigFileButLeavesOtherSections()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <section>
    <subsection>
      <add key=""key1"" value=""value1"" meta1=""data1"" />
    </subsection>
  </section>
  <section2>
    <subsection2>
      <add key=""key1"" value=""value1"" meta1=""data1"" />
    </subsection2>
  </section2>
</configuration>";
            var valueLookUp = new Dictionary<string, SettingValue>();

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                settings.UpdateSubsections("section", "subsection", valueLookUp.Values.ToList());
                var settingValues = settings.GetNestedSettingValues("section", "subsection");

                // Assert
                var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <section2>
    <subsection2>
      <add key=""key1"" value=""value1"" meta1=""data1"" />
    </subsection2>
  </section2>
</configuration>";

                Assert.Equal(result.Replace("\r\n", "\n"), File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath)).Replace("\r\n", "\n"));
            }
        }

        [Fact]
        public void CallingUpdateSubsectionsRemovesSubsectionAndSectionInConfigFileButLeavesOtherSubsections()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <section>
    <subsection>
      <add key=""key1"" value=""value1"" meta1=""data1"" />
    </subsection>
    <subsection2>
      <add key=""key1"" value=""value1"" meta1=""data1"" />
    </subsection2>
  </section>
</configuration>";
            var valueLookUp = new Dictionary<string, SettingValue>();

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                settings.UpdateSubsections("section", "subsection", valueLookUp.Values.ToList());
                var settingValues = settings.GetNestedSettingValues("section", "subsection");

                // Assert
                var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <section>
    <subsection2>
      <add key=""key1"" value=""value1"" meta1=""data1"" />
    </subsection2>
  </section>
</configuration>";

                Assert.Equal(result.Replace("\r\n", "\n"), File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath)).Replace("\r\n", "\n"));
            }
        }

        [Fact]
        public void CallingUpdateSubsectionsUpdatesSubsectionsIntoNestedSettingsWhenPresent()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <section>
    <subsection>
      <add key=""key1"" value=""value1"" meta1=""data1"" meta2=""data2"" />
    </subsection>
  </section>
</configuration>";

            var config2 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <section>
    <subsection>
      <add key=""key2"" value=""value2"" meta1=""data1"" />
    </subsection>
  </section>
</configuration>";

            var settingValue1 = new SettingValue("key1", "value1", isMachineWide: false);
            settingValue1.AdditionalData.Add("meta1", "data1");
            settingValue1.AdditionalData.Add("meta2", "data2");

            var settingValue2 = new SettingValue("key2", "value2", isMachineWide: false);
            settingValue2.AdditionalData.Add("meta1", "data1");

            var valueLookUp = new Dictionary<string, SettingValue>()
            {
                { "key1",  settingValue1},
                { "key2",  settingValue2}
            };

            using (var mockBaseDirectory = TestDirectory.Create())
            using (var mockChildDirectory = TestDirectory.Create(mockBaseDirectory))
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config1);
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockChildDirectory, config2);

                var configPaths = new List<string> { Path.Combine(mockChildDirectory, nugetConfigPath), Path.Combine(mockBaseDirectory, nugetConfigPath) };
                var settings = Settings.LoadSettingsGivenConfigPaths(configPaths);

                // Act
                settings.UpdateSubsections("section", "subsection", valueLookUp.Values.ToList());
                var settingValues = settings.GetNestedSettingValues("section", "subsection");

                // Assert
                settingValues.Should().NotBeNull();
                settingValues.Count.Should().Be(valueLookUp.Count);
                foreach (var settingValue in settingValues)
                {
                    var matchingValue = valueLookUp[settingValue.Key];
                    matchingValue.Should().NotBeNull();
                    settingValue.Value.ShouldBeEquivalentTo(matchingValue.Value);
                    settingValue.AdditionalData.ShouldBeEquivalentTo(matchingValue.AdditionalData);
                }
            }
        }

        [Fact]
        public void CallingUpdateSubsectionsRemovesSubsectionsFromNestedSettingsWhenEmpty()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <section>
    <subsection>
      <add key=""key1"" value=""value1"" meta1=""data1"" meta2=""data2"" />
    </subsection>
  </section>
</configuration>";

            var config2 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <section>
    <subsection>
      <add key=""key2"" value=""value2"" meta1=""data1"" />
    </subsection>
  </section>
</configuration>";

            var valueLookUp = new Dictionary<string, SettingValue>();

            using (var mockBaseDirectory = TestDirectory.Create())
            using (var mockChildDirectory = TestDirectory.Create(mockBaseDirectory))
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config1);
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockChildDirectory, config2);

                var configPaths = new List<string> { Path.Combine(mockChildDirectory, nugetConfigPath), Path.Combine(mockBaseDirectory, nugetConfigPath) };
                var settings = Settings.LoadSettingsGivenConfigPaths(configPaths);

                // Act
                settings.UpdateSubsections("section", "subsection", valueLookUp.Values.ToList());
                var settingValues = settings.GetNestedSettingValues("section", "subsection");

                // Assert
                settingValues.Should().NotBeNull();
                settingValues.Should().BeEmpty();
            }
        }

        [Fact]
        public void CallingUpdateSubsectionsRemovesSubsectionsFromNestedSettingsInConfigFileWhenEmpty()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <section>
    <subsection>
      <add key=""key1"" value=""value1"" meta1=""data1"" meta2=""data2"" />
    </subsection>
  </section>
</configuration>";

            var config2 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <section>
    <subsection>
      <add key=""key2"" value=""value2"" meta1=""data1"" />
    </subsection>
  </section>
</configuration>";

            var valueLookUp = new Dictionary<string, SettingValue>();

            using (var mockBaseDirectory = TestDirectory.Create())
            using (var mockChildDirectory = TestDirectory.Create(mockBaseDirectory))
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config1);
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockChildDirectory, config2);

                var configPaths = new List<string> { Path.Combine(mockChildDirectory, nugetConfigPath), Path.Combine(mockBaseDirectory, nugetConfigPath) };
                var settings = Settings.LoadSettingsGivenConfigPaths(configPaths);

                // Act
                settings.UpdateSubsections("section", "subsection", valueLookUp.Values.ToList());
                var settingValues = settings.GetNestedSettingValues("section", "subsection");

                // Assert
                var result1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>";

                var result2 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>";

                Assert.Equal(result1.Replace("\r\n", "\n"), File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath)).Replace("\r\n", "\n"));
                Assert.Equal(result2.Replace("\r\n", "\n"), File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath)).Replace("\r\n", "\n"));
            }
        }


        [Fact]
        public void CallingUpdateSubsectionsRemovesSubsectionsFromNestedSettingsInConfigFileButLeavesOtherSections()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <section>
    <subsection>
      <add key=""key1"" value=""value1"" meta1=""data1"" meta2=""data2"" />
    </subsection>
  </section>
  <section3>
    <subsection4>
      <add key=""key1"" value=""value1"" meta1=""data1"" meta2=""data2"" />
    </subsection4>
  </section3>
</configuration>";

            var config2 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <section>
    <subsection>
      <add key=""key2"" value=""value2"" meta1=""data1"" />
    </subsection>
  </section>
  <section2>
    <subsection3>
      <add key=""key1"" value=""value1"" meta1=""data1"" meta2=""data2"" />
    </subsection3>
  </section2>
</configuration>";

            var valueLookUp = new Dictionary<string, SettingValue>();

            using (var mockBaseDirectory = TestDirectory.Create())
            using (var mockChildDirectory = TestDirectory.Create(mockBaseDirectory))
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config1);
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockChildDirectory, config2);

                var configPaths = new List<string> { Path.Combine(mockChildDirectory, nugetConfigPath), Path.Combine(mockBaseDirectory, nugetConfigPath) };
                var settings = Settings.LoadSettingsGivenConfigPaths(configPaths);

                // Act
                settings.UpdateSubsections("section", "subsection", valueLookUp.Values.ToList());
                var settingValues = settings.GetNestedSettingValues("section", "subsection");

                // Assert
                var result1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <section3>
    <subsection4>
      <add key=""key1"" value=""value1"" meta1=""data1"" meta2=""data2"" />
    </subsection4>
  </section3>
</configuration>";

                var result2 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <section2>
    <subsection3>
      <add key=""key1"" value=""value1"" meta1=""data1"" meta2=""data2"" />
    </subsection3>
  </section2>
</configuration>";

                Assert.Equal(result1.Replace("\r\n", "\n"), File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath)).Replace("\r\n", "\n"));
                Assert.Equal(result2.Replace("\r\n", "\n"), File.ReadAllText(Path.Combine(mockChildDirectory, nugetConfigPath)).Replace("\r\n", "\n"));
            }
        }

        [Fact]
        public void CallingDeleteValueWithEmptyKeyThrowsException()
        {
            // Arrange
            var configFile = "NuGet.Config";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
                var settings = new Settings(mockBaseDirectory);

                // Act & Assert
                var ex = Record.Exception(() => settings.DeleteValue("SomeSection", ""));
                Assert.NotNull(ex);
                Assert.IsAssignableFrom<ArgumentException>(ex);
            }
        }

        [Fact]
        public void CallingDeleteValueWithEmptySectionThrowsException()
        {
            // Arrange
            var configFile = "NuGet.Config";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
                var settings = new Settings(mockBaseDirectory);

                // Act & Assert
                var ex = Record.Exception(() => settings.DeleteValue("", "SomeKey"));
                Assert.NotNull(ex);
                Assert.IsAssignableFrom<ArgumentException>(ex);
            }
        }

        [Fact]
        public void CallingDeleteValueWhenSectionNameDoesntExistReturnsFalse()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value=""value"" />
  </SectionName>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act & Assert
                Assert.False(settings.DeleteValue("SectionDoesNotExists", "SomeKey"));
            }
        }

        [Fact]
        public void CallingDeleteValueWhenKeyDoesntExistThrowsException()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value=""value"" />
  </SectionName>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act & Assert
                Assert.False(settings.DeleteValue("SectionName", "KeyDoesNotExist"));
            }
        }

        [Fact]
        public void CallingDeleteValueWithValidSectionAndKeyDeletesTheEntryAndReturnsTrue()
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
                var settings = new Settings(mockBaseDirectory);

                // Act & Assert
                Assert.True(settings.DeleteValue("SectionName", "DeleteMe"));
                var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""keyNotToDelete"" value=""value"" />
  </SectionName>
  <SectionName2>
    <add key=""key"" value=""value"" />
  </SectionName2>
</configuration>";
                Assert.Equal(result.Replace("\r\n", "\n"),
                    File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath)).Replace("\r\n", "\n"));
            }
        }

        [Fact]
        public void CallingDeleteSectionWithEmptySectionThrowsException()
        {
            // Arrange
            var configFile = "NuGet.Config";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
                var settings = new Settings(mockBaseDirectory);

                // Act & Assert
                var ex = Record.Exception(() => settings.DeleteSection(""));
                Assert.NotNull(ex);
                Assert.IsAssignableFrom<ArgumentException>(ex);
            }
        }

        [Fact]
        public void CallingDeleteSectionWhenSectionNameDoesntExistReturnsFalse()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value=""value"" />
  </SectionName>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act & Assert
                Assert.False(settings.DeleteSection("SectionDoesNotExists"));
            }
        }

        [Fact]
        public void CallingDeleteSectionWithValidSectionDeletesTheSectionAndReturnsTrue()
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
                var settings = new Settings(mockBaseDirectory);

                // Act & Assert
                Assert.True(settings.DeleteSection("SectionName"));
                var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName2>
    <add key=""key"" value=""value"" />
  </SectionName2>
</configuration>";
                Assert.Equal(result.Replace("\r\n", "\n"),
                    File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath)).Replace("\r\n", "\n"));
            }
        }


        [Fact]
        public void GetValueIgnoresClearedValues()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key1"" value=""foo"" />
    <clear />
    <add key=""key2"" value=""bar"" />
  </SectionName>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                var result1 = settings.GetValue("SectionName", "Key1");
                var result2 = settings.GetValue("SectionName", "Key2");

                // Assert
                Assert.Null(result1);
                Assert.Equal("bar", result2);
            }
        }

        [Fact]
        public void GetValue_IgnoresAdditionalAttributes()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key1"" value=""foo"" additional1=""some-value"" />
    <clear />
    <add key=""key2"" value=""bar"" additional2=""some-value"" someAttribute=""someAttributeValue"" />
  </SectionName>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                var result1 = settings.GetValue("SectionName", "Key1");
                var result2 = settings.GetValue("SectionName", "Key2");

                // Assert
                Assert.Null(result1);
                Assert.Equal("bar", result2);
            }
        }

        [Fact]
        public void GetValuesIgnoresClearedValues()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key1"" value=""value1"" />
    <add key=""key2"" value=""value2"" />
    <clear />
    <add key=""key3"" value=""value3"" />
    <add key=""key4"" value=""value4"" />
  </SectionName>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                var result = settings.GetSettingValues("SectionName");

                // Assert
                AssertEqualCollections(result, new[] { "key3", "value3", "key4", "value4" });
            }
        }

        [Fact]
        public void GetValuesWithIsPathTrue()
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
                var result = settings.GetSettingValues("SectionName", isPath: true);

                // Assert
                if (RuntimeEnvironmentHelper.IsWindows)
                {
                    AssertEqualCollections(
                    result,
                    new[]
                        {
                        "key1", Path.Combine(mockBaseDirectory, @"..\value1"),
                        "key2", Path.Combine(mockBaseDirectory, @"a\b\c"),
                        "key3", Path.Combine(mockBaseDirectory, @".\a\b\c"),
                        "key4", @"c:\value2",
                        "key5", @"http://value3",
                        "key6", @"\\a\b\c",
                        "key7", Path.Combine(Path.GetPathRoot(mockBaseDirectory), @"a\b\c")
                        });
                }
                else
                {
                    AssertEqualCollections(
                    result,
                    new[]
                        {
                        "key1", Path.Combine(mockBaseDirectory, @"../value1"),
                        "key2", Path.Combine(mockBaseDirectory, @"a/b/c"),
                        "key3", Path.Combine(mockBaseDirectory, @"./a/b/c"),
                        "key5", @"http://value3",
                        "key7", @"/a/b/c"
                        });
                }
            }
        }

        [Fact]
        public void GetValuesMultipleConfFilesAdditive()
        {
            // Arrange
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var nugetConfigPath = "NuGet.Config";
                var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key3"" value=""value3"" />
    <add key=""key4"" value=""value4"" />
  </SectionName>
</configuration>";
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1", "dir2"), config);
                config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key1"" value=""value1"" />
    <add key=""key2"" value=""value2"" />
  </SectionName>
</configuration>";
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1"), config);

                var settings = Settings.LoadDefaultSettings(Path.Combine(mockBaseDirectory, "dir1", "dir2"), null, null);

                // Act
                var result = settings.GetSettingValues("SectionName");

                // Assert
                AssertEqualCollections(result, new[] { "key1", "value1", "key2", "value2", "key3", "value3", "key4", "value4" });
            }
        }

        [Fact]
        public void GetValuesMultipleConfFilesClear()
        {
            // Arrange
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var nugetConfigPath = "NuGet.Config";
                var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <clear /> <!-- i.e. ignore values from prior conf files -->
    <add key=""key3"" value=""value3"" />
    <add key=""key4"" value=""value4"" />
  </SectionName>
</configuration>";
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, @"dir1\dir2"), config);
                config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key1"" value=""value1"" />
    <add key=""key2"" value=""value2"" />
  </SectionName>
</configuration>";
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1"), config);

                var settings = Settings.LoadDefaultSettings(Path.Combine(mockBaseDirectory, @"dir1\dir2"), null, null);

                // Act
                var result = settings.GetSettingValues("SectionName");

                // Assert
                AssertEqualCollections(result, new[] { "key3", "value3", "key4", "value4" });
            }
        }

        [Fact]
        public void GetSettingValuesMultipleConfFilesClear()
        {
            // Arrange
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var nugetConfigPath = "NuGet.Config";
                var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <clear /> <!-- i.e. ignore values from prior conf files -->
    <add key=""key3"" value=""value3"" />
    <add key=""key4"" value=""value4"" />
  </SectionName>
</configuration>";
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, @"dir1\dir2"), config);
                config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key1"" value=""value1"" />
    <add key=""key2"" value=""value2"" />
  </SectionName>
</configuration>";
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1"), config);

                var settings = Settings.LoadDefaultSettings(Path.Combine(mockBaseDirectory, @"dir1\dir2"), null, null);

                // Act
                var result = settings.GetSettingValues("SectionName");

                // Assert
                Assert.Equal<SettingValue>(
                    new[]
                        {
                        new SettingValue("key3", "value3", isMachineWide: false, priority: 0),
                        new SettingValue("key4", "value4", isMachineWide: false, priority: 0)
                        },
                    result);
            }
        }

        [Fact]
        public void GetSettingValues_ReadsAttributeValues_FromElements()
        {
            // Arrange
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var nugetConfigPath = "NuGet.Config";
                var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <clear />
    <add key=""key3"" value=""value3"" data-key3=""key3"" />
    <add key=""key4"" value=""value4"" data-key4=""key4"" someAttr=""someAttrValue"" />
  </SectionName>
</configuration>";
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, @"dir1\dir2"), config);
                config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key1"" value=""value1"" />
    <add key=""key2"" value=""value2"" />
  </SectionName>
</configuration>";
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1"), config);

                var settings = Settings.LoadDefaultSettings(Path.Combine(mockBaseDirectory, @"dir1\dir2"), null, null);

                // Act
                var result = settings.GetSettingValues("SectionName");

                // Assert
                Assert.Collection(result,
                    setting =>
                    {
                        Assert.Equal("key3", setting.Key);
                        Assert.Equal("value3", setting.Value);
                        Assert.Collection(setting.AdditionalData,
                            item =>
                            {
                                Assert.Equal("data-key3", item.Key);
                                Assert.Equal("key3", item.Value);
                            });
                    },
                    setting =>
                    {
                        Assert.Equal("key4", setting.Key);
                        Assert.Equal("value4", setting.Value);
                        Assert.Collection(setting.AdditionalData,
                            item =>
                            {
                                Assert.Equal("data-key4", item.Key);
                                Assert.Equal("key4", item.Value);
                            },
                            item =>
                            {
                                Assert.Equal("someAttr", item.Key);
                                Assert.Equal("someAttrValue", item.Value);
                            });
                    });
            }
        }

        [Fact]
        public void GetSettingValues_IgnoresAttributesFromClearedElements()
        {
            // Arrange
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var nugetConfigPath = "NuGet.Config";
                var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <clear />
    <add key=""key3"" value=""value3"" data-key3=""key3"" />
    <add key=""key4"" value=""value4"" data-key4=""key4"" someAttr=""someAttrValue"" />
  </SectionName>
</configuration>";
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, @"dir1\dir2"), config);
                config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key3"" value=""value3"" data-key3=""different-value"" not-inherited-attribute=""not-inherited"" />
  </SectionName>
</configuration>";
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1"), config);

                var settings = Settings.LoadDefaultSettings(Path.Combine(mockBaseDirectory, @"dir1\dir2"), null, null);

                // Act
                var result = settings.GetSettingValues("SectionName");

                // Assert
                Assert.Collection(result,
                    setting =>
                    {
                        Assert.Equal("key3", setting.Key);
                        Assert.Equal("value3", setting.Value);
                        Assert.Collection(setting.AdditionalData,
                            item =>
                            {
                                Assert.Equal("data-key3", item.Key);
                                Assert.Equal("key3", item.Value);
                            });
                    },
                    setting =>
                    {
                        Assert.Equal("key4", setting.Key);
                        Assert.Equal("value4", setting.Value);
                        Assert.Collection(setting.AdditionalData,
                            item =>
                            {
                                Assert.Equal("data-key4", item.Key);
                                Assert.Equal("key4", item.Value);
                            },
                            item =>
                            {
                                Assert.Equal("someAttr", item.Key);
                                Assert.Equal("someAttrValue", item.Value);
                            });
                    });
            }
        }

        [Fact]
        public void GetSingleValuesMultipleConfFiles()
        {
            // Arrange
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var nugetConfigPath = "NuGet.Config";
                var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key3"" value=""value3"" />
    <add key=""key4"" value=""value4"" />
  </SectionName>
</configuration>";
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1", "dir2"), config);
                config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key1"" value=""value1"" />
    <add key=""key2"" value=""value2"" />
  </SectionName>
</configuration>";
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1"), config);

                var settings = Settings.LoadDefaultSettings(Path.Combine(mockBaseDirectory, "dir1", "dir2"), null, null);

                // Assert
                Assert.Equal("value4", settings.GetValue("SectionName", "key4"));
                Assert.Equal("value3", settings.GetValue("SectionName", "key3"));
                Assert.Equal("value2", settings.GetValue("SectionName", "key2"));
                Assert.Equal("value1", settings.GetValue("SectionName", "key1"));
            }
        }

        [Fact]
        public void GetSingleValuesMultipleConfFilesWithDupes()
        {
            // Arrange
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var nugetConfigPath = "NuGet.Config";
                var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key1"" value=""LastOneWins1"" />
    <add key=""key2"" value=""LastOneWins2"" />
  </SectionName>
</configuration>";
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, @"dir1\dir2"), config);
                config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key1"" value=""value1"" />
    <add key=""key2"" value=""value2"" />
  </SectionName>
</configuration>";
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1"), config);

                var settings = Settings.LoadDefaultSettings(Path.Combine(mockBaseDirectory, @"dir1\dir2"), null, null);

                // Assert
                Assert.Equal("LastOneWins2", settings.GetValue("SectionName", "key2"));
                Assert.Equal("LastOneWins1", settings.GetValue("SectionName", "key1"));
            }
        }

        [Fact]
        public void GetSingleValuesMultipleConfFilesClear()
        {
            // Arrange
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var nugetConfigPath = "NuGet.Config";
                var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <clear /> <!-- i.e. ignore values from prior conf files -->
    <add key=""key2"" value=""value2"" />
  </SectionName>
</configuration>";
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, @"dir1\dir2"), config);
                config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key1"" value=""value1"" />
  </SectionName>
</configuration>";
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1"), config);

                var settings = Settings.LoadDefaultSettings(Path.Combine(mockBaseDirectory, @"dir1\dir2"), null, null);

                // Assert
                Assert.Equal("value2", settings.GetValue("SectionName", "key2"));
                Assert.Equal(null, settings.GetValue("SectionName", "key1"));
            }
        }

        [Fact]
        public void GetValueReturnsPathRelativeToConfigWhenPathIsNotRooted()
        {
            // Arrange
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""path-key"" value=""foo\bar"" />
  </SectionName>
</configuration>";
                ConfigurationFileTestUtility.CreateConfigurationFile("nuget.config", mockBaseDirectory, config);

                var settings = new Settings(mockBaseDirectory, "nuget.config");

                // Act
                var result = settings.GetValue("SectionName", "path-key", isPath: true);

                // Assert
                Assert.Equal(Path.Combine(mockBaseDirectory, @"foo\bar"), result);
            }
        }

        [Fact]
        public void GetValuesWithUserSpecifiedDefaultConfigFile()
        {
            // Arrange
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key3"" value=""value3"" />
    <add key=""key4"" value=""value4"" />
  </SectionName>
</configuration>";
                ConfigurationFileTestUtility.CreateConfigurationFile("NuGet.Config", Path.Combine(mockBaseDirectory, "dir1"), config);
                config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key1"" value=""value1"" />
    <add key=""key2"" value=""value2"" />
  </SectionName>
</configuration>";
                ConfigurationFileTestUtility.CreateConfigurationFile("UserDefinedConfigFile.confg", Path.Combine(mockBaseDirectory, "dir1", "dir2"), config);

                var settings = Settings.LoadDefaultSettings(
                    Path.Combine(mockBaseDirectory, "dir1", "dir2"),
                    "UserDefinedConfigFile.confg",
                    null);

                // Act
                var result = settings.GetSettingValues("SectionName");

                // Assert
                AssertEqualCollections(result, new[] { "key1", "value1", "key2", "value2" });
            }
        }

        [Theory]
        [InlineData(@"z:\foo", "windows")]
        [InlineData(@"x:\foo\bar\qux", "windows")]
        [InlineData(@"\\share\folder\subfolder", "windows")]
        [InlineData(@"/a/b/c", "linux")]
        public void GetValueReturnsPathWhenPathIsRooted(string value, string os)
        {
            if ((os == "linux" && RuntimeEnvironmentHelper.IsLinux) || (os == "windows" && RuntimeEnvironmentHelper.IsWindows))
            {
                // Arrange
                using (var mockBaseDirectory = TestDirectory.Create())
                {
                    var config = string.Format(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""path-key"" value=""{0}"" />
  </SectionName>
</configuration>", value);
                    ConfigurationFileTestUtility.CreateConfigurationFile("nuget.config", mockBaseDirectory, config);
                    var settings = new Settings(mockBaseDirectory, "nuget.config");

                    // Act
                    var result = settings.GetValue("SectionName", "path-key", isPath: true);

                    // Assert
                    Assert.Equal(value, result);
                }
            }
        }

        [Fact]
        public void GetValueReturnsPathRelativeToRootOfConfig()
        {
            // Arrange
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""path-key"" value=""/Blah"" />
  </SectionName>
</configuration>";
                ConfigurationFileTestUtility.CreateConfigurationFile("nuget.config", mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory, "nuget.config");

                // Act
                var result = settings.GetValue("SectionName", "path-key", isPath: true);

                // Assert
                Assert.Equal(Path.Combine(Path.GetPathRoot(mockBaseDirectory), "Blah"), result);
            }
        }

        [Fact]
        public void GetValueResolvesRelativePaths()
        {
            // Arrange
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""path-key"" value=""..\Blah"" />
  </SectionName>
</configuration>";
                ConfigurationFileTestUtility.CreateConfigurationFile("nuget.config", mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory, "nuget.config");

                // Act
                var result = settings.GetValue("SectionName", "path-key", isPath: true);

                // Assert
                Assert.Equal(Path.Combine(mockBaseDirectory, @"..\Blah"), result);
            }
        }

        // Checks that the correct files are read, in the right order,
        // when laoding machine wide settings.
        [Fact]
        public void LoadMachineWideSettings()
        {
            // Arrange
            var fileContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value=""value"" />
  </SectionName>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile("a1.config", Path.Combine(mockBaseDirectory, "nuget"), fileContent);
                ConfigurationFileTestUtility.CreateConfigurationFile("a1.config", Path.Combine(mockBaseDirectory, "nuget", "Config"), fileContent);
                ConfigurationFileTestUtility.CreateConfigurationFile("a2.config", Path.Combine(mockBaseDirectory, "nuget", "Config"), fileContent);
                ConfigurationFileTestUtility.CreateConfigurationFile("a3.xconfig", Path.Combine(mockBaseDirectory, "nuget", "Config"), fileContent);
                ConfigurationFileTestUtility.CreateConfigurationFile("a1.config", Path.Combine(mockBaseDirectory, "nuget", "Config", "IDE"), fileContent);
                ConfigurationFileTestUtility.CreateConfigurationFile("a2.config", Path.Combine(mockBaseDirectory, "nuget", "Config", "IDE"), fileContent);
                ConfigurationFileTestUtility.CreateConfigurationFile("a3.xconfig", Path.Combine(mockBaseDirectory, "nuget", "Config", "IDE"), fileContent);
                ConfigurationFileTestUtility.CreateConfigurationFile("a1.config", Path.Combine(mockBaseDirectory, "nuget", "Config", "IDE", "Version"), fileContent);
                ConfigurationFileTestUtility.CreateConfigurationFile("a2.config", Path.Combine(mockBaseDirectory, "nuget", "Config", "IDE", "Version"), fileContent);
                ConfigurationFileTestUtility.CreateConfigurationFile("a3.xconfig", Path.Combine(mockBaseDirectory, "nuget", "Config", "IDE", "Version"), fileContent);
                ConfigurationFileTestUtility.CreateConfigurationFile("a1.config", Path.Combine(mockBaseDirectory, "nuget", "Config", "IDE", "Version", "SKU"), fileContent);
                ConfigurationFileTestUtility.CreateConfigurationFile("a2.config", Path.Combine(mockBaseDirectory, "nuget", "Config", "IDE", "Version", "SKU"), fileContent);
                ConfigurationFileTestUtility.CreateConfigurationFile("a3.xconfig", Path.Combine(mockBaseDirectory, "nuget", "Config", "IDE", "Version", "SKU"), fileContent);
                ConfigurationFileTestUtility.CreateConfigurationFile("a1.config", Path.Combine(mockBaseDirectory, "nuget", "Config", "IDE", "Version", "SKU", "Dir"), fileContent);
                ConfigurationFileTestUtility.CreateConfigurationFile("a1_uppercase.Config", Path.Combine(mockBaseDirectory, "nuget", "Config"), fileContent);

                // Act
                var settings = Settings.LoadMachineWideSettings(
                    Path.Combine(mockBaseDirectory, "nuget", "Config"), "IDE", "Version", "SKU", "TestDir");

                // Assert
                var files = SettingsUtility.GetConfigFilePaths(settings).ToArray();

                Assert.Equal(9, files.Count());
                Assert.True(files.Contains(Path.Combine(mockBaseDirectory, "nuget", "Config", "IDE", "Version", "SKU", "a2.config")));
                Assert.True(files.Contains(Path.Combine(mockBaseDirectory, "nuget", "Config", "IDE", "Version", "SKU", "a1.config")));
                Assert.True(files.Contains(Path.Combine(mockBaseDirectory, "nuget", "Config", "IDE", "Version", "a2.config")));
                Assert.True(files.Contains(Path.Combine(mockBaseDirectory, "nuget", "Config", "IDE", "Version", "a1.config")));
                Assert.True(files.Contains(Path.Combine(mockBaseDirectory, "nuget", "Config", "IDE", "a2.config")));
                Assert.True(files.Contains(Path.Combine(mockBaseDirectory, "nuget", "Config", "IDE", "a1.config")));
                Assert.True(files.Contains(Path.Combine(mockBaseDirectory, "nuget", "Config", "a2.config")));
                Assert.True(files.Contains(Path.Combine(mockBaseDirectory, "nuget", "Config", "a1.config")));
                Assert.True(files.Contains(Path.Combine(mockBaseDirectory, "nuget", "Config", "a1_uppercase.Config")));
            }
        }

        // Tests method GetValue() with machine wide settings.
        [Fact]
        public void GetValueWithMachineWideSettings()
        {
            // Arrange
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var fileContent1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key1"" value=""value1"" />
    <add key=""key2"" value=""value2"" />
  </SectionName>
</configuration>";
                ConfigurationFileTestUtility.CreateConfigurationFile("a1.config", Path.Combine(mockBaseDirectory, "nuget", "Config"), fileContent1);
                var fileContent2 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key2"" value=""value3"" />
    <add key=""key3"" value=""value4"" />
  </SectionName>
</configuration>";
                ConfigurationFileTestUtility.CreateConfigurationFile("a1.config", Path.Combine(mockBaseDirectory, "nuget", "Config", "IDE"), fileContent2);
                var fileContent3 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key3"" value=""user"" />
  </SectionName>
</configuration>";
                ConfigurationFileTestUtility.CreateConfigurationFile("NuGet.Config", Path.Combine(mockBaseDirectory, "TestingGlobalPath"), fileContent3);

                var m = new Mock<IMachineWideSettings>();
                m.SetupGet(obj => obj.Settings).Returns(
                    Settings.LoadMachineWideSettings(Path.Combine(mockBaseDirectory, "nuget", "Config"), "IDE", "Version", "SKU"));

                // Act
                var settings = Settings.LoadSettings(
                    mockBaseDirectory,
                    null,
                    m.Object,
                    true,
                    true);

                // Assert
                var v = settings.GetValue("SectionName", "key1");
                Assert.Equal("value1", v);

                // the value in NuGet\Config\IDE\a1.config overrides the value in
                // NuGet\Config\a1.config
                v = settings.GetValue("SectionName", "key2");
                Assert.Equal("value3", v);

                // the value in user.config overrides the value in NuGet\Config\IDE\a1.config
                v = settings.GetValue("SectionName", "key3");
                Assert.Equal("user", v);
            }
        }

        // Tests method SetValue() with machine wide settings.
        // Verifies that the user specific config file is modified, while machine
        // wide settings files are not touched.
        [Fact]
        public void SetValueWithMachineWideSettings()
        {
            // Arrange
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var a1Config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key1"" value=""value1"" />
  </SectionName>
</configuration>".Replace("\r\n", "\n");
                var a2Config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key2"" value=""value2"" />
    <add key=""key3"" value=""value3"" />
  </SectionName>
</configuration>".Replace("\r\n", "\n");
                var userConfig = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key3"" value=""user"" />
  </SectionName>
</configuration>".Replace("\r\n", "\n");

                ConfigurationFileTestUtility.CreateConfigurationFile("a1.config", Path.Combine(mockBaseDirectory, "nuget", "Config"), a1Config);
                ConfigurationFileTestUtility.CreateConfigurationFile("a2.config", Path.Combine(mockBaseDirectory, "nuget", "Config", "IDE"), a2Config);
                ConfigurationFileTestUtility.CreateConfigurationFile("user.config", mockBaseDirectory, userConfig);

                var m = new Mock<IMachineWideSettings>();
                m.SetupGet(obj => obj.Settings).Returns(
                    Settings.LoadMachineWideSettings(mockBaseDirectory, "IDE", "Version", "SKU"));

                var settings = Settings.LoadDefaultSettings(
                    mockBaseDirectory,
                    "user.config",
                    m.Object);

                // Act
                settings.SetValue("SectionName", "key1", "newValue");

                // Assert
                var text = File.ReadAllText(Path.Combine(mockBaseDirectory, "user.config"));
                var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key3"" value=""user"" />
    <add key=""key1"" value=""newValue"" />
  </SectionName>
</configuration>".Replace("\r\n", "\n");
                Assert.Equal(result, text.Replace("\r\n", "\n"));

                text = File.ReadAllText(Path.Combine(mockBaseDirectory, "nuget", "Config", "a1.config"));
                Assert.Equal(a1Config, text.Replace("\r\n", "\n"));

                text = File.ReadAllText(Path.Combine(mockBaseDirectory, "nuget", "Config", "IDE", "a2.config"));
                Assert.Equal(a2Config, text.Replace("\r\n", "\n"));
            }
        }
        // Tests the scenario where there are two user settings, both created
        // with the same machine wide settings.
        [Fact]
        public void GetValueFromTwoUserSettingsWithMachineWideSettings()
        {
            // Arrange
            using (var mockBaseDirectory = TestDirectory.Create())
            using (var mockBaseDirectory1 = TestDirectory.Create())
            using (var mockBaseDirectory2 = TestDirectory.Create())
            {
                var FileContent1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key1"" value=""value1"" />
  </SectionName>
</configuration>".Replace("\r\n", "\n");
                var FileContent2 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key3"" value=""user1"" />
  </SectionName>
</configuration>".Replace("\r\n", "\n");
                var FileContent3 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key3"" value=""user2"" />
  </SectionName>
</configuration>".Replace("\r\n", "\n");
                ConfigurationFileTestUtility.CreateConfigurationFile("a1.config", Path.Combine(mockBaseDirectory, "nuget", "Config"), FileContent1);
                ConfigurationFileTestUtility.CreateConfigurationFile("NuGet.Config", Path.Combine(mockBaseDirectory1, "TestingGlobalPath"), FileContent2);
                ConfigurationFileTestUtility.CreateConfigurationFile("NuGet.Config", Path.Combine(mockBaseDirectory2, "TestingGlobalPath"), FileContent3);

                var m = new Mock<IMachineWideSettings>();
                m.SetupGet(obj => obj.Settings).Returns(
                    Settings.LoadMachineWideSettings(Path.Combine(mockBaseDirectory, "nuget", "Config"), "IDE", "Version", "SKU"));

                // Act
                var settings1 = Settings.LoadSettings(
                    mockBaseDirectory1,
                    null,
                    m.Object,
                    true,
                    true);
                var settings2 = Settings.LoadSettings(
                    mockBaseDirectory2,
                    null,
                    m.Object,
                    true,
                    true);

                // Assert
                var v = settings1.GetValue("SectionName", "key3");
                Assert.Equal("user1", v);
                v = settings1.GetValue("SectionName", "key1");
                Assert.Equal("value1", v);

                v = settings2.GetValue("SectionName", "key3");
                Assert.Equal("user2", v);
                v = settings2.GetValue("SectionName", "key1");
                Assert.Equal("value1", v);
            }
        }

        private void AssertEqualCollections(IList<SettingValue> actual, string[] expected)
        {
            Assert.Equal(actual.Count, expected.Length / 2);
            for (var i = 0; i < actual.Count; ++i)
            {
                Assert.Equal(expected[2 * i], actual[i].Key);
                Assert.Equal(expected[2 * i + 1], actual[i].Value);
            }
        }

#pragma warning restore CS0618 // Type or member is obsolete
    }
}