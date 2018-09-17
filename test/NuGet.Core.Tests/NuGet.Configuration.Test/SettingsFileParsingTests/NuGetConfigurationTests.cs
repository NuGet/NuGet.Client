// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Xml.Linq;
using FluentAssertions;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Configuration.Test
{
    public class NuGetConfigurationTests
    {
        [Fact]
        public void NuGetConfiguration_AsXNode_ReturnsExpectedXNode()
        {
            var configuration = new NuGetConfiguration(
                new VirtualSettingSection("Section",
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
        public void NuGetConfiguration__CaseInsensitive_ParsedSuccessfully()
        {
            // Arrange
            var config = @"
<cOnfiGUraTiOn>
</cOnfiGUraTiOn>";

            var nugetConfigPath = "NuGet.Config";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);

                // Act and Assert
                var settingsFile = new SettingsFile(mockBaseDirectory);
                settingsFile.Should().NotBeNull();
            }
        }
    }
}
