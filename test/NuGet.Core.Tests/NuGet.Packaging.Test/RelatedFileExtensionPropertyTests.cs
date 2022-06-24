// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.ContentModel;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class RelatedFileExtensionPropertyTests
    {
        [Fact]
        public void GetRelatedFileExtensionProperty_SingleAssemblyAsset_GetNullProperty()
        {
            // Arrange
            var collection = new ContentItemCollection();
            string assembly = "lib/net50/system.dll";
            string[] paths = new string[]
            {
                "lib/net50/system.dll"
            };
            collection.Load(paths);

            // Act
            string relatedProperty = collection.GetRelatedFileExtensionProperty(assembly, CreateAssetsFromPathList(paths));

            // Assert
            Assert.Equal(null, relatedProperty);
        }


        [Fact]
        public void GetRelatedFileExtensionProperty_MultipleAssemblyAssetsWithSamePrefix_GetNullProperty()
        {
            // Arrange
            var collection = new ContentItemCollection();
            string assembly = "lib/net50/system.dll";
            string[] paths = new string[]
            {
                "lib/net50/system.dll",
                "lib/net50/system.exe",
                "lib/net50/system.winmd"
            };
            collection.Load(paths);

            // Act
            string relatedProperty = collection.GetRelatedFileExtensionProperty(assembly, CreateAssetsFromPathList(paths));

            // Assert
            Assert.Equal(null, relatedProperty);
        }

        [Fact]
        public void GetRelatedFileExtensionProperty_MultipleAssemblyAssetsWithVariousPrefix_GetNullProperty()
        {
            // Arrange
            var collection = new ContentItemCollection();
            string assembly = "lib/net50/system.dll";
            string[] paths = new string[]
            {
                "lib/net50/system.dll",
                "lib/net50/system.exe",
                "lib/net50/system.Core.dll"
            };
            collection.Load(paths);

            // Act
            string relatedProperty = collection.GetRelatedFileExtensionProperty(assembly, CreateAssetsFromPathList(paths));

            // Assert
            Assert.Equal(null, relatedProperty);
        }

        [Fact]
        public void GetRelatedFileExtensionProperty_MultipleAssemblyAssetsWithVariousPrefixMixedCases_GetNullProperty()
        {
            // Arrange
            var collection = new ContentItemCollection();
            string assembly = "lib/net50/system.dll";
            string[] paths = new string[]
            {
                "lib/net50/system.dll",
                "lib/net50/system.EXE",
                "lib/net50/system.Core.DLL",
                "lib/net50/system.NET.DLL",
            };
            collection.Load(paths);

            // Act
            string relatedProperty = collection.GetRelatedFileExtensionProperty(assembly, CreateAssetsFromPathList(paths));

            // Assert
            Assert.Equal(null, relatedProperty);
        }

        [Fact]
        public void GetRelatedFileExtensionProperty_MultipleRelatedFilesWithSamePrefix_GetCorrectProperty()
        {
            // Arrange
            var collection = new ContentItemCollection();
            string assembly = "lib/net50/system.test.dll";
            string[] paths = new string[]
            {
                "lib/net50/system.test.dll",
                "lib/net50/system.test.pdb",
                "lib/net50/system.test.xml",
                "lib/net50/system.test.dll.config",
            };
            collection.Load(paths);

            // Act
            string relatedProperty = collection.GetRelatedFileExtensionProperty(assembly, CreateAssetsFromPathList(paths));

            string expectedRelatedProperty = ".dll.config;.pdb;.xml";
            // Assert
            Assert.Equal(expectedRelatedProperty, relatedProperty);
        }

        [Fact]
        public void GetRelatedFileExtensionProperty_MultipleRelatedFilesWithDifferentPrefix_GetNullProperty()
        {
            // Arrange
            var collection = new ContentItemCollection();
            string assembly = "lib/net50/system.test.dll";
            string[] paths = new string[]
            {
                "lib/net50/system.test.dll",
                "lib/net50/system.test2.dll",
                "lib/net50/system.test2.pdb",
                "lib/net50/system.test2.xml",
                "lib/net50/system.test2.dll.config"
            };
            collection.Load(paths);

            // Act
            string relatedProperty = collection.GetRelatedFileExtensionProperty(assembly, CreateAssetsFromPathList(paths));

            // Assert
            Assert.Equal(null, relatedProperty);
        }

        [Fact]
        public void GetRelatedFileExtensionProperty_MultipleRelatedFilesWithPrefixOfDifferentCase_GetCorrectProperty()
        {
            // Arrange
            var collection = new ContentItemCollection();
            string assembly = "lib/net50/system.test.dll";
            string[] paths = new string[]
            {
                "lib/net50/system.test.dll",
                "lib/net50/SYSTEM.TEST.pdb",
                "lib/net50/System.Test.xml"
            };
            collection.Load(paths);

            // Act
            string relatedProperty = collection.GetRelatedFileExtensionProperty(assembly, CreateAssetsFromPathList(paths));

            string expectedRelatedProperty = ".pdb;.xml";

            // Assert
            Assert.Equal(expectedRelatedProperty, relatedProperty);
        }

        [Fact]
        public void GetRelatedFileExtensionProperty_PlaceHolderAssembly_GetNullProperty()
        {
            // Arrange
            var collection = new ContentItemCollection();
            string assembly = "_._";
            string[] paths = new string[]
            {
                "_._"
            };
            collection.Load(paths);

            // Act
            string relatedProperty = collection.GetRelatedFileExtensionProperty(assembly, CreateAssetsFromPathList(paths));

            // Assert
            Assert.Equal(null, relatedProperty);
        }


        private IEnumerable<Asset> CreateAssetsFromPathList(string[] paths)
        {
            List<Asset> assets = new List<Asset>();
            foreach (string path in paths)
            {
                Asset asset = new Asset();
                asset.Path = path;
                assets.Add(asset);
            }
            return assets;
        }
    }
}
