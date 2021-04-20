// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Configuration.Test
{
    public class NamespaceItemTests
    {
        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void Constructor_WithInvalidNamespace_Throws(string name)
        {
            Assert.Throws<ArgumentException>(() => new NamespaceItem(name));
        }

        [Theory]
        [InlineData("NuGet.Common", "NuGet.Common")]
        [InlineData("nuget.common", "NuGet.Common")]
        [InlineData("NuGet.*", "NuGet.*")]
        [InlineData("NuGet.*", "nuget.*")]
        [InlineData("*", "*")]
        public void Equals_WithEquivalentNamespaces_ReturnsTrue(string first, string second)
        {
            var firstNamespace = new NamespaceItem(first);
            var secondNamespace = new NamespaceItem(second);

            firstNamespace.Should().Equals(secondNamespace);
        }

        [Theory]
        [InlineData("NuGet.", "NuGet.Common")]
        [InlineData("NuGet.*", "stuff")]
        [InlineData("NuGet.*", "NuGet.")]
        public void Equals_WithUnequivalentNamespaces_ReturnsFalse(string first, string second)
        {
            var firstNamespace = new NamespaceItem(first);
            var secondNamespace = new NamespaceItem(second);

            firstNamespace.Equals(secondNamespace).Should().BeFalse();
        }

        [Theory]
        [InlineData("NuGet.Common", "NuGet.Common")]
        [InlineData("nuget.common", "NuGet.Common")]
        [InlineData("NuGet.*", "NuGet.*")]
        [InlineData("NuGet.*", "nuget.*")]
        public void HashCode_WithEquivalentNamespaces_ReturnsTrue(string first, string second)
        {
            var firstNamespace = new NamespaceItem(first);
            var secondNamespace = new NamespaceItem(second);

            firstNamespace.GetHashCode().Should().Equals(secondNamespace.GetHashCode());
        }

        [Theory]
        [InlineData("NuGet.", "NuGet.Common")]
        [InlineData("NuGet.*", "stuff")]
        [InlineData("NuGet.*", "NuGet.")]
        public void HashCode_WithUnequivalentNamespaces_ReturnsFalse(string first, string second)
        {
            var firstNamespace = new NamespaceItem(first);
            var secondNamespace = new NamespaceItem(second);

            firstNamespace.GetHashCode().Equals(secondNamespace.GetHashCode()).Should().BeFalse();
        }

        [Theory]
        [InlineData("NuGet.Common")]
        [InlineData("nuget.common")]
        [InlineData("NuGet.*")]
        [InlineData("nuget.*")]
        public void Clone_CreatesEquivalentObjects(string namespaceName)
        {
            var original = new NamespaceItem(namespaceName);
            var clone = original.Clone() as NamespaceItem;

            original.Equals(clone).Should().BeTrue();
            original.GetHashCode().Equals(clone.GetHashCode()).Should().BeTrue();
            SettingsTestUtils.DeepEquals(original, clone).Should().BeTrue();
            ReferenceEquals(original, clone).Should().BeFalse();
            original.Id.Equals(clone.Id);
        }

        [Fact]
        public void ElementNameGetter_ReturnsNamespace()
        {
            var original = new NamespaceItem("item");
            original.ElementName.Should().Be("namespace");
        }

        [Fact]
        public void NamespaceItemParse_WithoutId_Throws()
        {
            // Arrange
            var config = @"
<configuration>
    <packageNamespaces>
        <packageSource key=""nuget.org"">
            <namespace stuff=""sadas""  />
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
            ex.Message.Should().Be(string.Format("Unable to parse config file because: Missing required attribute 'id' in element 'namespace'. Path: '{0}'.", Path.Combine(mockBaseDirectory, nugetConfigPath)));
        }

        [Fact]
        public void NamespaceItemParse_WithChildren_Throws()
        {
            // Arrange
            var config = @"
<configuration>
    <packageNamespaces>
        <packageSource key=""nuget.org"">
            <namespace id=""sadas"">
                <add key=""key"" value=""val"" />
            </namespace>
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
            ex.Message.Should().Be(string.Format("Error parsing NuGet.Config. Element 'namespace' cannot have descendant elements. Path: '{0}'.", Path.Combine(mockBaseDirectory, nugetConfigPath)));
        }

        [Fact]
        public void NamespaceItemParse_WithValidData_ParsesCorrectly()
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
            var item = (section.Items.First() as PackageSourceNamespacesItem).Namespaces.First();

            var expectedItem = new NamespaceItem("sadas");
            SettingsTestUtils.DeepEquals(item, expectedItem).Should().BeTrue();
        }

        [Fact]
        public void Update_UpdatesKeyCorrectly()
        {
            // Arrange
            var config = @"
<configuration>
    <packageNamespaces>
        <packageSource key=""nuget.org"">
            <namespace id=""original"" />
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
            var packageSourceNamespacesItem = section.Items.First() as PackageSourceNamespacesItem;
            var updatedItem = packageSourceNamespacesItem.Namespaces.First().Clone() as NamespaceItem;
            updatedItem.Id = "updated";

            packageSourceNamespacesItem.Namespaces.First().Update(updatedItem);
            SettingsTestUtils.DeepEquals(packageSourceNamespacesItem.Namespaces.First(), updatedItem).Should().BeTrue();

            settingsFile.SaveToDisk();

            // Assert
            var result = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <packageNamespaces>
        <packageSource key=""nuget.org"">
            <namespace id=""updated"" />
        </packageSource>
    </packageNamespaces>
</configuration>";

            result.Replace("\r\n", "\n")
                .Should().BeEquivalentTo(
                File.ReadAllText(Path.Combine(mockBaseDirectory, nugetConfigPath)).Replace("\r\n", "\n"));
        }
    }
}
