// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace NuGet.Configuration.Test
{
    public class SearchTreeTest
    {
        [Theory]
        [InlineData("public, Nuget", "Nuget")]
        [InlineData("public, nuget", "Nuget")]
        [InlineData("public, Nuget", "nuget")]
        [InlineData("public, nuget", "nuget")]
        [InlineData("public, Contoso.Opensource.*", "Contoso.Opensource.")]
        [InlineData("public, Contoso.Opensource.*", "Contoso.Opensource.MVC")]
        [InlineData("public, Contoso.Opensource.*", "Contoso.Opensource.MVC.ASP")]
        public void SearchTree_WithOneSource_Match(string packageNamespaces, string term)
        {
            // Arrange
            PackageNamespacesConfiguration configuration = GetPackageNamespacesConfiguration(packageNamespaces);
            SearchTree searchTree = new SearchTree(configuration);

            // Act & Assert
            configuration.AreNamespacesEnabled.Should().BeTrue();
            IReadOnlyList<string> configuredSources = searchTree.GetConfiguredPackageSources(term);
            Assert.Equal(1, configuredSources.Count);
            Assert.Equal(configuration.Namespaces.Keys.First(), configuredSources[0]);
        }

        [Theory]
        [InlineData("public, nuget", "nuge")]
        [InlineData("public, nuget", "nuget1")]
        [InlineData("public, Contoso.Opensource.*", "Cont")]
        [InlineData("public, Contoso.Opensource.*", "Contoso.Opensource")]
        public void SearchTree_WithOneSource_NoMatch(string packageNamespaces, string term)
        {
            // Arrange
            SearchTree searchTree = GetSearchTree(packageNamespaces);

            // Act & Assert
            IReadOnlyList<string> configuredSources = searchTree.GetConfiguredPackageSources(term);
            Assert.Null(configuredSources);
        }

        [Theory]
        [InlineData("nuget.org, nuget | privateRepository, private*", "nuget")]
        [InlineData("nuget.org, nuget |  privateRepository, private*", "private.")]
        public void SearchTree_WithMultipleSources_Match(string packageNamespaces, string term)
        {
            // Arrange
            PackageNamespacesConfiguration configuration = GetPackageNamespacesConfiguration(packageNamespaces);
            SearchTree searchTree = new SearchTree(configuration);

            // Act & Assert
            IReadOnlyList<string> configuredSources = searchTree.GetConfiguredPackageSources(term);
            Assert.Equal(1, configuredSources.Count);
            Assert.True(configuredSources[0].StartsWith(term.Substring(0, 5)));
        }

        [Theory]
        [InlineData("nuget.org,nuget | privateRepository,private*", "nuge")]
        [InlineData("nuget.org,nuget | privateRepository,private*", "nuget1")]
        [InlineData("nuget.org,nuget |  privateRepository,private*", "privat")]
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
        public void SearchTree_NoSources_NoMatch(string packageNamespaces, string term)
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
            SearchTree searchTree = GetSearchTree("source1, nuget.* |  source2, nuget.common,nuget.protocol.* | source3, nuget.common.identity");

            // Act & Assert
            IReadOnlyList<string> configuredSources = searchTree.GetConfiguredPackageSources("NuGet.Common1");
            Assert.Equal(1, configuredSources.Count);
            Assert.Equal("source1", configuredSources.First());

            configuredSources = searchTree.GetConfiguredPackageSources("NuGet.Id");
            Assert.Equal(1, configuredSources.Count);
            Assert.Equal("source1", configuredSources.First());

            configuredSources = searchTree.GetConfiguredPackageSources("NuGet.Common");
            Assert.Equal(1, configuredSources.Count);
            Assert.Equal("source2", configuredSources.First());

            configuredSources = searchTree.GetConfiguredPackageSources("NuGet.Common.Id");
            Assert.Equal(1, configuredSources.Count);
            Assert.Equal("source1", configuredSources.First());

            configuredSources = searchTree.GetConfiguredPackageSources("NuGet.Common.Identity");
            Assert.Equal(1, configuredSources.Count);
            Assert.Equal("source3", configuredSources.First());

            configuredSources = searchTree.GetConfiguredPackageSources("NuGet.Common.Identity.A");
            Assert.Equal(1, configuredSources.Count);
            Assert.Equal("source1", configuredSources.First());

            configuredSources = searchTree.GetConfiguredPackageSources("NuGet.Protocol1");
            Assert.Equal(1, configuredSources.Count);
            Assert.Equal("source1", configuredSources.First());

            configuredSources = searchTree.GetConfiguredPackageSources("NuGet.Protocol.Package.Id");
            Assert.Equal(1, configuredSources.Count);
            Assert.Equal("source2", configuredSources.First());
        }

        [Theory]
        [InlineData("source1, nuget.* |  source2, nuget.common | source3, nuGet.common.identity", "nu")]
        [InlineData("source1, nuget.* |  source2, nuget.common | source3, nuGet.common.identity", "nuget")]
        public void SearchTree_TopNodeIsGlobbing_NoMatch(string packageNamespaces, string term)
        {
            // Arrange
            SearchTree searchTree = GetSearchTree(packageNamespaces);

            // Act & Assert
            var packageSourcesMatch = searchTree.GetConfiguredPackageSources(term);
            Assert.Null(packageSourcesMatch);
        }

        [Theory]
        [InlineData("nuget.org, nuget", null)]
        [InlineData("nuget.org, nuget", "")]
        [InlineData("nuget.org, nuget", " ")]
        public void SearchTree_InvalidInput_Throws(string packageNamespaces, string term)
        {
            // Arrange
            PackageNamespacesConfiguration configuration = GetPackageNamespacesConfiguration(packageNamespaces);

            // Act & Assert
            configuration.AreNamespacesEnabled.Should().BeTrue();

            var exception = Assert.Throws<ArgumentException>(
                () => configuration.GetConfiguredPackageSources(term));

            Assert.Equal("Argument cannot be null, empty, or whitespace only." + Environment.NewLine + "Parameter name: term", exception.Message);
        }

        private SearchTree GetSearchTree(string packageNamespaces)
        {
            return new SearchTree(GetPackageNamespacesConfiguration(packageNamespaces));
        }

        private PackageNamespacesConfiguration GetPackageNamespacesConfiguration(string packageNamespaces)
        {
            string[] sections = packageNamespaces.Split('|');
            var namespaces = new Dictionary<string, IReadOnlyList<string>>();

            foreach (string section in sections)
            {
                string[] parts = section.Split(',');
                string sourceKey = parts[0].Trim();

                if (string.IsNullOrEmpty(sourceKey))
                {
                    continue;
                }

                var namespaceList = new List<string>();
                for (int i = 1; i < parts.Length; i++)
                {

                    namespaceList.Add(parts[i].Trim());
                }

                namespaces[sourceKey] = namespaceList;
            }

            return new PackageNamespacesConfiguration(namespaces);
        }
    }
}
