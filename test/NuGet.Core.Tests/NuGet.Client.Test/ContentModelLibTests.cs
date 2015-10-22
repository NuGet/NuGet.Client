using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.ContentModel;
using NuGet.Frameworks;
using NuGet.RuntimeModel;
using Xunit;

namespace NuGet.Client.Test
{
    public class ContentModelLibTests
    {
        [Fact]
        public void ContentModel_LibNoFilesAtRootNoAnyGroup()
        {
            // Arrange
            var conventions = new ManagedCodeConventions(
                new RuntimeGraph(
                    new List<CompatibilityProfile>() { new CompatibilityProfile("net46.app") }));

            var collection = new ContentItemCollection();
            collection.Load(new string[]
            {
                "lib/net46/a.dll",
                "lib/uap10.0/a.dll",
            });

            // Act
            var groups = collection.FindItemGroups(conventions.Patterns.RuntimeAssemblies)
                .Select(group => ((NuGetFramework)group.Properties["tfm"]))
                .ToList();

            // Assert
            Assert.Equal(0, groups.Count(framework => framework == NuGetFramework.AnyFramework));
        }

        [Fact]
        public void ContentModel_LibRootFolderOnly()
        {
            // Arrange
            var conventions = new ManagedCodeConventions(
                new RuntimeGraph(
                    new List<CompatibilityProfile>() { new CompatibilityProfile("net46.app") }));

            var collection = new ContentItemCollection();
            collection.Load(new string[]
            {
                "lib/a.dll"
            });

            // Act
            var groups = collection.FindItemGroups(conventions.Patterns.RuntimeAssemblies).ToList();

            // Assert
            Assert.Equal(1, groups.Count);
            Assert.Equal(NuGetFramework.Parse("net"), groups[0].Properties["tfm"]);
        }

        [Fact]
        public void ContentModel_LibRootAndTFM()
        {
            // Arrange
            var conventions = new ManagedCodeConventions(
                new RuntimeGraph(
                    new List<CompatibilityProfile>() { new CompatibilityProfile("net46.app") }));

            var collection = new ContentItemCollection();
            collection.Load(new string[]
            {
                "lib/net46/a.dll",
                "lib/a.dll",
            });

            // Act
            var groups = collection.FindItemGroups(conventions.Patterns.RuntimeAssemblies)
                .OrderBy(group => ((NuGetFramework)group.Properties["tfm"]).GetShortFolderName())
                .ToList();

            // Assert
            Assert.Equal(2, groups.Count);
            Assert.Equal(NuGetFramework.Parse("net"), groups[0].Properties["tfm"]);
            Assert.Equal(NuGetFramework.Parse("net46"), groups[1].Properties["tfm"]);
        }

        [Fact]
        public void ContentModel_LibRootIgnoreSubFolder()
        {
            // Arrange
            var conventions = new ManagedCodeConventions(
                new RuntimeGraph(
                    new List<CompatibilityProfile>() { new CompatibilityProfile("net46.app") }));

            var collection = new ContentItemCollection();
            collection.Load(new string[]
            {
                "lib/a.dll",
                "lib/x86/b.dll"
            });

            var criteria = conventions.Criteria.ForFramework(NuGetFramework.Parse("net46"));

            // Act
            var group = collection.FindBestItemGroup(criteria, conventions.Patterns.RuntimeAssemblies);

            // Assert
            Assert.Equal(1, group.Items.Count());
        }

        [Fact]
        public void ContentModel_LibNet46WithSubFoldersAreIgnored()
        {
            // Arrange
            var conventions = new ManagedCodeConventions(
                new RuntimeGraph(
                    new List<CompatibilityProfile>() { new CompatibilityProfile("net46.app") }));

            var criteria = conventions.Criteria.ForFramework(NuGetFramework.Parse("net46"));

            var collection = new ContentItemCollection();
            collection.Load(new string[]
            {
                "lib/net46/a.dll",
                "lib/net46/sub/a.dll",
            });

            // Act
            var group = collection.FindBestItemGroup(criteria, conventions.Patterns.RuntimeAssemblies);

            // Assert
            Assert.Equal(1, group.Items.Count);
            Assert.Equal("lib/net46/a.dll", group.Items[0].Path);
        }
    }
}
