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
                new AbstractSettingSection("Section",
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
        public void AddOrUpdate_OnMachineWideConfig_Throws()
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
                var ex = Record.Exception(() => settingsFile.AddOrUpdate("Section", new AddItem("key2", "value2")));
                ex.Should().NotBeNull();
                ex.Should().BeOfType<InvalidOperationException>();
                ex.Message.Should().Be("Unable to update setting since it is in a machine wide NuGet.Config");

                settingsFile.SaveToDisk();

                var section = settingsFile.GetSection("Section");
                section.Items.Count.Should().Be(1);

                var updatedFileHash = ConfigurationFileTestUtility.GetFileHash(Path.Combine(mockBaseDirectory, nugetConfigPath));
                updatedFileHash.Should().BeEquivalentTo(configFileHash);
            }
        }
    }
}
