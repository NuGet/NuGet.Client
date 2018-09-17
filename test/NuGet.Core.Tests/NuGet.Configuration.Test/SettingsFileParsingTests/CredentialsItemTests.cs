// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Configuration.Test
{
    public class CredentialsItemTests
    {
        [Theory]
        [InlineData(null, "user", "pass")]
        [InlineData("", "user", "pass")]
        [InlineData("name", null, "pass")]
        [InlineData("name", "", "pass")]
        [InlineData("name", "user", null)]
        [InlineData("name", "user", "")]
        public void CredentialsItem_Constructor_WithEmptyOrNullParameters_Throws(string name, string username, string password)
        {
            var ex = Record.Exception(() => new CredentialsItem(name, username, password, isPasswordClearText: true, validAuthenticationTypes: null));
            ex.Should().NotBeNull();
            ex.Should().BeOfType<ArgumentException>();
        }

        [Fact]
        public void CredentialsItem_Parsing_WithoutUsername_Throws()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <packageSourceCredentials>
        <NuGet.Org meta1='data1'>
            <add key='Password' value='password' />
        </NuGet.Org>
    </packageSourceCredentials>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);

                // Act and Assert
                var ex = Record.Exception(() => new SettingsFile(mockBaseDirectory));

                ex.Should().NotBeNull();
                ex.Should().BeOfType<NuGetConfigurationException>();
                ex.Message.Should().Be(string.Format("Unable to parse config file because: Credentials item must have username and password. Path: '{0}'.", Path.Combine(mockBaseDirectory, nugetConfigPath)));
            }
        }

        [Fact]
        public void CredentialsItem_Parsing_WithoutPasswordOrClearTextPassword_Throws()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <packageSourceCredentials>
        <NuGet.Org meta1='data1'>
            <add key='Username' value='username' />
        </NuGet.Org>
    </packageSourceCredentials>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);

                // Act and Assert
                var ex = Record.Exception(() => new SettingsFile(mockBaseDirectory));

                ex.Should().NotBeNull();
                ex.Should().BeOfType<NuGetConfigurationException>();
                ex.Message.Should().Be(string.Format("Unable to parse config file because: Credentials item must have username and password. Path: '{0}'.", Path.Combine(mockBaseDirectory, nugetConfigPath)));
            }
        }

        [Fact]
        public void CredentialsItem_Parsing_WithUsernamePasswordAndClearTextPassword_TakesFirstAndIgnoresRest()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <packageSourceCredentials>
        <NuGet.Org meta1='data1'>
            <add key='Username' value='username' />
            <add key='Password' value='password' />
            <add key='ClearTextPassword' value='clearTextPassword' />
        </NuGet.Org>
    </packageSourceCredentials>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);

                // Act and Assert
                var settingsFile = new SettingsFile(mockBaseDirectory);
                settingsFile.Should().NotBeNull();

                var section = settingsFile.GetSection("packageSourceCredentials");
                section.Should().NotBeNull();
                section.Items.Count.Should().Be(1);

                var item = section.Items.First() as CredentialsItem;
                item.Should().NotBeNull();

                item.Password.Should().Be("password");
                item.IsPasswordClearText.Should().BeFalse();
            }
        }

        [Fact]
        public void CredentialsItem_Parsing_WithMultipleUsernames_TakesFirstAndIgnoresRest()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <packageSourceCredentials>
        <NuGet.Org meta1='data1'>
            <add key='Username' value='username' />
            <add key='Username' value='username2' />
            <add key='Password' value='password' />
        </NuGet.Org>
    </packageSourceCredentials>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);

                // Act and Assert
                var settingsFile = new SettingsFile(mockBaseDirectory);
                settingsFile.Should().NotBeNull();

                var section = settingsFile.GetSection("packageSourceCredentials");
                section.Should().NotBeNull();
                section.Items.Count.Should().Be(1);

                var item = section.Items.First() as CredentialsItem;
                item.Should().NotBeNull();

                item.Username.Should().Be("username");
            }
        }

        [Fact]
        public void CredentialsItem_Parsing_WithMultipleValidAuthenticationTypes_TakesFirstAndIgnoresRest()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <packageSourceCredentials>
        <NuGet.Org meta1='data1'>
            <add key='Username' value='username' />
            <add key='Password' value='password' />
            <add key='ValidAuthenticationTypes' value='one,two,three' />
            <add key='ValidAuthenticationTypes' value='four,five,six' />
        </NuGet.Org>
    </packageSourceCredentials>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);

                // Act and Assert
                var settingsFile = new SettingsFile(mockBaseDirectory);
                settingsFile.Should().NotBeNull();

                var section = settingsFile.GetSection("packageSourceCredentials");
                section.Should().NotBeNull();
                section.Items.Count.Should().Be(1);

                var item = section.Items.First() as CredentialsItem;
                item.Should().NotBeNull();

                item.ValidAuthenticationTypes.Should().Be("one,two,three");
            }
        }

        [Fact]
        public void CredentialsItem_Update_WhenItemIsNotCredentialsItem_Throws()
        {
            // Arrange
            var credentials = new CredentialsItem("name", "user", "pass", isPasswordClearText: true, validAuthenticationTypes: null);

            // Act
            var ex = Record.Exception(() => credentials.Update(new AddItem("key", "value")));

            // Assert
            ex.Should().NotBeNull();
            ex.Should().BeOfType<InvalidOperationException>();
            ex.Message.Should().Be("The item passed to the Update method cannot refer to a different item than the one being updated.");

            credentials.ElementName.Should().Be("name");
            credentials.Username.Should().Be("user");
            credentials.Password.Should().Be("pass");
        }

        [Fact]
        public void CredentialsItem_Update_WhenOriginIsMachineWide_Throws()
        {
            // Arrange
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var origin = new SettingsFile(mockBaseDirectory, fileName: Settings.DefaultSettingsFileName, isMachineWide: true);

                var xelement = new XElement("name",
                                new XElement("add", new XAttribute("key", "Username"), new XAttribute("value", "user")),
                                new XElement("add", new XAttribute("key", "Password"), new XAttribute("value", "pass")));

                var credentials = new CredentialsItem(xelement, origin);

                // Act
                var ex = Record.Exception(() =>
                    credentials.Update(new CredentialsItem("name", "user", "notpass", isPasswordClearText: true, validAuthenticationTypes: null)));

                // Assert
                ex.Should().NotBeNull();
                ex.Should().BeOfType<InvalidOperationException>();
                ex.Message.Should().Be("Unable to update setting since it is in a machine-wide NuGet.Config.");

                credentials.Password.Should().Be("pass");
            }
        }

        [Fact]
        public void CredentialsItem_Update_ChangeUsername_UpdatesObjectAndXNode()
        {
            // Arrange
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var origin = new SettingsFile(mockBaseDirectory);

                var xelement = new XElement("name",
                                new XElement("add", new XAttribute("key", "Username"), new XAttribute("value", "user")),
                                new XElement("add", new XAttribute("key", "Password"), new XAttribute("value", "pass")));

                var credentials = new CredentialsItem(xelement, origin);

                // Act
                credentials.Update(new CredentialsItem("name", "newuser", "pass", isPasswordClearText: false, validAuthenticationTypes: null));

                // Assert
                credentials.Username.Should().Be("newuser");

                var credentialElement = credentials.AsXNode() as XElement;
                var childElements = credentialElement.Elements().ToList();

                childElements.Count.Should().Be(2);
                childElements[0].Name.LocalName.Should().Be("add");
                var elattr = childElements[0].Attributes().ToList();
                elattr.Count.Should().Be(2);
                elattr[0].Value.Should().Be("Username");
                elattr[1].Value.Should().Be("newuser");
            }
        }

        [Fact]
        public void CredentialsItem_Update_ChangePassword_UpdatesObjectAndXNode()
        {
            // Arrange
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var origin = new SettingsFile(mockBaseDirectory);

                var xelement = new XElement("name",
                                new XElement("add", new XAttribute("key", "Username"), new XAttribute("value", "user")),
                                new XElement("add", new XAttribute("key", "Password"), new XAttribute("value", "pass")));

                var credentials = new CredentialsItem(xelement, origin);

                // Act
                credentials.Update(new CredentialsItem("name", "user", "newpass", isPasswordClearText: false, validAuthenticationTypes: null));

                // Assert
                credentials.Password.Should().Be("newpass");

                var credentialElement = credentials.AsXNode() as XElement;
                var childElements = credentialElement.Elements().ToList();

                childElements.Count.Should().Be(2);
                childElements[1].Name.LocalName.Should().Be("add");
                var elattr = childElements[1].Attributes().ToList();
                elattr.Count.Should().Be(2);
                elattr[0].Value.Should().Be("Password");
                elattr[1].Value.Should().Be("newpass");
            }
        }

        [Fact]
        public void CredentialsItem_Update_ChangeClearTextPassword_UpdatesObjectAndXNode()
        {
            // Arrange
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var origin = new SettingsFile(mockBaseDirectory);

                var xelement = new XElement("name",
                                    new XElement("add", new XAttribute("key", "Username"), new XAttribute("value", "user")),
                                    new XElement("add", new XAttribute("key", "ClearTextPassword"), new XAttribute("value", "pass")));

                var credentials = new CredentialsItem(xelement, origin);

                // Act
                credentials.Update(new CredentialsItem("name", "user", "newpass", isPasswordClearText: true, validAuthenticationTypes: null));

                // Assert
                credentials.Password.Should().Be("newpass");

                var credentialElement = credentials.AsXNode() as XElement;
                var childElements = credentialElement.Elements().ToList();

                childElements.Count.Should().Be(2);
                childElements[1].Name.LocalName.Should().Be("add");
                var elattr = childElements[1].Attributes().ToList();
                elattr.Count.Should().Be(2);
                elattr[0].Value.Should().Be("ClearTextPassword");
                elattr[1].Value.Should().Be("newpass");
            }
        }

        [Fact]
        public void CredentialsItem_Update_MakingPasswordClearText_UpdatesObjectAndXNode()
        {
            // Arrange
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var origin = new SettingsFile(mockBaseDirectory);

                var xelement = new XElement("name",
                                    new XElement("add", new XAttribute("key", "Username"), new XAttribute("value", "user")),
                                    new XElement("add", new XAttribute("key", "Password"), new XAttribute("value", "pass")));

                var credentials = new CredentialsItem(xelement, origin);

                // Act
                credentials.Update(new CredentialsItem("name", "user", "newpass", isPasswordClearText: true, validAuthenticationTypes: null));

                // Assert
                credentials.Password.Should().Be("newpass");

                var credentialElement = credentials.AsXNode() as XElement;
                var childElements = credentialElement.Elements().ToList();

                childElements.Count.Should().Be(2);
                childElements[1].Name.LocalName.Should().Be("add");
                var elattr = childElements[1].Attributes().ToList();
                elattr.Count.Should().Be(2);
                elattr[0].Value.Should().Be("ClearTextPassword");
                elattr[1].Value.Should().Be("newpass");
            }
        }

        [Fact]
        public void CredentialsItem_Update_MakingPasswordEncrypted_UpdatesObjectAndXNode()
        {
            // Arrange
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var origin = new SettingsFile(mockBaseDirectory);

                var xelement = new XElement("name",
                                new XElement("add", new XAttribute("key", "Username"), new XAttribute("value", "user")),
                                new XElement("add", new XAttribute("key", "ClearTextPassword"), new XAttribute("value", "pass")));

                var credentials = new CredentialsItem(xelement, origin);

                // Act
                credentials.Update(new CredentialsItem("name", "user", "newpass", isPasswordClearText: false, validAuthenticationTypes: null));

                // Assert
                credentials.Password.Should().Be("newpass");

                var credentialElement = credentials.AsXNode() as XElement;
                var childElements = credentialElement.Elements().ToList();

                childElements.Count.Should().Be(2);
                childElements[1].Name.LocalName.Should().Be("add");
                var elattr = childElements[1].Attributes().ToList();
                elattr.Count.Should().Be(2);
                elattr[0].Value.Should().Be("Password");
                elattr[1].Value.Should().Be("newpass");
            }
        }


        [Fact]
        public void CredentialsItem_AsXNode_WithUsernameAndPassword_ReturnsCorrectElement()
        {
            // Arrange
            var credentialsItem = new CredentialsItem("name", "username", "password", isPasswordClearText: false, validAuthenticationTypes: null);

            // Act
            var xnode = credentialsItem.AsXNode();

            // Assert
            xnode.Should().BeOfType<XElement>();
            var xelement = xnode as XElement;

            xelement.Name.LocalName.Should().Be("name");
            var elements = xelement.Elements().ToList();
            elements.Count.Should().Be(2);
            elements[0].Name.LocalName.Should().Be("add");
            var elattr = elements[0].Attributes().ToList();
            elattr.Count.Should().Be(2);
            elattr[0].Value.Should().Be("Username");
            elattr[1].Value.Should().Be("username");

            elements[1].Name.LocalName.Should().Be("add");
            elattr = elements[1].Attributes().ToList();
            elattr.Count.Should().Be(2);
            elattr[0].Value.Should().Be("Password");
            elattr[1].Value.Should().Be("password");
        }

        [Fact]
        public void CredentialsItem_AsXNode_WithUsernameAndClearTextPassword_ReturnsCorrectElement()
        {
            // Arrange
            var credentialsItem = new CredentialsItem("name", "username", "password", isPasswordClearText: true, validAuthenticationTypes: null);

            // Act
            var xnode = credentialsItem.AsXNode();

            // Assert
            xnode.Should().BeOfType<XElement>();
            var xelement = xnode as XElement;

            xelement.Name.LocalName.Should().Be("name");
            var elements = xelement.Elements().ToList();
            elements.Count.Should().Be(2);
            elements[0].Name.LocalName.Should().Be("add");
            var elattr = elements[0].Attributes().ToList();
            elattr.Count.Should().Be(2);
            elattr[0].Value.Should().Be("Username");
            elattr[1].Value.Should().Be("username");

            elements[1].Name.LocalName.Should().Be("add");
            elattr = elements[1].Attributes().ToList();
            elattr.Count.Should().Be(2);
            elattr[0].Value.Should().Be("ClearTextPassword");
            elattr[1].Value.Should().Be("password");
        }
    }
}
