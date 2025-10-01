// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using FluentAssertions;
using NuGet.Frameworks;
using Xunit;

namespace NuGet.Commands.Test.RestoreCommandTests.Utility
{
    public class FrameworkRuntimeDefinitionTests
    {
        [Fact]
        public void Constructor_WithValidParameters_SetsPropertiesCorrectly()
        {
            // Arrange
            string targetAlias = "net6.0";
            var framework = FrameworkConstants.CommonFrameworks.Net60;
            string runtimeIdentifier = "win-x64";

            // Act
            var definition = new FrameworkRuntimeDefinition(targetAlias, framework, runtimeIdentifier);

            // Assert
            definition.TargetAlias.Should().Be(targetAlias);
            definition.Framework.Should().Be(framework);
            definition.RuntimeIdentifier.Should().Be(runtimeIdentifier);
            definition.Name.Should().Be("net6.0/win-x64");
        }

        [Fact]
        public void Constructor_WithNullTargetAlias_SetsEmptyString()
        {
            // Arrange
            var framework = FrameworkConstants.CommonFrameworks.Net60;
            string runtimeIdentifier = "win-x64";

            // Act
            var definition = new FrameworkRuntimeDefinition(null!, framework, runtimeIdentifier);

            // Assert
            definition.TargetAlias.Should().Be(string.Empty);
            definition.Framework.Should().Be(framework);
            definition.RuntimeIdentifier.Should().Be(runtimeIdentifier);
        }

        [Fact]
        public void Constructor_WithNullRuntimeIdentifier_SetsEmptyString()
        {
            // Arrange
            string targetAlias = "net6.0";
            var framework = FrameworkConstants.CommonFrameworks.Net60;

            // Act
            var definition = new FrameworkRuntimeDefinition(targetAlias, framework, null);

            // Assert
            definition.TargetAlias.Should().Be(targetAlias);
            definition.Framework.Should().Be(framework);
            definition.RuntimeIdentifier.Should().Be(string.Empty);
            definition.Name.Should().Be("net6.0");
        }

        [Fact]
        public void Constructor_WithNullFramework_ThrowsArgumentNullException()
        {
            // Arrange
            string targetAlias = "net6.0";
            string runtimeIdentifier = "win-x64";

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new FrameworkRuntimeDefinition(targetAlias, null!, runtimeIdentifier));
        }

        [Fact]
        public void Equals_WithSameValues_ReturnsTrue()
        {
            // Arrange
            string targetAlias = "net6.0";
            var framework = FrameworkConstants.CommonFrameworks.Net60;
            string runtimeIdentifier = "win-x64";

            var definition1 = new FrameworkRuntimeDefinition(targetAlias, framework, runtimeIdentifier);
            var definition2 = new FrameworkRuntimeDefinition(targetAlias, framework, runtimeIdentifier);

            // Act & Assert
            definition1.Equals(definition2).Should().BeTrue();
            definition1.Should().Be(definition2);
        }

        [Fact]
        public void Equals_WithDifferentTargetAlias_ReturnsFalse()
        {
            // Arrange
            var framework = FrameworkConstants.CommonFrameworks.Net60;
            string runtimeIdentifier = "win-x64";

            var definition1 = new FrameworkRuntimeDefinition("net6.0", framework, runtimeIdentifier);
            var definition2 = new FrameworkRuntimeDefinition("net7.0", framework, runtimeIdentifier);

            // Act & Assert
            definition1.Equals(definition2).Should().BeFalse();
            definition1.Should().NotBe(definition2);
        }

        [Fact]
        public void Equals_WithDifferentFramework_ReturnsFalse()
        {
            // Arrange
            string targetAlias = "net6.0";
            string runtimeIdentifier = "win-x64";

            var definition1 = new FrameworkRuntimeDefinition(targetAlias, FrameworkConstants.CommonFrameworks.Net60, runtimeIdentifier);
            var definition2 = new FrameworkRuntimeDefinition(targetAlias, FrameworkConstants.CommonFrameworks.Net70, runtimeIdentifier);

            // Act & Assert
            definition1.Equals(definition2).Should().BeFalse();
            definition1.Should().NotBe(definition2);
        }

        [Fact]
        public void Equals_WithDifferentRuntimeIdentifier_ReturnsFalse()
        {
            // Arrange
            string targetAlias = "net6.0";
            var framework = FrameworkConstants.CommonFrameworks.Net60;

            var definition1 = new FrameworkRuntimeDefinition(targetAlias, framework, "win-x64");
            var definition2 = new FrameworkRuntimeDefinition(targetAlias, framework, "linux-x64");

            // Act & Assert
            definition1.Equals(definition2).Should().BeFalse();
            definition1.Should().NotBe(definition2);
        }

        [Fact]
        public void Equals_WithNull_ReturnsFalse()
        {
            // Arrange
            var definition = new FrameworkRuntimeDefinition("net6.0", FrameworkConstants.CommonFrameworks.Net60, "win-x64");

            // Act & Assert
            definition.Equals(null).Should().BeFalse();
        }

        [Theory]
        [InlineData("net6.0", "NET6.0")] // Case sensitive
        [InlineData("win-x64", "WIN-X64")] // Case sensitive
        public void Equals_WithTargetAlias_CaseSensitiveComparison(string value1, string value2)
        {
            // Arrange
            var framework = FrameworkConstants.CommonFrameworks.Net60;

            var definition1 = new FrameworkRuntimeDefinition(value1, framework, "win-x64");
            var definition2 = new FrameworkRuntimeDefinition(value2, framework, "win-x64");

            // Act & Assert
            definition1.Equals(definition2).Should().BeTrue();
            definition1.Should().Be(definition2);
        }

        [Fact]
        public void GetHashCode_WithSameValues_ReturnsSameHashCode()
        {
            // Arrange
            string targetAlias = "net6.0";
            var framework = FrameworkConstants.CommonFrameworks.Net60;
            string runtimeIdentifier = "win-x64";

            var definition1 = new FrameworkRuntimeDefinition(targetAlias, framework, runtimeIdentifier);
            var definition2 = new FrameworkRuntimeDefinition(targetAlias, framework, runtimeIdentifier);

            // Act & Assert
            definition1.GetHashCode().Should().Be(definition2.GetHashCode());
        }

        [Fact]
        public void GetHashCode_WithDifferentValues_ReturnsDifferentHashCodes()
        {
            // Arrange
            var framework = FrameworkConstants.CommonFrameworks.Net60;

            var definition1 = new FrameworkRuntimeDefinition("net6.0", framework, "win-x64");
            var definition2 = new FrameworkRuntimeDefinition("net7.0", framework, "win-x64");

            // Act & Assert
            definition1.GetHashCode().Should().NotBe(definition2.GetHashCode());
        }

        [Fact]
        public void ToString_WithAllValues_ReturnsExpectedFormat()
        {
            // Arrange
            string targetAlias = "net6.0";
            var framework = FrameworkConstants.CommonFrameworks.Net60;
            string runtimeIdentifier = "win-x64";

            var definition = new FrameworkRuntimeDefinition(targetAlias, framework, runtimeIdentifier);

            // Act
            string result = definition.ToString();

            // Assert
            result.Should().Be("net6.0~net6.0~win-x64");
        }

        [Theory]
        [InlineData("net472", "net5.0", -1)] // Different framework versions
        [InlineData("net5.0", "net472", 1)]
        [InlineData("net6.0", "net6.0", 0)] // Same framework
        public void CompareTo_ByFramework_ReturnsExpectedResult(string framework1, string framework2, int expected)
        {
            // Arrange
            var fw1 = NuGetFramework.Parse(framework1);
            var fw2 = NuGetFramework.Parse(framework2);
            var definition1 = new FrameworkRuntimeDefinition("alias1", fw1, "win-x64");
            var definition2 = new FrameworkRuntimeDefinition("alias1", fw2, "win-x64");

            // Act
            int result = definition1.CompareTo(definition2);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("alias1", "alias2", -1)]
        [InlineData("alias2", "alias1", 1)]
        [InlineData("alias1", "alias1", 0)]
        public void CompareTo_ByTargetAlias_WhenFrameworksEqual_ReturnsExpectedResult(string alias1, string alias2, int expected)
        {
            // Arrange
            var framework = FrameworkConstants.CommonFrameworks.Net60;
            var definition1 = new FrameworkRuntimeDefinition(alias1, framework, "win-x64");
            var definition2 = new FrameworkRuntimeDefinition(alias2, framework, "win-x64");

            // Act
            int result = definition1.CompareTo(definition2);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("linux-x64", "win-x64", -11)]
        [InlineData("win-x64", "linux-x64", 11)]
        [InlineData("win-x64", "win-x64", 0)]
        public void CompareTo_ByRuntimeIdentifier_WhenFrameworkAndAliasEqual_ReturnsExpectedResult(string rid1, string rid2, int expected)
        {
            // Arrange
            var framework = FrameworkConstants.CommonFrameworks.Net60;
            string targetAlias = "net6.0";
            var definition1 = new FrameworkRuntimeDefinition(targetAlias, framework, rid1);
            var definition2 = new FrameworkRuntimeDefinition(targetAlias, framework, rid2);

            // Act
            int result = definition1.CompareTo(definition2);

            // Assert
            result.Should().Be(expected);
        }

        [Fact]
        public void CompareTo_WithNull_ReturnsOne()
        {
            // Arrange
            var definition = new FrameworkRuntimeDefinition("net6.0", FrameworkConstants.CommonFrameworks.Net60, "win-x64");

            // Act
            int result = definition.CompareTo(null);

            // Assert
            result.Should().Be(1);
        }

        [Fact]
        public void CompareTo_ComparisonPriority_FrameworkThenAliasThenRuntimeIdentifier()
        {
            // Arrange
            var definition1 = new FrameworkRuntimeDefinition("b", FrameworkConstants.CommonFrameworks.Net50, "z");
            var definition2 = new FrameworkRuntimeDefinition("a", FrameworkConstants.CommonFrameworks.Net60, "a");

            // Act
            int result = definition1.CompareTo(definition2);

            // Assert
            // Even though definition2 has smaller alias and RID, framework comparison should take precedence
            // net5.0 < net6.0, so result should be negative
            result.Should().BeLessThan(0);
        }

        [Fact]
        public void Name_Property_WithEmptyRuntimeIdentifier_ReturnsFrameworkOnly()
        {
            // Arrange
            var framework = FrameworkConstants.CommonFrameworks.Net60;

            var definition = new FrameworkRuntimeDefinition("alias", framework, string.Empty);

            // Act & Assert
            definition.Name.Should().Be(framework.ToString());
        }

        [Fact]
        public void Name_Property_WithNullRuntimeIdentifier_ReturnsFrameworkOnly()
        {
            // Arrange
            var framework = FrameworkConstants.CommonFrameworks.Net60;

            var definition = new FrameworkRuntimeDefinition("alias", framework, null);

            // Act & Assert
            definition.Name.Should().Be(framework.ToString());
        }
    }
}
