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
            configuration.Namespaces.Should().HaveCount(1);
            var packageSourcesMatchFull = configuration.GetPrefixMatchPackageSourceNames("stuff");
            Assert.True(packageSourcesMatchFull.PackageNamespaceSectionPresent);
            Assert.Equal(1, packageSourcesMatchFull.PackageSourceNames.Count);
            Assert.Equal("nuget.org", packageSourcesMatchFull.PackageSourceNames.First());

            var packageSourcesMatchPartial = configuration.GetPrefixMatchPackageSourceNames("stu");
            Assert.True(packageSourcesMatchPartial.PackageNamespaceSectionPresent);
            Assert.Null(packageSourcesMatchPartial.PackageSourceNames);

            var packageSourcesNoMatch = configuration.GetPrefixMatchPackageSourceNames("random");
            Assert.True(packageSourcesNoMatch.PackageNamespaceSectionPresent);
            Assert.Null(packageSourcesNoMatch.PackageSourceNames);
        }

        [Fact]
        public void SearchTree_WithOneSourceMultipart()
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
            configuration.Namespaces.Should().HaveCount(1);

            // No match
            var packageSourcesMatchPartial1 = configuration.GetPrefixMatchPackageSourceNames("Cont");
            Assert.True(packageSourcesMatchPartial1.PackageNamespaceSectionPresent);
            Assert.Null(packageSourcesMatchPartial1.PackageSourceNames);

            // No match
            var packageSourcesMatchPartial2 = configuration.GetPrefixMatchPackageSourceNames("Contoso.Opensource");
            Assert.True(packageSourcesMatchPartial2.PackageNamespaceSectionPresent);
            Assert.Null(packageSourcesMatchPartial2.PackageSourceNames);

            // Match
            var packageSourcesMatchFull1 = configuration.GetPrefixMatchPackageSourceNames("Contoso.Opensource.MVC");
            Assert.True(packageSourcesMatchFull1.PackageNamespaceSectionPresent);
            Assert.Equal(1, packageSourcesMatchFull1.PackageSourceNames.Count);
            Assert.Equal("publicrepository", packageSourcesMatchFull1.PackageSourceNames.First());

            // Match
            var packageSourcesMatchFull2 = configuration.GetPrefixMatchPackageSourceNames("Contoso.Opensource.MVC.ASP");
            Assert.True(packageSourcesMatchFull2.PackageNamespaceSectionPresent);
            Assert.Equal(1, packageSourcesMatchFull2.PackageSourceNames.Count);
            Assert.Equal("publicrepository", packageSourcesMatchFull2.PackageSourceNames.First());

            // No match
            var packageSourcesNoMatch = configuration.GetPrefixMatchPackageSourceNames("random");
            Assert.True(packageSourcesNoMatch.PackageNamespaceSectionPresent);
            Assert.Null(packageSourcesNoMatch.PackageSourceNames);
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
            configuration.Namespaces.Should().HaveCount(3);

            var packageSourcesMatchFull1 = configuration.GetPrefixMatchPackageSourceNames("stuff");
            Assert.True(packageSourcesMatchFull1.PackageNamespaceSectionPresent);
            Assert.Equal(1, packageSourcesMatchFull1.PackageSourceNames.Count);
            Assert.Equal("nuget.org", packageSourcesMatchFull1.PackageSourceNames.First());

            var packageSourcesMatchPartial1 = configuration.GetPrefixMatchPackageSourceNames("stu");
            Assert.True(packageSourcesMatchPartial1.PackageNamespaceSectionPresent);
            Assert.Null(packageSourcesMatchPartial1.PackageSourceNames);

            var packageSourcesMatchFull2 = configuration.GetPrefixMatchPackageSourceNames("moreStuff");
            Assert.True(packageSourcesMatchFull2.PackageNamespaceSectionPresent);
            Assert.Equal(1, packageSourcesMatchFull2.PackageSourceNames.Count);
            Assert.Equal("contoso", packageSourcesMatchFull2.PackageSourceNames.First());

            var packageSourcesMatchPartial2 = configuration.GetPrefixMatchPackageSourceNames("PrivateTest");
            Assert.True(packageSourcesMatchPartial2.PackageNamespaceSectionPresent);
            Assert.Equal(1, packageSourcesMatchPartial2.PackageSourceNames.Count);
            Assert.Equal("privaterepository", packageSourcesMatchPartial2.PackageSourceNames.First());

            var packageSourcesNoMatch = configuration.GetPrefixMatchPackageSourceNames("random");
            Assert.True(packageSourcesNoMatch.PackageNamespaceSectionPresent);
            Assert.Null(packageSourcesNoMatch.PackageSourceNames);
        }

        [Fact]
        public void SearchTree_WithMultipleSourcesMultiparts()
        {
            // Arrange
            using var mockBaseDirectory = TestDirectory.Create();
            var configPath1 = Path.Combine(mockBaseDirectory, "NuGet.Config");
            SettingsTestUtils.CreateConfigurationFile(configPath1, @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageNamespaces>
        <packageSource key=""PublicRepository""> 
            <namespace id=""Contoso.Public.*"" />
            <namespace id=""Contoso.Opensource.*"" />
        </packageSource>
        <packageSource key=""PrivateRepository"">
            <namespace id=""Contoso.Opensource"" />
        </packageSource>
        <packageSource key=""SharedRepository"">
            <namespace id=""Contoso.MVC*"" />
        </packageSource>
        <packageSource key=""MetaRepository"">
            <namespace id=""meta.cache*"" />
        </packageSource>
    </packageNamespaces>
</configuration>");
            var settings = Settings.LoadSettingsGivenConfigPaths(new string[] { configPath1 });

            // Act & Assert
            var configuration = PackageNamespacesConfiguration.GetPackageNamespacesConfiguration(settings);
            configuration.Namespaces.Should().HaveCount(4);

            var packageSourcesMatchPartial1 = configuration.GetPrefixMatchPackageSourceNames("Contoso");
            Assert.True(packageSourcesMatchPartial1.PackageNamespaceSectionPresent);
            Assert.Null(packageSourcesMatchPartial1.PackageSourceNames);

            var packageSourcesMatchPartial2 = configuration.GetPrefixMatchPackageSourceNames("Contoso.Opensource");
            Assert.Equal(1, packageSourcesMatchPartial2.PackageSourceNames.Count);
            Assert.Equal("privaterepository", packageSourcesMatchPartial2.PackageSourceNames.First());

            var packageSourcesMatchFull2 = configuration.GetPrefixMatchPackageSourceNames("Contoso.MVC");
            Assert.True(packageSourcesMatchFull2.PackageNamespaceSectionPresent);
            Assert.Equal(1, packageSourcesMatchFull2.PackageSourceNames.Count);
            Assert.Equal("sharedrepository", packageSourcesMatchFull2.PackageSourceNames.First());

            var packageSourcesMatchFull3 = configuration.GetPrefixMatchPackageSourceNames("meta.cache");
            Assert.True(packageSourcesMatchFull3.PackageNamespaceSectionPresent);
            Assert.Equal(1, packageSourcesMatchFull3.PackageSourceNames.Count);
            Assert.Equal("metarepository", packageSourcesMatchFull3.PackageSourceNames.First());


            var packageSourcesMatchFull4 = configuration.GetPrefixMatchPackageSourceNames("meta.cache.test");
            Assert.True(packageSourcesMatchFull4.PackageNamespaceSectionPresent);
            Assert.Equal(1, packageSourcesMatchFull4.PackageSourceNames.Count);
            Assert.Equal("metarepository", packageSourcesMatchFull4.PackageSourceNames.First());

            var packageSourcesNoMatch = configuration.GetPrefixMatchPackageSourceNames("random");
            Assert.True(packageSourcesNoMatch.PackageNamespaceSectionPresent);
            Assert.Null(packageSourcesNoMatch.PackageSourceNames);
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
            configuration.Namespaces.Should().HaveCount(0);

            var packageSourcesMatchPartial = configuration.GetPrefixMatchPackageSourceNames("stuff");
            Assert.False(packageSourcesMatchPartial.PackageNamespaceSectionPresent);
            Assert.Null(packageSourcesMatchPartial.PackageSourceNames);
        }
    }
}
