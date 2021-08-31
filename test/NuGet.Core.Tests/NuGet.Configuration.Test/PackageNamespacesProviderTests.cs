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
    public class PackageNamespacesProviderTests
    {
        [Fact]
        public void Constructor_WithNullSettingsThrows()
        {
            Assert.Throws<ArgumentNullException>(() => new PackageNamespacesProvider(null));
        }

        [Fact]
        public void GetPackageSourceNamespaces_WithOneConfig_ReturnsCorrectNamespaces()
        {
            // Arrange
            using var mockBaseDirectory = TestDirectory.Create();
            var configPath1 = Path.Combine(mockBaseDirectory, "NuGet.Config");
            SettingsTestUtils.CreateConfigurationFile(configPath1, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <packageSource key=""nuget.org"">
            <package prefix=""stuff"" />
        </packageSource>
    </packageSourceMapping>
</configuration>");

            var settings = Settings.LoadSettingsGivenConfigPaths(new string[] { configPath1 });

            // Act & Assert
            var namespaceProvider = new PackageNamespacesProvider(settings);
            IReadOnlyList<PackageNamespacesSourceItem> packageSourceNamespaces = namespaceProvider.GetPackageSourceNamespaces();
            packageSourceNamespaces.Should().HaveCount(1);
            var packageSourceNamespace = packageSourceNamespaces.First();
            packageSourceNamespace.Key.Should().Be("nuget.org");
            packageSourceNamespace.Namespaces.Should().HaveCount(1);
            packageSourceNamespace.Namespaces.First().Id.Should().Be("stuff");
        }

        [Fact]
        public void GetPackageSourceNamespaces_WithMultipleConfigs_ReturnsClosestNamespaces()
        {
            // Arrange
            using var mockBaseDirectory = TestDirectory.Create();
            var configPath1 = Path.Combine(mockBaseDirectory, "NuGet.Config");
            SettingsTestUtils.CreateConfigurationFile(configPath1, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <packageSource key=""nuget.org"">
            <package prefix=""stuff"" />
        </packageSource>
    </packageSourceMapping>
</configuration>");
            var configPath2 = Path.Combine(mockBaseDirectory, "NuGet.Config.2");
            SettingsTestUtils.CreateConfigurationFile(configPath2, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <packageSource key=""nuget.org"">
            <package prefix=""stuff2"" />
        </packageSource>
    </packageSourceMapping>
</configuration>");
            var settings = Settings.LoadSettingsGivenConfigPaths(new string[] { configPath1, configPath2 });

            // Act & Assert
            var namespaceProvider = new PackageNamespacesProvider(settings);
            IReadOnlyList<PackageNamespacesSourceItem> packageSourceNamespaces = namespaceProvider.GetPackageSourceNamespaces();
            packageSourceNamespaces.Should().HaveCount(1);
            var packageSourceNamespace = packageSourceNamespaces.First();
            packageSourceNamespace.Key.Should().Be("nuget.org");
            packageSourceNamespace.Namespaces.Should().HaveCount(1);
            packageSourceNamespace.Namespaces.First().Id.Should().Be("stuff");
        }

        [Fact]
        public void GetPackageSourceNamespaces_WithMultipleConfigs_CombinesDifferentKeys()
        {
            // Arrange
            using var mockBaseDirectory = TestDirectory.Create();
            var configPath1 = Path.Combine(mockBaseDirectory, "NuGet.Config");
            SettingsTestUtils.CreateConfigurationFile(configPath1, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <packageSource key=""nuget.org"">
            <package prefix=""stuff"" />
        </packageSource>
    </packageSourceMapping>
</configuration>");
            var configPath2 = Path.Combine(mockBaseDirectory, "NuGet.Config.2");
            SettingsTestUtils.CreateConfigurationFile(configPath2, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <packageSource key=""contoso"">
            <package prefix=""stuff2"" />
        </packageSource>
    </packageSourceMapping>
</configuration>");
            var settings = Settings.LoadSettingsGivenConfigPaths(new string[] { configPath1, configPath2 });

            // Act & Assert
            var namespaceProvider = new PackageNamespacesProvider(settings);
            IReadOnlyList<PackageNamespacesSourceItem> packageSourceNamespaces = namespaceProvider.GetPackageSourceNamespaces();
            packageSourceNamespaces.Should().HaveCount(2);

            var contosoNamespace = packageSourceNamespaces.First();
            contosoNamespace.Key.Should().Be("contoso");
            contosoNamespace.Namespaces.Should().HaveCount(1);
            contosoNamespace.Namespaces.First().Id.Should().Be("stuff2");

            var nugetOrgNamespace = packageSourceNamespaces.Last();
            nugetOrgNamespace.Key.Should().Be("nuget.org");
            nugetOrgNamespace.Namespaces.Should().HaveCount(1);
            nugetOrgNamespace.Namespaces.First().Id.Should().Be("stuff");
        }

        [Fact]
        public void GetPackageSourceNamespaces_WithMultipleConfigs_RespectsClearTag()
        {
            // Arrange
            using var mockBaseDirectory = TestDirectory.Create();
            var configPath1 = Path.Combine(mockBaseDirectory, "NuGet.Config");
            SettingsTestUtils.CreateConfigurationFile(configPath1, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <clear />
        <packageSource key=""nuget.org"">
            <package prefix=""stuff"" />
        </packageSource>
    </packageSourceMapping>
</configuration>");
            var configPath2 = Path.Combine(mockBaseDirectory, "NuGet.Config.2");
            SettingsTestUtils.CreateConfigurationFile(configPath2, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <packageSource key=""contoso"">
            <package prefix=""stuff2"" />
        </packageSource>
    </packageSourceMapping>
</configuration>");
            var settings = Settings.LoadSettingsGivenConfigPaths(new string[] { configPath1, configPath2 });

            // Act & Assert
            var namespaceProvider = new PackageNamespacesProvider(settings);
            IReadOnlyList<PackageNamespacesSourceItem> packageSourceNamespaces = namespaceProvider.GetPackageSourceNamespaces();
            packageSourceNamespaces.Should().HaveCount(1);

            var contosoNamespace = packageSourceNamespaces.First();
            contosoNamespace.Key.Should().Be("nuget.org");
            contosoNamespace.Namespaces.Should().HaveCount(1);
            contosoNamespace.Namespaces.First().Id.Should().Be("stuff");
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
            <package prefix=""stuff"" />
        </packageSource>
        <packageSource key=""contoso"">
            <package prefix=""stuff2"" />
        </packageSource>
    </packageSourceMapping>
</configuration>");
            var settings = Settings.LoadSettingsGivenConfigPaths(new string[] { configPath1 });

            var namespaceProvider = new PackageNamespacesProvider(settings);
            IReadOnlyList<PackageNamespacesSourceItem> packageSourceNamespaces = namespaceProvider.GetPackageSourceNamespaces();
            packageSourceNamespaces.Should().HaveCount(2);
            var contosoNamespace = packageSourceNamespaces.First(e => e.Key.Equals("contoso"));

            // Act & Assert
            namespaceProvider.Remove(new PackageNamespacesSourceItem[] { contosoNamespace });
            var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <packageSource key=""nuget.org"">
            <package prefix=""stuff"" />
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
            <package prefix=""stuff"" />
        </packageSource>
        <packageSource key=""contoso"">
            <package prefix=""stuff2"" />
        </packageSource>
    </packageSourceMapping>
</configuration>";
            SettingsTestUtils.CreateConfigurationFile(configPath1, configContent);
            var settings = Settings.LoadSettingsGivenConfigPaths(new string[] { configPath1 });

            var namespaceProvider = new PackageNamespacesProvider(settings);
            IReadOnlyList<PackageNamespacesSourceItem> packageSourceNamespaces = namespaceProvider.GetPackageSourceNamespaces();
            packageSourceNamespaces.Should().HaveCount(2);

            // Act & Assert
            namespaceProvider.Remove(new PackageNamespacesSourceItem[] { new PackageNamespacesSourceItem("localConfig", new NamespaceItem[] { new NamespaceItem("item") }) });


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
            <package prefix=""stuff"" />
        </packageSource>
    </packageSourceMapping>
</configuration>");
            var configPath2 = Path.Combine(mockBaseDirectory, "NuGet.Config.2");
            SettingsTestUtils.CreateConfigurationFile(configPath2, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <packageSource key=""contoso"">
            <package prefix=""stuff2"" />
        </packageSource>
    </packageSourceMapping>
</configuration>");
            var settings = Settings.LoadSettingsGivenConfigPaths(new string[] { configPath1, configPath2 });

            // Act & Assert
            var namespaceProvider = new PackageNamespacesProvider(settings);
            IReadOnlyList<PackageNamespacesSourceItem> packageSourceNamespaces = namespaceProvider.GetPackageSourceNamespaces();
            namespaceProvider.Remove(new PackageNamespacesSourceItem[] { packageSourceNamespaces.Last() });
            var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>";

            result.Replace("\r\n", "\n")
                .Should().BeEquivalentTo(
                File.ReadAllText(configPath1).Replace("\r\n", "\n"));
        }

        [Fact]
        public void AddOrUpdatePackageSourceNamespace_WithUpdatedNamespace()
        {
            // Arrange
            using var mockBaseDirectory = TestDirectory.Create();
            var configPath1 = Path.Combine(mockBaseDirectory, "NuGet.Config");
            SettingsTestUtils.CreateConfigurationFile(configPath1, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <packageSource key=""nuget.org"">
            <package prefix=""stuff"" />
        </packageSource>
    </packageSourceMapping>
</configuration>");
            var configPath2 = Path.Combine(mockBaseDirectory, "NuGet.Config.2");
            SettingsTestUtils.CreateConfigurationFile(configPath2, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <packageSource key=""contoso"">
            <package prefix=""stuff2"" />
        </packageSource>
    </packageSourceMapping>
</configuration>");
            var settings = Settings.LoadSettingsGivenConfigPaths(new string[] { configPath1, configPath2 });

            // Act & Assert
            var namespaceProvider = new PackageNamespacesProvider(settings);
            IReadOnlyList<PackageNamespacesSourceItem> packageSourceNamespaces = namespaceProvider.GetPackageSourceNamespaces();
            var namespaceToUpdate = packageSourceNamespaces.Last();
            namespaceToUpdate.Namespaces.Add(new NamespaceItem("added"));
            namespaceProvider.AddOrUpdatePackageSourceNamespace(namespaceToUpdate);
            var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <packageSource key=""nuget.org"">
            <package prefix=""stuff"" />
            <package prefix=""added"" />
        </packageSource>
    </packageSourceMapping>
</configuration>";

            result.Replace("\r\n", "\n")
                .Should().BeEquivalentTo(
                File.ReadAllText(configPath1).Replace("\r\n", "\n"));
        }

        [Fact]
        public void AddOrUpdatePackageSourceNamespace_WithANewNamespace_AddInFurthestConfig()
        {
            // Arrange
            using var mockBaseDirectory = TestDirectory.Create();
            var configPath1 = Path.Combine(mockBaseDirectory, "NuGet.Config");
            SettingsTestUtils.CreateConfigurationFile(configPath1, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <packageSource key=""nuget.org"">
            <package prefix=""stuff"" />
        </packageSource>
    </packageSourceMapping>
</configuration>");
            var configPath2 = Path.Combine(mockBaseDirectory, "NuGet.Config.2");
            SettingsTestUtils.CreateConfigurationFile(configPath2, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <packageSource key=""contoso"">
            <package prefix=""stuff2"" />
        </packageSource>
    </packageSourceMapping>
</configuration>");
            var settings = Settings.LoadSettingsGivenConfigPaths(new string[] { configPath1, configPath2 });

            // Act & Assert
            var namespaceProvider = new PackageNamespacesProvider(settings);
            var namespaceToAdd = new PackageNamespacesSourceItem("localSource", new NamespaceItem[] { new NamespaceItem("added") });
            namespaceProvider.AddOrUpdatePackageSourceNamespace(namespaceToAdd);

            var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <packageSource key=""contoso"">
            <package prefix=""stuff2"" />
        </packageSource>
        <packageSource key=""localSource"">
            <package prefix=""added"" />
        </packageSource>
    </packageSourceMapping>
</configuration>";

            result.Replace("\r\n", "\n")
                .Should().BeEquivalentTo(
                File.ReadAllText(configPath2).Replace("\r\n", "\n"));
        }

        [Fact]
        public void AddOrUpdatePackageSourceNamespace_WithAClearItem_WithANewNamespace_AddInFurthestMatchingConfig()
        {
            // Arrange
            using var mockBaseDirectory = TestDirectory.Create();
            var configPath1 = Path.Combine(mockBaseDirectory, "NuGet.Config");
            SettingsTestUtils.CreateConfigurationFile(configPath1, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <clear />
        <packageSource key=""nuget.org"">
            <package prefix=""stuff"" />
        </packageSource>
    </packageSourceMapping>
</configuration>");
            var configPath2 = Path.Combine(mockBaseDirectory, "NuGet.Config.2");
            SettingsTestUtils.CreateConfigurationFile(configPath2, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <packageSource key=""contoso"">
            <package prefix=""stuff2"" />
        </packageSource>
    </packageSourceMapping>
</configuration>");
            var settings = Settings.LoadSettingsGivenConfigPaths(new string[] { configPath1, configPath2 });

            // Act & Assert
            var namespaceProvider = new PackageNamespacesProvider(settings);
            var namespaceToAdd = new PackageNamespacesSourceItem("localSource", new NamespaceItem[] { new NamespaceItem("added") });
            namespaceProvider.AddOrUpdatePackageSourceNamespace(namespaceToAdd);

            var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageSourceMapping>
        <clear />
        <packageSource key=""nuget.org"">
            <package prefix=""stuff"" />
        </packageSource>
        <packageSource key=""localSource"">
            <package prefix=""added"" />
        </packageSource>
    </packageSourceMapping>
</configuration>";

            result.Replace("\r\n", "\n")
                .Should().BeEquivalentTo(
                File.ReadAllText(configPath1).Replace("\r\n", "\n"));
        }
    }
}
