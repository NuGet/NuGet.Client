// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Client;
using NuGet.ContentModel;
using NuGet.Frameworks;
using NuGet.RuntimeModel;
using Xunit;

namespace NuGet.Packaging.Test.ContentModelTests
{
    /// <summary>
    /// Tests for the AssetClassifier decision tree implementation.
    /// </summary>
    public class AssetClassifierTests
    {
        private static ManagedCodeConventions CreateConventions()
        {
            return new ManagedCodeConventions(new RuntimeGraph());
        }

        #region lib/ pattern tests

        [Theory]
        [InlineData("lib/net472/MyAssembly.dll", "net472")]
        [InlineData("lib/netstandard2.0/MyAssembly.dll", "netstandard2.0")]
        [InlineData("lib/net6.0/MyAssembly.dll", "net6.0")]
        [InlineData("lib/net8.0/MyAssembly.exe", "net8.0")]
        [InlineData("lib/net48/MyAssembly.winmd", "net48")]
        public void Classify_LibTfmAssembly_ReturnsRuntimeAssembly(string path, string expectedTfm)
        {
            // Arrange
            var conventions = CreateConventions();
            var classifier = conventions.CreateAssetClassifier();

            // Act
            var item = classifier.Classify(path, out AssetType assetType);

            // Assert
            Assert.NotNull(item);
            Assert.Equal(AssetType.RuntimeAssembly, assetType);
            Assert.Equal(path, item.Path);
            Assert.True(item.TryGetValue("tfm", out object? tfmValue));
            var tfm = tfmValue as NuGetFramework;
            Assert.NotNull(tfm);
            Assert.Equal(expectedTfm, tfm.GetShortFolderName());
        }

        [Fact]
        public void Classify_LibLegacyPattern_ReturnsRuntimeAssemblyWithNetDefault()
        {
            // Arrange - lib/{assembly} without TFM (legacy)
            var conventions = CreateConventions();
            var classifier = conventions.CreateAssetClassifier();
            var path = "lib/MyAssembly.dll";

            // Act
            var item = classifier.Classify(path, out AssetType assetType);

            // Assert
            Assert.NotNull(item);
            Assert.Equal(AssetType.RuntimeAssembly, assetType);
            Assert.True(item.TryGetValue("tfm", out object? tfmValue));
            var tfm = tfmValue as NuGetFramework;
            Assert.NotNull(tfm);
            Assert.Equal(FrameworkConstants.FrameworkIdentifiers.Net, tfm.Framework);
        }

        [Fact]
        public void Classify_LibEmptyFolder_ReturnsRuntimeAssembly()
        {
            // Arrange
            var conventions = CreateConventions();
            var classifier = conventions.CreateAssetClassifier();
            var path = "lib/net472/_._";

            // Act
            var item = classifier.Classify(path, out AssetType assetType);

            // Assert
            Assert.NotNull(item);
            Assert.Equal(AssetType.RuntimeAssembly, assetType);
            Assert.True(item.TryGetValue("assembly", out object? asmValue));
            Assert.Equal("_._", asmValue);
        }

        [Theory]
        [InlineData("lib/net472/en-US/MyAssembly.resources.dll", "en-US")]
        [InlineData("lib/net6.0/fr/MyAssembly.resources.dll", "fr")]
        [InlineData("lib/netstandard2.0/de-DE/MyAssembly.resources.dll", "de-DE")]
        public void Classify_LibResourceAssembly_ReturnsResourceAssembly(string path, string expectedLocale)
        {
            // Arrange
            var conventions = CreateConventions();
            var classifier = conventions.CreateAssetClassifier();

            // Act
            var item = classifier.Classify(path, out AssetType assetType);

            // Assert
            Assert.NotNull(item);
            Assert.Equal(AssetType.ResourceAssembly, assetType);
            Assert.True(item.TryGetValue("locale", out object? localeValue));
            Assert.Equal(expectedLocale, localeValue);
        }

        #endregion

        #region ref/ pattern tests

        [Theory]
        [InlineData("ref/net472/MyAssembly.dll", "net472")]
        [InlineData("ref/netstandard2.0/MyAssembly.dll", "netstandard2.0")]
        [InlineData("ref/net6.0/MyAssembly.dll", "net6.0")]
        public void Classify_RefTfmAssembly_ReturnsCompileRefAssembly(string path, string expectedTfm)
        {
            // Arrange
            var conventions = CreateConventions();
            var classifier = conventions.CreateAssetClassifier();

            // Act
            var item = classifier.Classify(path, out AssetType assetType);

            // Assert
            Assert.NotNull(item);
            Assert.Equal(AssetType.CompileRefAssembly, assetType);
            Assert.True(item.TryGetValue("tfm", out object? tfmValue));
            var tfm = tfmValue as NuGetFramework;
            Assert.NotNull(tfm);
            Assert.Equal(expectedTfm, tfm.GetShortFolderName());
        }

        [Fact]
        public void Classify_RefAnyTfm_ReturnsCompileRefAssembly()
        {
            // Arrange
            var conventions = CreateConventions();
            var classifier = conventions.CreateAssetClassifier();
            var path = "ref/any/MyAssembly.dll";

            // Act
            var item = classifier.Classify(path, out AssetType assetType);

            // Assert
            Assert.NotNull(item);
            Assert.Equal(AssetType.CompileRefAssembly, assetType);
        }

        #endregion

        #region runtimes/ pattern tests

        [Theory]
        [InlineData("runtimes/win-x64/lib/net472/MyAssembly.dll", "win-x64", "net472")]
        [InlineData("runtimes/linux-x64/lib/net6.0/MyAssembly.dll", "linux-x64", "net6.0")]
        [InlineData("runtimes/osx-arm64/lib/net8.0/MyAssembly.dll", "osx-arm64", "net8.0")]
        public void Classify_RuntimesLib_ReturnsRuntimeAssembly(string path, string expectedRid, string expectedTfm)
        {
            // Arrange
            var conventions = CreateConventions();
            var classifier = conventions.CreateAssetClassifier();

            // Act
            var item = classifier.Classify(path, out AssetType assetType);

            // Assert
            Assert.NotNull(item);
            Assert.Equal(AssetType.RuntimeAssembly, assetType);
            Assert.True(item.TryGetValue("rid", out object? ridValue));
            Assert.Equal(expectedRid, ridValue);
            Assert.True(item.TryGetValue("tfm", out object? tfmValue));
            var tfm = tfmValue as NuGetFramework;
            Assert.NotNull(tfm);
            Assert.Equal(expectedTfm, tfm.GetShortFolderName());
        }

        [Theory]
        [InlineData("runtimes/win-x64/native/MyNative.dll", "win-x64")]
        [InlineData("runtimes/linux-x64/native/libmynative.so", "linux-x64")]
        [InlineData("runtimes/osx-arm64/native/libmynative.dylib", "osx-arm64")]
        public void Classify_RuntimesNative_ReturnsNativeLibrary(string path, string expectedRid)
        {
            // Arrange
            var conventions = CreateConventions();
            var classifier = conventions.CreateAssetClassifier();

            // Act
            var item = classifier.Classify(path, out AssetType assetType);

            // Assert
            Assert.NotNull(item);
            Assert.Equal(AssetType.NativeLibrary, assetType);
            Assert.True(item.TryGetValue("rid", out object? ridValue));
            Assert.Equal(expectedRid, ridValue);
            // Native assets without TFM should use AnyFramework
            Assert.True(item.TryGetValue("tfm", out object? tfmValue));
            Assert.Equal(AnyFramework.AnyFramework, tfmValue);
        }

        [Theory]
        [InlineData("runtimes/win-x64/nativeassets/net472/MyNative.dll", "win-x64")]
        [InlineData("runtimes/linux-x64/nativeassets/net6.0/libmynative.so", "linux-x64")]
        public void Classify_RuntimesNativeAssets_ReturnsNativeLibrary(string path, string expectedRid)
        {
            // Arrange
            var conventions = CreateConventions();
            var classifier = conventions.CreateAssetClassifier();

            // Act
            var item = classifier.Classify(path, out AssetType assetType);

            // Assert
            Assert.NotNull(item);
            Assert.Equal(AssetType.NativeLibrary, assetType);
            Assert.True(item.TryGetValue("rid", out object? ridValue));
            Assert.Equal(expectedRid, ridValue);
        }

        [Theory]
        [InlineData("runtimes/win-x64/lib/net472/en-US/MyAssembly.resources.dll", "win-x64", "en-US")]
        public void Classify_RuntimesResourceAssembly_ReturnsResourceAssembly(string path, string expectedRid, string expectedLocale)
        {
            // Arrange
            var conventions = CreateConventions();
            var classifier = conventions.CreateAssetClassifier();

            // Act
            var item = classifier.Classify(path, out AssetType assetType);

            // Assert
            Assert.NotNull(item);
            Assert.Equal(AssetType.ResourceAssembly, assetType);
            Assert.True(item.TryGetValue("rid", out object? ridValue));
            Assert.Equal(expectedRid, ridValue);
            Assert.True(item.TryGetValue("locale", out object? localeValue));
            Assert.Equal(expectedLocale, localeValue);
        }

        #endregion

        #region build/ pattern tests

        [Theory]
        [InlineData("build/net472/MyPackage.props", "net472")]
        [InlineData("build/net6.0/MyPackage.targets", "net6.0")]
        public void Classify_BuildTfmMsbuild_ReturnsMSBuildFile(string path, string expectedTfm)
        {
            // Arrange
            var conventions = CreateConventions();
            var classifier = conventions.CreateAssetClassifier();

            // Act
            var item = classifier.Classify(path, out AssetType assetType);

            // Assert
            Assert.NotNull(item);
            Assert.Equal(AssetType.MSBuildFile, assetType);
            Assert.True(item.TryGetValue("tfm", out object? tfmValue));
            var tfm = tfmValue as NuGetFramework;
            Assert.NotNull(tfm);
            Assert.Equal(expectedTfm, tfm.GetShortFolderName());
        }

        [Theory]
        [InlineData("build/MyPackage.props")]
        [InlineData("build/MyPackage.targets")]
        public void Classify_BuildMsbuildNoTfm_ReturnsMSBuildFileWithAnyTfm(string path)
        {
            // Arrange
            var conventions = CreateConventions();
            var classifier = conventions.CreateAssetClassifier();

            // Act
            var item = classifier.Classify(path, out AssetType assetType);

            // Assert
            Assert.NotNull(item);
            Assert.Equal(AssetType.MSBuildFile, assetType);
            Assert.True(item.TryGetValue("tfm", out object? tfmValue));
            Assert.Equal(AnyFramework.AnyFramework, tfmValue);
        }

        #endregion

        #region buildMultiTargeting/ pattern tests

        [Theory]
        [InlineData("buildMultiTargeting/MyPackage.props")]
        [InlineData("buildMultiTargeting/MyPackage.targets")]
        public void Classify_BuildMultiTargeting_ReturnsMSBuildMultiTargetingFile(string path)
        {
            // Arrange
            var conventions = CreateConventions();
            var classifier = conventions.CreateAssetClassifier();

            // Act
            var item = classifier.Classify(path, out AssetType assetType);

            // Assert
            Assert.NotNull(item);
            Assert.Equal(AssetType.MSBuildMultiTargetingFile, assetType);
        }

        [Theory]
        [InlineData("buildCrossTargeting/MyPackage.props")]
        [InlineData("buildCrossTargeting/MyPackage.targets")]
        public void Classify_BuildCrossTargeting_ReturnsMSBuildMultiTargetingFile(string path)
        {
            // Arrange - deprecated but still supported
            var conventions = CreateConventions();
            var classifier = conventions.CreateAssetClassifier();

            // Act
            var item = classifier.Classify(path, out AssetType assetType);

            // Assert
            Assert.NotNull(item);
            Assert.Equal(AssetType.MSBuildMultiTargetingFile, assetType);
        }

        #endregion

        #region buildTransitive/ pattern tests

        [Theory]
        [InlineData("buildTransitive/net472/MyPackage.props", "net472")]
        [InlineData("buildTransitive/net6.0/MyPackage.targets", "net6.0")]
        public void Classify_BuildTransitiveTfm_ReturnsMSBuildTransitiveFile(string path, string expectedTfm)
        {
            // Arrange
            var conventions = CreateConventions();
            var classifier = conventions.CreateAssetClassifier();

            // Act
            var item = classifier.Classify(path, out AssetType assetType);

            // Assert
            Assert.NotNull(item);
            Assert.Equal(AssetType.MSBuildTransitiveFile, assetType);
            Assert.True(item.TryGetValue("tfm", out object? tfmValue));
            var tfm = tfmValue as NuGetFramework;
            Assert.NotNull(tfm);
            Assert.Equal(expectedTfm, tfm.GetShortFolderName());
        }

        [Theory]
        [InlineData("buildTransitive/MyPackage.props")]
        [InlineData("buildTransitive/MyPackage.targets")]
        public void Classify_BuildTransitiveNoTfm_ReturnsMSBuildTransitiveFileWithAnyTfm(string path)
        {
            // Arrange
            var conventions = CreateConventions();
            var classifier = conventions.CreateAssetClassifier();

            // Act
            var item = classifier.Classify(path, out AssetType assetType);

            // Assert
            Assert.NotNull(item);
            Assert.Equal(AssetType.MSBuildTransitiveFile, assetType);
            Assert.True(item.TryGetValue("tfm", out object? tfmValue));
            Assert.Equal(AnyFramework.AnyFramework, tfmValue);
        }

        #endregion

        #region contentFiles/ pattern tests

        [Theory]
        [InlineData("contentFiles/cs/net472/MyFile.cs", "cs")]
        [InlineData("contentFiles/vb/net6.0/MyFile.vb", "vb")]
        [InlineData("contentFiles/any/any/MyFile.txt", "any")]
        [InlineData("contentFiles/cs/netstandard2.0/Images/logo.png", "cs")]
        public void Classify_ContentFiles_ReturnsContentFile(string path, string expectedLang)
        {
            // Arrange
            var conventions = CreateConventions();
            var classifier = conventions.CreateAssetClassifier();

            // Act
            var item = classifier.Classify(path, out AssetType assetType);

            // Assert
            Assert.NotNull(item);
            Assert.Equal(AssetType.ContentFile, assetType);
            Assert.True(item.TryGetValue("codeLanguage", out object? langValue));
            Assert.Equal(expectedLang, langValue);
        }

        #endregion

        #region tools/ pattern tests

        [Theory]
        [InlineData("tools/net472/any/mytool.exe", "any")]
        [InlineData("tools/net6.0/win-x64/mytool.exe", "win-x64")]
        public void Classify_Tools_ReturnsToolsAssembly(string path, string expectedRid)
        {
            // Arrange
            var conventions = CreateConventions();
            var classifier = conventions.CreateAssetClassifier();

            // Act
            var item = classifier.Classify(path, out AssetType assetType);

            // Assert
            Assert.NotNull(item);
            Assert.Equal(AssetType.ToolsAssembly, assetType);
            Assert.True(item.TryGetValue("rid", out object? ridValue));
            Assert.Equal(expectedRid, ridValue);
        }

        #endregion

        #region embed/ pattern tests

        [Theory]
        [InlineData("embed/net472/MyAssembly.dll", "net472")]
        [InlineData("embed/net6.0/MyAssembly.dll", "net6.0")]
        public void Classify_Embed_ReturnsEmbedAssembly(string path, string expectedTfm)
        {
            // Arrange
            var conventions = CreateConventions();
            var classifier = conventions.CreateAssetClassifier();

            // Act
            var item = classifier.Classify(path, out AssetType assetType);

            // Assert
            Assert.NotNull(item);
            Assert.Equal(AssetType.EmbedAssembly, assetType);
            Assert.True(item.TryGetValue("tfm", out object? tfmValue));
            var tfm = tfmValue as NuGetFramework;
            Assert.NotNull(tfm);
            Assert.Equal(expectedTfm, tfm.GetShortFolderName());
        }

        #endregion

        #region Edge cases

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("rootfile.txt")]
        [InlineData("unknown/path/file.txt")]
        public void Classify_InvalidPaths_ReturnsNull(string? path)
        {
            // Arrange
            var conventions = CreateConventions();
            var classifier = conventions.CreateAssetClassifier();

            // Act
            var item = classifier.Classify(path!, out AssetType assetType);

            // Assert
            Assert.Null(item);
            Assert.Equal(AssetType.None, assetType);
        }

        [Fact]
        public void Classify_CaseInsensitive_MatchesPaths()
        {
            // Arrange
            var conventions = CreateConventions();
            var classifier = conventions.CreateAssetClassifier();

            // Act
            var item1 = classifier.Classify("LIB/NET472/MyAssembly.dll", out AssetType type1);
            var item2 = classifier.Classify("Lib/Net472/MyAssembly.dll", out AssetType type2);
            var item3 = classifier.Classify("lib/net472/MyAssembly.dll", out AssetType type3);

            // Assert
            Assert.NotNull(item1);
            Assert.NotNull(item2);
            Assert.NotNull(item3);
            Assert.Equal(AssetType.RuntimeAssembly, type1);
            Assert.Equal(AssetType.RuntimeAssembly, type2);
            Assert.Equal(AssetType.RuntimeAssembly, type3);
        }

        #endregion

        #region ContentItemCollection integration tests

        [Fact]
        public void ClassifyAssets_WithMultipleAssets_ClassifiesCorrectly()
        {
            // Arrange
            var conventions = CreateConventions();
            var classifier = conventions.CreateAssetClassifier();
            var collection = new ContentItemCollection();
            collection.Load(new[]
            {
                "lib/net472/MyAssembly.dll",
                "ref/net472/MyAssembly.dll",
                "runtimes/win-x64/native/MyNative.dll",
                "build/net472/MyPackage.props",
                "contentFiles/cs/net472/MyFile.cs"
            });

            // Act
            var allAssets = collection.ClassifyAllAssets(classifier);

            // Assert
            Assert.True(allAssets.ContainsKey(AssetType.RuntimeAssembly));
            Assert.True(allAssets.ContainsKey(AssetType.CompileRefAssembly));
            Assert.True(allAssets.ContainsKey(AssetType.NativeLibrary));
            Assert.True(allAssets.ContainsKey(AssetType.MSBuildFile));
            Assert.True(allAssets.ContainsKey(AssetType.ContentFile));
        }

        [Fact]
        public void ClassifyAssets_FilterByType_ReturnsOnlyMatchingType()
        {
            // Arrange
            var conventions = CreateConventions();
            var classifier = conventions.CreateAssetClassifier();
            var collection = new ContentItemCollection();
            collection.Load(new[]
            {
                "lib/net472/MyAssembly.dll",
                "ref/net472/MyAssembly.dll",
                "build/net472/MyPackage.props"
            });

            // Act
            var runtimeAssets = collection.ClassifyAssets(classifier, AssetType.RuntimeAssembly);

            // Assert
            Assert.Single(runtimeAssets);
            Assert.Equal("lib/net472/MyAssembly.dll", runtimeAssets[0].Path);
        }

        #endregion
    }
}
