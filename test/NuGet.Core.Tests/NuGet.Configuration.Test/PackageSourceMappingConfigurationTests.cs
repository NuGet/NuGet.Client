// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Configuration.Test
{
    public class PackageSourceMappingConfigurationTests
    {
        [Fact]
        public void GetPackageSourceMappingConfiguration_WithNoSourceMappings_IsNotEnabled()
        {
            // Arrange
            using var mockBaseDirectory = TestDirectory.Create();
            var configPath1 = Path.Combine(mockBaseDirectory, "NuGet.Config");
            SettingsTestUtils.CreateConfigurationFile(configPath1, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>");
            var settings = Settings.LoadSettingsGivenConfigPaths(new string[] { configPath1 });

            // Act & Assert
            var packageSourceMapping = PackageSourceMapping.GetPackageSourceMapping(settings);
            packageSourceMapping.IsEnabled.Should().BeFalse();
            packageSourceMapping.Patterns.Should().HaveCount(0);
        }

        [Fact]
        public void GetPackageSourceMappingConfiguration_WithOneSource()
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
            var settings = Settings.LoadSettingsGivenConfigPaths(new string[] { configPath1 });

            // Act & Assert
            var packageSourceMapping = PackageSourceMapping.GetPackageSourceMapping(settings);
            packageSourceMapping.IsEnabled.Should().BeTrue();
            packageSourceMapping.Patterns.Should().HaveCount(1);
            KeyValuePair<string,List<string>> patternsForSource = packageSourceMapping.Patterns.First();
            patternsForSource.Key.Should().Be("nuget.org");
            patternsForSource.Value.Should().BeEquivalentTo(new string[] { "stuff" });
        }

        [Fact]
        public void GetPackageSourceMappingConfiguration_WithMultipleSources()
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
            <package pattern=""moreStuff"" />
        </packageSource>
    </packageSourceMapping>
</configuration>");
            var settings = Settings.LoadSettingsGivenConfigPaths(new string[] { configPath1 });

            // Act & Assert
            var packageSourceMapping = PackageSourceMapping.GetPackageSourceMapping(settings);
            packageSourceMapping.IsEnabled.Should().BeTrue();
            packageSourceMapping.Patterns.Should().HaveCount(2);

            List<string> nugetPatterns = packageSourceMapping.Patterns["nuget.org"];
            nugetPatterns.Should().BeEquivalentTo(new string[] { "stuff" });

            List<string> contosoPattern = packageSourceMapping.Patterns["contoso"];
            contosoPattern.Should().BeEquivalentTo(new string[] { "moreStuff" });
        }

        [Fact]
        public void AddMapping_AddNewSourceAndPattern()
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
            var settings = Settings.LoadSettingsGivenConfigPaths(new string[] { configPath1 });

            // Act & Assert
            var packageSourceMapping = PackageSourceMapping.GetPackageSourceMapping(settings);
            packageSourceMapping.AddMapping("newPackageID", "newSource");
            packageSourceMapping.IsEnabled.Should().BeTrue();
            packageSourceMapping.Patterns.Should().HaveCount(2);
            KeyValuePair<string, List<string>> patternsForSource = packageSourceMapping.Patterns.First();
            patternsForSource.Key.Should().Be("nuget.org");
            patternsForSource.Value.Should().BeEquivalentTo(new string[] { "stuff" });
            KeyValuePair<string, List<string>> patternsForSource2 = packageSourceMapping.Patterns.Last();
            patternsForSource2.Key.Should().Be("newPackageID");
            patternsForSource2.Value.Should().BeEquivalentTo(new string[] { "newSource" });
        }

        [Fact]
        public void AddMapping_AddNewPatternWithExistingSource()
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
            var settings = Settings.LoadSettingsGivenConfigPaths(new string[] { configPath1 });

            // Act & Assert
            var packageSourceMapping = PackageSourceMapping.GetPackageSourceMapping(settings);
            packageSourceMapping.AddMapping("nuget.org", "newSource");
            packageSourceMapping.IsEnabled.Should().BeTrue();
            packageSourceMapping.Patterns.Should().HaveCount(1);
            KeyValuePair<string, List<string>> patternsForSource = packageSourceMapping.Patterns.First();
            patternsForSource.Key.Should().Be("nuget.org");
            patternsForSource.Value.Should().HaveCount(2);
            patternsForSource.Value.First().Should().Be("stuff");
            patternsForSource.Value.Last().Should().Be("newSource");
        }
    }
}
