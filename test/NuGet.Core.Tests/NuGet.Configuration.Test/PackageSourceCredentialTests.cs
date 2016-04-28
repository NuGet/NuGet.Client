// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;

namespace NuGet.Configuration
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

#if !IS_CORECLR
        [Fact]
        public void FromUserInput_WithStorePasswordEncrypted_EncryptsPassword()
        {
            var credentials = PackageSourceCredential.FromUserInput("source", "user", "password", storePasswordInClearText: false);

            Assert.NotEqual("password", credentials.PasswordText);
            Assert.Equal("password", credentials.Password);
        }
#else
        [Fact]
        public void FromUserInput_WithStorePasswordEncrypted_Throws()
        {
            Assert.Throws<NuGetConfigurationException>(() => PackageSourceCredential.FromUserInput("source", "user", "password", storePasswordInClearText: false));
        }

        [Fact]
        public void Password_WithEncryptedPassword_Throws()
        {
            var credentials = new PackageSourceCredential("source", "user", "password", isPasswordClearText: false);

            Assert.Throws<NuGetConfigurationException>(() => credentials.Password);
        }
#endif

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
    }
}
