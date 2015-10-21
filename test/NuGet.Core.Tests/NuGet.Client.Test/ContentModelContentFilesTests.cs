﻿using System.Collections.Generic;
using System.Linq;
using NuGet.ContentModel;
using NuGet.Frameworks;
using NuGet.RuntimeModel;
using Xunit;

namespace NuGet.Client.Test
{
    public class ContentModelContentFilesTests
    {
        [Fact]
        public void ContentFiles_BasicContentModelCheck()
        {
            // Arrange
            var conventions = new ManagedCodeConventions(
                new RuntimeGraph(
                    new List<CompatibilityProfile>() { new CompatibilityProfile("net46.app") }));

            var criteria = conventions.Criteria.ForFramework(NuGetFramework.Parse("net46"));

            var collection = new ContentItemCollection();
            collection.Load(new string[]
            {
                "contentFiles/any/net45/config1.xml",
                "contentFiles/cs/net45/config2.xml",
                "contentFiles/vb/net45/config3.xml",
                "contentFiles/any/uap/config4.xml",
                "contentFiles/cs/uap/config5.xml",
                "contentFiles/vb/uap/config6.xml"
            });

            // Act
            var contentFileGroups = collection.FindItemGroups(conventions.Patterns.ContentFiles);

            // Assert
            Assert.Equal(6, contentFileGroups.Count());

            Assert.Equal("any|cs|vb", string.Join("|",
                contentFileGroups.Select(group =>
                    group.Properties[ManagedCodeConventions.PropertyNames.CodeLanguage])
                    .Distinct()
                    .OrderBy(s => s)));

            Assert.Equal("net45|uap", string.Join("|",
                contentFileGroups.Select(group =>
                    group.Properties[ManagedCodeConventions.PropertyNames.TargetFrameworkMoniker] as NuGetFramework)
                    .Select(f => f.GetShortFolderName())
                    .Distinct()
                    .OrderBy(s => s)));
        }

        [Fact]
        public void ContentFiles_FrameworkNormalize()
        {
            // Arrange
            var conventions = new ManagedCodeConventions(
                new RuntimeGraph(
                    new List<CompatibilityProfile>() { new CompatibilityProfile("net46.app") }));

            var criteria = conventions.Criteria.ForFramework(NuGetFramework.Parse("net46"));

            var collection = new ContentItemCollection();
            collection.Load(new string[]
            {
                "contentFiles/any/net4.5/config1.xml",
                "contentFiles/any/net450/config2.xml",
                "contentFiles/any/net4500/config3.xml",
                "contentFiles/any/uap10.0/config4.xml",
                "contentFiles/any/uap10.00/config5.xml",
                "contentFiles/any/uap10.0.0/config6.xml"
            });

            // Act
            var contentFileGroups = collection.FindItemGroups(conventions.Patterns.ContentFiles);

            // Assert
            Assert.Equal(2, contentFileGroups.Count());

            Assert.Equal("net45|uap10.0", string.Join("|",
                contentFileGroups.Select(group =>
                    group.Properties[ManagedCodeConventions.PropertyNames.TargetFrameworkMoniker] as NuGetFramework)
                    .Select(f => f.GetShortFolderName())
                    .Distinct()
                    .OrderBy(s => s)));
        }

        [Fact]
        public void ContentFiles_InvalidPaths()
        {
            // Arrange
            var conventions = new ManagedCodeConventions(
                new RuntimeGraph(
                    new List<CompatibilityProfile>() { new CompatibilityProfile("net46.app") }));

            var criteria = conventions.Criteria.ForFramework(NuGetFramework.Parse("net46"));

            var collection = new ContentItemCollection();
            collection.Load(new string[]
            {
                "contentFiles/any/config1.xml",
                "contentFiles/in.valid/net45/config2.xml",
                "contentFiles/any/config3.xml",
                "contentFiles/config4.xml",
                "contentFiles",
                "contentFiles/+/uap10.0.0/config6.xml"
            });

            // Act
            var contentFileGroups = collection.FindItemGroups(conventions.Patterns.ContentFiles);

            // Assert
            Assert.Equal(0, contentFileGroups.Count());
        }

        [Fact]
        public void ContentFiles_BasicContentModelSubFolders()
        {
            // Arrange
            var conventions = new ManagedCodeConventions(
                new RuntimeGraph(
                    new List<CompatibilityProfile>() { new CompatibilityProfile("net46.app") }));

            var criteria = conventions.Criteria.ForFramework(NuGetFramework.Parse("net46"));

            var collection = new ContentItemCollection();
            collection.Load(new string[]
            {
                "contentFiles/any/net45/config1.xml",
                "contentFiles/any/net45/folder/a/b/c/config2.xml",
            });

            // Act
            var contentFileGroups = collection.FindItemGroups(conventions.Patterns.ContentFiles);

            // Assert
            Assert.Equal(1, contentFileGroups.Count());
            Assert.Equal(2, contentFileGroups.Single().Items.Count);

            Assert.Equal("any", contentFileGroups.Select(group =>
                    (string)group.Properties[ManagedCodeConventions.PropertyNames.CodeLanguage])
                    .Single());

            Assert.Equal("contentFiles/any/net45/config1.xml|contentFiles/any/net45/folder/a/b/c/config2.xml",
                string.Join("|",
                contentFileGroups.SelectMany(group => group.Items)
                    .Select(item => item.Path)
                    .OrderBy(s => s)));
        }

        [Fact]
        public void ContentFiles_Empty()
        {
            // Arrange
            var conventions = new ManagedCodeConventions(
                new RuntimeGraph(
                    new List<CompatibilityProfile>() { new CompatibilityProfile("net46.app") }));

            var criteria = conventions.Criteria.ForFramework(NuGetFramework.Parse("net46"));

            var collection = new ContentItemCollection();
            collection.Load(new string[]
            {
                // empty
            });

            // Act
            var contentFileGroups = collection.FindItemGroups(conventions.Patterns.ContentFiles);

            // Assert
            Assert.Equal(0, contentFileGroups.Count());
        }
    }
}
