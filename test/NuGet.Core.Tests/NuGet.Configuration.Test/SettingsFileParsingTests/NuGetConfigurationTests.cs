// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using FluentAssertions;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Configuration.Test
{
    public class NuGetConfigurationTests
    {
        [Fact]
        public void AsXNode_ReturnsExpectedXNode()
        {
            var configuration = new NuGetConfiguration(
                new SettingsSection("Section",
                    new AddItem("key0", "value0")));

            var expectedXNode = new XElement("configuration",
                new XElement("Section",
                    new XElement("add",
                        new XAttribute("key", "key0"),
                        new XAttribute("value", "value0"))));

            var xNode = configuration.AsXNode();

            XNode.DeepEquals(xNode, expectedXNode).Should().BeTrue();
        }

        [Fact]
        public void AddChild_OnMachineWideConfig_ReturnsFalse()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <Section>
        <add key='key0' value='value0' />
    </Section>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                var configFileHash = ConfigurationFileTestUtility.GetFileHash(Path.Combine(mockBaseDirectory, nugetConfigPath));

                var settingsFile = new SettingsFile(mockBaseDirectory, nugetConfigPath, isMachineWide: true);

                // Act
                var section = settingsFile.GetSection("Section");
                section.Should().NotBeNull();

                section.AddChild(new AddItem("key2", "value2")).Should().BeFalse();
                section.Children.Count.Should().Be(1);

                var updatedFileHash = ConfigurationFileTestUtility.GetFileHash(Path.Combine(mockBaseDirectory, nugetConfigPath));
                updatedFileHash.Should().BeEquivalentTo(configFileHash);
            }
        }
    }
}
