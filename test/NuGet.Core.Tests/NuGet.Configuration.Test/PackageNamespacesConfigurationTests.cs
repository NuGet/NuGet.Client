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
    public class PackageNamespacesConfigurationTests
    {
        [Fact]
        public void GetPackageNamespacesConfiguration_WithOneSource()
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
            var settings = Settings.LoadSettingsGivenConfigPaths(new string[] { configPath1 });

            // Act & Assert
            var configuration = PackageNamespacesConfiguration.GetPackageNamespacesConfiguration(settings);
            configuration.AreNamespacesEnabled.Should().BeTrue();
            configuration.Namespaces.Should().HaveCount(1);
            KeyValuePair<string, IReadOnlyList<string>> namespaceForSource = configuration.Namespaces.First();
            namespaceForSource.Key.Should().Be("nuget.org");
            namespaceForSource.Value.Should().BeEquivalentTo(new string[] { "stuff" });
        }

        [Fact]
        public void GetPackageNamespacesConfiguration_WithMultipleSources()
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
            <package prefix=""moreStuff"" />
        </packageSource>
    </packageSourceMapping>
</configuration>");
            var settings = Settings.LoadSettingsGivenConfigPaths(new string[] { configPath1 });

            // Act & Assert
            var configuration = PackageNamespacesConfiguration.GetPackageNamespacesConfiguration(settings);
            configuration.AreNamespacesEnabled.Should().BeTrue();
            configuration.Namespaces.Should().HaveCount(2);

            IReadOnlyList<string> nugetNamespaces = configuration.Namespaces["nuget.org"];
            nugetNamespaces.Should().BeEquivalentTo(new string[] { "stuff" });

            IReadOnlyList<string> contosoNamespace = configuration.Namespaces["contoso"];
            contosoNamespace.Should().BeEquivalentTo(new string[] { "moreStuff" });
        }
    }
}
