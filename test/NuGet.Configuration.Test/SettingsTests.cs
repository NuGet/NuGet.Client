using System;
using System.IO;
using System.Text;
using Xunit;
using Xunit.Extensions;
using System.Linq;
using System.Collections.Generic;

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
            Exception ex = Record.Exception(() => new Settings(null));
            Assert.NotNull(ex);
            ArgumentException tex = Assert.IsAssignableFrom<ArgumentException>(ex);
        }

        [Fact]
        public void WillGetConfigurationFromSpecifiedPath()
        {
            // Arrange 
            const string configFile = "NuGet.Config";
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            string config = @"
<configuration>
    <SectionName>
        <add key='key1' value='value1' />
        <add key='key2' value='value2' />
    </SectionName>
</configuration>";
            CreateConfigurationFile(configFile, mockBaseDirectory, config);

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
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
            var settings = new Settings(mockBaseDirectory);

            // Act & Assert
            Exception ex = Record.Exception(() => settings.GetSettingValues(null));
            Assert.NotNull(ex);
            ArgumentException tex = Assert.IsAssignableFrom<ArgumentException>(ex);
        }

        [Fact]
        public void CallingGetValueWithNullSectionWillThrowException()
        {
            // Arrange
            const string configFile = "NuGet.Config";
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
            var settings = new Settings(mockBaseDirectory);

            // Act & Assert
            Exception ex = Record.Exception(() => settings.GetValue(null, "SomeKey"));
            Assert.NotNull(ex);
            ArgumentException tex = Assert.IsAssignableFrom<ArgumentException>(ex);
        }

        [Fact]
        public void CallingGetValueWithNullKeyWillThrowException()
        {
            // Arrange
            const string configFile = "NuGet.Config";
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
            var settings = new Settings(mockBaseDirectory);

            // Act & Assert
            Exception ex = Record.Exception(() => settings.GetValue("SomeSection", null));
            Assert.NotNull(ex);
            ArgumentException tex = Assert.IsAssignableFrom<ArgumentException>(ex);
        }

        [Fact]
        public void CallingCtorWithMalformedConfigThrowsException()
        {
            // Arrange
            const string configFile = "NuGet.Config";
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration><sectionName></configuration>");

            // Act & Assert
            Exception ex = Record.Exception(() => new Settings(mockBaseDirectory));
            Assert.NotNull(ex);
            System.Xml.XmlException tex = Assert.IsAssignableFrom<System.Xml.XmlException>(ex);

        }

        [Fact]
        public void UserSetting_CallingGetValuesWithNonExistantSectionReturnsEmpty()
        {
            // Arrange
            const string configFile = "NuGet.Config";
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
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
            var nugetConfigPath = "NuGet.config";
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            CreateConfigurationFile(nugetConfigPath, mockBaseDirectory,config);
            Settings settings = new Settings(mockBaseDirectory);
            
            // Act and Assert
            Exception ex = Record.Exception(() => settings.GetSettingValues("SectionName"));
            Assert.NotNull(ex);
            InvalidDataException tex = Assert.IsAssignableFrom<InvalidDataException>(ex);
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
            var nugetConfigPath = "NuGet.config";
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

            // Act and Assert
            Exception ex = Record.Exception(() => settings.GetSettingValues("packageSources"));
            Assert.NotNull(ex);
            InvalidDataException tex = Assert.IsAssignableFrom<InvalidDataException>(ex);
            Assert.Equal(String.Format("Unable to parse config file '{0}'.", Path.Combine(mockBaseDirectory, nugetConfigPath)), ex.Message);
        }

        [Fact]
        public void CallingGetValuesWithoutSectionReturnsEmptyList()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            string config = @"
<configuration>
    <SectionName>
        <add key='key1' value='value1' />
        <add key='key2' value='value2' />
    </SectionName>
</configuration>";
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
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
            string config = @"
<configuration>
    <SectionName>
        <add key='key1' value='value1' />
        <add key='key2' value='value2' />
    </SectionName>
</configuration>";
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
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
            string config = @"
<configuration>
    <SectionName>
        <add key='key1' value='value1' />
        <add key='key2' value='value2' />
    </SectionName>
</configuration>";
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
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
            string config = @"
<configuration>
    <SectionName>
        <add key='key1' value='value1' />
        <add key='key2' value='value2' />
    </SectionName>
</configuration>";
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
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
            string config = @"
<configuration>
    <SectionName>
        <add key='key1' value='value1' />
    </SectionName>
    <SectionNameTwo>
        <add key='key2' value='value2' />
    </SectionNameTwo>
</configuration>";
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
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
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
            Settings settings = new Settings(mockBaseDirectory);

            // Act & Assert
            Exception ex = Record.Exception(() => settings.SetValue("", "SomeKey", "SomeValue"));
            Assert.NotNull(ex);
            ArgumentException tex = Assert.IsAssignableFrom<ArgumentException>(ex);
        }

        [Fact]
        public void CallingSetValueWithEmptyKeyThrowsException()
        {
            // Arrange
            const string configFile = "NuGet.Config";
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
            Settings settings = new Settings(mockBaseDirectory);

            // Act & Assert
            Exception ex = Record.Exception(() => settings.SetValue("SomeKey", "", "SomeValue"));
            Assert.NotNull(ex);
            ArgumentException tex = Assert.IsAssignableFrom<ArgumentException>(ex);
        }

        [Fact]
        public void CallingSetValueWillAddSectionIfItDoesNotExist()
        {
            // Arrange
            const string configFile = "NuGet.Config";
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            string config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value=""value"" />
  </SectionName>
</configuration>";
            CreateConfigurationFile(configFile, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

            // Act
            settings.SetValue("NewSectionName", "key", "value");

            // Assert
            string result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value=""value"" />
  </SectionName>
  <NewSectionName>
    <add key=""key"" value=""value"" />
  </NewSectionName>
</configuration>";
            Assert.Equal(RemovedLineEndings(result), ReadConfigurationFile(Path.Combine(mockBaseDirectory, configFile)));
        }

        [Fact]
        public void CallingSetValueWillAddToSectionIfItExist()
        {
            // Arrange
            const string configFile = "NuGet.Config";
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            string config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value=""value"" />
  </SectionName>
</configuration>";
            CreateConfigurationFile(configFile, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

            // Act
            settings.SetValue("SectionName", "keyTwo", "valueTwo");

            // Assert
            string result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value=""value"" />
    <add key=""keyTwo"" value=""valueTwo"" />
  </SectionName>
</configuration>";
            Assert.Equal(RemovedLineEndings(result), ReadConfigurationFile(Path.Combine(mockBaseDirectory, configFile)));
        }

        [Fact]
        public void CallingSetValueWillOverrideValueIfKeyExists()
        {
            // Arrange
            const string configFile = "NuGet.Config";
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            string config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value=""value"" />
  </SectionName>
</configuration>";
            CreateConfigurationFile(configFile, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

            // Act
            settings.SetValue("SectionName", "key", "NewValue");

            // Assert
            string result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value=""NewValue"" />
  </SectionName>
</configuration>";
            Assert.Equal(RemovedLineEndings(result), ReadConfigurationFile(Path.Combine(mockBaseDirectory, configFile)));
        }

        [Fact]
        public void CallingSetValuesWithEmptySectionThrowsException()
        {
            // Arrange 
            var values = new List<KeyValuePair<string, string>>() { new KeyValuePair<string, string>("key", "value") };
            const string configFile = "NuGet.Config";
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
            Settings settings = new Settings(mockBaseDirectory);

            // Act & Assert
            Exception ex = Record.Exception(() => settings.SetValues("", values));
            Assert.NotNull(ex);
            ArgumentException tex = Assert.IsAssignableFrom<ArgumentException>(ex);
        }

        [Fact]
        public void CallingSetValuesWithNullValuesThrowsException()
        {
            // Arrange 
            const string configFile = "NuGet.Config";
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
            Settings settings = new Settings(mockBaseDirectory);

            // Act & Assert
            Exception ex = Record.Exception(() => settings.SetValues("Section", null));
            Assert.NotNull(ex);
            ArgumentNullException tex = Assert.IsAssignableFrom<ArgumentNullException>(ex);
        }

        [Fact]
        public void CallingSetValuesWithEmptyKeyThrowsException()
        {
            // Arrange
            const string configFile = "NuGet.Config";
            var values = new List<KeyValuePair<string, string>>() { new KeyValuePair<string, string>("", "value") };
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
            Settings settings = new Settings(mockBaseDirectory);

            // Act & Assert
            Exception ex = Record.Exception(() => settings.SetValues("Section", values));
            Assert.NotNull(ex);
            ArgumentException tex = Assert.IsAssignableFrom<ArgumentException>(ex);
        }

        [Fact]
        public void CallingSetValuseWillAddSectionIfItDoesNotExist()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            string config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value=""value"" />
  </SectionName>
</configuration>";
            var values = new List<KeyValuePair<string, string>>() { new KeyValuePair<string, string>("key", "value") };
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

            // Act
            settings.SetValues("NewSectionName", values);

            // Assert
            string result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value=""value"" />
  </SectionName>
  <NewSectionName>
    <add key=""key"" value=""value"" />
  </NewSectionName>
</configuration>";
            Assert.Equal(RemovedLineEndings(result), ReadConfigurationFile(Path.Combine(mockBaseDirectory, nugetConfigPath)));
        }

        [Fact]
        public void CallingSetValuesWillAddToSectionIfItExist()
        {
            // Arrange 
            var nugetConfigPath = "NuGet.Config";
            string config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value=""value"" />
  </SectionName>
</configuration>";
            var values = new List<KeyValuePair<string, string>>() { new KeyValuePair<string, string>("keyTwo", "valueTwo") };
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

            // Act
            settings.SetValues("SectionName", values);

            // Assert
            string result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value=""value"" />
    <add key=""keyTwo"" value=""valueTwo"" />
  </SectionName>
</configuration>";
            Assert.Equal(RemovedLineEndings(result), ReadConfigurationFile(Path.Combine(mockBaseDirectory, nugetConfigPath)));
        }

        [Fact]
        public void CallingSetValuesWillOverrideValueIfKeyExists()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";

            string config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value=""value"" />
  </SectionName>
</configuration>";
            var values = new List<KeyValuePair<string, string>>() { new KeyValuePair<string, string>("key", "NewValue") };
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

            // Act
            settings.SetValues("SectionName", values);

            // Assert
            string result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value=""NewValue"" />
  </SectionName>
</configuration>";
            Assert.Equal(RemovedLineEndings(result), ReadConfigurationFile(Path.Combine(mockBaseDirectory, nugetConfigPath)));
        }

        [Fact]
        public void CallingSetValuesWilladdValuesInOrder()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";

            string config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value=""Value"" />
  </SectionName>
</configuration>";
            var values = new List<KeyValuePair<string, string>>() { new KeyValuePair<string, string>("key1", "Value1"),
                                                                    new KeyValuePair<string, string>("key2", "Value2") };
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

            // Act
            settings.SetValues("SectionName", values);

            // Assert
            string result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value=""Value"" />
    <add key=""key1"" value=""Value1"" />
    <add key=""key2"" value=""Value2"" />
  </SectionName>
</configuration>";
            Assert.Equal(RemovedLineEndings(result), ReadConfigurationFile(Path.Combine(mockBaseDirectory, nugetConfigPath)));
        }

        [Fact]
        public void CallingSetNestedValuesAddsItemsInNestedElement()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            string config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>";
            var values = new[] { new KeyValuePair<string, string>("key1", "Value1"),
                                  new KeyValuePair<string, string>("key2", "Value2") };
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

            // Act
            settings.SetNestedValues("SectionName", "MyKey", values);

            // Assert
            string result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <MyKey>
      <add key=""key1"" value=""Value1"" />
      <add key=""key2"" value=""Value2"" />
    </MyKey>
  </SectionName>
</configuration>";
            Assert.Equal(RemovedLineEndings(result), ReadConfigurationFile(Path.Combine(mockBaseDirectory, nugetConfigPath)));
        }

        [Fact]
        public void CallingSetNestedValuesPreservesOtherKeys()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            string config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <MyKey>
      <add key=""key1"" value=""Value1"" />
      <add key=""key2"" value=""Value2"" />
    </MyKey>
  </SectionName>
</configuration>";
            var values = new[] { new KeyValuePair<string, string>("key3", "Value3"),
                                  new KeyValuePair<string, string>("key4", "Value4") };
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

            // Act
            settings.SetNestedValues("SectionName", "MyKey2", values);

            // Assert
            string result = @"<?xml version=""1.0"" encoding=""utf-8""?>
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
            Assert.Equal(RemovedLineEndings(result), ReadConfigurationFile(Path.Combine(mockBaseDirectory, nugetConfigPath)));
        }

        [Fact]
        public void CallingSetNestedAppendsValuesToExistingKeys()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            string config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <MyKey>
      <add key=""key1"" value=""Value1"" />
      <add key=""key2"" value=""Value2"" />
    </MyKey>
  </SectionName>
</configuration>";
            var values = new[] { new KeyValuePair<string, string>("key3", "Value3"),
                                  new KeyValuePair<string, string>("key4", "Value4") };
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

            // Act
            settings.SetNestedValues("SectionName", "MyKey", values);

            // Assert
            string result = @"<?xml version=""1.0"" encoding=""utf-8""?>
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
            Assert.Equal(RemovedLineEndings(result), ReadConfigurationFile(Path.Combine(mockBaseDirectory, nugetConfigPath)));
        }

        [Fact]
        public void CallingDeleteValueWithEmptyKeyThrowsException()
        {
            // Arrange
            const string configFile = "NuGet.Config";
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
            Settings settings = new Settings(mockBaseDirectory);

            // Act & Assert
            Exception ex = Record.Exception(() => settings.DeleteValue("SomeSection", ""));
            Assert.NotNull(ex);
            ArgumentException tex = Assert.IsAssignableFrom<ArgumentException>(ex);
        }

        [Fact]
        public void CallingDeleteValueWithEmptySectionThrowsException()
        {
            // Arrange
            const string configFile = "NuGet.Config";
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
            Settings settings = new Settings(mockBaseDirectory);
   
            // Act & Assert
            Exception ex = Record.Exception(() => settings.DeleteValue("", "SomeKey"));
            Assert.NotNull(ex);
            ArgumentException tex = Assert.IsAssignableFrom<ArgumentException>(ex);
        }

        [Fact]
        public void CallingDeleteValueWhenSectionNameDoesntExistReturnsFalse()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            string config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value="""" />
  </SectionName>
</configuration>";
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            CreateConfigurationFile(nugetConfigPath, mockBaseDirectory,config);
            Settings settings = new Settings(mockBaseDirectory);

            // Act & Assert
            Assert.False(settings.DeleteValue("SectionDoesNotExists", "SomeKey"));
        }

        [Fact]
        public void CallingDeleteValueWhenKeyDoesntExistThrowsException()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            string config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value="""" />
  </SectionName>
</configuration>";
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

            // Act & Assert
            Assert.False(settings.DeleteValue("SectionName", "KeyDoesNotExist"));
        }

        [Fact]
        public void CallingDeleteValueWithValidSectionAndKeyDeletesTheEntryAndReturnsTrue()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            string config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""DeleteMe"" value=""value"" />
    <add key=""keyNotToDelete"" value=""value"" />
  </SectionName>
  <SectionName2>
    <add key=""key"" value=""value"" />
  </SectionName2>
</configuration>";
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

            // Act & Assert
            Assert.True(settings.DeleteValue("SectionName", "DeleteMe"));
            string result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""keyNotToDelete"" value=""value"" />
  </SectionName>
  <SectionName2>
    <add key=""key"" value=""value"" />
  </SectionName2>
</configuration>";
            Assert.Equal(RemovedLineEndings(result), ReadConfigurationFile(Path.Combine(mockBaseDirectory, nugetConfigPath)));
        }

        [Fact]
        public void CallingDeleteSectionWithEmptySectionThrowsException()
        {
            // Arrange
            const string configFile = "NuGet.Config";
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
            Settings settings = new Settings(mockBaseDirectory);

            // Act & Assert
            Exception ex = Record.Exception(() => settings.DeleteSection(""));
            Assert.NotNull(ex);
            ArgumentException tex = Assert.IsAssignableFrom<ArgumentException>(ex);
        }

        [Fact]
        public void CallingDeleteSectionWhenSectionNameDoesntExistReturnsFalse()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            string config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value="""" />
  </SectionName>
</configuration>";
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

            // Act & Assert
            Assert.False(settings.DeleteSection("SectionDoesNotExists"));
        }

        [Fact]
        public void CallingDeleteSectionWithValidSectionDeletesTheSectionAndReturnsTrue()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            string config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""DeleteMe"" value=""value"" />
    <add key=""keyNotToDelete"" value=""value"" />
  </SectionName>
  <SectionName2>
    <add key=""key"" value=""value"" />
  </SectionName2>
</configuration>";
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

            // Act & Assert
            Assert.True(settings.DeleteSection("SectionName"));
            string result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName2>
    <add key=""key"" value=""value"" />
  </SectionName2>
</configuration>";
            Assert.Equal(RemovedLineEndings(result), ReadConfigurationFile(Path.Combine(mockBaseDirectory, nugetConfigPath)));
        }

        [Fact]
        public void SettingsUtility_SetEncryptedValue()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            string config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value=""value"" />
  </SectionName>
</configuration>";
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

            // Act
            SettingsUtility.SetEncryptedValue(settings, "SectionName", "key", "NewValue");
            
            // Assert
            var content = ReadConfigurationFile(Path.Combine(mockBaseDirectory, nugetConfigPath));
            Assert.False(content.Contains("NewValue"));
        }

        [Fact]
        public void SettingsUtility_GetEncryptedValue()
        {
            // Arrange
            // Arrange
            const string configFile = "NuGet.Config";
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
            Settings settings = new Settings(mockBaseDirectory);
            SettingsUtility.SetEncryptedValue(settings, "SectionName", "key", "value");

            // Act
            var result = SettingsUtility.GetDecryptedValue(settings,"SectionName", "key");

            // Assert
            Assert.Equal("value", result);
        }

        [Fact]
        public void SettingsUtility_GetDecryptedValueWithEmptyValueReturnsEmptyString()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            string config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value="""" />
  </SectionName>
</configuration>";
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
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
            string config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key"" value="""" />
  </SectionName>
</configuration>";
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

            // Act
            var result = SettingsUtility.GetDecryptedValue(settings,"SectionName", "NoKeyByThatName");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetValueIgnoresClearedValues()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            string config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key1"" value=""foo"" />
    <clear />
    <add key=""key2"" value=""bar"" />
  </SectionName>
</configuration>";
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

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
            string config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key1"" value=""value1"" />
    <add key=""key2"" value=""value2"" />
    <clear />
    <add key=""key3"" value=""value3"" />
    <add key=""key4"" value=""value4"" />
  </SectionName>
</configuration>";
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
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
            string config = @"<?xml version=""1.0"" encoding=""utf-8""?>
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
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
            Settings settings = new Settings(mockBaseDirectory);

            // Act
            var result = settings.GetSettingValues("SectionName", isPath: true);

            // Assert
            AssertEqualCollections(
                result,
                new[] {
                    "key1", String.Format(@"{0}\..\value1",mockBaseDirectory),
                    "key2", String.Format(@"{0}\a\b\c",mockBaseDirectory),
                    "key3", String.Format(@"{0}\.\a\b\c",mockBaseDirectory),

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
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            Directory.CreateDirectory(Path.Combine(mockBaseDirectory, "dir1"));
            Directory.CreateDirectory(Path.Combine(mockBaseDirectory, @"dir1\dir2"));
            var nugetConfigPath = "NuGet.Config";
            string config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key3"" value=""value3"" />
    <add key=""key4"" value=""value4"" />
  </SectionName>
</configuration>";
            CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, @"dir1\dir2"), config);
            config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key1"" value=""value1"" />
    <add key=""key2"" value=""value2"" />
  </SectionName>
</configuration>";
            CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1"), config);

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
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            Directory.CreateDirectory(Path.Combine(mockBaseDirectory, "dir1"));
            Directory.CreateDirectory(Path.Combine(mockBaseDirectory, @"dir1\dir2"));
            var nugetConfigPath = "NuGet.Config";
            string config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <clear /> <!-- i.e. ignore values from prior conf files -->
    <add key=""key3"" value=""value3"" />
    <add key=""key4"" value=""value4"" />
  </SectionName>
</configuration>";
            CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, @"dir1\dir2"), config);
            config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key1"" value=""value1"" />
    <add key=""key2"" value=""value2"" />
  </SectionName>
</configuration>";
            CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1"), config);

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
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            Directory.CreateDirectory(Path.Combine(mockBaseDirectory, "dir1"));
            Directory.CreateDirectory(Path.Combine(mockBaseDirectory, @"dir1\dir2"));
            var nugetConfigPath = "NuGet.Config";
            string config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <clear /> <!-- i.e. ignore values from prior conf files -->
    <add key=""key3"" value=""value3"" />
    <add key=""key4"" value=""value4"" />
  </SectionName>
</configuration>";
            CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, @"dir1\dir2"), config);
            config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key1"" value=""value1"" />
    <add key=""key2"" value=""value2"" />
  </SectionName>
</configuration>";
            CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1"), config);

            var settings = Settings.LoadDefaultSettings(Path.Combine(mockBaseDirectory, @"dir1\dir2"), null, null);

            // Act
            var result = settings.GetSettingValues("SectionName");

            // Assert
            Assert.Equal<SettingValue>(
                new[] {
                    new SettingValue("key3", "value3", isMachineWide: false, priority: 0),
                    new SettingValue("key4", "value4", isMachineWide: false, priority: 0)
                },
                result);
        }

        [Fact]
        public void GetSingleValuesMultipleConfFiles()
        {
            // Arrange
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            Directory.CreateDirectory(Path.Combine(mockBaseDirectory, "dir1"));
            Directory.CreateDirectory(Path.Combine(mockBaseDirectory, @"dir1\dir2"));
            var nugetConfigPath = "NuGet.Config";
            string config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key3"" value=""value3"" />
    <add key=""key4"" value=""value4"" />
  </SectionName>
</configuration>";
            CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, @"dir1\dir2"), config);
            config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key1"" value=""value1"" />
    <add key=""key2"" value=""value2"" />
  </SectionName>
</configuration>";
            CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1"), config);

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
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            Directory.CreateDirectory(Path.Combine(mockBaseDirectory, "dir1"));
            Directory.CreateDirectory(Path.Combine(mockBaseDirectory, @"dir1\dir2"));
            var nugetConfigPath = "NuGet.Config";
            string config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key1"" value=""LastOneWins1"" />
    <add key=""key2"" value=""LastOneWins2"" />
  </SectionName>
</configuration>";
            CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, @"dir1\dir2"), config);
            config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key1"" value=""value1"" />
    <add key=""key2"" value=""value2"" />
  </SectionName>
</configuration>";
            CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1"), config);

            var settings = Settings.LoadDefaultSettings(Path.Combine(mockBaseDirectory, @"dir1\dir2"), null, null);

            // Assert
            Assert.Equal("LastOneWins2", settings.GetValue("SectionName", "key2"));
            Assert.Equal("LastOneWins1", settings.GetValue("SectionName", "key1"));
        }

        [Fact]
        public void GetSingleValuesMultipleConfFilesClear()
        {
            // Arrange
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            Directory.CreateDirectory(Path.Combine(mockBaseDirectory, "dir1"));
            Directory.CreateDirectory(Path.Combine(mockBaseDirectory, @"dir1\dir2"));
            var nugetConfigPath = "NuGet.Config";
            string config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <clear /> <!-- i.e. ignore values from prior conf files -->
    <add key=""key2"" value=""value2"" />
  </SectionName>
</configuration>";
            CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, @"dir1\dir2"), config);
            config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key1"" value=""value1"" />    
  </SectionName>
</configuration>";
            CreateConfigurationFile(nugetConfigPath, Path.Combine(mockBaseDirectory, "dir1"), config);

            var settings = Settings.LoadDefaultSettings(Path.Combine(mockBaseDirectory, @"dir1\dir2"), null, null);

            // Assert
            Assert.Equal("value2", settings.GetValue("SectionName", "key2"));
            Assert.Equal(null, settings.GetValue("SectionName", "key1"));
        }

        [Fact]
        public void GetValueReturnsPathRelativeToConfigWhenPathIsNotRooted()
        {
            // Arrange
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            string config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""path-key"" value=""foo\bar"" />
  </SectionName>
</configuration>";
            CreateConfigurationFile("nuget.config", mockBaseDirectory, config);
            
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
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            Directory.CreateDirectory(Path.Combine(mockBaseDirectory, "dir1"));
            Directory.CreateDirectory(Path.Combine(mockBaseDirectory, @"dir1\dir2"));

            string config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key3"" value=""value3"" />
    <add key=""key4"" value=""value4"" />
  </SectionName>
</configuration>";
            CreateConfigurationFile("NuGet.Config", Path.Combine(mockBaseDirectory, "dir1"), config);
            config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""key1"" value=""value1"" />
    <add key=""key2"" value=""value2"" />
  </SectionName>
</configuration>";
            CreateConfigurationFile("UserDefinedConfigFile.confg", Path.Combine(mockBaseDirectory, @"dir1\dir2"), config);

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
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            string config = String.Format(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""path-key"" value=""{0}"" />
  </SectionName>
</configuration>", value);
            CreateConfigurationFile("nuget.config", mockBaseDirectory, config);
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
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            string config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""path-key"" value=""\Blah"" />
  </SectionName>
</configuration>";
            CreateConfigurationFile("nuget.config", mockBaseDirectory, config);
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
            var mockBaseDirectory = Test.TestFilesystemUtility.CreateRandomTestFolder();
            Directory.CreateDirectory(mockBaseDirectory);
            string config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <SectionName>
    <add key=""path-key"" value=""..\Blah"" />
  </SectionName>
</configuration>";
            CreateConfigurationFile("nuget.config", mockBaseDirectory, config);
            var settings = new Settings(mockBaseDirectory, "nuget.config");

            // Act
            string result = settings.GetValue("SectionName", "path-key", isPath: true);

            // Assert
      
            Assert.Equal(String.Format(@"{0}\..\Blah", mockBaseDirectory), result);
        }

        private void CreateConfigurationFile(string configurationPath, string mockBaseDirectory, string configurationContent)
        {
            using (FileStream file = File.Create(Path.Combine(mockBaseDirectory, configurationPath)))
            {
                Byte[] info = new UTF8Encoding(true).GetBytes(configurationContent);
                file.Write(info, 0, info.Count());
            }
        }

        private string ReadConfigurationFile(string path)
        {
            using (FileStream fs = File.OpenRead(path))
            {
                using (var streamReader = new StreamReader(fs))
                {
                    return RemovedLineEndings(streamReader.ReadToEnd());
                }
            }
        }
        // this method is for removing LineEndings for CI build
        private string RemovedLineEndings(string result)
        {
            return result.Replace("\n", "").Replace("\r", "");
        }

        private void AssertEqualCollections(IList<SettingValue> actual, string[] expected)
        {
            Assert.Equal(actual.Count, expected.Length / 2);
            for (int i = 0; i < actual.Count; ++i)
            {
                Assert.Equal(expected[2 * i], actual[i].Key);
                Assert.Equal(expected[2 * i + 1], actual[i].Value);
            }
        }
    }
}
