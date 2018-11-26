// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using FluentAssertions;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Configuration.Test
{
    public class PackageSourceCredentialTests
    {
        [Fact]
        public void Constructor_WithClearTextPassword_DoesNotEncryptPassword()
        {
            var credentials = new PackageSourceCredential("source", "user", "password", isPasswordClearText: true);

            Assert.Equal("password", credentials.PasswordText);
            Assert.Equal("password", credentials.Password);
        }

        [Fact]
        public void Constructor_WithEncryptedPassword_DoesNotEncryptPassword()
        {
            var credentials = new PackageSourceCredential("source", "user", "password", isPasswordClearText: false);

            // Password string should be stored as-is with no modification OR validation
            Assert.Equal("password", credentials.PasswordText);
        }

        [Fact]
        public void FromUserInput_WithStorePasswordInClearText_DoesNotEncryptsPassword()
        {
            var credentials = PackageSourceCredential.FromUserInput("source", "user", "password", storePasswordInClearText: true);

            Assert.Equal("password", credentials.PasswordText);
            Assert.Equal("password", credentials.Password);
        }

        [PlatformFact(Platform.Windows)]
        public void FromUserInput_WithStorePasswordEncrypted_OnWindows_EncryptsPassword()
        {
            var credentials = PackageSourceCredential.FromUserInput("source", "user", "password", storePasswordInClearText: false);

            Assert.NotEqual("password", credentials.PasswordText);
            Assert.Equal("password", credentials.Password);
        }

        [PlatformFact(Platform.Linux)]
        public void FromUserInput_WithStorePasswordEncrypted_OnLinux_Throws()
        {
            Assert.Throws<NuGetConfigurationException>(() => PackageSourceCredential.FromUserInput("source", "user", "password", storePasswordInClearText: false));
        }

        [PlatformFact(Platform.Linux)]
        public void Password_WithEncryptedPassword_OnLinux_Throws()
        {
            var credentials = new PackageSourceCredential("source", "user", "password", isPasswordClearText: false);

            Assert.Throws<NuGetConfigurationException>(() => credentials.Password);
        }

        [PlatformFact(Platform.Darwin)]
        public void FromUserInput_WithStorePasswordEncrypted_OnMacOS_Throws()
        {
            Assert.Throws<NuGetConfigurationException>(() => PackageSourceCredential.FromUserInput("source", "user", "password", storePasswordInClearText: false));
        }

        [PlatformFact(Platform.Darwin)]
        public void Password_WithEncryptedPassword_OnMacOS_Throws()
        {
            var credentials = new PackageSourceCredential("source", "user", "password", isPasswordClearText: false);

            Assert.Throws<NuGetConfigurationException>(() => credentials.Password);
        }

        [Fact]
        public void IsValid_WithNonEmptyValues_ReturnsTrue()
        {
            var credentials = new PackageSourceCredential("source", "user", "password", isPasswordClearText: false);

            Assert.True(credentials.IsValid());
        }

        [Theory]
        [InlineData("", "password")]
        [InlineData("username", "")]
        [InlineData("", "")]
        public void IsValid_WithEmptyPassword_ReturnsFalse(string username, string password)
        {
            var credentials = new PackageSourceCredential("source", username, password, isPasswordClearText: false);

            Assert.False(credentials.IsValid());
        }

        [Fact]
        public void AsCredentialsItem_WithClearTextPassword_WorksCorrectly()
        {
            var credentials = new PackageSourceCredential(
                "source",
                "user",
                "password",
                isPasswordClearText: false);

            var expectedItem = new CredentialsItem("source", "user", "password", isPasswordClearText: false);

            SettingsTestUtils.DeepEquals(credentials.AsCredentialsItem(), expectedItem).Should().BeTrue();
        }

        [Fact]
        public void AsCredentialsItem_WithPassword_WorksCorrectly()
        {
            var credentials = new PackageSourceCredential(
                "source",
                "user",
                "password",
                isPasswordClearText: true);

            var expectedItem = new CredentialsItem("source", "user", "password", isPasswordClearText: true);

            SettingsTestUtils.DeepEquals(credentials.AsCredentialsItem(), expectedItem).Should().BeTrue();
        }

        [Fact]
        public void AsCredentialsItem_WithSpaceOnSourceName_WorksCorrectly()
        {
            var credentials = new PackageSourceCredential(
                "source name",
                "user",
                "password",
                isPasswordClearText: true);

            var expectedItem = new CredentialsItem("source name", "user", "password", isPasswordClearText: true);

            SettingsTestUtils.DeepEquals(credentials.AsCredentialsItem(), expectedItem).Should().BeTrue();
        }
    }
}
