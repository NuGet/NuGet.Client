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
    public class ContentModelToolsTests
    {

        [Fact]
        public void ContentModel_NoRuntimeIdentifierNoMatch()
        {
            // Arrange
            var conventions = new ManagedCodeConventions(
                new RuntimeGraph(
                    new List<CompatibilityProfile>() { new CompatibilityProfile("net46.app") }));

            var collection = new ContentItemCollection();
            collection.Load(new string[]
            {
                "tools/net46/a.dll",
            });

            // Act
            var groups = collection.FindItemGroups(conventions.Patterns.ToolsAssemblies)
                .Select(group => ((NuGetFramework)group.Properties["tfm"]))
                .ToList();

            // Assert
            Assert.Equal(0, groups.Count);
        }

        [Fact]
        public void ContentModel_NoTFMAndRuntimeIdentifierNoMatch()
        {
            // Arrange
            var conventions = new ManagedCodeConventions(
                new RuntimeGraph(
                    new List<CompatibilityProfile>() { new CompatibilityProfile("net46.app") }));

            var collection = new ContentItemCollection();
            collection.Load(new string[]
            {
                "tools/a.dll",
            });

            // Act
            var groups = collection.FindItemGroups(conventions.Patterns.ToolsAssemblies)
                .Select(group => ((NuGetFramework)group.Properties["tfm"]))
                .ToList();

            // Assert
            Assert.Equal(0, groups.Count);
        }

        [Fact]
        public void ContentModel_AnyTFMDefaulsToDotnet()
        {
            // Arrange
            var conventions = new ManagedCodeConventions(
                new RuntimeGraph(
                    new List<CompatibilityProfile>() { new CompatibilityProfile("net46.app") }));

            var collection = new ContentItemCollection();
            var rid = "win-x86";
            collection.Load(new string[]
            {
                $"tools/any/{rid}/a.dll",
            });

            // Act
            var groups = collection.FindItemGroups(conventions.Patterns.ToolsAssemblies)
                .ToList();

            // Assert
            Assert.Equal(1, groups.Count);
            Assert.Equal(FrameworkConstants.CommonFrameworks.DotNet, (NuGetFramework)groups.First().Properties["tfm"]);
            Assert.Equal(rid, groups.First().Properties["rid"]);
        }

        [Fact]
        public void ContentModel_AnyTFMDefaultsToDotnetndAnyRIDisAnyRID()
        {
            // Arrange
            var conventions = new ManagedCodeConventions(
                new RuntimeGraph(
                    new List<CompatibilityProfile>() { new CompatibilityProfile("net46.app") }));

            var collection = new ContentItemCollection();
            var rid = "any";
            collection.Load(new string[]
            {
                $"tools/any/{rid}/a.dll",
            });

            // Act
            var groups = collection.FindItemGroups(conventions.Patterns.ToolsAssemblies)
                .ToList();

            // Assert
            Assert.Equal(1, groups.Count);
            Assert.Equal(FrameworkConstants.CommonFrameworks.DotNet, (NuGetFramework)groups.First().Properties["tfm"]);
            Assert.Equal(rid, groups.First().Properties["rid"]);
        }

        [Fact]
        public void ContentModel_GetNearestRIDAndTFM() // TODO NK - how to make sure that Any maps to any TFM. 
        {
            // Arrange
            var runtimes = new List<RuntimeDescription>()
            {
                new RuntimeDescription("a"),
                new RuntimeDescription("b", new string[] { "a" }),
                new RuntimeDescription("c", new string[] { "b" }),
                new RuntimeDescription("d", new string[] { "c" }),
            };

            var conventions = new ManagedCodeConventions(
                new RuntimeGraph(
                    runtimes,
                    new List<CompatibilityProfile>() { new CompatibilityProfile("net46.app") }));

            var criteria = conventions.Criteria.ForFrameworkAndRuntime(NuGetFramework.Parse("net46"), "d");

            var collection = new ContentItemCollection();
            var rid = "a";
            collection.Load(new string[]
            {
                $"tools/net46/{rid}/a.dll",
            });

            // Act
            var group = collection.FindBestItemGroup(criteria, conventions.Patterns.ToolsAssemblies);

            // Assert
            Assert.Equal(FrameworkConstants.CommonFrameworks.Net46, (NuGetFramework)group.Properties["tfm"]);
            Assert.Equal(rid, group.Properties["rid"]);
            Assert.Equal($"tools/net46/{rid}/a.dll", group.Items.Single().Path);
        }
    }
}
