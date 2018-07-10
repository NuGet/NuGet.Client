// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Configuration.Test
{
    public class PackageSourceCredentialTests
    {
        [Fact]
        public void Constructor_WithClearTextPassword_DoesNotEncryptPassword()
        {
            var credentials = new PackageSourceCredential("source", "user", "password", isPasswordClearText: true, validAuthenticationTypesText: null);

            Assert.Equal("password", credentials.PasswordText);
            Assert.Equal("password", credentials.Password);
        }

        [Fact]
        public void Constructor_WithEncryptedPassword_DoesNotEncryptPassword()
        {
            var credentials = new PackageSourceCredential("source", "user", "password", isPasswordClearText: false, validAuthenticationTypesText: null);

            // Password string should be stored as-is with no modification OR validation
            Assert.Equal("password", credentials.PasswordText);
        }

        [Fact]
        public void FromUserInput_WithStorePasswordInClearText_DoesNotEncryptsPassword()
        {
            var credentials = PackageSourceCredential.FromUserInput("source", "user", "password", storePasswordInClearText: true, validAuthenticationTypesText: null);

            Assert.Equal("password", credentials.PasswordText);
            Assert.Equal("password", credentials.Password);
        }

        [PlatformFact(Platform.Windows)]
        public void FromUserInput_WithStorePasswordEncrypted_OnWindows_EncryptsPassword()
        {
            var credentials = PackageSourceCredential.FromUserInput("source", "user", "password", storePasswordInClearText: false, validAuthenticationTypesText: null);

            Assert.NotEqual("password", credentials.PasswordText);
            Assert.Equal("password", credentials.Password);
        }

        [PlatformFact(Platform.Linux)]
        public void FromUserInput_WithStorePasswordEncrypted_OnLinux_Throws()
        {
            Assert.Throws<NuGetConfigurationException>(() => PackageSourceCredential.FromUserInput("source", "user", "password", storePasswordInClearText: false, validAuthenticationTypesText: null));
        }

        [PlatformFact(Platform.Linux)]
        public void Password_WithEncryptedPassword_OnLinux_Throws()
        {
            var credentials = new PackageSourceCredential("source", "user", "password", isPasswordClearText: false, validAuthenticationTypesText: null);

            Assert.Throws<NuGetConfigurationException>(() => credentials.Password);
        }

        [PlatformFact(Platform.Darwin)]
        public void FromUserInput_WithStorePasswordEncrypted_OnMacOS_Throws()
        {
            Assert.Throws<NuGetConfigurationException>(() => PackageSourceCredential.FromUserInput("source", "user", "password", storePasswordInClearText: false, validAuthenticationTypesText: null));
        }

        [PlatformFact(Platform.Darwin)]
        public void Password_WithEncryptedPassword_OnMacOS_Throws()
        {
            var credentials = new PackageSourceCredential("source", "user", "password", isPasswordClearText: false, validAuthenticationTypesText: null);

            Assert.Throws<NuGetConfigurationException>(() => credentials.Password);
        }

        [Fact]
        public void IsValid_WithNonEmptyValues_ReturnsTrue()
        {
            var credentials = new PackageSourceCredential("source", "user", "password", isPasswordClearText: false, validAuthenticationTypesText: null);

            Assert.True(credentials.IsValid());
        }

        [Theory]
        [InlineData("", "password")]
        [InlineData("username", "")]
        [InlineData("", "")]
        public void IsValid_WithEmptyPassword_ReturnsFalse(string username, string password)
        {
            var credentials = new PackageSourceCredential("source", username, password, isPasswordClearText: false, validAuthenticationTypesText: null);

            Assert.False(credentials.IsValid());
        }


        [Fact]
        void ValidAuthenticationTypes_ParsesSingle()
        {
            var credentials = new PackageSourceCredential(
                "source",
                "user",
                "password",
                isPasswordClearText: false,
                validAuthenticationTypesText: "basic");

            Assert.Equal(new[] { "basic" }, credentials.ValidAuthenticationTypes);
        }

        [Fact]
        void ValidAuthenticationTypes_ParsesMultiple()
        {
            var credentials = new PackageSourceCredential(
                "source",
                "user",
                "password",
                isPasswordClearText: false,
                validAuthenticationTypesText: "basic, negotiate");

            Assert.Equal(new[] { "basic", "negotiate" }, credentials.ValidAuthenticationTypes);
        }

        [Fact]
        void ValidAuthenticationTypes_ParsesStar()
        {
            var credentials = new PackageSourceCredential(
                "source",
                "user",
                "password",
                isPasswordClearText: false,
                validAuthenticationTypesText: "*");

            Assert.Empty(credentials.ValidAuthenticationTypes);
        }

        [Fact]
        void ValidAuthenticationTypes_ReturnsEmptyIfTextEmpty()
        {
            var credentials = new PackageSourceCredential(
                "source",
                "user",
                "password",
                isPasswordClearText: false,
                validAuthenticationTypesText: "");

            Assert.Empty(credentials.ValidAuthenticationTypes);
        }

        [Fact]
        void ValidAuthenticationTypes_ReturnsEmptyIfTextNull()
        {
            var credentials = new PackageSourceCredential(
                "source",
                "user",
                "password",
                isPasswordClearText: false,
                validAuthenticationTypesText: null);

            Assert.Empty(credentials.ValidAuthenticationTypes);
        }
    }
}
