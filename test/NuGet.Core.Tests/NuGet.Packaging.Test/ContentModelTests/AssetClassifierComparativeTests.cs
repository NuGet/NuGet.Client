// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NuGet.Client;
using NuGet.ContentModel;
using NuGet.Frameworks;
using NuGet.RuntimeModel;
using Xunit;

namespace NuGet.Packaging.Test.ContentModelTests
{
    /// <summary>
    /// Comparative tests that verify the new AssetClassifier produces identical results
    /// to the existing PatternSet-based pattern matching.
    /// </summary>
    public class AssetClassifierComparativeTests
    {
        private static ManagedCodeConventions CreateConventions()
        {
            return new ManagedCodeConventions(new RuntimeGraph());
        }

        #region lib/ comparative tests

        [Theory]
        [InlineData("lib/net472/MyAssembly.dll")]
        [InlineData("lib/netstandard2.0/MyAssembly.dll")]
        [InlineData("lib/net6.0/MyAssembly.dll")]
        [InlineData("lib/net8.0/MyAssembly.exe")]
        [InlineData("lib/net48/MyAssembly.winmd")]
        [InlineData("lib/net472/_._")]
        public void Lib_RuntimeAssemblies_ProducesSameResultAsPatternSet(string path)
        {
            // Arrange
            var conventions = CreateConventions();
            var classifier = conventions.CreateAssetClassifier();
            var collection = new ContentItemCollection();
            collection.Load(new[] { path });

            // Act - Old way using PatternSet
            var oldGroups = new List<ContentItemGroup>();
            collection.PopulateItemGroups(conventions.Patterns.RuntimeAssemblies, oldGroups);

            // Act - New way using AssetClassifier
            var newItems = collection.ClassifyAssets(classifier, AssetType.RuntimeAssembly);

            // Assert - Both should find the item
            oldGroups.Should().HaveCountGreaterThan(0, "PatternSet should find the item");
            newItems.Should().HaveCount(1, "AssetClassifier should find exactly one item");

            // Assert - The properties should match
            var oldItem = oldGroups.SelectMany(g => g.Items).First();
            var newItem = newItems.First();

            oldItem.Path.Should().Be(newItem.Path);

            // Compare TFM
            if (oldItem.TryGetValue("tfm", out object oldTfm))
            {
                newItem.TryGetValue("tfm", out object newTfm).Should().BeTrue();
                oldTfm.Should().Be(newTfm, "TFM should match");
            }

            // Compare assembly
            if (oldItem.TryGetValue("assembly", out object oldAsm))
            {
                newItem.TryGetValue("assembly", out object newAsm).Should().BeTrue();
                oldAsm.Should().Be(newAsm, "Assembly should match");
            }
        }

        [Theory]
        [InlineData("lib/net472/en-US/MyAssembly.resources.dll", "en-US")]
        [InlineData("lib/net6.0/fr/MyAssembly.resources.dll", "fr")]
        [InlineData("lib/netstandard2.0/de-DE/MyAssembly.resources.dll", "de-DE")]
        public void Lib_ResourceAssemblies_ProducesSameResultAsPatternSet(string path, string expectedLocale)
        {
            // Arrange
            var conventions = CreateConventions();
            var classifier = conventions.CreateAssetClassifier();
            var collection = new ContentItemCollection();
            collection.Load(new[] { path });

            // Act - Old way
            var oldGroups = new List<ContentItemGroup>();
            collection.PopulateItemGroups(conventions.Patterns.ResourceAssemblies, oldGroups);

            // Act - New way
            var newItems = collection.ClassifyAssets(classifier, AssetType.ResourceAssembly);

            // Assert
            oldGroups.Should().HaveCountGreaterThan(0, "PatternSet should find resource assembly");
            newItems.Should().HaveCount(1, "AssetClassifier should find resource assembly");

            var oldItem = oldGroups.SelectMany(g => g.Items).First();
            var newItem = newItems.First();

            oldItem.Path.Should().Be(newItem.Path);

            // Verify locale matches
            oldItem.TryGetValue("locale", out object oldLocale).Should().BeTrue();
            newItem.TryGetValue("locale", out object newLocale).Should().BeTrue();
            oldLocale.Should().Be(newLocale);
            newLocale.Should().Be(expectedLocale);
        }

        #endregion

        #region ref/ comparative tests

        [Theory]
        [InlineData("ref/net472/MyAssembly.dll")]
        [InlineData("ref/netstandard2.0/MyAssembly.dll")]
        [InlineData("ref/net6.0/MyAssembly.dll")]
        public void Ref_CompileRefAssemblies_ProducesSameResultAsPatternSet(string path)
        {
            // Arrange
            var conventions = CreateConventions();
            var classifier = conventions.CreateAssetClassifier();
            var collection = new ContentItemCollection();
            collection.Load(new[] { path });

            // Act - Old way
            var oldGroups = new List<ContentItemGroup>();
            collection.PopulateItemGroups(conventions.Patterns.CompileRefAssemblies, oldGroups);

            // Act - New way
            var newItems = collection.ClassifyAssets(classifier, AssetType.CompileRefAssembly);

            // Assert
            oldGroups.Should().HaveCountGreaterThan(0);
            newItems.Should().HaveCount(1);

            var oldItem = oldGroups.SelectMany(g => g.Items).First();
            var newItem = newItems.First();

            oldItem.Path.Should().Be(newItem.Path);
            CompareProperty(oldItem, newItem, "tfm");
            CompareProperty(oldItem, newItem, "assembly");
        }

        #endregion

        #region runtimes/ comparative tests

        [Theory]
        [InlineData("runtimes/win-x64/lib/net472/MyAssembly.dll")]
        [InlineData("runtimes/linux-x64/lib/net6.0/MyAssembly.dll")]
        [InlineData("runtimes/osx-arm64/lib/net8.0/MyAssembly.dll")]
        public void Runtimes_Lib_ProducesSameResultAsPatternSet(string path)
        {
            // Arrange
            var conventions = CreateConventions();
            var classifier = conventions.CreateAssetClassifier();
            var collection = new ContentItemCollection();
            collection.Load(new[] { path });

            // Act - Old way
            var oldGroups = new List<ContentItemGroup>();
            collection.PopulateItemGroups(conventions.Patterns.RuntimeAssemblies, oldGroups);

            // Act - New way
            var newItems = collection.ClassifyAssets(classifier, AssetType.RuntimeAssembly);

            // Assert
            oldGroups.Should().HaveCountGreaterThan(0);
            newItems.Should().HaveCount(1);

            var oldItem = oldGroups.SelectMany(g => g.Items).First();
            var newItem = newItems.First();

            oldItem.Path.Should().Be(newItem.Path);
            CompareProperty(oldItem, newItem, "tfm");
            CompareProperty(oldItem, newItem, "rid");
            CompareProperty(oldItem, newItem, "assembly");
        }

        [Theory]
        [InlineData("runtimes/win-x64/native/MyNative.dll")]
        [InlineData("runtimes/linux-x64/native/libmynative.so")]
        [InlineData("runtimes/osx-arm64/native/libmynative.dylib")]
        public void Runtimes_Native_ProducesSameResultAsPatternSet(string path)
        {
            // Arrange
            var conventions = CreateConventions();
            var classifier = conventions.CreateAssetClassifier();
            var collection = new ContentItemCollection();
            collection.Load(new[] { path });

            // Act - Old way
            var oldGroups = new List<ContentItemGroup>();
            collection.PopulateItemGroups(conventions.Patterns.NativeLibraries, oldGroups);

            // Act - New way
            var newItems = collection.ClassifyAssets(classifier, AssetType.NativeLibrary);

            // Assert
            oldGroups.Should().HaveCountGreaterThan(0);
            newItems.Should().HaveCount(1);

            var oldItem = oldGroups.SelectMany(g => g.Items).First();
            var newItem = newItems.First();

            oldItem.Path.Should().Be(newItem.Path);
            CompareProperty(oldItem, newItem, "rid");
        }

        [Theory]
        [InlineData("runtimes/win-x64/nativeassets/net472/MyNative.dll")]
        [InlineData("runtimes/linux-x64/nativeassets/net6.0/libmynative.so")]
        public void Runtimes_NativeAssets_ProducesSameResultAsPatternSet(string path)
        {
            // Arrange
            var conventions = CreateConventions();
            var classifier = conventions.CreateAssetClassifier();
            var collection = new ContentItemCollection();
            collection.Load(new[] { path });

            // Act - Old way
            var oldGroups = new List<ContentItemGroup>();
            collection.PopulateItemGroups(conventions.Patterns.NativeLibraries, oldGroups);

            // Act - New way
            var newItems = collection.ClassifyAssets(classifier, AssetType.NativeLibrary);

            // Assert
            oldGroups.Should().HaveCountGreaterThan(0);
            newItems.Should().HaveCount(1);

            var oldItem = oldGroups.SelectMany(g => g.Items).First();
            var newItem = newItems.First();

            oldItem.Path.Should().Be(newItem.Path);
            CompareProperty(oldItem, newItem, "tfm");
            CompareProperty(oldItem, newItem, "rid");
        }

        #endregion

        #region build/ comparative tests

        [Theory]
        [InlineData("build/net472/MyPackage.props")]
        [InlineData("build/net6.0/MyPackage.targets")]
        [InlineData("build/MyPackage.props")]
        [InlineData("build/MyPackage.targets")]
        public void Build_MSBuildFiles_ProducesSameResultAsPatternSet(string path)
        {
            // Arrange
            var conventions = CreateConventions();
            var classifier = conventions.CreateAssetClassifier();
            var collection = new ContentItemCollection();
            collection.Load(new[] { path });

            // Act - Old way
            var oldGroups = new List<ContentItemGroup>();
            collection.PopulateItemGroups(conventions.Patterns.MSBuildFiles, oldGroups);

            // Act - New way
            var newItems = collection.ClassifyAssets(classifier, AssetType.MSBuildFile);

            // Assert
            oldGroups.Should().HaveCountGreaterThan(0, $"PatternSet should match {path}");
            newItems.Should().HaveCount(1, $"AssetClassifier should match {path}");

            var oldItem = oldGroups.SelectMany(g => g.Items).First();
            var newItem = newItems.First();

            oldItem.Path.Should().Be(newItem.Path);
            CompareProperty(oldItem, newItem, "msbuild");
        }

        [Theory]
        [InlineData("buildMultiTargeting/MyPackage.props")]
        [InlineData("buildMultiTargeting/MyPackage.targets")]
        public void BuildMultiTargeting_ProducesSameResultAsPatternSet(string path)
        {
            // Arrange
            var conventions = CreateConventions();
            var classifier = conventions.CreateAssetClassifier();
            var collection = new ContentItemCollection();
            collection.Load(new[] { path });

            // Act - Old way
            var oldGroups = new List<ContentItemGroup>();
            collection.PopulateItemGroups(conventions.Patterns.MSBuildMultiTargetingFiles, oldGroups);

            // Act - New way
            var newItems = collection.ClassifyAssets(classifier, AssetType.MSBuildMultiTargetingFile);

            // Assert
            oldGroups.Should().HaveCountGreaterThan(0);
            newItems.Should().HaveCount(1);

            var oldItem = oldGroups.SelectMany(g => g.Items).First();
            var newItem = newItems.First();

            oldItem.Path.Should().Be(newItem.Path);
        }

        [Theory]
        [InlineData("buildTransitive/net472/MyPackage.props")]
        [InlineData("buildTransitive/net6.0/MyPackage.targets")]
        [InlineData("buildTransitive/MyPackage.props")]
        public void BuildTransitive_ProducesSameResultAsPatternSet(string path)
        {
            // Arrange
            var conventions = CreateConventions();
            var classifier = conventions.CreateAssetClassifier();
            var collection = new ContentItemCollection();
            collection.Load(new[] { path });

            // Act - Old way
            var oldGroups = new List<ContentItemGroup>();
            collection.PopulateItemGroups(conventions.Patterns.MSBuildTransitiveFiles, oldGroups);

            // Act - New way
            var newItems = collection.ClassifyAssets(classifier, AssetType.MSBuildTransitiveFile);

            // Assert
            oldGroups.Should().HaveCountGreaterThan(0);
            newItems.Should().HaveCount(1);

            var oldItem = oldGroups.SelectMany(g => g.Items).First();
            var newItem = newItems.First();

            oldItem.Path.Should().Be(newItem.Path);
        }

        #endregion

        #region contentFiles/ comparative tests

        [Theory]
        [InlineData("contentFiles/cs/net472/MyFile.cs")]
        [InlineData("contentFiles/vb/net6.0/MyFile.vb")]
        [InlineData("contentFiles/any/any/MyFile.txt")]
        [InlineData("contentFiles/cs/netstandard2.0/Images/logo.png")]
        public void ContentFiles_ProducesSameResultAsPatternSet(string path)
        {
            // Arrange
            var conventions = CreateConventions();
            var classifier = conventions.CreateAssetClassifier();
            var collection = new ContentItemCollection();
            collection.Load(new[] { path });

            // Act - Old way
            var oldGroups = new List<ContentItemGroup>();
            collection.PopulateItemGroups(conventions.Patterns.ContentFiles, oldGroups);

            // Act - New way
            var newItems = collection.ClassifyAssets(classifier, AssetType.ContentFile);

            // Assert
            oldGroups.Should().HaveCountGreaterThan(0);
            newItems.Should().HaveCount(1);

            var oldItem = oldGroups.SelectMany(g => g.Items).First();
            var newItem = newItems.First();

            oldItem.Path.Should().Be(newItem.Path);
            CompareProperty(oldItem, newItem, "codeLanguage");
            CompareProperty(oldItem, newItem, "tfm");
        }

        #endregion

        #region tools/ comparative tests

        [Theory]
        [InlineData("tools/net472/any/mytool.exe")]
        [InlineData("tools/net6.0/win-x64/mytool.dll")]
        public void Tools_ProducesSameResultAsPatternSet(string path)
        {
            // Arrange
            var conventions = CreateConventions();
            var classifier = conventions.CreateAssetClassifier();
            var collection = new ContentItemCollection();
            collection.Load(new[] { path });

            // Act - Old way
            var oldGroups = new List<ContentItemGroup>();
            collection.PopulateItemGroups(conventions.Patterns.ToolsAssemblies, oldGroups);

            // Act - New way
            var newItems = collection.ClassifyAssets(classifier, AssetType.ToolsAssembly);

            // Assert
            oldGroups.Should().HaveCountGreaterThan(0);
            newItems.Should().HaveCount(1);

            var oldItem = oldGroups.SelectMany(g => g.Items).First();
            var newItem = newItems.First();

            oldItem.Path.Should().Be(newItem.Path);
            CompareProperty(oldItem, newItem, "tfm");
            CompareProperty(oldItem, newItem, "rid");
        }

        #endregion

        #region embed/ comparative tests

        [Theory]
        [InlineData("embed/net472/MyAssembly.dll")]
        [InlineData("embed/net6.0/MyAssembly.dll")]
        public void Embed_ProducesSameResultAsPatternSet(string path)
        {
            // Arrange
            var conventions = CreateConventions();
            var classifier = conventions.CreateAssetClassifier();
            var collection = new ContentItemCollection();
            collection.Load(new[] { path });

            // Act - Old way
            var oldGroups = new List<ContentItemGroup>();
            collection.PopulateItemGroups(conventions.Patterns.EmbedAssemblies, oldGroups);

            // Act - New way
            var newItems = collection.ClassifyAssets(classifier, AssetType.EmbedAssembly);

            // Assert
            oldGroups.Should().HaveCountGreaterThan(0);
            newItems.Should().HaveCount(1);

            var oldItem = oldGroups.SelectMany(g => g.Items).First();
            var newItem = newItems.First();

            oldItem.Path.Should().Be(newItem.Path);
            CompareProperty(oldItem, newItem, "tfm");
            CompareProperty(oldItem, newItem, "assembly");
        }

        #endregion

        #region Complex package tests

        [Fact]
        public void ComplexPackage_AllAssetTypes_ProducesSameResults()
        {
            // Arrange - Simulate a complex package with many asset types
            var conventions = CreateConventions();
            var classifier = conventions.CreateAssetClassifier();
            var collection = new ContentItemCollection();

            var paths = new[]
            {
                // lib assemblies
                "lib/net472/MyLib.dll",
                "lib/net6.0/MyLib.dll",
                "lib/netstandard2.0/MyLib.dll",
                // ref assemblies
                "ref/net472/MyLib.dll",
                "ref/net6.0/MyLib.dll",
                // runtime-specific assemblies
                "runtimes/win-x64/lib/net472/MyLib.dll",
                "runtimes/linux-x64/lib/net6.0/MyLib.dll",
                // native libraries
                "runtimes/win-x64/native/MyNative.dll",
                "runtimes/linux-x64/native/libmynative.so",
                // resource assemblies
                "lib/net472/en-US/MyLib.resources.dll",
                "lib/net472/fr/MyLib.resources.dll",
                // build files
                "build/net472/MyPackage.props",
                "build/net472/MyPackage.targets",
                "build/MyPackage.props",
                // buildTransitive
                "buildTransitive/net472/MyPackage.props",
                // contentFiles
                "contentFiles/cs/net472/MyCode.cs",
                "contentFiles/any/any/readme.txt",
                // tools
                "tools/net472/any/mytool.exe",
                // embed
                "embed/net472/MyEmbed.dll",
            };

            collection.Load(paths);

            // Act & Assert - Compare each asset type
            ComparePatternSetWithClassifier(collection, conventions.Patterns.RuntimeAssemblies, classifier, AssetType.RuntimeAssembly, "RuntimeAssemblies");
            ComparePatternSetWithClassifier(collection, conventions.Patterns.CompileRefAssemblies, classifier, AssetType.CompileRefAssembly, "CompileRefAssemblies");
            ComparePatternSetWithClassifier(collection, conventions.Patterns.NativeLibraries, classifier, AssetType.NativeLibrary, "NativeLibraries");
            ComparePatternSetWithClassifier(collection, conventions.Patterns.ResourceAssemblies, classifier, AssetType.ResourceAssembly, "ResourceAssemblies");
            ComparePatternSetWithClassifier(collection, conventions.Patterns.MSBuildFiles, classifier, AssetType.MSBuildFile, "MSBuildFiles");
            ComparePatternSetWithClassifier(collection, conventions.Patterns.MSBuildTransitiveFiles, classifier, AssetType.MSBuildTransitiveFile, "MSBuildTransitiveFiles");
            ComparePatternSetWithClassifier(collection, conventions.Patterns.ContentFiles, classifier, AssetType.ContentFile, "ContentFiles");
            ComparePatternSetWithClassifier(collection, conventions.Patterns.ToolsAssemblies, classifier, AssetType.ToolsAssembly, "ToolsAssemblies");
            ComparePatternSetWithClassifier(collection, conventions.Patterns.EmbedAssemblies, classifier, AssetType.EmbedAssembly, "EmbedAssemblies");
        }

        private void ComparePatternSetWithClassifier(
            ContentItemCollection collection,
            PatternSet patternSet,
            AssetClassifier classifier,
            AssetType assetType,
            string description)
        {
            // Get old results
            var oldGroups = new List<ContentItemGroup>();
            collection.PopulateItemGroups(patternSet, oldGroups);
            var oldPaths = oldGroups.SelectMany(g => g.Items).Select(i => i.Path).OrderBy(p => p).ToList();

            // Get new results
            var newItems = collection.ClassifyAssets(classifier, assetType);
            var newPaths = newItems.Select(i => i.Path).OrderBy(p => p).ToList();

            // Compare
            newPaths.Should().BeEquivalentTo(oldPaths, $"{description} should produce same paths");
        }

        #endregion

        #region Edge case tests

        [Theory]
        [InlineData("lib/portable-net45+win8/MyAssembly.dll")]
        [InlineData("lib/portable-net45+win8+wp8+wpa81/MyAssembly.dll")]
        public void PortableFrameworks_ProducesSameResults(string path)
        {
            // Arrange
            var conventions = CreateConventions();
            var classifier = conventions.CreateAssetClassifier();
            var collection = new ContentItemCollection();
            collection.Load(new[] { path });

            // Act - Old way
            var oldGroups = new List<ContentItemGroup>();
            collection.PopulateItemGroups(conventions.Patterns.RuntimeAssemblies, oldGroups);

            // Act - New way
            var newItems = collection.ClassifyAssets(classifier, AssetType.RuntimeAssembly);

            // Assert - Both should match (or both should not match)
            var oldCount = oldGroups.SelectMany(g => g.Items).Count();
            newItems.Count.Should().Be(oldCount, $"Both implementations should agree on {path}");
        }

        [Theory]
        [InlineData("lib/net472/agq-CM/MyAssembly.resources.dll", "agq-CM")] // Three-letter language code
        [InlineData("lib/net472/zh-Hans/MyAssembly.resources.dll", "zh-Hans")] // Script subtag
        public void ExoticLocales_ProducesSameResults(string path, string expectedLocale)
        {
            // Arrange
            var conventions = CreateConventions();
            var classifier = conventions.CreateAssetClassifier();
            var collection = new ContentItemCollection();
            collection.Load(new[] { path });

            // Act - Old way
            var oldGroups = new List<ContentItemGroup>();
            collection.PopulateItemGroups(conventions.Patterns.ResourceAssemblies, oldGroups);

            // Act - New way
            var newItems = collection.ClassifyAssets(classifier, AssetType.ResourceAssembly);

            // Assert - Both should match
            var oldCount = oldGroups.SelectMany(g => g.Items).Count();
            newItems.Count.Should().Be(oldCount, $"Both implementations should agree on {path}");

            if (newItems.Count > 0)
            {
                newItems[0].TryGetValue("locale", out object locale).Should().BeTrue();
                locale.Should().Be(expectedLocale);
            }
        }

        #endregion

        #region Helper methods

        private static void CompareProperty(ContentItem oldItem, ContentItem newItem, string propertyName)
        {
            var oldHas = oldItem.TryGetValue(propertyName, out object oldValue);
            var newHas = newItem.TryGetValue(propertyName, out object newValue);

            oldHas.Should().Be(newHas, $"Both should have (or not have) property '{propertyName}'");

            if (oldHas && newHas)
            {
                // For frameworks, compare the short folder name
                if (oldValue is NuGetFramework oldFw && newValue is NuGetFramework newFw)
                {
                    oldFw.GetShortFolderName().Should().Be(newFw.GetShortFolderName(),
                        $"Property '{propertyName}' framework should match");
                }
                else
                {
                    oldValue.Should().Be(newValue, $"Property '{propertyName}' should match");
                }
            }
        }

        #endregion
    }
}
