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
    public class SettingsUtilityTests
    {
        [Fact]
        public void GetGlobalPackagesFolder_Default()
        {
            // Arrange
#if !IS_CORECLR
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
#else
            string userProfile = null;
            if (RuntimeEnvironmentHelper.IsWindows)
            {
                userProfile = Environment.GetEnvironmentVariable("UserProfile");
            }
            else
            {
                userProfile = Environment.GetEnvironmentVariable("HOME");
            }
#endif
            var expectedPath = Path.Combine(userProfile, ".nuget", SettingsUtility.DefaultGlobalPackagesFolderPath);

            // Act
            var globalPackagesFolderPath = SettingsUtility.GetGlobalPackagesFolder(NullSettings.Instance);

            // Assert
            globalPackagesFolderPath.Should().Be(expectedPath);
        }

        [Fact]
        public void GetGlobalPackagesFolder_FromNuGetConfig()
        {
            // Arrange
            var config = @"<?xml version='1.0' encoding='utf-8'?>
<configuration>
    <config>
        <add key='globalPackagesFolder' value='a' />
    </config>
</configuration>";

            var nugetConfigPath = "NuGet.Config";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                // Act
                var globalPackagesFolderPath = SettingsUtility.GetGlobalPackagesFolder(settings);

                // Assert
                globalPackagesFolderPath.Should().Be(Path.Combine(mockBaseDirectory, "a"));
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

            var nugetConfigPath = "NuGet.Config";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var settings = new Settings(mockBaseDirectory);

                var expected = Path.GetFullPath(Path.Combine(mockBaseDirectory, @"..\..\NuGetPackages"));

                // Act
                var globalPackagesFolderPath = SettingsUtility.GetGlobalPackagesFolder(settings);

                // Assert
                globalPackagesFolderPath.Should().Be(expected);
            }
        }

#if !IS_CORECLR
        [Fact]
        public void SetEncryptedValueForAddItem_UpdatesExistingItemValueWithEncryptedOne()
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

                // Act
                SettingsUtility.SetEncryptedValueForAddItem(settings, "SectionName", "key", "NewValue");

                // Assert
                var content = File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath));
                content.Should().NotContain("NewValue");
            }
        }

        [Fact]
        public void GetDecryptedValueForAddItem_ReturnsDecryptedValue()
        {
            // Arrange
            var configFile = "NuGet.Config";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(configFile, mockBaseDirectory, @"<configuration></configuration>");
                var settings = new Settings(mockBaseDirectory);

                SettingsUtility.SetEncryptedValueForAddItem(settings, "SectionName", "key", "value");

                // Act
                var result = SettingsUtility.GetDecryptedValueForAddItem(settings, "SectionName", "key");

                // Assert
                result.Should().Be("value");
            }
        }
#endif

        [Fact]
        public void GetDecryptedValueForAddItem_WithNoKey_ReturnsNull()
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

                // Act
                var result = SettingsUtility.GetDecryptedValueForAddItem(settings, "SectionName", "NoKeyByThatName");

                // Assert
                result.Should().BeNull();
            }
        }
    }
}
