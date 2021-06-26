// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Test.Utility;
using Moq;
using Xunit;

namespace NuGet.Configuration.Test
{
    public class SearchTreeTest
    {
        [Theory]
        [InlineData("public,Nuget", "Nuget")]
        [InlineData("public,nuget", "Nuget")]
        [InlineData("public,Nuget", "nuget")]
        [InlineData("public,nuget", "nuget")]
        [InlineData("public,nuget", " nuget")]
        [InlineData("public,nuget", "nuget ")]
        [InlineData("public,nuget", " nuget ")]
        [InlineData("public, nuget", "nuget")]
        [InlineData("public,nuget ", "nuget")]
        [InlineData("public, nuget ", "nuget")]
        [InlineData("public, nuget ", " nuget ")]
        [InlineData(" public , nuget ", " nuget ")]
        [InlineData("   public    ,    nuget    ", "   nuget   ")]
        [InlineData("public,Contoso.Opensource.*", "Contoso.Opensource.")]
        [InlineData("public,Contoso.Opensource.*", "Contoso.Opensource.MVC")]
        [InlineData("public,Contoso.Opensource.*", "Contoso.Opensource.MVC.ASP")]
        [InlineData("public,Contoso.Opensource.* ", "Contoso.Opensource.MVC.ASP")]
        [InlineData(" public,Contoso.Opensource.*", "Contoso.Opensource.MVC.ASP")]
        [InlineData(" public,Contoso.Opensource.* ", " Contoso.Opensource.MVC.ASP ")]
        public void SearchTree_WithOneSource_Match(string packageNamespaces, string term)
        {
            // Arrange
            PackageNamespacesConfiguration configuration = PackageNamespacesConfigurationUtility.GetPackageNamespacesConfiguration(packageNamespaces);
            SearchTree searchTree = new SearchTree(configuration);

            // Act & Assert
            configuration.AreNamespacesEnabled.Should().BeTrue();
            IReadOnlyList<string> configuredSources = searchTree.GetConfiguredPackageSources(term);
            Assert.Equal(1, configuredSources.Count);
            Assert.Equal(configuration.Namespaces.Keys.First().Trim() + ".com", configuredSources[0]);
        }

        [Theory]
        [InlineData("public,nuget", "nuge")]
        [InlineData("public,nuget", "nuget1")]
        [InlineData("public,Contoso.Opensource.*", "Cont")]
        [InlineData("public,Contoso.Opensource.*", "Contoso.Opensource")]
        [InlineData(" public , Contoso.Opensource.* ", " Contoso.Opensource ")]
        public void SearchTree_WithOneSource_NoMatch(string packageNamespaces, string term)
        {
            // Arrange
            SearchTree searchTree = GetSearchTree(packageNamespaces);

            // Act & Assert
            IReadOnlyList<string> configuredSources = searchTree.GetConfiguredPackageSources(term);
            Assert.Null(configuredSources);
        }

        [Theory]
        [InlineData("nuget.org,nuget|privateRepository,private*", "nuget")]
        [InlineData("nuget.org,nuget|privateRepository,private*", "private.")]
        [InlineData(" nuget.org , nuget | privateRepository , private* ", " private. ")]
        public void SearchTree_WithMultipleSources_Match(string packageNamespaces, string term)
        {
            // Arrange
            PackageNamespacesConfiguration configuration = PackageNamespacesConfigurationUtility.GetPackageNamespacesConfiguration(packageNamespaces);
            SearchTree searchTree = new SearchTree(configuration);

            // Act & Assert
            IReadOnlyList<string> configuredSources = searchTree.GetConfiguredPackageSources(term);
            Assert.Equal(1, configuredSources.Count);
            Assert.True(configuredSources[0].StartsWith(term.Trim().Substring(0, 5)));
        }

        [Theory]
        [InlineData("nuget.org,nuget|privateRepository,private*", "nuge")]
        [InlineData("nuget.org,nuget|privateRepository,private*", "nuget1")]
        [InlineData("nuget.org,nuget|privateRepository,private*", "privat")]
        [InlineData(" nuget.org , nuget | privateRepository , private* ", " privat ")]
        public void SearchTree_WithMultipleSources_NoMatch(string packageNamespaces, string term)
        {
            // Arrange
            SearchTree searchTree = GetSearchTree(packageNamespaces);

            // Act & Assert
            IReadOnlyList<string> configuredSources = searchTree.GetConfiguredPackageSources(term);
            Assert.Null(configuredSources);
        }

        [Theory]
        [InlineData("", "nuget")]
        [InlineData(" ", "nuget")]
        [InlineData("  ", "nuget")]
        public void SearchTree_NoSources_NoMatch(string packageNamespaces, string term)
        {
            // Arrange
            SearchTree searchTree = GetSearchTree(packageNamespaces);

            // Act & Assert
            IReadOnlyList<string> configuredSources = searchTree.GetConfiguredPackageSources(term);
            Assert.Null(configuredSources);
        }

        [Theory]
        [InlineData(" , nuget ", " nuget ")]
        [InlineData(",nuget", "nuget")]
        [InlineData(",", "nuget")]
        [InlineData(" ,", "nuget")]
        [InlineData(", ", "nuget")]
        [InlineData(" , ", "nuget")]
        [InlineData(" , |, ", "nuget")]
        [InlineData(" , | , ", "nuget")]
        public void SearchTree_MalformedSources_NoMatch(string packageNamespaces, string term)
        {
            // Arrange
            SearchTree searchTree = GetSearchTree(packageNamespaces);

            // Act & Assert
            IReadOnlyList<string> configuredSources = searchTree.GetConfiguredPackageSources(term);
            Assert.Null(configuredSources);
        }

        [Fact]
        public void SearchTree_TopNodeIsGlobbing_Match()
        {
            // Arrange
            SearchTree searchTree = GetSearchTree("source1,nuget.*|source2,nuget.common,nuget.protocol.*|source3,nuget.common.identity");

            // Act & Assert
            IReadOnlyList<string> configuredSources = searchTree.GetConfiguredPackageSources("NuGet.Common1");
            Assert.Equal(1, configuredSources.Count);
            Assert.Equal("source1.com", configuredSources.First());

            configuredSources = searchTree.GetConfiguredPackageSources("NuGet.Id");
            Assert.Equal(1, configuredSources.Count);
            Assert.Equal("source1.com", configuredSources.First());

            configuredSources = searchTree.GetConfiguredPackageSources("NuGet.Common");
            Assert.Equal(1, configuredSources.Count);
            Assert.Equal("source2.com", configuredSources.First());

            configuredSources = searchTree.GetConfiguredPackageSources("NuGet.Common.Id");
            Assert.Equal(1, configuredSources.Count);
            Assert.Equal("source1.com", configuredSources.First());

            configuredSources = searchTree.GetConfiguredPackageSources("NuGet.Common.Identity");
            Assert.Equal(1, configuredSources.Count);
            Assert.Equal("source3.com", configuredSources.First());

            configuredSources = searchTree.GetConfiguredPackageSources("NuGet.Common.Identity.A");
            Assert.Equal(1, configuredSources.Count);
            Assert.Equal("source1.com", configuredSources.First());

            configuredSources = searchTree.GetConfiguredPackageSources("NuGet.Protocol1");
            Assert.Equal(1, configuredSources.Count);
            Assert.Equal("source1.com", configuredSources.First());

            configuredSources = searchTree.GetConfiguredPackageSources("NuGet.Protocol.Package.Id");
            Assert.Equal(1, configuredSources.Count);
            Assert.Equal("source2.com", configuredSources.First());
        }

        [Theory]
        [InlineData("source1,nuget.*|source2,nuget.common|source3,nuGet.common.identity", "nu")]
        [InlineData(" source1 , nuget.* | source2 , nuget.common | source3 , nuGet.common.identity ", " nu ")]
        [InlineData("source1,nuget.*|source2,nuget.common|source3,nuGet.common.identity", "nuget")]
        [InlineData(" source1 , nuget.* | source2 , nuget.common | source3 , nuGet.common.identity ", " nuget ")]
        public void SearchTree_TopNodeIsGlobbing_NoMatch(string packageNamespaces, string term)
        {
            // Arrange
            SearchTree searchTree = GetSearchTree(packageNamespaces);

            // Act & Assert
            var packageSourcesMatch = searchTree.GetConfiguredPackageSources(term);
            Assert.Null(packageSourcesMatch);
        }

        [Theory]
        [InlineData("test.org,nuget", null)]
        [InlineData("test.org,nuget", "")]
        [InlineData("test.org,nuget", " ")]
        [InlineData(" test.org , nuget", " ")]
        public void SearchTree_InvalidSearchInput_Throws(string packageNamespaces, string term)
        {
            // Arrange
            PackageNamespacesConfiguration configuration = PackageNamespacesConfigurationUtility.GetPackageNamespacesConfiguration(packageNamespaces);

            // Act & Assert
            configuration.AreNamespacesEnabled.Should().BeTrue();

            var exception = Assert.Throws<ArgumentException>(
                () => configuration.GetConfiguredPackageSources(term));

            Assert.Equal("Argument cannot be null, empty, or whitespace only." + Environment.NewLine + "Parameter name: term", exception.Message);
        }

        private SearchTree GetSearchTree(string packageNamespaces)
        {
            return new SearchTree(PackageNamespacesConfigurationUtility.GetPackageNamespacesConfiguration(packageNamespaces));
        }

        private PackageNamespacesConfiguration GetPackageNamespacesConfiguration(string packageNamespaces)
        {
            string[] sections = packageNamespaces.Split('|');
            var sourceKeys = new HashSet<string>();
            var namespaces = new Dictionary<string, IReadOnlyList<string>>();

            var namespacesList = new List<PackageNamespacesSourceItem>();

            foreach (string section in sections)
            {
                string[] parts = section.Split(',');
                string sourceKey = parts[0];

                if (string.IsNullOrWhiteSpace(sourceKey))
                {
                    continue;
                }

                sourceKeys.Add(sourceKey);

                var namespaceItems = new List<NamespaceItem>();
                for (int i = 1; i < parts.Length; i++)
                {
                    namespaceItems.Add(new NamespaceItem(parts[i]));
                }

                namespacesList.Add(new PackageNamespacesSourceItem(sourceKey, namespaceItems));
            }

            var packageSourcesVirtualSection = new VirtualSettingSection(ConfigurationConstants.PackageSources,
                sourceKeys.Select(ns => new SourceItem(ns, ns.Trim() + ".com")).ToArray()
                );

            var packageNamespacesVirtualSection = new VirtualSettingSection(ConfigurationConstants.PackageNamespaces,
                namespacesList.ToArray()
                );

            var settings = new Moq.Mock<ISettings>(Moq.MockBehavior.Loose);


            settings.Setup(s => s.GetSection("packageSources"))
                .Returns(packageSourcesVirtualSection)
                .Verifiable();
            settings.Setup(s => s.GetConfigFilePaths())
                .Returns(new List<string>());
            settings.Setup(s => s.GetSection(ConfigurationConstants.DisabledPackageSources))
                .Returns(new VirtualSettingSection(ConfigurationConstants.DisabledPackageSources))
                .Verifiable();
            settings.Setup(s => s.GetSection(ConfigurationConstants.PackageNamespaces))
                    .Returns(packageNamespacesVirtualSection);

            return PackageNamespacesConfiguration.GetPackageNamespacesConfiguration(settings.Object);
        }
    }
}
