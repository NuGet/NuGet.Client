// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using FluentAssertions;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Configuration.Test
{
    public class PackageSourceMappingProviderTests
    {
        [Fact]
        public void Constructor_WithNullSettingsThrows()
        {
            Assert.Throws<ArgumentNullException>(() => new PackageSourceMappingProvider(null));
        }

        [Fact]
        public void GetPackageSourceMappingItems_WithOneConfig_ReturnsCorrectPatterns()
        {
            // Arrange
            using var mockBaseDirectory = TestDirectory.Create();
            var configPath1 = Path.Combine(mockBaseDirectory, "NuGet.Config");
            SettingsTestUtils.CreateConfigurationFile(configPath1, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <packageSource key=""nuget.org"">
            <package pattern=""stuff"" />
        </packageSource>
    </packageSourceMapping>
</configuration>");

            var settings = Settings.LoadSettingsGivenConfigPaths(new string[] { configPath1 });

            // Act & Assert
            var sourceMappingProvider = new PackageSourceMappingProvider(settings);
            IReadOnlyList<PackageSourceMappingSourceItem> packageSourceMappingItems = sourceMappingProvider.GetPackageSourceMappingItems();
            packageSourceMappingItems.Should().HaveCount(1);
            var packageSourceMappingSourceItem = packageSourceMappingItems.First();
            packageSourceMappingSourceItem.Key.Should().Be("nuget.org");
            packageSourceMappingSourceItem.Patterns.Should().HaveCount(1);
            packageSourceMappingSourceItem.Patterns.First().Pattern.Should().Be("stuff");
        }

        [Fact]
        public void GetPackageSourceMappingItems_WithMultipleConfigs_ReturnsClosestPatterns()
        {
            // Arrange
            using var mockBaseDirectory = TestDirectory.Create();
            var configPath1 = Path.Combine(mockBaseDirectory, "NuGet.Config");
            SettingsTestUtils.CreateConfigurationFile(configPath1, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <packageSource key=""nuget.org"">
            <package pattern=""stuff"" />
        </packageSource>
    </packageSourceMapping>
</configuration>");
            var configPath2 = Path.Combine(mockBaseDirectory, "NuGet.Config.2");
            SettingsTestUtils.CreateConfigurationFile(configPath2, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <packageSource key=""nuget.org"">
            <package pattern=""stuff2"" />
        </packageSource>
    </packageSourceMapping>
</configuration>");
            var settings = Settings.LoadSettingsGivenConfigPaths(new string[] { configPath1, configPath2 });

            // Act & Assert
            var sourceMappingProvider = new PackageSourceMappingProvider(settings);
            IReadOnlyList<PackageSourceMappingSourceItem> packageSourceMappingItems = sourceMappingProvider.GetPackageSourceMappingItems();
            packageSourceMappingItems.Should().HaveCount(1);
            var packageSourceMappingSourceItem = packageSourceMappingItems.First();
            packageSourceMappingSourceItem.Key.Should().Be("nuget.org");
            packageSourceMappingSourceItem.Patterns.Should().HaveCount(1);
            packageSourceMappingSourceItem.Patterns.First().Pattern.Should().Be("stuff");
        }

        [Fact]
        public void GetPackageSourceMappingItems_WithMultipleConfigs_CombinesDifferentKeys()
        {
            // Arrange
            using var mockBaseDirectory = TestDirectory.Create();
            var configPath1 = Path.Combine(mockBaseDirectory, "NuGet.Config");
            SettingsTestUtils.CreateConfigurationFile(configPath1, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <packageSource key=""nuget.org"">
            <package pattern=""stuff"" />
        </packageSource>
    </packageSourceMapping>
</configuration>");
            var configPath2 = Path.Combine(mockBaseDirectory, "NuGet.Config.2");
            SettingsTestUtils.CreateConfigurationFile(configPath2, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <packageSource key=""contoso"">
            <package pattern=""stuff2"" />
        </packageSource>
    </packageSourceMapping>
</configuration>");
            var settings = Settings.LoadSettingsGivenConfigPaths(new string[] { configPath1, configPath2 });

            // Act & Assert
            var sourceMappingProvider = new PackageSourceMappingProvider(settings);
            IReadOnlyList<PackageSourceMappingSourceItem> packageSourceMappingItems = sourceMappingProvider.GetPackageSourceMappingItems();
            packageSourceMappingItems.Should().HaveCount(2);

            var contosoSourceItem = packageSourceMappingItems.First();
            contosoSourceItem.Key.Should().Be("contoso");
            contosoSourceItem.Patterns.Should().HaveCount(1);
            contosoSourceItem.Patterns.First().Pattern.Should().Be("stuff2");

            var nugetOrgSourceItem = packageSourceMappingItems.Last();
            nugetOrgSourceItem.Key.Should().Be("nuget.org");
            nugetOrgSourceItem.Patterns.Should().HaveCount(1);
            nugetOrgSourceItem.Patterns.First().Pattern.Should().Be("stuff");
        }

        [Fact]
        public void GetPackageSourceMappingItems_WithMultipleConfigs_RespectsClearTag()
        {
            // Arrange
            using var mockBaseDirectory = TestDirectory.Create();
            var configPath1 = Path.Combine(mockBaseDirectory, "NuGet.Config");
            SettingsTestUtils.CreateConfigurationFile(configPath1, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <clear />
        <packageSource key=""nuget.org"">
            <package pattern=""stuff"" />
        </packageSource>
    </packageSourceMapping>
</configuration>");
            var configPath2 = Path.Combine(mockBaseDirectory, "NuGet.Config.2");
            SettingsTestUtils.CreateConfigurationFile(configPath2, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <packageSource key=""contoso"">
            <package pattern=""stuff2"" />
        </packageSource>
    </packageSourceMapping>
</configuration>");
            var settings = Settings.LoadSettingsGivenConfigPaths(new string[] { configPath1, configPath2 });

            // Act & Assert
            var sourceMappingProvider = new PackageSourceMappingProvider(settings);
            IReadOnlyList<PackageSourceMappingSourceItem> packageSourceMappingItems = sourceMappingProvider.GetPackageSourceMappingItems();
            packageSourceMappingItems.Should().HaveCount(1);

            var contosoSourceItem = packageSourceMappingItems.First();
            contosoSourceItem.Key.Should().Be("nuget.org");
            contosoSourceItem.Patterns.Should().HaveCount(1);
            contosoSourceItem.Patterns.First().Pattern.Should().Be("stuff");
        }

        [Fact]
        public void Remove_WithOneConfig_RemovesElementOfInterest()
        {
            // Arrange
            using var mockBaseDirectory = TestDirectory.Create();
            var configPath1 = Path.Combine(mockBaseDirectory, "NuGet.Config");
            SettingsTestUtils.CreateConfigurationFile(configPath1, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <packageSource key=""nuget.org"">
            <package pattern=""stuff"" />
        </packageSource>
        <packageSource key=""contoso"">
            <package pattern=""stuff2"" />
        </packageSource>
    </packageSourceMapping>
</configuration>");
            var settings = Settings.LoadSettingsGivenConfigPaths(new string[] { configPath1 });

            var sourceMappingProvider = new PackageSourceMappingProvider(settings);
            IReadOnlyList<PackageSourceMappingSourceItem> packageSourceMappingItems = sourceMappingProvider.GetPackageSourceMappingItems();
            packageSourceMappingItems.Should().HaveCount(2);
            var contosoSourceItem = packageSourceMappingItems.First(e => e.Key.Equals("contoso"));

            // Act & Assert
            sourceMappingProvider.Remove(new PackageSourceMappingSourceItem[] { contosoSourceItem });
            var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <packageSource key=""nuget.org"">
            <package pattern=""stuff"" />
        </packageSource>
    </packageSourceMapping>
</configuration>";

            result.Replace("\r\n", "\n")
                .Should().BeEquivalentTo(
                File.ReadAllText(configPath1).Replace("\r\n", "\n"));
        }

        [Fact]
        public void Remove_WithElementNotInConfig_DoesntChangeConfig()
        {
            // Arrange
            using var mockBaseDirectory = TestDirectory.Create();
            var configPath1 = Path.Combine(mockBaseDirectory, "NuGet.Config");
            var configContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <packageSource key=""nuget.org"">
            <package pattern=""stuff"" />
        </packageSource>
        <packageSource key=""contoso"">
            <package pattern=""stuff2"" />
        </packageSource>
    </packageSourceMapping>
</configuration>";
            SettingsTestUtils.CreateConfigurationFile(configPath1, configContent);
            var settings = Settings.LoadSettingsGivenConfigPaths(new string[] { configPath1 });

            var sourceMappingProvider = new PackageSourceMappingProvider(settings);
            IReadOnlyList<PackageSourceMappingSourceItem> packageSourceMappingItems = sourceMappingProvider.GetPackageSourceMappingItems();
            packageSourceMappingItems.Should().HaveCount(2);

            // Act & Assert
            sourceMappingProvider.Remove(new PackageSourceMappingSourceItem[] { new PackageSourceMappingSourceItem("localConfig", new PackagePatternItem[] { new PackagePatternItem("item") }) });


            File.ReadAllText(configPath1).Replace("\r\n", "\n")
                .Should().BeEquivalentTo(configContent.Replace("\r\n", "\n"));
        }

        [Fact]
        public void Remove_WithMultipleConfigs_ChangesOriginConfig()
        {
            // Arrange
            using var mockBaseDirectory = TestDirectory.Create();
            var configPath1 = Path.Combine(mockBaseDirectory, "NuGet.Config");
            SettingsTestUtils.CreateConfigurationFile(configPath1, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <packageSource key=""nuget.org"">
            <package pattern=""stuff"" />
        </packageSource>
    </packageSourceMapping>
</configuration>");
            var configPath2 = Path.Combine(mockBaseDirectory, "NuGet.Config.2");
            SettingsTestUtils.CreateConfigurationFile(configPath2, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <packageSource key=""contoso"">
            <package pattern=""stuff2"" />
        </packageSource>
    </packageSourceMapping>
</configuration>");
            var settings = Settings.LoadSettingsGivenConfigPaths(new string[] { configPath1, configPath2 });

            // Act & Assert
            var sourceMappingProvider = new PackageSourceMappingProvider(settings);
            IReadOnlyList<PackageSourceMappingSourceItem> packageSourceMappingItems = sourceMappingProvider.GetPackageSourceMappingItems();
            sourceMappingProvider.Remove(new PackageSourceMappingSourceItem[] { packageSourceMappingItems.Last() });
            var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>";

            result.Replace("\r\n", "\n")
                .Should().BeEquivalentTo(
                File.ReadAllText(configPath1).Replace("\r\n", "\n"));
        }

        [Fact]
        public void AddOrUpdatePackageSourceMapping_WithUpdatedPatterns()
        {
            // Arrange
            using var mockBaseDirectory = TestDirectory.Create();
            var configPath1 = Path.Combine(mockBaseDirectory, "NuGet.Config");
            SettingsTestUtils.CreateConfigurationFile(configPath1, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <packageSource key=""nuget.org"">
            <package pattern=""stuff"" />
        </packageSource>
    </packageSourceMapping>
</configuration>");
            var configPath2 = Path.Combine(mockBaseDirectory, "NuGet.Config.2");
            SettingsTestUtils.CreateConfigurationFile(configPath2, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <packageSource key=""contoso"">
            <package pattern=""stuff2"" />
        </packageSource>
    </packageSourceMapping>
</configuration>");
            var settings = Settings.LoadSettingsGivenConfigPaths(new string[] { configPath1, configPath2 });

            // Act & Assert
            var sourceMappingProvider = new PackageSourceMappingProvider(settings);
            IReadOnlyList<PackageSourceMappingSourceItem> packageSourceMappingItems = sourceMappingProvider.GetPackageSourceMappingItems();
            var packageSourceMappingItem = packageSourceMappingItems.Last();
            packageSourceMappingItem.Patterns.Add(new PackagePatternItem("added"));
            sourceMappingProvider.AddOrUpdatePackageSourceMappingSourceItem(packageSourceMappingItem);
            var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <packageSource key=""nuget.org"">
            <package pattern=""stuff"" />
            <package pattern=""added"" />
        </packageSource>
    </packageSourceMapping>
</configuration>";

            result.Replace("\r\n", "\n")
                .Should().BeEquivalentTo(
                File.ReadAllText(configPath1).Replace("\r\n", "\n"));
        }

        [Fact]
        public void AddOrUpdatePackageSourceMapping_WithANewPattern_AddInFurthestConfig()
        {
            // Arrange
            using var mockBaseDirectory = TestDirectory.Create();
            var configPath1 = Path.Combine(mockBaseDirectory, "NuGet.Config");
            SettingsTestUtils.CreateConfigurationFile(configPath1, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <packageSource key=""nuget.org"">
            <package pattern=""stuff"" />
        </packageSource>
    </packageSourceMapping>
</configuration>");
            var configPath2 = Path.Combine(mockBaseDirectory, "NuGet.Config.2");
            SettingsTestUtils.CreateConfigurationFile(configPath2, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <packageSource key=""contoso"">
            <package pattern=""stuff2"" />
        </packageSource>
    </packageSourceMapping>
</configuration>");
            var settings = Settings.LoadSettingsGivenConfigPaths(new string[] { configPath1, configPath2 });

            // Act & Assert
            var sourceMappingProvider = new PackageSourceMappingProvider(settings);
            var patternToAdd = new PackageSourceMappingSourceItem("localSource", new PackagePatternItem[] { new PackagePatternItem("added") });
            sourceMappingProvider.AddOrUpdatePackageSourceMappingSourceItem(patternToAdd);

            var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <packageSource key=""contoso"">
            <package pattern=""stuff2"" />
        </packageSource>
        <packageSource key=""localSource"">
            <package pattern=""added"" />
        </packageSource>
    </packageSourceMapping>
</configuration>";

            result.Replace("\r\n", "\n")
                .Should().BeEquivalentTo(
                File.ReadAllText(configPath2).Replace("\r\n", "\n"));
        }

        [Fact]
        public void AddOrUpdatePackageSourceMapping_WithAClearItem_WithANewPattern_AddInFurthestMatchingConfig()
        {
            // Arrange
            using var mockBaseDirectory = TestDirectory.Create();
            var configPath1 = Path.Combine(mockBaseDirectory, "NuGet.Config");
            SettingsTestUtils.CreateConfigurationFile(configPath1, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <clear />
        <packageSource key=""nuget.org"">
            <package pattern=""stuff"" />
        </packageSource>
    </packageSourceMapping>
</configuration>");
            var configPath2 = Path.Combine(mockBaseDirectory, "NuGet.Config.2");
            SettingsTestUtils.CreateConfigurationFile(configPath2, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <packageSource key=""contoso"">
            <package pattern=""stuff2"" />
        </packageSource>
    </packageSourceMapping>
</configuration>");
            var settings = Settings.LoadSettingsGivenConfigPaths(new string[] { configPath1, configPath2 });

            // Act & Assert
            var sourceMappingProvider = new PackageSourceMappingProvider(settings);
            var patternToAdd = new PackageSourceMappingSourceItem("localSource", new PackagePatternItem[] { new PackagePatternItem("added") });
            sourceMappingProvider.AddOrUpdatePackageSourceMappingSourceItem(patternToAdd);

            var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <clear />
        <packageSource key=""nuget.org"">
            <package pattern=""stuff"" />
        </packageSource>
        <packageSource key=""tempSource"">
            <package pattern=""added"" />
        </packageSource>
    </packageSourceMapping>
</configuration>";

            result.Replace("\r\n", "\n")
                .Should().BeEquivalentTo(
                File.ReadAllText(configPath1).Replace("\r\n", "\n"));
        }

        [Fact]
        public void SavePackageSourceMappingsTest_Add()
        {
            // Arrange
            using var mockBaseDirectory = TestDirectory.Create();
            var configPath1 = Path.Combine(mockBaseDirectory, "NuGet.Config");
            SettingsTestUtils.CreateConfigurationFile(configPath1, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <clear />
        <packageSource key=""nuget.org"">
            <package pattern=""stuff"" />
        </packageSource>
    </packageSourceMapping>
</configuration>");

            PackagePatternItem testPackagePatternItem = new PackagePatternItem("stuff");
            ObservableCollection<PackagePatternItem> testPackagePatternItems = new ObservableCollection<PackagePatternItem>();
            testPackagePatternItems.Add(testPackagePatternItem);
            PackageSourceMappingSourceItem testMappingItem = new PackageSourceMappingSourceItem("nuget.org", testPackagePatternItems);

            ObservableCollection<PackagePatternItem> packagePatternItems = new ObservableCollection<PackagePatternItem>();
            for (int i = 0; i < 3; i++)
            {
                PackagePatternItem tempPackagePatternItem = new PackagePatternItem(i.ToString());
                packagePatternItems.Add(tempPackagePatternItem);
            }
            PackageSourceMappingSourceItem tempMapping = new PackageSourceMappingSourceItem("tempSource", packagePatternItems);
            ObservableCollection<PackageSourceMappingSourceItem> tempMappings = new ObservableCollection<PackageSourceMappingSourceItem>();
            tempMappings.Add(testMappingItem);
            tempMappings.Add(tempMapping);

            var settings = Settings.LoadSettingsGivenConfigPaths(new string[] { configPath1 });

            // Act & Assert
            var sourceMappingProvider = new PackageSourceMappingProvider(settings);
            sourceMappingProvider.SavePackageSourceMappings(tempMappings);

            var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <clear />
        <packageSource key=""nuget.org"">
            <package pattern=""stuff"" />
        </packageSource>
        <packageSource key=""tempSource"">
            <package pattern=""0"" />
            <package pattern=""1"" />
            <package pattern=""2"" />
        </packageSource>
    </packageSourceMapping>
</configuration>";

            result.Replace("\r\n", "\n")
                .Should().BeEquivalentTo(
                File.ReadAllText(configPath1).Replace("\r\n", "\n"));
        }

        [Fact]
        public void SavePackageSourceMappingsTest_Update()
        {
            // Arrange
            using var mockBaseDirectory = TestDirectory.Create();
            var configPath1 = Path.Combine(mockBaseDirectory, "NuGet.Config");
            SettingsTestUtils.CreateConfigurationFile(configPath1, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <clear />
        <packageSource key=""nuget.org"">
            <package pattern=""stuff"" />
        </packageSource>
    </packageSourceMapping>
</configuration>");

            PackagePatternItem testPackagePatternItem = new PackagePatternItem("stuff");
            ObservableCollection<PackagePatternItem> testPackagePatternItems = new ObservableCollection<PackagePatternItem>();
            testPackagePatternItems.Add(testPackagePatternItem);
            PackageSourceMappingSourceItem testMappingItem = new PackageSourceMappingSourceItem("nuget.org", testPackagePatternItems);

            ObservableCollection<PackagePatternItem> packagePatternItems = new ObservableCollection<PackagePatternItem>();
            for (int i = 0; i < 3; i++)
            {
                PackagePatternItem tempPackagePatternItem = new PackagePatternItem(i.ToString());
                packagePatternItems.Add(tempPackagePatternItem);
            }
            PackageSourceMappingSourceItem tempMapping = new PackageSourceMappingSourceItem("nuget.org", packagePatternItems);
            ObservableCollection<PackageSourceMappingSourceItem> tempMappings = new ObservableCollection<PackageSourceMappingSourceItem>();
            tempMappings.Add(testMappingItem);
            tempMappings.Add(tempMapping);

            var settings = Settings.LoadSettingsGivenConfigPaths(new string[] { configPath1 });

            // Act & Assert
            var sourceMappingProvider = new PackageSourceMappingProvider(settings);
            sourceMappingProvider.SavePackageSourceMappings(tempMappings);

            var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <clear />
        <packageSource key=""nuget.org"">
            <package pattern=""0"" />
            <package pattern=""1"" />
            <package pattern=""2"" />
        </packageSource>
    </packageSourceMapping>
</configuration>";

            result.Replace("\r\n", "\n")
                .Should().BeEquivalentTo(
                File.ReadAllText(configPath1).Replace("\r\n", "\n"));
        }

        [Fact]
        public void SavePackageSourceMappingsTest_Remove()
        {
            // Arrange
            using var mockBaseDirectory = TestDirectory.Create();
            var configPath1 = Path.Combine(mockBaseDirectory, "NuGet.Config");
            SettingsTestUtils.CreateConfigurationFile(configPath1, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <clear />
        <packageSource key=""nuget.org"">
            <package pattern=""stuff"" />
        </packageSource>
    </packageSourceMapping>
</configuration>");

            PackagePatternItem testPackagePatternItem = new PackagePatternItem("stuff");
            ObservableCollection<PackagePatternItem> testPackagePatternItems = new ObservableCollection<PackagePatternItem>();
            testPackagePatternItems.Add(testPackagePatternItem);
            PackageSourceMappingSourceItem testMappingItem = new PackageSourceMappingSourceItem("nuget.org", testPackagePatternItems);
            PackageSourceMappingSourceItem testMappingItem2 = new PackageSourceMappingSourceItem("nuget2.org", testPackagePatternItems);
            ObservableCollection<PackageSourceMappingSourceItem> tempMappings = new ObservableCollection<PackageSourceMappingSourceItem>();
            tempMappings.Add(testMappingItem);
            tempMappings.Add(testMappingItem2);

            var settings = Settings.LoadSettingsGivenConfigPaths(new string[] { configPath1 });

            // Act & Assert
            var sourceMappingProvider = new PackageSourceMappingProvider(settings);
            sourceMappingProvider.SavePackageSourceMappings(tempMappings);

            tempMappings.Remove(testMappingItem2);
            sourceMappingProvider.SavePackageSourceMappings(tempMappings);


            var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <clear />
        <packageSource key=""nuget.org"">
            <package pattern=""stuff"" />
        </packageSource>
    </packageSourceMapping>
</configuration>";

            result.Replace("\r\n", "\n")
                .Should().BeEquivalentTo(
                File.ReadAllText(configPath1).Replace("\r\n", "\n"));
        }

        [Fact]
        public void SavePackageSourceMappingsTest_Remove_Update_Add()
        {
            // Arrange
            using var mockBaseDirectory = TestDirectory.Create();
            var configPath1 = Path.Combine(mockBaseDirectory, "NuGet.Config");
            SettingsTestUtils.CreateConfigurationFile(configPath1, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <clear />
        <packageSource key=""nuget.org"">
            <package pattern=""stuff"" />
        </packageSource>
    </packageSourceMapping>
</configuration>");

            PackagePatternItem testPackagePatternItem = new PackagePatternItem("stuff");
            ObservableCollection<PackagePatternItem> testPackagePatternItems = new ObservableCollection<PackagePatternItem>();
            testPackagePatternItems.Add(testPackagePatternItem);
            PackageSourceMappingSourceItem testMappingItem = new PackageSourceMappingSourceItem("nuget.org", testPackagePatternItems);
            PackageSourceMappingSourceItem testMappingItem2 = new PackageSourceMappingSourceItem("nuget2.org", testPackagePatternItems);
            ObservableCollection<PackageSourceMappingSourceItem> tempMappings = new ObservableCollection<PackageSourceMappingSourceItem>();
            tempMappings.Add(testMappingItem);
            tempMappings.Add(testMappingItem2);

            var settings = Settings.LoadSettingsGivenConfigPaths(new string[] { configPath1 });

            // Act & Assert
            var sourceMappingProvider = new PackageSourceMappingProvider(settings);
            sourceMappingProvider.SavePackageSourceMappings(tempMappings);

            //Update
            ObservableCollection<PackagePatternItem> packagePatternItems = new ObservableCollection<PackagePatternItem>();
            for (int i = 0; i < 3; i++)
            {
                PackagePatternItem tempPackagePatternItem = new PackagePatternItem(i.ToString());
                packagePatternItems.Add(tempPackagePatternItem);
            }
            PackageSourceMappingSourceItem tempMapping = new PackageSourceMappingSourceItem("nuget.org", packagePatternItems);
            tempMappings.Add(tempMapping);

            //Add
            PackagePatternItem packagePatternItemAdd = new PackagePatternItem("stuff");
            ObservableCollection<PackagePatternItem> packagePatternItemsAdd = new ObservableCollection<PackagePatternItem>();
            packagePatternItemsAdd.Add(packagePatternItemAdd);
            PackageSourceMappingSourceItem testMappingItemAdd = new PackageSourceMappingSourceItem("newSource", packagePatternItemsAdd);

            tempMappings.Add(testMappingItemAdd);
            tempMappings.Remove(testMappingItem2);

            sourceMappingProvider.SavePackageSourceMappings(tempMappings);


            var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <clear />
        <packageSource key=""nuget.org"">
            <package pattern=""0"" />
            <package pattern=""1"" />
            <package pattern=""2"" />
        </packageSource>
        <packageSource key=""newSource"">
            <package pattern=""stuff"" />
        </packageSource>
    </packageSourceMapping>
</configuration>";

            result.Replace("\r\n", "\n")
                .Should().BeEquivalentTo(
                File.ReadAllText(configPath1).Replace("\r\n", "\n"));
        }

    }
}
