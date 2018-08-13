// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;
using Moq;
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
        public void Constructor_WithEmptyOrNullParameters_Throws(string name, string username, string password)
        {
            var ex = Record.Exception(() => new CredentialsItem(name, username, password, isPasswordClearText: true, validAuthenticationTypes: null));
            ex.Should().NotBeNull();
            ex.Should().BeOfType<ArgumentNullException>();
        }

        [Fact]
        public void Parsing_WithoutUsername_Throws()
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
                ex.Message.Should().Be(string.Format("Unable to parse config file '{0}'.", Path.Combine(mockBaseDirectory, nugetConfigPath)));
            }
        }

        [Fact]
        public void Parsing_WithoutPasswordOrClearTextPassword_Throws()
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
                ex.Message.Should().Be(string.Format("Unable to parse config file '{0}'.", Path.Combine(mockBaseDirectory, nugetConfigPath)));
            }
        }

        [Fact]
        public void Parsing_WithUsernamePasswordAndClearTextPassword_Throws()
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
                var ex = Record.Exception(() => new SettingsFile(mockBaseDirectory));

                ex.Should().NotBeNull();
                ex.Should().BeOfType<NuGetConfigurationException>();
                ex.Message.Should().Be(string.Format("Unable to parse config file '{0}'.", Path.Combine(mockBaseDirectory, nugetConfigPath)));
            }
        }

        [Fact]
        public void WithAdditionalMetada_Throws()
        {
            // Arrange
            var nugetConfigPath = "NuGet.Config";
            var config = @"
<configuration>
    <packageSourceCredentials>
        <NuGet.Org meta1='data1'>
            <add key='Username' value='username' />
            <add key='Password' value='password' />
        </NuGet.Org>
    </packageSourceCredentials>
</configuration>";

            using (var mockBaseDirectory = TestDirectory.Create())
            {
                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);

                // Act
                var ex = Record.Exception(() => new SettingsFile(mockBaseDirectory));

                // Assert
                ex.Should().NotBeNull();
                ex.Should().BeOfType<NuGetConfigurationException>();
                ex.Message.Should().Be(string.Format("Unable to parse config file '{0}'.", Path.Combine(mockBaseDirectory, nugetConfigPath)));
            }
        }

        [Fact]
        public void Update_WhenItemIsNotCredentialsItem_ReturnsFalse()
        {
            // Arrange
            var credentials = new CredentialsItem("name", "user", "pass", isPasswordClearText: true, validAuthenticationTypes: null);

            // Act
            credentials.Update(new AddItem("key", "value")).Should().BeFalse();

            // Assert
            credentials.Name.Should().Be("name");
            credentials.Username.Value.Should().Be("user");
            credentials.Password.Value.Should().Be("pass");
        }

        [Fact]
        public void Update_WhenItemIsADifferentCredentialsItem_ReturnsFalse()
        {
            // Arrange
            var credentials = new CredentialsItem("name", "user", "pass", isPasswordClearText: true, validAuthenticationTypes: null);

            // Act
            credentials.Update(new CredentialsItem("notName", "user", "notpass", isPasswordClearText: true, validAuthenticationTypes: null)).Should().BeFalse();

            // Assert
            credentials.Name.Should().Be("name");
            credentials.Username.Value.Should().Be("user");
            credentials.Password.Value.Should().Be("pass");
        }

        [Fact]
        public void Update_WhenOriginIsMachineWide_ReturnsFalse()
        {
            // Arrange
            var origin = new Mock<ISettingsFile>(MockBehavior.Strict);
            origin.Setup(s => s.IsMachineWide)
                .Returns(true);

            var xelement = new XElement("name",
                                new XElement("add", new XAttribute("key", "Username"), new XAttribute("value", "user")),
                                new XElement("add", new XAttribute("key", "Password"), new XAttribute("value", "pass")));

            var credentials = new CredentialsItem(xelement, origin.Object);

            // Act
            credentials.Update(new CredentialsItem("name", "user", "notpass", isPasswordClearText: true, validAuthenticationTypes: null)).Should().BeFalse();

            // Assert
            credentials.Password.Value.Should().Be("pass");
        }

        [Fact]
        public void Update_ChangeUsername_UpdatesObjectAndXNode()
        {
            // Arrange
            var origin = new Mock<ISettingsFile>(MockBehavior.Strict);
            origin.Setup(o => o.IsMachineWide)
                .Returns(false);
            origin.Setup(o => o.IsDirty)
                .Returns(true);
            origin.SetupSet(o => o.IsDirty = It.IsAny<bool>()).Verifiable();

            var xelement = new XElement("name",
                                new XElement("add", new XAttribute("key", "Username"), new XAttribute("value", "user")),
                                new XElement("add", new XAttribute("key", "Password"), new XAttribute("value", "pass")));

            var credentials = new CredentialsItem(xelement, origin.Object);

            // Act
            credentials.Update(new CredentialsItem("name", "newuser", "pass", isPasswordClearText: false, validAuthenticationTypes: null), isBatchOperation: true).Should().BeTrue();

            // Assert
            credentials.Username.Value.Should().Be("newuser");

            var credentialElement = credentials.AsXNode() as XElement;
            var childElements = credentialElement.Elements().ToList();

            childElements.Count.Should().Be(2);
            childElements[0].Name.LocalName.Should().Be("add");
            var elattr = childElements[0].Attributes().ToList();
            elattr.Count.Should().Be(2);
            elattr[0].Value.Should().Be("Username");
            elattr[1].Value.Should().Be("newuser");
        }

        [Fact]
        public void Update_ChangePassword_UpdatesObjectAndXNode()
        {
            // Arrange
            var origin = new Mock<ISettingsFile>(MockBehavior.Strict);
            origin.Setup(s => s.IsMachineWide)
                .Returns(false);
            origin.SetupSet(o => o.IsDirty = It.IsAny<bool>()).Verifiable();

            var xelement = new XElement("name",
                                new XElement("add", new XAttribute("key", "Username"), new XAttribute("value", "user")),
                                new XElement("add", new XAttribute("key", "Password"), new XAttribute("value", "pass")));

            var credentials = new CredentialsItem(xelement, origin.Object);

            // Act
            credentials.Update(new CredentialsItem("name", "user", "newpass", isPasswordClearText: false, validAuthenticationTypes: null), isBatchOperation: true).Should().BeTrue();

            // Assert
            credentials.Password.Value.Should().Be("newpass");

            var credentialElement = credentials.AsXNode() as XElement;
            var childElements = credentialElement.Elements().ToList();

            childElements.Count.Should().Be(2);
            childElements[1].Name.LocalName.Should().Be("add");
            var elattr = childElements[1].Attributes().ToList();
            elattr.Count.Should().Be(2);
            elattr[0].Value.Should().Be("Password");
            elattr[1].Value.Should().Be("newpass");
        }

        [Fact]
        public void Update_ChangeClearTextPassword_UpdatesObjectAndXNode()
        {
            // Arrange
            var origin = new Mock<ISettingsFile>(MockBehavior.Strict);
            origin.Setup(s => s.IsMachineWide)
                .Returns(false);
            origin.SetupSet(o => o.IsDirty = It.IsAny<bool>()).Verifiable();

            var xelement = new XElement("name",
                                new XElement("add", new XAttribute("key", "Username"), new XAttribute("value", "user")),
                                new XElement("add", new XAttribute("key", "ClearTextPassword"), new XAttribute("value", "pass")));

            var credentials = new CredentialsItem(xelement, origin.Object);

            // Act
            credentials.Update(new CredentialsItem("name", "user", "newpass", isPasswordClearText: true, validAuthenticationTypes: null), isBatchOperation: true).Should().BeTrue();

            // Assert
            credentials.Password.Value.Should().Be("newpass");

            var credentialElement = credentials.AsXNode() as XElement;
            var childElements = credentialElement.Elements().ToList();

            childElements.Count.Should().Be(2);
            childElements[1].Name.LocalName.Should().Be("add");
            var elattr = childElements[1].Attributes().ToList();
            elattr.Count.Should().Be(2);
            elattr[0].Value.Should().Be("ClearTextPassword");
            elattr[1].Value.Should().Be("newpass");
        }

        [Fact]
        public void Update_MakingPasswordClearText_UpdatesObjectAndXNode()
        {
            // Arrange
            var origin = new Mock<ISettingsFile>(MockBehavior.Strict);
            origin.Setup(s => s.IsMachineWide)
                .Returns(false);
            origin.SetupSet(o => o.IsDirty = It.IsAny<bool>()).Verifiable();

            var xelement = new XElement("name",
                                new XElement("add", new XAttribute("key", "Username"), new XAttribute("value", "user")),
                                new XElement("add", new XAttribute("key", "Password"), new XAttribute("value", "pass")));

            var credentials = new CredentialsItem(xelement, origin.Object);

            // Act
            credentials.Update(new CredentialsItem("name", "user", "newpass", isPasswordClearText: true, validAuthenticationTypes: null), isBatchOperation: true).Should().BeTrue();

            // Assert
            credentials.Password.Value.Should().Be("newpass");

            var credentialElement = credentials.AsXNode() as XElement;
            var childElements = credentialElement.Elements().ToList();

            childElements.Count.Should().Be(2);
            childElements[1].Name.LocalName.Should().Be("add");
            var elattr = childElements[1].Attributes().ToList();
            elattr.Count.Should().Be(2);
            elattr[0].Value.Should().Be("ClearTextPassword");
            elattr[1].Value.Should().Be("newpass");
        }

        [Fact]
        public void Update_MakingPasswordEncrypted_UpdatesObjectAndXNode()
        {
            // Arrange
            var origin = new Mock<ISettingsFile>(MockBehavior.Strict);
            origin.Setup(s => s.IsMachineWide)
                .Returns(false);
            origin.SetupSet(o => o.IsDirty = It.IsAny<bool>()).Verifiable();

            var xelement = new XElement("name",
                                new XElement("add", new XAttribute("key", "Username"), new XAttribute("value", "user")),
                                new XElement("add", new XAttribute("key", "ClearTextPassword"), new XAttribute("value", "pass")));

            var credentials = new CredentialsItem(xelement, origin.Object);

            // Act
            credentials.Update(new CredentialsItem("name", "user", "newpass", isPasswordClearText: false, validAuthenticationTypes: null), isBatchOperation: true).Should().BeTrue();

            // Assert
            credentials.Password.Value.Should().Be("newpass");

            var credentialElement = credentials.AsXNode() as XElement;
            var childElements = credentialElement.Elements().ToList();

            childElements.Count.Should().Be(2);
            childElements[1].Name.LocalName.Should().Be("add");
            var elattr = childElements[1].Attributes().ToList();
            elattr.Count.Should().Be(2);
            elattr[0].Value.Should().Be("Password");
            elattr[1].Value.Should().Be("newpass");
        }


        [Fact]
        public void AsXNode_WithUsernameAndPassword_ReturnsCorrectElement()
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
        public void AsXNode_WithUsernameAndClearTextPassword_ReturnsCorrectElement()
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
