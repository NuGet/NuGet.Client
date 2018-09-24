// Copyright(c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Configuration.Test
{
    public class SettingTextTests
    {
        [Fact]
        public void SettingText_ParsedSuccessfully()
        {
            // Arrange
            var config = @"
<configuration>
    <SectionName>
        <Item>This is a test</Item>
    </SectionName>
</configuration>";
            var nugetConfigPath = "NuGet.Config";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                SettingsTestUtils.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);

                // Act and Assert
                var settingsFile = new SettingsFile(mockBaseDirectory);
                settingsFile.Should().NotBeNull();

                var section = settingsFile.GetSection("SectionName");
                section.Should().NotBeNull();

                section.Items.Count.Should().Be(1);
                var item = section.Items.First() as UnknownItem;
                item.Should().NotBeNull();

                item.Children.Count.Should().Be(1);
                var text = item.Children.First() as SettingText;
                text.Should().NotBeNull();

                text.Value.Should().Be("This is a test");
            }
        }

        [Fact]
        public void SettingText_UpdatesSuccessfully()
        {
            // Arrange
            var config = @"
<configuration>
    <SectionName>
        <Item>This is a test</Item>
    </SectionName>
</configuration>";
            var nugetConfigPath = "NuGet.Config";
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                SettingsTestUtils.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);

                // Act and Assert
                var settingsFile = new SettingsFile(mockBaseDirectory);
                settingsFile.Should().NotBeNull();

                var section = settingsFile.GetSection("SectionName");
                section.Should().NotBeNull();

                section.Items.Count.Should().Be(1);
                var item = section.Items.First() as UnknownItem;
                item.Should().NotBeNull();

                item.Children.Count.Should().Be(1);
                var text = item.Children.First() as SettingText;
                text.Should().NotBeNull();

                text.Value.Should().Be("This is a test");

                text.Value = "This is another test";

                settingsFile.AddOrUpdate("SectionName", new UnknownItem(item.ElementName, attributes: null, children: new List<SettingBase>() { text }));

                section = settingsFile.GetSection("SectionName");
                section.Should().NotBeNull();

                section.Items.Count.Should().Be(1);
                item = section.Items.First() as UnknownItem;
                item.Should().NotBeNull();

                item.Children.Count.Should().Be(1);
                text = item.Children.First() as SettingText;
                text.Should().NotBeNull();

                text.Value.Should().Be("This is another test");
            }
        }
    }
}
