// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Configuration.Test
{
    public class SourceItemTests
    {
        [Fact]
        public void SourceItem_CaseInsensitive_ParsedSuccessfully()
        {
            // Arrange
            var config = @"
<configuration>
    <PACkagEsourCEs>
        <AdD key='nugetorg' value='http://serviceIndexorg.test/api/index.json' />
    </PACkagEsourCEs>
</configuration>";

            var expectedValue = new SourceItem("nugetorg", "http://serviceIndexorg.test/api/index.json");

            var nugetConfigPath = "NuGet.Config";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);

                // Act and Assert
                var settingsFile = new SettingsFile(mockBaseDirectory);
                settingsFile.Should().NotBeNull();

                var section = settingsFile.GetSection("PACkagEsourCEs");
                section.Should().NotBeNull();

                var children = section.Items.ToList();

                children.Should().NotBeEmpty();
                children.Count.Should().Be(1);

                children[0].DeepEquals(expectedValue).Should().BeTrue();
            }
        }

        [Fact]
        public void SourceItem_ParsedSuccessfully()
        {
            // Arrange
            var config = @"
<configuration>
    <packageSources>
        <add key='nugetorg' value='http://serviceIndexorg.test/api/index.json' />
        <add key='nuget2' value='http://serviceIndex.test/api/index.json' protocolVersion='3' />
    </packageSources>
</configuration>";

            var expectedValues = new List<SourceItem>()
            {
                new SourceItem("nugetorg","http://serviceIndexorg.test/api/index.json"),
                new SourceItem("nuget2","http://serviceIndex.test/api/index.json", "3" ),
            };

            var nugetConfigPath = "NuGet.Config";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);

                // Act and Assert
                var settingsFile = new SettingsFile(mockBaseDirectory);
                settingsFile.Should().NotBeNull();

                var section = settingsFile.GetSection("packageSources");
                section.Should().NotBeNull();

                var children = section.Items.ToList();

                children.Should().NotBeEmpty();
                children.Count.Should().Be(2);

                for (var i = 0; i < children.Count; i++)
                {
                    children[i].DeepEquals(expectedValues[i]).Should().BeTrue();
                }
            }
        }

        [Fact]
        public void SourceItem_Parsing_ElementWithChildren_Throws()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <packageSources>
        <add key='nugetorg' value='http://serviceIndexorg.test/api/index.json'>
            <add key='key2' value='value2' />
        </add>
    </packageSources>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);

                // Act and Assert
                var ex = Record.Exception(() => new SettingsFile(mockBaseDirectory));

                ex.Should().NotBeNull();
                ex.Should().BeOfType<NuGetConfigurationException>();
                ex.Message.Should().Be(string.Format("Error parsing NuGet.Config. Element '{0}' cannot have descendant elements. Path: '{1}'.", "add", Path.Combine(mockBaseDirectory, nugetConfigPath)));
            }
        }
    }
}
