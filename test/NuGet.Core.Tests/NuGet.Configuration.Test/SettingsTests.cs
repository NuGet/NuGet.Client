// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Moq;
using Xunit;

namespace NuGet.Configuration.Test
{
    public class SettingsTests
    {
        [Theory]
        [InlineData(@"D:\", @"C:\Users\SomeUsers\AppData\Roaming\nuget\nuget.config", @"C:\Users\SomeUsers\AppData\Roaming\nuget", @"nuget.config")]
        [InlineData(@"D:\", (string)null, @"D:\", (string)null)]
        [InlineData(@"D:\", "nuget.config", @"D:\", "nuget.config")]
        public void TestGetFileNameAndItsRoot(string root, string settingsPath, string expectedRoot, string expectedFileName)
        {
            // Act
            var tuple = Settings.GetFileNameAndItsRoot(root, settingsPath);

            // Assert
            Assert.Equal(tuple.Item1, expectedFileName);
            Assert.Equal(tuple.Item2, expectedRoot);
        }

        [Fact]
        public void CallingCtorWithNullRootWithThrowException()
        {
            // Act & Assert
            var ex = Record.Exception(() => new Settings(null));
            Assert.NotNull(ex);
            var tex = Assert.IsAssignableFrom<ArgumentException>(ex);
        }

        [Fact]
        public void WillGetConfigurationFromSpecifiedPath()
        {
            // Arrange 
            const string configFile = "NuGet.Config";
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            var config = @"
<configuration>
    <SectionName>
        <add key='key1' value='value1' />
        <add key='key2' value='value2' />
    </SectionName>
</configuration>";
            TestFilesystemUtility.CreateConfigurationFile(configFile, mockBaseDirectory, config);

            // Act
            var settings = new Settings(mockBaseDirectory);

            // Assert 
            Assert.Equal("value1", settings.GetValue("SectionName", "key1"));
            Assert.Equal("value2", settings.GetValue("SectionName", "key2"));
        }

        [Fact]
        public void CallingGetSettingValuesWithNullSectionWillThrowException()
        {
            // Arrange
            const string configFile = "NuGet.Config";
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            TestFilesystemUtility.CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
            var settings = new Settings(mockBaseDirectory);

            // Act & Assert
            Exception ex = Record.Exception(() => settings.GetSettingValues(null));
            Assert.NotNull(ex);
            var tex = Assert.IsAssignableFrom<ArgumentException>(ex);
        }

        [Fact]
        public void CallingGetValueWithNullSectionWillThrowException()
        {
            // Arrange
            const string configFile = "NuGet.Config";
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            TestFilesystemUtility.CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
            var settings = new Settings(mockBaseDirectory);

            // Act & Assert
            Exception ex = Record.Exception(() => settings.GetValue(null, "SomeKey"));
            Assert.NotNull(ex);
            var tex = Assert.IsAssignableFrom<ArgumentException>(ex);
        }

        [Fact]
        public void CallingGetValueWithNullKeyWillThrowException()
        {
            // Arrange
            const string configFile = "NuGet.Config";
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            TestFilesystemUtility.CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
            var settings = new Settings(mockBaseDirectory);

            // Act & Assert
            Exception ex = Record.Exception(() => settings.GetValue("SomeSection", null));
            Assert.NotNull(ex);
            var tex = Assert.IsAssignableFrom<ArgumentException>(ex);
        }

        [Fact]
        public void CallingCtorWithMalformedConfigThrowsException()
        {
            // Arrange
            const string configFile = "NuGet.Config";
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            TestFilesystemUtility.CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration><sectionName></configuration>");

            // Act & Assert
            var ex = Record.Exception(() => new Settings(mockBaseDirectory));
            Assert.NotNull(ex);
            var tex = Assert.IsAssignableFrom<XmlException>(ex);
        }

        [Fact]
        public void UserSetting_CallingGetValuesWithNonExistantSectionReturnsEmpty()
        {
            // Arrange
            const string configFile = "NuGet.Config";
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            TestFilesystemUtility.CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
            Settings settings = new Settings(mockBaseDirectory);

            // Act
            var result = settings.GetSettingValues("DoesNotExisit");

            // Assert 
            Assert.Empty(result);
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
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

            // Act and Assert
            Exception ex = Record.Exception(() => settings.GetSettingValues("SectionName"));
            Assert.NotNull(ex);
            var tex = Assert.IsAssignableFrom<InvalidDataException>(ex);
            Assert.Equal(String.Format("Unable to parse config file '{0}'.", Path.Combine(mockBaseDirectory, nugetConfigPath)), ex.Message);
        }

        [Fact]
        public void GetValuesThrowsIfSettingsIsMissingKeys()
        {
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
<packageSources>
<add key="""" value=""C:\Temp\Nuget"" />
</packageSources>
<activePackageSource>
<add key=""test2"" value=""C:\Temp\Nuget"" />
</activePackageSource>
</configuration>";
            var nugetConfigPath = "NuGet.Config";
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

            // Act and Assert
            Exception ex = Record.Exception(() => settings.GetSettingValues("packageSources"));
            Assert.NotNull(ex);
            var tex = Assert.IsAssignableFrom<InvalidDataException>(ex);
            Assert.Equal(String.Format("Unable to parse config file '{0}'.", Path.Combine(mockBaseDirectory, nugetConfigPath)), ex.Message);
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
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

            // Act
            var result = settings.GetSettingValues("NotTheSectionName");

            // Arrange 
            Assert.Empty(result);
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
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

            // Act
            var result = settings.GetValue("NotTheSectionName", "key1");

            // Arrange 
            Assert.Null(result);
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
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

            // Act
            var result = settings.GetValue("SectionName", "key3");

            // Assert 
            Assert.Null(result);
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
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

            // Act
            var result = settings.GetSettingValues("SectionName");

            // Assert 
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
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
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

            // Act
            var result1 = settings.GetValue("SectionName", "key1");
            var result2 = settings.GetValue("SectionNameTwo", "key2");

            // Assert 
            Assert.Equal("value1", result1);
            Assert.Equal("value2", result2);
        }

        [Fact]
        public void CallingSetValueWithEmptySectionNameThrowsException()
        {
            // Arrange
            const string configFile = "NuGet.Config";
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            TestFilesystemUtility.CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
            Settings settings = new Settings(mockBaseDirectory);

            // Act & Assert
            Exception ex = Record.Exception(() => settings.SetValue("", "SomeKey", "SomeValue"));
            Assert.NotNull(ex);
            var tex = Assert.IsAssignableFrom<ArgumentException>(ex);
        }

        [Fact]
        public void CallingSetValueWithEmptyKeyThrowsException()
        {
            // Arrange
            const string configFile = "NuGet.Config";
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            TestFilesystemUtility.CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
            Settings settings = new Settings(mockBaseDirectory);

            // Act & Assert
            Exception ex = Record.Exception(() => settings.SetValue("SomeKey", "", "SomeValue"));
            Assert.NotNull(ex);
            var tex = Assert.IsAssignableFrom<ArgumentException>(ex);
        }

        [Fact]
        public void CallingSetValueWillAddSectionIfItDoesNotExist()
        {
            // Arrange
            const string configFile = "NuGet.Config";
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value=""value"" />
  </SectionName>
</configuration>";
            TestFilesystemUtility.CreateConfigurationFile(configFile, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

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

        [Fact]
        public void CallingSetValueWillAddToSectionIfItExist()
        {
            // Arrange
            const string configFile = "NuGet.Config";
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value=""value"" />
  </SectionName>
</configuration>";
            TestFilesystemUtility.CreateConfigurationFile(configFile, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

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

        [Fact]
        public void CallingSetValueWillOverrideValueIfKeyExists()
        {
            // Arrange
            const string configFile = "NuGet.Config";
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value=""value"" />
  </SectionName>
</configuration>";
            TestFilesystemUtility.CreateConfigurationFile(configFile, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

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

        [Fact]
        public void CallingSetValuesWithEmptySectionThrowsException()
        {
            // Arrange 
            var values = new List<SettingValue>() { new SettingValue("key", "value", isMachineWide: false) };
            const string configFile = "NuGet.Config";
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            TestFilesystemUtility.CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
            Settings settings = new Settings(mockBaseDirectory);

            // Act & Assert
            Exception ex = Record.Exception(() => settings.SetValues("", values));
            Assert.NotNull(ex);
            var tex = Assert.IsAssignableFrom<ArgumentException>(ex);
        }

        [Fact]
        public void CallingSetValuesWithNullValuesThrowsException()
        {
            // Arrange 
            const string configFile = "NuGet.Config";
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            TestFilesystemUtility.CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
            Settings settings = new Settings(mockBaseDirectory);

            // Act & Assert
            Exception ex = Record.Exception(() => settings.SetValues("Section", null));
            Assert.NotNull(ex);
            var tex = Assert.IsAssignableFrom<ArgumentNullException>(ex);
        }

        [Fact]
        public void CallingSetValuesWithEmptyKeyThrowsException()
        {
            // Arrange
            const string configFile = "NuGet.Config";
            var values = new[] { new SettingValue("", "value", isMachineWide: false) };
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            TestFilesystemUtility.CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
            Settings settings = new Settings(mockBaseDirectory);

            // Act & Assert
            Exception ex = Record.Exception(() => settings.SetValues("Section", values));
            Assert.NotNull(ex);
            var tex = Assert.IsAssignableFrom<ArgumentException>(ex);
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
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

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
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

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
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

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
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

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
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

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
            Assert.Equal(result.Replace("\r\n", "\n"),
                File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath)).Replace("\r\n", "\n"));
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
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

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
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

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

        [Fact]
        public void CallingDeleteValueWithEmptyKeyThrowsException()
        {
            // Arrange
            const string configFile = "NuGet.Config";
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            TestFilesystemUtility.CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
            Settings settings = new Settings(mockBaseDirectory);

            // Act & Assert
            Exception ex = Record.Exception(() => settings.DeleteValue("SomeSection", ""));
            Assert.NotNull(ex);
            var tex = Assert.IsAssignableFrom<ArgumentException>(ex);
        }

        [Fact]
        public void CallingDeleteValueWithEmptySectionThrowsException()
        {
            // Arrange
            const string configFile = "NuGet.Config";
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            TestFilesystemUtility.CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
            Settings settings = new Settings(mockBaseDirectory);

            // Act & Assert
            Exception ex = Record.Exception(() => settings.DeleteValue("", "SomeKey"));
            Assert.NotNull(ex);
            var tex = Assert.IsAssignableFrom<ArgumentException>(ex);
        }

        [Fact]
        public void CallingDeleteValueWhenSectionNameDoesntExistReturnsFalse()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value="""" />
  </SectionName>
</configuration>";
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

            // Act & Assert
            Assert.False(settings.DeleteValue("SectionDoesNotExists", "SomeKey"));
        }

        [Fact]
        public void CallingDeleteValueWhenKeyDoesntExistThrowsException()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value="""" />
  </SectionName>
</configuration>";
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

            // Act & Assert
            Assert.False(settings.DeleteValue("SectionName", "KeyDoesNotExist"));
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
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

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

        [Fact]
        public void CallingDeleteSectionWithEmptySectionThrowsException()
        {
            // Arrange
            const string configFile = "NuGet.Config";
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            TestFilesystemUtility.CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
            Settings settings = new Settings(mockBaseDirectory);

            // Act & Assert
            Exception ex = Record.Exception(() => settings.DeleteSection(""));
            Assert.NotNull(ex);
            var tex = Assert.IsAssignableFrom<ArgumentException>(ex);
        }

        [Fact]
        public void CallingDeleteSectionWhenSectionNameDoesntExistReturnsFalse()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value="""" />
  </SectionName>
</configuration>";
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

            // Act & Assert
            Assert.False(settings.DeleteSection("SectionDoesNotExists"));
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
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

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

        [Fact]
        public void SettingsUtility_SetEncryptedValue()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value=""value"" />
  </SectionName>
</configuration>";
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

            // Act
            SettingsUtility.SetEncryptedValue(settings, "SectionName", "key", "NewValue");

            // Assert
            var content = File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath));
            Assert.False(content.Contains("NewValue"));
        }

        [Fact]
        public void SettingsUtility_GetEncryptedValue()
        {
            // Arrange
            // Arrange
            const string configFile = "NuGet.Config";
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            TestFilesystemUtility.CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
            Settings settings = new Settings(mockBaseDirectory);
            SettingsUtility.SetEncryptedValue(settings, "SectionName", "key", "value");

            // Act
            var result = SettingsUtility.GetDecryptedValue(settings, "SectionName", "key");

            // Assert
            Assert.Equal("value", result);
        }

        [Fact]
        public void SettingsUtility_GetDecryptedValueWithEmptyValueReturnsEmptyString()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value="""" />
  </SectionName>
</configuration>";
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

            // Act
            var result = SettingsUtility.GetDecryptedValue(settings, "SectionName", "key");

            // Assert
            Assert.Equal(String.Empty, result);
        }

        [Fact]
        public void SettingsUtility_GetDecryptedValueWithNoKeyReturnsNull()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value="""" />
  </SectionName>
</configuration>";
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

            // Act
            var result = SettingsUtility.GetDecryptedValue(settings, "SectionName", "NoKeyByThatName");

            // Assert
            Assert.Null(result);
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
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

            // Act
            var result1 = settings.GetValue("SectionName", "Key1");
            var result2 = settings.GetValue("SectionName", "Key2");

            // Assert
            Assert.Null(result1);
            Assert.Equal("bar", result2);
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
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
            var settings = new Settings(mockBaseDirectory);

            // Act
            var result1 = settings.GetValue("SectionName", "Key1");
            var result2 = settings.GetValue("SectionName", "Key2");

            // Assert
            Assert.Null(result1);
            Assert.Equal("bar", result2);
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
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

            // Act
            var result = settings.GetSettingValues("SectionName");

            // Assert
            AssertEqualCollections(result, new[] { "key3", "value3", "key4", "value4" });
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
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

            // Act
            var result = settings.GetSettingValues("SectionName", isPath: true);

            // Assert
            AssertEqualCollections(
                result,
                new[]
                    {
                        "key1", String.Format(@"{0}\..\value1", mockBaseDirectory),
                        "key2", String.Format(@"{0}\a\b\c", mockBaseDirectory),
                        "key3", String.Format(@"{0}\.\a\b\c", mockBaseDirectory),
                        "key4", @"c:\value2",
                        "key5", @"http://value3",
                        "key6", @"\\a\b\c",
                        "key7", @"\a\b\c"
                    });
        }

        [Fact]
        public void GetValuesMultipleConfFilesAdditive()
        {
            // Arrange
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key3"" value=""value3"" />
    <add key=""key4"" value=""value4"" />
  </SectionName>
</configuration>";
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, @"dir1\dir2"), config);
            config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key1"" value=""value1"" />
    <add key=""key2"" value=""value2"" />
  </SectionName>
</configuration>";
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1"), config);

            var settings = Settings.LoadDefaultSettings(Path.Combine(mockBaseDirectory, @"dir1\dir2"), null, null);

            // Act
            var result = settings.GetSettingValues("SectionName");

            // Assert
            AssertEqualCollections(result, new[] { "key1", "value1", "key2", "value2", "key3", "value3", "key4", "value4" });
        }

        [Fact]
        public void GetValuesMultipleConfFilesClear()
        {
            // Arrange
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();

            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <clear /> <!-- i.e. ignore values from prior conf files -->
    <add key=""key3"" value=""value3"" />
    <add key=""key4"" value=""value4"" />
  </SectionName>
</configuration>";
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, @"dir1\dir2"), config);
            config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key1"" value=""value1"" />
    <add key=""key2"" value=""value2"" />
  </SectionName>
</configuration>";
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1"), config);

            var settings = Settings.LoadDefaultSettings(Path.Combine(mockBaseDirectory, @"dir1\dir2"), null, null);

            // Act
            var result = settings.GetSettingValues("SectionName");

            // Assert
            AssertEqualCollections(result, new[] { "key3", "value3", "key4", "value4" });
        }

        [Fact]
        public void GetSettingValuesMultipleConfFilesClear()
        {
            // Arrange
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();

            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <clear /> <!-- i.e. ignore values from prior conf files -->
    <add key=""key3"" value=""value3"" />
    <add key=""key4"" value=""value4"" />
  </SectionName>
</configuration>";
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, @"dir1\dir2"), config);
            config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key1"" value=""value1"" />
    <add key=""key2"" value=""value2"" />
  </SectionName>
</configuration>";
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1"), config);

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

        [Fact]
        public void GetSettingValues_ReadsAttributeValues_FromElements()
        {
            // Arrange
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();

            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <clear />
    <add key=""key3"" value=""value3"" data-key3=""key3"" />
    <add key=""key4"" value=""value4"" data-key4=""key4"" someAttr=""someAttrValue"" />
  </SectionName>
</configuration>";
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, @"dir1\dir2"), config);
            config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key1"" value=""value1"" />
    <add key=""key2"" value=""value2"" />
  </SectionName>
</configuration>";
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1"), config);

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

        [Fact]
        public void GetSettingValues_IgnoresAttributesFromClearedElements()
        {
            // Arrange
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();

            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <clear />
    <add key=""key3"" value=""value3"" data-key3=""key3"" />
    <add key=""key4"" value=""value4"" data-key4=""key4"" someAttr=""someAttrValue"" />
  </SectionName>
</configuration>";
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, @"dir1\dir2"), config);
            config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key3"" value=""value3"" data-key3=""different-value"" not-inherited-attribute=""not-inherited"" />
  </SectionName>
</configuration>";
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1"), config);

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

        [Fact]
        public void SettingsValuesProvideOriginData()
        {
            // Arrange
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();

            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key1"" value=""value3"" />
    <add key=""key3"" value=""value4"" />
  </SectionName>
</configuration>";
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, @"dir1\dir2"), config);
            config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key1"" value=""value1"" />
    <add key=""key2"" value=""value2"" />
  </SectionName>
</configuration>";
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1"), config);

            // Act
            var settings = Settings.LoadDefaultSettings(Path.Combine(mockBaseDirectory, @"dir1\dir2"), null, null);
            var values = settings.GetSettingValues("SectionName");
            var key1Value = values.Where(s => s.Key.Equals("key1")).OrderByDescending(s => s.Priority).First();
            var key2Value = values.Single(s => s.Key.Equals("key2"));
            var key3Value = values.Single(s => s.Key.Equals("key3"));
            var parentConfig = Path.Combine(mockBaseDirectory, "dir1", "NuGet.Config");
            var childConfig = Path.Combine(mockBaseDirectory, "dir1", "dir2", "NuGet.Config");

            // Assert
            Assert.Equal(childConfig, ((Settings)key1Value.Origin).ConfigFilePath); // key1 was overidden, so it has a new origin!
            Assert.Equal(parentConfig, ((Settings)key2Value.Origin).ConfigFilePath);
            Assert.Equal(childConfig, ((Settings)key3Value.Origin).ConfigFilePath);
        }

        [Fact]
        public void GetSingleValuesMultipleConfFiles()
        {
            // Arrange
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();

            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key3"" value=""value3"" />
    <add key=""key4"" value=""value4"" />
  </SectionName>
</configuration>";
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, @"dir1\dir2"), config);
            config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key1"" value=""value1"" />
    <add key=""key2"" value=""value2"" />
  </SectionName>
</configuration>";
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1"), config);

            var settings = Settings.LoadDefaultSettings(Path.Combine(mockBaseDirectory, @"dir1\dir2"), null, null);

            // Assert
            Assert.Equal("value4", settings.GetValue("SectionName", "key4"));
            Assert.Equal("value3", settings.GetValue("SectionName", "key3"));
            Assert.Equal("value2", settings.GetValue("SectionName", "key2"));
            Assert.Equal("value1", settings.GetValue("SectionName", "key1"));
        }

        [Fact]
        public void GetSingleValuesMultipleConfFilesWithDupes()
        {
            // Arrange
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();

            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key1"" value=""LastOneWins1"" />
    <add key=""key2"" value=""LastOneWins2"" />
  </SectionName>
</configuration>";
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, @"dir1\dir2"), config);
            config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key1"" value=""value1"" />
    <add key=""key2"" value=""value2"" />
  </SectionName>
</configuration>";
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1"), config);

            var settings = Settings.LoadDefaultSettings(Path.Combine(mockBaseDirectory, @"dir1\dir2"), null, null);

            // Assert
            Assert.Equal("LastOneWins2", settings.GetValue("SectionName", "key2"));
            Assert.Equal("LastOneWins1", settings.GetValue("SectionName", "key1"));
        }

        [Fact]
        public void GetSingleValuesMultipleConfFilesClear()
        {
            // Arrange
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();

            var nugetConfigPath = "NuGet.Config";
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <clear /> <!-- i.e. ignore values from prior conf files -->
    <add key=""key2"" value=""value2"" />
  </SectionName>
</configuration>";
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, @"dir1\dir2"), config);
            config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key1"" value=""value1"" />    
  </SectionName>
</configuration>";
            TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1"), config);

            var settings = Settings.LoadDefaultSettings(Path.Combine(mockBaseDirectory, @"dir1\dir2"), null, null);

            // Assert
            Assert.Equal("value2", settings.GetValue("SectionName", "key2"));
            Assert.Equal(null, settings.GetValue("SectionName", "key1"));
        }

        [Fact]
        public void GetValueReturnsPathRelativeToConfigWhenPathIsNotRooted()
        {
            // Arrange
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""path-key"" value=""foo\bar"" />
  </SectionName>
</configuration>";
            TestFilesystemUtility.CreateConfigurationFile("nuget.config", mockBaseDirectory, config);

            var settings = new Settings(mockBaseDirectory, "nuget.config");

            // Act
            string result = settings.GetValue("SectionName", "path-key", isPath: true);

            // Assert
            Assert.Equal(String.Format(@"{0}\foo\bar", mockBaseDirectory), result);
        }

        [Fact]
        public void GetValuesWithUserSpecifiedDefaultConfigFile()
        {
            // Arrange
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();

            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key3"" value=""value3"" />
    <add key=""key4"" value=""value4"" />
  </SectionName>
</configuration>";
            TestFilesystemUtility.CreateConfigurationFile("NuGet.Config", Path.Combine(mockBaseDirectory, "dir1"), config);
            config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key1"" value=""value1"" />
    <add key=""key2"" value=""value2"" />
  </SectionName>
</configuration>";
            TestFilesystemUtility.CreateConfigurationFile("UserDefinedConfigFile.confg", Path.Combine(mockBaseDirectory, @"dir1\dir2"), config);

            var settings = Settings.LoadDefaultSettings(
                Path.Combine(mockBaseDirectory, @"dir1\dir2"),
                "UserDefinedConfigFile.confg",
                null);

            // Act
            var result = settings.GetSettingValues("SectionName");

            // Assert
            AssertEqualCollections(result, new[] { "key1", "value1", "key2", "value2", "key3", "value3", "key4", "value4" });
        }

        [Theory]
        [InlineData(@"z:\foo")]
        [InlineData(@"x:\foo\bar\qux")]
        [InlineData(@"\\share\folder\subfolder")]
        public void GetValueReturnsPathWhenPathIsRooted(string value)
        {
            // Arrange
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            var config = String.Format(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""path-key"" value=""{0}"" />
  </SectionName>
</configuration>", value);
            TestFilesystemUtility.CreateConfigurationFile("nuget.config", mockBaseDirectory, config);
            var settings = new Settings(mockBaseDirectory, "nuget.config");

            // Act
            string result = settings.GetValue("SectionName", "path-key", isPath: true);

            // Assert
            Assert.Equal(value, result);
        }

        [Fact]
        public void GetValueReturnsPathRelativeToRootOfConfig()
        {
            // Arrange
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""path-key"" value=""\Blah"" />
  </SectionName>
</configuration>";
            TestFilesystemUtility.CreateConfigurationFile("nuget.config", mockBaseDirectory, config);
            var settings = new Settings(mockBaseDirectory, "nuget.config");

            // Act
            string result = settings.GetValue("SectionName", "path-key", isPath: true);

            // Assert
            Assert.Equal(String.Format(@"{0}Blah", Path.GetPathRoot(mockBaseDirectory)), result);
        }

        [Fact]
        public void GetValueResolvesRelativePaths()
        {
            // Arrange
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""path-key"" value=""..\Blah"" />
  </SectionName>
</configuration>";
            TestFilesystemUtility.CreateConfigurationFile("nuget.config", mockBaseDirectory, config);
            var settings = new Settings(mockBaseDirectory, "nuget.config");

            // Act
            string result = settings.GetValue("SectionName", "path-key", isPath: true);

            // Assert
            Assert.Equal(String.Format(@"{0}\..\Blah", mockBaseDirectory), result);
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

            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();

            TestFilesystemUtility.CreateConfigurationFile("a1.config", Path.Combine(mockBaseDirectory, @"NuGet"), fileContent);
            TestFilesystemUtility.CreateConfigurationFile("a1.config", Path.Combine(mockBaseDirectory, @"NuGet\Config"), fileContent);
            TestFilesystemUtility.CreateConfigurationFile("a2.config", Path.Combine(mockBaseDirectory, @"NuGet\Config"), fileContent);
            TestFilesystemUtility.CreateConfigurationFile("a3.xconfig", Path.Combine(mockBaseDirectory, @"NuGet\Config"), fileContent);
            TestFilesystemUtility.CreateConfigurationFile("a1.config", Path.Combine(mockBaseDirectory, @"NuGet\Config\IDE"), fileContent);
            TestFilesystemUtility.CreateConfigurationFile("a2.config", Path.Combine(mockBaseDirectory, @"NuGet\Config\IDE"), fileContent);
            TestFilesystemUtility.CreateConfigurationFile("a3.xconfig", Path.Combine(mockBaseDirectory, @"NuGet\Config\IDE"), fileContent);
            TestFilesystemUtility.CreateConfigurationFile("a1.config", Path.Combine(mockBaseDirectory, @"NuGet\Config\IDE\Version"), fileContent);
            TestFilesystemUtility.CreateConfigurationFile("a2.config", Path.Combine(mockBaseDirectory, @"NuGet\Config\IDE\Version"), fileContent);
            TestFilesystemUtility.CreateConfigurationFile("a3.xconfig", Path.Combine(mockBaseDirectory, @"NuGet\Config\IDE\Version"), fileContent);
            TestFilesystemUtility.CreateConfigurationFile("a1.config", Path.Combine(mockBaseDirectory, @"NuGet\Config\IDE\Version\SKU"), fileContent);
            TestFilesystemUtility.CreateConfigurationFile("a2.config", Path.Combine(mockBaseDirectory, @"NuGet\Config\IDE\Version\SKU"), fileContent);
            TestFilesystemUtility.CreateConfigurationFile("a3.xconfig", Path.Combine(mockBaseDirectory, @"NuGet\Config\IDE\Version\SKU"), fileContent);
            TestFilesystemUtility.CreateConfigurationFile("a1.config", Path.Combine(mockBaseDirectory, @"NuGet\Config\IDE\Version\SKU\Dir"), fileContent);

            // Act
            var settings = Settings.LoadMachineWideSettings(
                mockBaseDirectory, "IDE", "Version", "SKU", "TestDir");

            // Assert
            var files = settings.Select(s => s.ConfigFilePath).ToArray();
            Assert.Equal(
                files,
                new string[]
                    {
                        String.Format(@"{0}\NuGet\Config\IDE\Version\SKU\a1.config", mockBaseDirectory),
                        String.Format(@"{0}\NuGet\Config\IDE\Version\SKU\a2.config", mockBaseDirectory),
                        String.Format(@"{0}\NuGet\Config\IDE\Version\a1.config", mockBaseDirectory),
                        String.Format(@"{0}\NuGet\Config\IDE\Version\a2.config", mockBaseDirectory),
                        String.Format(@"{0}\NuGet\Config\IDE\a1.config", mockBaseDirectory),
                        String.Format(@"{0}\NuGet\Config\IDE\a2.config", mockBaseDirectory),
                        String.Format(@"{0}\NuGet\Config\a1.config", mockBaseDirectory),
                        String.Format(@"{0}\NuGet\Config\a2.config", mockBaseDirectory)
                    });
        }

        // Tests method GetValue() with machine wide settings.
        [Fact]
        public void GetValueWithMachineWideSettings()
        {
            // Arrange            
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
            var fileContent1 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key1"" value=""value1"" />
    <add key=""key2"" value=""value2"" />
  </SectionName>
</configuration>";
            TestFilesystemUtility.CreateConfigurationFile("a1.config", Path.Combine(mockBaseDirectory, @"NuGet\Config"), fileContent1);
            var fileContent2 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key2"" value=""value3"" />
    <add key=""key3"" value=""value4"" />
  </SectionName>
</configuration>";
            TestFilesystemUtility.CreateConfigurationFile("a1.config", Path.Combine(mockBaseDirectory, @"NuGet\Config\IDE"), fileContent2);
            var fileContent3 = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key3"" value=""user"" />
  </SectionName>
</configuration>";
            TestFilesystemUtility.CreateConfigurationFile("user.config", mockBaseDirectory, fileContent3);

            var m = new Mock<IMachineWideSettings>();
            m.SetupGet(obj => obj.Settings).Returns(
                Settings.LoadMachineWideSettings(mockBaseDirectory, "IDE", "Version", "SKU"));

            // Act
            var settings = Settings.LoadDefaultSettings(
                mockBaseDirectory,
                "user.config",
                m.Object);

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

        // Tests method SetValue() with machine wide settings.
        // Verifies that the user specific config file is modified, while machine
        // wide settings files are not touched.
        [Fact]
        public void SetValueWithMachineWideSettings()
        {
            // Arrange
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
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

            TestFilesystemUtility.CreateConfigurationFile("a1.config", Path.Combine(mockBaseDirectory, @"NuGet\Config"), a1Config);
            TestFilesystemUtility.CreateConfigurationFile("a2.config", Path.Combine(mockBaseDirectory, @"NuGet\Config\IDE"), a2Config);
            TestFilesystemUtility.CreateConfigurationFile("user.config", mockBaseDirectory, userConfig);

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

            text = File.ReadAllText(Path.Combine(mockBaseDirectory, @"NuGet\Config\a1.config"));
            Assert.Equal(a1Config, text.Replace("\r\n", "\n"));

            text = File.ReadAllText(Path.Combine(mockBaseDirectory, @"NuGet\Config\IDE\a2.config"));
            Assert.Equal(a2Config, text.Replace("\r\n", "\n"));
        }

        // Tests that when configFileName is not null, the specified
        // file must exist.
        [Fact]
        public void UserSpecifiedConfigFileMustExist()
        {
            // Arrange
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();

            // Act and assert
            Exception ex = Record.Exception(() => Settings.LoadDefaultSettings(
                mockBaseDirectory,
                configFileName: "user.config",
                machineWideSettings: null));
            Assert.NotNull(ex);
            var tex = Assert.IsAssignableFrom<InvalidOperationException>(ex);
            Assert.Equal(String.Format(@"File '{0}' does not exist.", Path.Combine(mockBaseDirectory, "user.config")), ex.Message);
        }

        // Tests the scenario where there are two user settings, both created
        // with the same machine wide settings.
        [Fact]
        public void GetValueFromTwoUserSettingsWithMachineWideSettings()
        {
            // Arrange            
            var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder();
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
            TestFilesystemUtility.CreateConfigurationFile("a1.config", Path.Combine(mockBaseDirectory, @"NuGet\Config"), FileContent1);
            TestFilesystemUtility.CreateConfigurationFile("user1.config", mockBaseDirectory, FileContent2);
            TestFilesystemUtility.CreateConfigurationFile("user2.config", mockBaseDirectory, FileContent3);

            var m = new Mock<IMachineWideSettings>();
            m.SetupGet(obj => obj.Settings).Returns(
                Settings.LoadMachineWideSettings(mockBaseDirectory, "IDE", "Version", "SKU"));

            // Act
            var settings1 = Settings.LoadDefaultSettings(
                mockBaseDirectory,
                "user1.config",
                m.Object);
            var settings2 = Settings.LoadDefaultSettings(
                mockBaseDirectory,
                "user2.config",
                m.Object);

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

        [Fact]
        public void GetGlobalPackagesFolder_FromNuGetConfig()
        {
            // Arrange
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
<config>
<add key=""globalPackagesFolder"" value=""C:\Temp\NuGet"" />
</config>
</configuration>";

            var nugetConfigPath = "NuGet.config";
            using (var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder())
            {
                TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                Settings settings = new Settings(mockBaseDirectory);

                // Act
                var globalPackagesFolderPath = SettingsUtility.GetGlobalPackagesFolder(settings);

                // Assert
                Assert.Equal(@"C:\Temp\NuGet", globalPackagesFolderPath);
            }
        }

        [Fact]
        public void GetGlobalPackagesFolder_FromNuGetConfig_RelativePath()
        {
            // Arrange
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
<config>
<add key=""globalPackagesFolder"" value=""..\..\NuGetPackages"" />
</config>
</configuration>";

            var nugetConfigPath = "NuGet.config";
            using (var mockBaseDirectory = TestFilesystemUtility.CreateRandomTestFolder())
            {
                TestFilesystemUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                Settings settings = new Settings(mockBaseDirectory);

                // Act
                var globalPackagesFolderPath = SettingsUtility.GetGlobalPackagesFolder(settings);

                // Assert
                Assert.Equal(@"..\..\NuGetPackages", globalPackagesFolderPath);
            }
        }

        [Fact]
        public void GetGlobalPackagesFolder_Default()
        {
            // Arrange
#if !DNXCORE50
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
#else
            var userProfile = Environment.GetEnvironmentVariable("UserProfile");
#endif
            var expectedPath = Path.Combine(userProfile, SettingsUtility.DefaultGlobalPackagesFolderPath);

            // Act
            var globalPackagesFolderPath = SettingsUtility.GetGlobalPackagesFolder(new NullSettings());

            // Assert
            Assert.Equal(expectedPath, globalPackagesFolderPath);
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
    }
}
