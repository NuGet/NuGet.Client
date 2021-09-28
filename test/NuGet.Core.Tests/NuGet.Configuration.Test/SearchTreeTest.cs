// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Test.Utility;
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
        public void SearchTree_WithOneSource_Match(string packagePatterns, string term)
        {
            // Arrange
            var configuration = PackageSourceMappingUtility.GetpackageSourceMapping(packagePatterns);
            SearchTree searchTree = new SearchTree(configuration);

            // Act & Assert
            configuration.IsEnabled.Should().BeTrue();
            IReadOnlyList<string> configuredSources = searchTree.GetConfiguredPackageSources(term);
            Assert.Equal(1, configuredSources.Count);
            Assert.Equal(configuration.Patterns.Keys.First().Trim(), configuredSources[0]);
        }

        [Theory]
        [InlineData("public,nuget", "nuge")]
        [InlineData("public,nuget", "nuget1")]
        [InlineData("public,Contoso.Opensource.*", "Cont")]
        [InlineData("public,Contoso.Opensource.*", "Contoso.Opensource")]
        [InlineData(" public , Contoso.Opensource.* ", " Contoso.Opensource ")]
        public void SearchTree_WithOneSource_NoMatch(string packagePatterns, string term)
        {
            // Arrange
            SearchTree searchTree = GetSearchTree(packagePatterns);

            // Act & Assert
            IReadOnlyList<string> configuredSources = searchTree.GetConfiguredPackageSources(term);
            Assert.Null(configuredSources);
        }

        [Theory]
        [InlineData("nuget.org,nuget|privateRepository,private*", "nuget")]
        [InlineData("nuget.org,nuget|privateRepository,private*", "private.")]
        [InlineData(" nuget.org , nuget | privateRepository , private* ", " private. ")]
        public void SearchTree_WithMultipleSources_Match(string packagePatterns, string term)
        {
            // Arrange
            var configuration = PackageSourceMappingUtility.GetpackageSourceMapping(packagePatterns);
            SearchTree searchTree = new SearchTree(configuration);

            // Act & Assert
            IReadOnlyList<string> configuredSources = searchTree.GetConfiguredPackageSources(term);
            Assert.Equal(1, configuredSources.Count);
            Assert.True(configuredSources[0].StartsWith(term.Trim().Substring(0, 5)));
        }

        [Theory]
        [InlineData(" Encyclopaedia , encyclopaedia* | encyclopædia , encyclopædia* ", " encyclopaedia ")]
        [InlineData(" encyclopaedia , Encyclopaedia* | encyclopædia , encyclopædia* ", " encyclopædia. ")]
        [InlineData(" encyclopaedia , encyclopaedia* | encyclopedia , encyclopedia* ", "ENCYCLOPEDIA.")]
        public void SearchTree_InternationalSources_MatchesWithOne(string packagePatterns, string term)
        {
            // Arrange
            var configuration = PackageSourceMappingUtility.GetpackageSourceMapping(packagePatterns);
            SearchTree searchTree = new SearchTree(configuration);

            // Act & Assert
            IReadOnlyList<string> configuredSources = searchTree.GetConfiguredPackageSources(term);
            Assert.Equal(1, configuredSources.Count);
            Assert.True(term.Trim().StartsWith(configuredSources[0], StringComparison.OrdinalIgnoreCase));
        }

        [Theory]
        [InlineData("nuget.org,nuget|privateRepository,private*", "nuge")]
        [InlineData("nuget.org,nuget|privateRepository,private*", "nuget1")]
        [InlineData("nuget.org,nuget|privateRepository,private*", "privat")]
        [InlineData(" nuget.org , nuget | privateRepository , private* ", " privat ")]
        public void SearchTree_WithMultipleSources_NoMatch(string packagePatterns, string term)
        {
            // Arrange
            SearchTree searchTree = GetSearchTree(packagePatterns);

            // Act & Assert
            IReadOnlyList<string> configuredSources = searchTree.GetConfiguredPackageSources(term);
            Assert.Null(configuredSources);
        }

        [Theory]
        [InlineData("", "nuget")]
        [InlineData(" ", "nuget")]
        [InlineData("  ", "nuget")]
        public void SearchTree_NoSources_NoMatch(string packagePatterns, string term)
        {
            // Arrange
            SearchTree searchTree = GetSearchTree(packagePatterns);

            // Act & Assert
            IReadOnlyList<string> configuredSources = searchTree.GetConfiguredPackageSources(term);
            Assert.Null(configuredSources);
        }

        [Theory]
        [InlineData("nuget", "nuget")]
        [InlineData(" , nuget ", " nuget ")]
        [InlineData(",nuget", "nuget")]
        [InlineData(",", "nuget")]
        [InlineData(" ,", "nuget")]
        [InlineData(", ", "nuget")]
        [InlineData(" , ", "nuget")]
        [InlineData(" , |, ", "nuget")]
        [InlineData(" , | , ", "nuget")]
        public void SearchTree_MalformedSources_NoMatch(string packagePatterns, string term)
        {
            // Arrange
            SearchTree searchTree = GetSearchTree(packagePatterns);

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
        [InlineData("source1,nuget.*|source2,nuget.common|source3,nuGet.common.identity", "nu")]
        [InlineData(" source1 , nuget.* | source2 , nuget.common | source3 , nuGet.common.identity ", " nu ")]
        [InlineData("source1,nuget.*|source2,nuget.common|source3,nuGet.common.identity", "nuget")]
        [InlineData(" source1 , nuget.* | source2 , nuget.common | source3 , nuGet.common.identity ", " nuget ")]
        public void SearchTree_TopNodeIsGlobbing_NoMatch(string packagePatterns, string term)
        {
            // Arrange
            SearchTree searchTree = GetSearchTree(packagePatterns);

            // Act & Assert
            var packageSourcesMatch = searchTree.GetConfiguredPackageSources(term);
            Assert.Null(packageSourcesMatch);
        }

        [Theory]
        [InlineData("nuget.org,nuget", null)]
        [InlineData("nuget.org,nuget", "")]
        [InlineData("nuget.org,nuget", " ")]
        [InlineData(" nuget.org , nuget", " ")]
        public void SearchTree_InvalidSearchInput_Throws(string packagePatterns, string term)
        {
            // Arrange
            var configuration = PackageSourceMappingUtility.GetpackageSourceMapping(packagePatterns);

            // Act & Assert
            configuration.IsEnabled.Should().BeTrue();

            var exception = Assert.Throws<ArgumentException>(
                () => configuration.GetConfiguredPackageSources(term));

            Assert.Equal("Argument cannot be null, empty, or whitespace only." + Environment.NewLine + "Parameter name: term", exception.Message);
        }

        private SearchTree GetSearchTree(string packagePatterns)
        {
            return new SearchTree(PackageSourceMappingUtility.GetpackageSourceMapping(packagePatterns));
        }
    }
}
