// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Configuration.Test
{
    public class PackageNamespacesSourceItemTests
    {
        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void Constructor_WithInvalidKey_Throws(string key)
        {
            Assert.Throws<ArgumentException>(() => new PackageNamespacesSourceItem(key, new List<NamespaceItem>() { new NamespaceItem("stuff") }));
        }

        [Fact]
        public void Constructor_WithEmptyNamespaces_Throws()
        {
            Assert.Throws<ArgumentException>(() => new PackageNamespacesSourceItem("name", new List<NamespaceItem>() { }));
        }

        [Fact]
        public void Constructor_WithNullNamespaces_Throws()
        {
            Assert.Throws<ArgumentException>(() => new PackageNamespacesSourceItem("name", null));
        }

        [Fact]
        public void Equals_WithEquivalentName_ReturnsTrue()
        {
            var left = new PackageNamespacesSourceItem("name", new List<NamespaceItem>() { new NamespaceItem("stuff") });
            var right = new PackageNamespacesSourceItem("name", new List<NamespaceItem>() { new NamespaceItem("stuff") });
            left.Equals(right).Should().BeTrue();
        }

        [Fact]
        public void Equals_WithDifferentlyCasedName_ReturnsTrue()
        {
            var left = new PackageNamespacesSourceItem("NAME", new List<NamespaceItem>() { new NamespaceItem("stuff") });
            var right = new PackageNamespacesSourceItem("name", new List<NamespaceItem>() { new NamespaceItem("stuff") });
            left.Equals(right).Should().BeTrue();
        }

        [Fact]
        public void Equals_WithDifferentNames_ReturnsFalse()
        {
            var left = new PackageNamespacesSourceItem("name2", new List<NamespaceItem>() { new NamespaceItem("stuff"), new NamespaceItem("stuff2") });
            var right = new PackageNamespacesSourceItem("name", new List<NamespaceItem>() { new NamespaceItem("stuff"), new NamespaceItem("stuff2") });
            left.Equals(right).Should().BeFalse();
        }

        [Fact]
        public void Equals_WithDifferentNamespaces_ReturnsTrue()
        {
            var left = new PackageNamespacesSourceItem("name", new List<NamespaceItem>() { new NamespaceItem("stuff"), new NamespaceItem("stuff2") });
            var right = new PackageNamespacesSourceItem("name", new List<NamespaceItem>() { new NamespaceItem("stuff"), new NamespaceItem("stuff3") });
            left.Equals(right).Should().BeTrue();
        }

        [Fact]
        public void HashCode_WithEquivalentNamespaces_ReturnsTrue()
        {
            var left = new PackageNamespacesSourceItem("name", new List<NamespaceItem>() { new NamespaceItem("stuff") });
            var right = new PackageNamespacesSourceItem("name", new List<NamespaceItem>() { new NamespaceItem("stuff") });
            left.GetHashCode().Equals(right.GetHashCode()).Should().BeTrue();
        }

        [Fact]
        public void HashCode_WithDifferentlyCasedName_ReturnsTrue()
        {
            var left = new PackageNamespacesSourceItem("NAME", new List<NamespaceItem>() { new NamespaceItem("stuff") });
            var right = new PackageNamespacesSourceItem("name", new List<NamespaceItem>() { new NamespaceItem("stuff") });
            left.GetHashCode().Equals(right.GetHashCode()).Should().BeTrue();
        }

        [Fact]
        public void HashCode_WithDifferentNames_ReturnsFalse()
        {
            var left = new PackageNamespacesSourceItem("name2", new List<NamespaceItem>() { new NamespaceItem("stuff"), new NamespaceItem("stuff2") });
            var right = new PackageNamespacesSourceItem("name", new List<NamespaceItem>() { new NamespaceItem("stuff"), new NamespaceItem("stuff2") });
            left.GetHashCode().Equals(right.GetHashCode()).Should().BeFalse();
        }

        [Fact]
        public void HashCode_WithDifferentNamespaces_ReturnsTrue()
        {
            var left = new PackageNamespacesSourceItem("name", new List<NamespaceItem>() { new NamespaceItem("stuff"), new NamespaceItem("stuff2") });
            var right = new PackageNamespacesSourceItem("name", new List<NamespaceItem>() { new NamespaceItem("stuff"), new NamespaceItem("stuff3") });
            left.GetHashCode().Equals(right.GetHashCode()).Should().BeTrue();
        }

        [Fact]
        public void ElementNameGetter_ReturnsPackageSource()
        {
            var original = new PackageNamespacesSourceItem("name", new List<NamespaceItem>() { new NamespaceItem("stuff"), new NamespaceItem("stuff2") });
            original.ElementName.Should().Be("packageSource");
        }

        [Fact]
        public void Clone_CreatesEquivalentNamespaces()
        {
            var original = new PackageNamespacesSourceItem("name", new List<NamespaceItem>() { new NamespaceItem("stuff"), new NamespaceItem("stuff2") });
            var clone = original.Clone() as PackageNamespacesSourceItem;
            original.Equals(clone).Should().BeTrue();
            original.GetHashCode().Equals(clone.GetHashCode()).Should().BeTrue();
            SettingsTestUtils.DeepEquals(original, clone).Should().BeTrue();
            ReferenceEquals(original, clone).Should().BeFalse();
            original.Key.Equals(clone.Key);
        }

        [Fact]
        public void PackageSourceNamespacesItemParse_WithoutKey_Throws()
        {
            // Arrange
            var config = @"
<configuration>
    <packageNamespaces>
        <packageSource id=""nuget.org"">
            <namespace id=""sadas""  />
        </packageSource>
    </packageNamespaces>
</configuration>";
            var nugetConfigPath = "NuGet.Config";
            using var mockBaseDirectory = TestDirectory.Create();
            SettingsTestUtils.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);

            // Act and Assert
            var ex = Record.Exception(() => new SettingsFile(mockBaseDirectory));

            ex.Should().NotBeNull();
            ex.Should().BeOfType<NuGetConfigurationException>();
            ex.Message.Should().Be(string.Format("Unable to parse config file because: Missing required attribute 'key' in element 'packageSource'. Path: '{0}'.", Path.Combine(mockBaseDirectory, nugetConfigPath)));
        }

        [Fact]
        public void PackageSourceNamespacesItemParse_WithoutNamespaces_Throws()
        {
            // Arrange
            var config = @"
<configuration>
    <packageNamespaces>
        <packageSource key=""nuget.org"">
        </packageSource>
    </packageNamespaces>
</configuration>";
            var nugetConfigPath = "NuGet.Config";
            using var mockBaseDirectory = TestDirectory.Create();
            SettingsTestUtils.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);

            // Act and Assert
            var ex = Record.Exception(() => new SettingsFile(mockBaseDirectory));

            ex.Should().NotBeNull();
            ex.Should().BeOfType<NuGetConfigurationException>();
            ex.Message.Should().Be(string.Format("Package source namespace '{0}' must have at least one namespace. Path: '{1}'", "nuget.org", Path.Combine(mockBaseDirectory, nugetConfigPath)));
        }

        [Fact]
        public void PackageSourceNamespacesItemParse_WithValidData_ParsesCorrectly()
        {
            // Arrange
            var config = @"
<configuration>
    <packageNamespaces>
        <packageSource key=""nuget.org"">
            <namespace id=""sadas"" />
        </packageSource>
    </packageNamespaces>
</configuration>";
            var nugetConfigPath = "NuGet.Config";
            using var mockBaseDirectory = TestDirectory.Create();
            SettingsTestUtils.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);

            // Act and Assert
            var settingsFile = new SettingsFile(mockBaseDirectory);
            var section = settingsFile.GetSection("packageNamespaces");
            section.Should().NotBeNull();

            section.Items.Count.Should().Be(1);
            var packageSourceNamespaceItem = section.Items.First() as PackageNamespacesSourceItem;
            var item = packageSourceNamespaceItem.Namespaces.First();
            var expectedItem = new NamespaceItem("sadas");
            SettingsTestUtils.DeepEquals(item, expectedItem).Should().BeTrue();
        }

        [Fact]
        public void PackageSourceNamespacesItemParse_WithUnrecognizedItems_UnknownItemsAreIgnored()
        {
            // Arrange
            // Arrange
            var config = @"
<configuration>
    <packageNamespaces>
        <packageSource key=""nuget.org"">
            <namespace id=""sadas"" />
            <notANamespace id=""sadas"" />
        </packageSource>
    </packageNamespaces>
</configuration>";
            var nugetConfigPath = "NuGet.Config";
            using var mockBaseDirectory = TestDirectory.Create();
            SettingsTestUtils.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);

            // Act and Assert
            var settingsFile = new SettingsFile(mockBaseDirectory);
            var section = settingsFile.GetSection("packageNamespaces");
            section.Should().NotBeNull();

            section.Items.Count.Should().Be(1);
            var packageSourceNamespaceItem = section.Items.First() as PackageNamespacesSourceItem;
            var item = packageSourceNamespaceItem.Namespaces.First();
            var expectedItem = new NamespaceItem("sadas");
            SettingsTestUtils.DeepEquals(item, expectedItem).Should().BeTrue();
        }

        [Fact]
        public void Update_WithAdditionalNamespace_AddsAdditionalNamespace()
        {
            // Arrange
            var config = @"
<configuration>
    <packageNamespaces>
        <packageSource key=""nuget.org"">
            <namespace id=""first"" />
        </packageSource>
    </packageNamespaces>
</configuration>";
            var nugetConfigPath = "NuGet.Config";
            using var mockBaseDirectory = TestDirectory.Create();
            SettingsTestUtils.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);

            // Act and Assert
            var settingsFile = new SettingsFile(mockBaseDirectory);
            settingsFile.TryGetSection("packageNamespaces", out var section).Should().BeTrue();
            section.Should().NotBeNull();

            section.Items.Count.Should().Be(1);
            var packageSourceNamespacesItem = section.Items.First() as PackageNamespacesSourceItem;
            packageSourceNamespacesItem.Namespaces.Should().HaveCount(1);

            var clone = packageSourceNamespacesItem.Clone() as PackageNamespacesSourceItem;
            clone.Namespaces.Add(new NamespaceItem("second"));

            packageSourceNamespacesItem.Update(clone);
            settingsFile.SaveToDisk();

            // Assert
            var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageNamespaces>
        <packageSource key=""nuget.org"">
            <namespace id=""first"" />
            <namespace id=""second"" />
        </packageSource>
    </packageNamespaces>
</configuration>";

            result.Replace("\r\n", "\n")
                .Should().BeEquivalentTo(
                File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath)).Replace("\r\n", "\n"));
        }

        [Fact]
        public void Update_WithRemovedNamespace_RemovesNamespace()
        {
            // Arrange
            var config = @"
<configuration>
    <packageNamespaces>
        <packageSource key=""nuget.org"">
            <namespace id=""first"" />
            <namespace id=""second"" />
        </packageSource>
    </packageNamespaces>
</configuration>";
            var nugetConfigPath = "NuGet.Config";
            using var mockBaseDirectory = TestDirectory.Create();
            SettingsTestUtils.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);

            // Act and Assert
            var settingsFile = new SettingsFile(mockBaseDirectory);
            settingsFile.TryGetSection("packageNamespaces", out var section).Should().BeTrue();
            section.Should().NotBeNull();

            section.Items.Count.Should().Be(1);
            var packageSourceNamespacesItem = section.Items.First() as PackageNamespacesSourceItem;
            packageSourceNamespacesItem.Namespaces.Should().HaveCount(2);

            var clone = packageSourceNamespacesItem.Clone() as PackageNamespacesSourceItem;
            clone.Namespaces.RemoveAt(1);

            packageSourceNamespacesItem.Update(clone);
            settingsFile.SaveToDisk();

            // Assert
            var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageNamespaces>
        <packageSource key=""nuget.org"">
            <namespace id=""first"" />
        </packageSource>
    </packageNamespaces>
</configuration>";

            result.Replace("\r\n", "\n")
                .Should().BeEquivalentTo(
                File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath)).Replace("\r\n", "\n"));
        }

        [Fact]
        public void Update_WithoutAnyNamespaces_Throws()
        {
            // Arrange
            var config = @"
<configuration>
    <packageNamespaces>
        <packageSource key=""nuget.org"">
            <namespace id=""first"" />
        </packageSource>
    </packageNamespaces>
</configuration>";
            var nugetConfigPath = "NuGet.Config";
            using var mockBaseDirectory = TestDirectory.Create();
            SettingsTestUtils.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);

            // Act and Assert
            var settingsFile = new SettingsFile(mockBaseDirectory);
            settingsFile.TryGetSection("packageNamespaces", out var section).Should().BeTrue();
            section.Should().NotBeNull();

            section.Items.Count.Should().Be(1);
            var packageSourceNamespacesItem = section.Items.First() as PackageNamespacesSourceItem;
            packageSourceNamespacesItem.Namespaces.Should().HaveCount(1);

            var clone = packageSourceNamespacesItem.Clone() as PackageNamespacesSourceItem;
            clone.Namespaces.Clear();

            var ex = Record.Exception(() => packageSourceNamespacesItem.Update(clone));

            ex.Should().NotBeNull();
            ex.Should().BeOfType<InvalidOperationException>();
            ex.Message.Should().Be(string.Format("Package source namespace 'nuget.org' must have at least one namespace."));
        }

        [Fact]
        public void Update_WithAddedAndRemovedNamespaces_CorrectlyAddsAndRemovesNamspaces()
        {
            // Arrange
            var config = @"
<configuration>
    <packageNamespaces>
        <packageSource key=""nuget.org"">
            <namespace id=""first"" />
            <namespace id=""second"" />
        </packageSource>
    </packageNamespaces>
</configuration>";
            var nugetConfigPath = "NuGet.Config";
            using var mockBaseDirectory = TestDirectory.Create();
            SettingsTestUtils.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);

            // Act and Assert
            var settingsFile = new SettingsFile(mockBaseDirectory);
            settingsFile.TryGetSection("packageNamespaces", out var section).Should().BeTrue();
            section.Should().NotBeNull();

            section.Items.Count.Should().Be(1);
            var packageSourceNamespacesItem = section.Items.First() as PackageNamespacesSourceItem;
            packageSourceNamespacesItem.Namespaces.Should().HaveCount(2);

            var clone = packageSourceNamespacesItem.Clone() as PackageNamespacesSourceItem;
            clone.Namespaces.RemoveAt(1);
            clone.Namespaces.Add(new NamespaceItem("third"));

            packageSourceNamespacesItem.Update(clone);
            settingsFile.SaveToDisk();

            // Assert
            var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageNamespaces>
        <packageSource key=""nuget.org"">
            <namespace id=""first"" />
            <namespace id=""third"" />
        </packageSource>
    </packageNamespaces>
</configuration>";

            result.Replace("\r\n", "\n")
                .Should().BeEquivalentTo(
                File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath)).Replace("\r\n", "\n"));
        }
    }
}
