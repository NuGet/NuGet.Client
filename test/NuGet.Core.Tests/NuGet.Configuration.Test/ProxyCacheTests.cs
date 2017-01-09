﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;
using Moq;
using NuGet.Common;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Configuration.Test
{
    public class ProxyCacheTests
    {
        [Fact]
        public void GetUserConfiguredProxy_IfValueIsNotFoundInEnvironmentOrSettings_ReturnsNull()
        {
            // Arrange
            var settings = Mock.Of<ISettings>();
            var environment = Mock.Of<IEnvironmentVariableReader>();
            var proxyCache = new ProxyCache(settings, environment);

            // Act
            var proxy = proxyCache.GetUserConfiguredProxy();

            // Assert
            Assert.Null(proxy);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void GetUserConfiguredProxy_IgnoresNullOrEmptyHostValuesInSetting(string host)
        {
            // Arrange
            var settings = new Mock<ISettings>(MockBehavior.Strict);
            settings.Setup(s => s.GetValue("config", "http_proxy", false)).Returns(host);
            var environment = Mock.Of<IEnvironmentVariableReader>();
            var proxyCache = new ProxyCache(settings.Object, environment);

            // Act
            var proxy = proxyCache.GetUserConfiguredProxy();

            // Assert
            Assert.Null(proxy);
        }

        [Fact, Platform(Platform.Windows)]
        public void GetUserConfiguredProxy_OnWindows_ReadsCredentialsFromSettings()
        {
            // Arrange
            var host = "http://127.0.0.1";
            var user = "username";
            var encryptedPassword = EncryptionUtility.EncryptString("password");
            var settings = new Mock<ISettings>(MockBehavior.Strict);
            settings.Setup(s => s.GetValue("config", "http_proxy", false)).Returns(host);
            settings.Setup(s => s.GetValue("config", "http_proxy.user", false)).Returns(user);
            settings.Setup(s => s.GetValue("config", "http_proxy.password", false)).Returns(encryptedPassword);
            settings.Setup(s => s.GetValue("config", "no_proxy", false)).Returns("");
            var environment = Mock.Of<IEnvironmentVariableReader>();
            var proxyCache = new ProxyCache(settings.Object, environment);

            // Act
            var proxy = proxyCache.GetUserConfiguredProxy() as WebProxy;

            // Assert
            AssertProxy(host, user, "password", proxy);
        }

        [Fact]
        public void GetUserConfiguredProxy_IfNullOrEmptyInSettings_DoesNotSetProxyCredentials()
        {
            // Arrange
            var host = "http://127.0.0.1";
            var settings = new Mock<ISettings>(MockBehavior.Strict);
            settings.Setup(s => s.GetValue("config", "http_proxy", false)).Returns(host);
            settings.Setup(s => s.GetValue("config", "http_proxy.user", false)).Returns<string>(null);
            settings.Setup(s => s.GetValue("config", "http_proxy.password", false)).Returns<string>(null);
            settings.Setup(s => s.GetValue("config", "no_proxy", false)).Returns("");
            var environment = Mock.Of<IEnvironmentVariableReader>();
            var proxyCache = new ProxyCache(settings.Object, environment);

            // Act
            var proxy = proxyCache.GetUserConfiguredProxy() as WebProxy;

            // Assert
            AssertProxy(host, null, null, proxy);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("random-junk-value")]
        public void GetUserConfiguredProxy_IfNotValid_IgnoresEnvironmentVariable(string proxyValue)
        {
            // Arrange
            var settings = Mock.Of<ISettings>();
            var environment = new Mock<IEnvironmentVariableReader>(MockBehavior.Strict);
            environment.Setup(s => s.GetEnvironmentVariable("http_proxy")).Returns(proxyValue);

            var proxyCache = new ProxyCache(settings, environment.Object);

            // Act
            var proxy = proxyCache.GetUserConfiguredProxy();

            // Assert
            Assert.Null(proxy);
        }

        [Fact]
        public void GetUserConfiguredProxy_ReadsHostFromEnvironmentVariable()
        {
            // Arrange
            var settings = Mock.Of<ISettings>();
            var environment = new Mock<IEnvironmentVariableReader>(MockBehavior.Strict);
            environment.Setup(s => s.GetEnvironmentVariable("http_proxy")).Returns("http://localhost:8081");
            environment.Setup(s => s.GetEnvironmentVariable("no_proxy")).Returns("");

            var proxyCache = new ProxyCache(settings, environment.Object);

            // Act
            var proxy = proxyCache.GetUserConfiguredProxy() as WebProxy;

            // Assert
            AssertProxy("http://localhost:8081/", null, null, proxy);
        }

        [Theory]
        [InlineData("http://username:password@localhost:8081/proxy.dat", "http://localhost:8081/proxy.dat", "username", "password")]
        [InlineData("http://localhost:8081/proxy/.conf", "http://localhost:8081/proxy/.conf", null, null)]
        public void GetUserConfiguredProxy_ReadsCredentialsFromEnvironmentVariable(string input, string host, string username, string password)
        {
            // Arrange
            var settings = Mock.Of<ISettings>();
            var environment = new Mock<IEnvironmentVariableReader>(MockBehavior.Strict);
            environment.Setup(s => s.GetEnvironmentVariable("http_proxy")).Returns(input);
            environment.Setup(s => s.GetEnvironmentVariable("no_proxy")).Returns("");

            var proxyCache = new ProxyCache(settings, environment.Object);

            // Act
            var proxy = proxyCache.GetUserConfiguredProxy() as WebProxy;

            // Assert
            AssertProxy(host, username, password, proxy);
        }

        private static void AssertProxy(string proxyAddress, string username, string password, WebProxy actual)
        {
            Assert.NotNull(actual);
            Assert.Equal(proxyAddress, actual.ProxyAddress.OriginalString);

            if (username == null)
            {
                Assert.Null(actual.Credentials);
            }
            else
            {
                Assert.NotNull(actual.Credentials);
                Assert.IsType<NetworkCredential>(actual.Credentials);
                var credentials = (NetworkCredential)actual.Credentials;
                Assert.Equal(username, credentials.UserName);
                Assert.Equal(password, credentials.Password);
            }
        }
    }
}
