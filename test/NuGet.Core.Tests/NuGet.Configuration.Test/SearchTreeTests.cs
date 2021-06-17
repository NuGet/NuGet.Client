// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Configuration.Test
{
    public class SearchTreeTests
    {
        [Fact]
        public void SearchTree_WithOneSource()
        {
            // Arrange
            using var mockBaseDirectory = TestDirectory.Create();
            var configPath1 = Path.Combine(mockBaseDirectory, "NuGet.Config");
            SettingsTestUtils.CreateConfigurationFile(configPath1, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageNamespaces>
        <clear />
        <packageSource key=""nuget.org"">
            <namespace id=""stuff"" />
        </packageSource>
    </packageNamespaces>
</configuration>");
            var settings = Settings.LoadSettingsGivenConfigPaths(new string[] { configPath1 });

            // Act & Assert
            var configuration = PackageNamespacesConfiguration.GetPackageNamespacesConfiguration(settings);
            Assert.True(configuration.AreNamespacesEnabled);

            configuration.Namespaces.Should().HaveCount(1);
            var packageSourcesMatchFull = configuration.GetConfiguredPackageSources("stuff");
            Assert.Equal(1, packageSourcesMatchFull.Count);
            Assert.Equal("nuget.org", packageSourcesMatchFull.First());

            var tooLongNoMatch = configuration.GetConfiguredPackageSources("stuff.something");
            Assert.Null(tooLongNoMatch);

            var packageSourcesMatchPartial = configuration.GetConfiguredPackageSources("stu");
            Assert.Null(packageSourcesMatchPartial);

            var packageSourcesNoMatch = configuration.GetConfiguredPackageSources("random");
            Assert.Null(packageSourcesNoMatch);
        }

        [Fact]
        public void SearchTree_WithOneSourceGlobbing()
        {
            // Arrange
            using var mockBaseDirectory = TestDirectory.Create();
            var configPath1 = Path.Combine(mockBaseDirectory, "NuGet.Config");
            SettingsTestUtils.CreateConfigurationFile(configPath1, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageNamespaces>
        <clear />
        <packageSource key=""PublicRepository"">
            <namespace id=""Contoso.Opensource.*"" />
        </packageSource>
    </packageNamespaces>
</configuration>");
            var settings = Settings.LoadSettingsGivenConfigPaths(new string[] { configPath1 });

            // Act & Assert
            var configuration = PackageNamespacesConfiguration.GetPackageNamespacesConfiguration(settings);
            Assert.True(configuration.AreNamespacesEnabled);
            configuration.Namespaces.Should().HaveCount(1);

            // No match
            var packageSourcesMatchPartial1 = configuration.GetConfiguredPackageSources("Cont");
            Assert.Null(packageSourcesMatchPartial1);

            // No match
            var packageSourcesMatchPartial2 = configuration.GetConfiguredPackageSources("Contoso.Opensource");
            Assert.Null(packageSourcesMatchPartial2);

            // No match
            var packageSourcesMatchPartial3 = configuration.GetConfiguredPackageSources("Contoso.Opensource.");
            Assert.Null(packageSourcesMatchPartial3);

            // Match
            var packageSourcesMatchFull1 = configuration.GetConfiguredPackageSources("Contoso.Opensource.MVC");
            Assert.Equal(1, packageSourcesMatchFull1.Count);
            Assert.Equal("publicrepository", packageSourcesMatchFull1.First());

            // Match
            var packageSourcesMatchFull2 = configuration.GetConfiguredPackageSources("Contoso.Opensource.MVC.ASP");

            Assert.Equal(1, packageSourcesMatchFull2.Count);
            Assert.Equal("publicrepository", packageSourcesMatchFull2.First());

            // No match
            var packageSourcesNoMatch = configuration.GetConfiguredPackageSources("random");
            Assert.Null(packageSourcesNoMatch);
        }

        [Fact]
        public void SearchTree_WithMultipleSources()
        {
            // Arrange
            using var mockBaseDirectory = TestDirectory.Create();
            var configPath1 = Path.Combine(mockBaseDirectory, "NuGet.Config");
            SettingsTestUtils.CreateConfigurationFile(configPath1, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageNamespaces>
        <packageSource key=""nuget.org"">
            <namespace id=""stuff"" />
        </packageSource>
        <packageSource key=""contoso"">
            <namespace id=""moreStuff"" />
        </packageSource>
        <packageSource key=""privateRepository"">
            <namespace id=""private*"" />
        </packageSource>
    </packageNamespaces>
</configuration>");
            var settings = Settings.LoadSettingsGivenConfigPaths(new string[] { configPath1 });

            // Act & Assert
            var configuration = PackageNamespacesConfiguration.GetPackageNamespacesConfiguration(settings);
            Assert.True(configuration.AreNamespacesEnabled);
            configuration.Namespaces.Should().HaveCount(3);

            var packageSourcesMatchFull1 = configuration.GetConfiguredPackageSources("stuff");

            Assert.Equal(1, packageSourcesMatchFull1.Count);
            Assert.Equal("nuget.org", packageSourcesMatchFull1.First());

            var packageSourcesMatchPartial1 = configuration.GetConfiguredPackageSources("stu");
            Assert.Null(packageSourcesMatchPartial1);

            var packageSourcesMatchFull2 = configuration.GetConfiguredPackageSources("moreStuff");
            Assert.Equal(1, packageSourcesMatchFull2.Count);
            Assert.Equal("contoso", packageSourcesMatchFull2.First());

            var packageSourcesMatchPartial2 = configuration.GetConfiguredPackageSources("PrivateTest");
            Assert.Equal(1, packageSourcesMatchPartial2.Count);
            Assert.Equal("privaterepository", packageSourcesMatchPartial2.First());

            var packageSourcesNoMatch = configuration.GetConfiguredPackageSources("random");
            Assert.Null(packageSourcesNoMatch);
        }

        [Fact]
        public void SearchTree_NoSources()
        {
            // Arrange
            using var mockBaseDirectory = TestDirectory.Create();
            var configPath1 = Path.Combine(mockBaseDirectory, "NuGet.Config");
            SettingsTestUtils.CreateConfigurationFile(configPath1, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>");
            var settings = Settings.LoadSettingsGivenConfigPaths(new string[] { configPath1 });

            // Act & Assert
            var configuration = PackageNamespacesConfiguration.GetPackageNamespacesConfiguration(settings);
            Assert.False(configuration.AreNamespacesEnabled);
            configuration.Namespaces.Should().HaveCount(0);

            var packageSourcesMatchPartial = configuration.GetConfiguredPackageSources("stuff");
            Assert.Null(packageSourcesMatchPartial);
        }

        [Fact]
        public void SearchTree_TopNodeIsGlobbing()
        {
            // Arrange
            using var mockBaseDirectory = TestDirectory.Create();
            var configPath1 = Path.Combine(mockBaseDirectory, "NuGet.Config");
            SettingsTestUtils.CreateConfigurationFile(configPath1, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageNamespaces>
        <packageSource key=""source1"">
            <namespace id=""NuGet.*"" />
        </packageSource>
        <packageSource key=""source2"">
            <namespace id=""NuGet.Common"" />
        </packageSource>
        <packageSource key=""source3"">
            <namespace id=""NuGet.Common.Identity"" />
        </packageSource>
    </packageNamespaces>
</configuration>");
            var settings = Settings.LoadSettingsGivenConfigPaths(new string[] { configPath1 });

            // Act & Assert
            var configuration = PackageNamespacesConfiguration.GetPackageNamespacesConfiguration(settings);
            Assert.True(configuration.AreNamespacesEnabled);

            configuration.Namespaces.Should().HaveCount(3);
            var configuredSources = configuration.GetConfiguredPackageSources("NuGet.Common1");
            Assert.Equal(1, configuredSources.Count);
            Assert.Equal("source1", configuredSources.First());

            configuredSources = configuration.GetConfiguredPackageSources("Nu");
            Assert.Equal(null, configuredSources);

            configuredSources = configuration.GetConfiguredPackageSources("NuGet");
            Assert.Equal(null, configuredSources);

            configuredSources = configuration.GetConfiguredPackageSources("NuGet.Id");
            Assert.Equal(1, configuredSources.Count);
            Assert.Equal("source1", configuredSources.First());

            configuredSources = configuration.GetConfiguredPackageSources("NuGet.Common");
            Assert.Equal(1, configuredSources.Count);
            Assert.Equal("source2", configuredSources.First());

            configuredSources = configuration.GetConfiguredPackageSources("NuGet.Common.Id");
            Assert.Equal(1, configuredSources.Count);
            Assert.Equal("source1", configuredSources.First());

            configuredSources = configuration.GetConfiguredPackageSources("NuGet.Common.Identity");
            Assert.Equal(1, configuredSources.Count);
            Assert.Equal("source3", configuredSources.First());

            configuredSources = configuration.GetConfiguredPackageSources("NuGet.Common.Identity.A");
            Assert.Equal(1, configuredSources.Count);
            Assert.Equal("source1", configuredSources.First());
        }

        [Fact]
        public void SearchTree_TopNodeIsNotGlobbing()
        {
            // Arrange
            using var mockBaseDirectory = TestDirectory.Create();
            var configPath1 = Path.Combine(mockBaseDirectory, "NuGet.Config");
            SettingsTestUtils.CreateConfigurationFile(configPath1, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageNamespaces>
        <packageSource key=""source1"">
            <namespace id=""NuGet.Common"" />
        </packageSource>
        <packageSource key=""source2"">
            <namespace id=""NuGet"" />
        </packageSource>
    </packageNamespaces>
</configuration>");
            var settings = Settings.LoadSettingsGivenConfigPaths(new string[] { configPath1 });

            // Act & Assert
            var configuration = PackageNamespacesConfiguration.GetPackageNamespacesConfiguration(settings);
            Assert.True(configuration.AreNamespacesEnabled);
            configuration.Namespaces.Should().HaveCount(2);

            // Since previous node is not globbing it shouldn't match anything.
            var configuredSources = configuration.GetConfiguredPackageSources("NuGet.Common1");
            Assert.Equal(null, configuredSources);

            configuredSources = configuration.GetConfiguredPackageSources("NuGet");
            Assert.Equal(1, configuredSources.Count);
            Assert.Equal("source2", configuredSources.First());

            configuredSources = configuration.GetConfiguredPackageSources("NuGet.common");
            Assert.Equal(1, configuredSources.Count);
            Assert.Equal("source1", configuredSources.First());
        }

        [Fact]
        public void SearchTree_TopBottomNodesBothGlobbing()
        {
            // Arrange
            using var mockBaseDirectory = TestDirectory.Create();
            var configPath1 = Path.Combine(mockBaseDirectory, "NuGet.Config");
            SettingsTestUtils.CreateConfigurationFile(configPath1, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageNamespaces>
        <packageSource key=""source1"">
            <namespace id=""NuGet.*"" />
        </packageSource>
        <packageSource key=""source2"">
            <namespace id=""NuGet.Common.*"" />
        </packageSource>
    </packageNamespaces>
</configuration>");
            var settings = Settings.LoadSettingsGivenConfigPaths(new string[] { configPath1 });

            // Act & Assert
            var configuration = PackageNamespacesConfiguration.GetPackageNamespacesConfiguration(settings);
            Assert.True(configuration.AreNamespacesEnabled);
            configuration.Namespaces.Should().HaveCount(2);

            var configuredSources = configuration.GetConfiguredPackageSources("NuGet.Common1");
            Assert.Equal(1, configuredSources.Count);
            Assert.Equal("source1", configuredSources.First());

            configuredSources = configuration.GetConfiguredPackageSources("NuGet.Common");
            Assert.Equal(1, configuredSources.Count);
            Assert.Equal("source1", configuredSources.First());

            configuredSources = configuration.GetConfiguredPackageSources("NuGet.Common.Package.Id");
            Assert.Equal(1, configuredSources.Count);
            Assert.Equal("source2", configuredSources.First());
        }

        [Fact]
        public void SearchTree_InvalidInput_Throws()
        {
            // Arrange
            using var mockBaseDirectory = TestDirectory.Create();
            var configPath1 = Path.Combine(mockBaseDirectory, "NuGet.Config");
            SettingsTestUtils.CreateConfigurationFile(configPath1, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageNamespaces>
        <packageSource key=""source1"">
            <namespace id=""NuGet.*"" />
        </packageSource>
    </packageNamespaces>
</configuration>");
            var settings = Settings.LoadSettingsGivenConfigPaths(new string[] { configPath1 });

            // Act & Assert
            var configuration = PackageNamespacesConfiguration.GetPackageNamespacesConfiguration(settings);

            var exception = Assert.Throws<ArgumentException>(
                () => configuration.GetConfiguredPackageSources(null));

            Assert.Equal("Argument cannot be null, empty, or white space only." + Environment.NewLine + "Parameter name: term", exception.Message);

            exception = Assert.Throws<ArgumentException>(
                () => configuration.GetConfiguredPackageSources(string.Empty));

            Assert.Equal("Argument cannot be null, empty, or white space only." + Environment.NewLine + "Parameter name: term", exception.Message);

            exception = Assert.Throws<ArgumentException>(
                () => configuration.GetConfiguredPackageSources(" "));

            Assert.Equal("Argument cannot be null, empty, or white space only." + Environment.NewLine + "Parameter name: term", exception.Message);
        }
    }
}
