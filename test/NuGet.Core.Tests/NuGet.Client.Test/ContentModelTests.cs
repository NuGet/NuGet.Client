// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.ContentModel;
using NuGet.Frameworks;
using NuGet.RuntimeModel;
using Xunit;

namespace NuGet.Client.Test
{
    public class ContentModelTests
    {
        [Fact]
        public void ContentModel_LibAnyMapsToDotnet()
        {
            // Arrange
            var conventions = new ManagedCodeConventions(
                new RuntimeGraph(
                    new List<CompatibilityProfile>() { new CompatibilityProfile("net46.app") }));

            var collection = new ContentItemCollection();
            collection.Load(new string[]
            {
                "lib/any/a.dll",
            });

            // Act
            var groups = collection.FindItemGroups(conventions.Patterns.RuntimeAssemblies)
                .OrderBy(group => ((NuGetFramework)group.Properties["tfm"]).GetShortFolderName())
                .ToList();

            // Assert
            Assert.Equal(1, groups.Count);
            Assert.Equal(FrameworkConstants.CommonFrameworks.DotNet, (NuGetFramework)groups[0].Properties["tfm"]);
        }

        [Fact]
        public void ContentModel_RefAnyMapsToDotnet()
        {
            // Arrange
            var conventions = new ManagedCodeConventions(
                new RuntimeGraph(
                    new List<CompatibilityProfile>() { new CompatibilityProfile("net46.app") }));

            var collection = new ContentItemCollection();
            collection.Load(new string[]
            {
                "ref/any/a.dll",
            });

            // Act
            var groups = collection.FindItemGroups(conventions.Patterns.CompileRefAssemblies)
                .OrderBy(group => ((NuGetFramework)group.Properties["tfm"]).GetShortFolderName())
                .ToList();

            // Assert
            Assert.Equal(1, groups.Count);
            Assert.Equal(FrameworkConstants.CommonFrameworks.DotNet, (NuGetFramework)groups[0].Properties["tfm"]);
        }

        [Fact]
        public void ContentModel_RuntimesAnyMapsToDotnet()
        {
            // Arrange
            var conventions = new ManagedCodeConventions(
                new RuntimeGraph(
                    new List<CompatibilityProfile>() { new CompatibilityProfile("net46.app") }));

            var collection = new ContentItemCollection();
            collection.Load(new string[]
            {
                "runtimes/any/lib/any/a.dll",
            });

            // Act
            var groups = collection.FindItemGroups(conventions.Patterns.RuntimeAssemblies)
                .OrderBy(group => ((NuGetFramework)group.Properties["tfm"]).GetShortFolderName())
                .ToList();

            // Assert
            Assert.Equal(1, groups.Count);
            Assert.Equal(FrameworkConstants.CommonFrameworks.DotNet, (NuGetFramework)groups[0].Properties["tfm"]);
        }

        [Fact]
        public void ContentModel_ResourcesAnyMapsToDotnet()
        {
            // Arrange
            var conventions = new ManagedCodeConventions(
                new RuntimeGraph(
                    new List<CompatibilityProfile>() { new CompatibilityProfile("net46.app") }));

            var collection = new ContentItemCollection();
            collection.Load(new string[]
            {
                "lib/any/en-us/a.resources.dll",
            });

            // Act
            var groups = collection.FindItemGroups(conventions.Patterns.ResourceAssemblies)
                .OrderBy(group => ((NuGetFramework)group.Properties["tfm"]).GetShortFolderName())
                .ToList();

            // Assert
            Assert.Equal(1, groups.Count);
            Assert.Equal(FrameworkConstants.CommonFrameworks.DotNet, (NuGetFramework)groups[0].Properties["tfm"]);
        }

        [Fact]
        public void ContentModel_MSBuildAnyMapsToDotnet()
        {
            // Arrange
            var conventions = new ManagedCodeConventions(
                new RuntimeGraph(
                    new List<CompatibilityProfile>() { new CompatibilityProfile("net46.app") }));

            var collection = new ContentItemCollection();
            collection.Load(new string[]
            {
                "build/any/a.targets",
            });

            // Act
            var groups = collection.FindItemGroups(conventions.Patterns.MSBuildFiles)
                .OrderBy(group => ((NuGetFramework)group.Properties["tfm"]).GetShortFolderName())
                .ToList();

            // Assert
            Assert.Equal(1, groups.Count);
            Assert.Equal(FrameworkConstants.CommonFrameworks.DotNet, (NuGetFramework)groups[0].Properties["tfm"]);
        }

        [Fact]
        public void ContentModel_ContentFilesAnyMapsToAny()
        {
            // Arrange
            var conventions = new ManagedCodeConventions(
                new RuntimeGraph(
                    new List<CompatibilityProfile>() { new CompatibilityProfile("net46.app") }));

            var collection = new ContentItemCollection();
            collection.Load(new string[]
            {
                "contentFiles/any/any/a.txt",
            });

            // Act
            var groups = collection.FindItemGroups(conventions.Patterns.ContentFiles)
                .OrderBy(group => ((NuGetFramework)group.Properties["tfm"]).GetShortFolderName())
                .ToList();

            // Assert
            Assert.Equal(1, groups.Count);
            Assert.Equal(NuGetFramework.AnyFramework, (NuGetFramework)groups[0].Properties["tfm"]);
        }
    }
}
