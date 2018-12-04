// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading;
using Moq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Plugins;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Credentials.Test
{
    public class SecurePluginCredentialProviderTests
    {
        [Fact]
        public void Create_ThrowsForNullPlugin()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new SecurePluginCredentialProvider(CreateDefaultPluginManager(), null, true, NullLogger.Instance));

            Assert.Equal("pluginDiscoveryResult", exception.ParamName);
        }

        public void Create_ThrowsForNullPluginManager()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new SecurePluginCredentialProvider(null, CreatePluginDiscoveryResult(), true, NullLogger.Instance));

            Assert.Equal("pluginManager", exception.ParamName);
        }

        [Fact]
        public void Create_ThrowsForNullLogger()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new SecurePluginCredentialProvider(CreateDefaultPluginManager(), CreatePluginDiscoveryResult(), true, null));

            Assert.Equal("logger", exception.ParamName);
        }

        [Fact]
        public void Type_IsICredentialProvider()
        {
            var provider = new SecurePluginCredentialProvider(CreateDefaultPluginManager(), CreatePluginDiscoveryResult(), true, NullLogger.Instance);
            Assert.True(provider is ICredentialProvider);
        }

        [Fact]
        public void Provider_IdContainsPath()
        {
            var pluginResult = CreatePluginDiscoveryResult();
            var provider = new SecurePluginCredentialProvider(CreateDefaultPluginManager(), pluginResult, true, NullLogger.Instance);
            Assert.Contains(pluginResult.PluginFile.Path, provider.Id);
        }

        [PlatformFact(Platform.Windows)]
        public void GetCredentialsAsync_ReturnsValidCredentials()
        {
            var uri = new Uri("https://api.nuget.org/v3/index.json");
            var authUsername = "username";
            var authPassword = "password";
            var expectation = new TestExpectation(
                serviceIndexJson: null,
                sourceUri: null,
                operationClaims: new[] { OperationClaim.Authentication },
                connectionOptions: ConnectionOptions.CreateDefault(),
                pluginVersion: Protocol.Plugins.ProtocolConstants.CurrentVersion,
                uri: uri,
                authenticationUsername: authUsername,
                authenticationPassword: authPassword,
                success: true
                );

            using (var test = new PluginManagerMock(
                pluginFilePath: "a",
                pluginFileState: PluginFileState.Valid,
                expectations: expectation))
            {
                var discoveryResult = new PluginDiscoveryResult(new PluginFile("a", new Lazy<PluginFileState>(() => PluginFileState.Valid)));
                var provider = new SecurePluginCredentialProvider(test.PluginManager, discoveryResult, true, NullLogger.Instance);

                System.Net.IWebProxy proxy = null;
                var credType = CredentialRequestType.Unauthorized;
                var message = "nothing";
                var isRetry = false;
                var isInteractive = false;
                var token = CancellationToken.None;
                var credentialResponse = provider.GetAsync(uri, proxy, credType, message, isRetry, isInteractive, token).Result;

                Assert.True(credentialResponse.Status == CredentialStatus.Success);
                Assert.NotNull(credentialResponse.Credentials);
                Assert.Equal(authUsername, credentialResponse.Credentials.GetCredential(uri, null).UserName);
                Assert.Equal(authPassword, credentialResponse.Credentials.GetCredential(uri, null).Password);
            }
        }

        [PlatformFact(Platform.Windows)]
        public void TryCreate_DoesNotCreateNonCredentialsPluginTwice()
        {
            var uri = new Uri("https://api.nuget.org/v3/index.json");
            var authUsername = "username";
            var authPassword = "password";

            var expectation = new TestExpectation(
                serviceIndexJson: null,
                sourceUri: null,
                operationClaims: new[] { OperationClaim.DownloadPackage },
                connectionOptions: ConnectionOptions.CreateDefault(),
                pluginVersion: ProtocolConstants.CurrentVersion,
                uri: uri,
                authenticationUsername: authUsername,
                authenticationPassword: authPassword,
                success: false
                );

            using (var test = new PluginManagerMock(
                pluginFilePath: "a",
                pluginFileState: PluginFileState.Valid,
                expectations: expectation))
            {
                var discoveryResult = new PluginDiscoveryResult(new PluginFile("a", new Lazy<PluginFileState>(() => PluginFileState.Valid)));
                var provider = new SecurePluginCredentialProvider(test.PluginManager, discoveryResult, true, NullLogger.Instance);

                System.Net.IWebProxy proxy = null;
                var credType = CredentialRequestType.Unauthorized;
                var message = "nothing";
                var isRetry = false;
                var isInteractive = false;
                var token = CancellationToken.None;
                var credentialResponse = provider.GetAsync(uri, proxy, credType, message, isRetry, isInteractive, token).Result;

                Assert.True(credentialResponse.Status == CredentialStatus.ProviderNotApplicable);
                Assert.Null(credentialResponse.Credentials);

                var credentialResponse2 = provider.GetAsync(uri, proxy, credType, message, isRetry, isInteractive, token).Result;
                Assert.True(credentialResponse2.Status == CredentialStatus.ProviderNotApplicable);
                Assert.Null(credentialResponse2.Credentials);
            }
        }

        [PlatformFact(Platform.Windows)]
        public void GetCredentialsAsync_ReturnsValidCredentialsFromMultiOperationClaim()
        {
            var uri = new Uri("https://api.nuget.org/v3/index.json");
            var authUsername = "username";
            var authPassword = "password";
            var expectation = new TestExpectation(
                serviceIndexJson: null,
                sourceUri: null,
                operationClaims: new[] { OperationClaim.Authentication, OperationClaim.DownloadPackage },
                connectionOptions: ConnectionOptions.CreateDefault(),
                pluginVersion: ProtocolConstants.CurrentVersion,
                uri: uri,
                authenticationUsername: authUsername,
                authenticationPassword: authPassword,
                success: true
                );

            using (var test = new PluginManagerMock(
                pluginFilePath: "a",
                pluginFileState: PluginFileState.Valid,
                expectations: expectation))
            {
                var discoveryResult = new PluginDiscoveryResult(new PluginFile("a", new Lazy<PluginFileState>(() => PluginFileState.Valid)));
                var provider = new SecurePluginCredentialProvider(test.PluginManager, discoveryResult, true, NullLogger.Instance);

                System.Net.IWebProxy proxy = null;
                var credType = CredentialRequestType.Unauthorized;
                var message = "nothing";
                var isRetry = false;
                var isInteractive = false;
                var token = CancellationToken.None;
                var credentialResponse = provider.GetAsync(uri, proxy, credType, message, isRetry, isInteractive, token).Result;

                Assert.True(credentialResponse.Status == CredentialStatus.Success);
                Assert.NotNull(credentialResponse.Credentials);
                Assert.Equal(authUsername, credentialResponse.Credentials.GetCredential(uri, null).UserName);
                Assert.Equal(authPassword, credentialResponse.Credentials.GetCredential(uri, null).Password);
            }
        }

        [PlatformFact(Platform.Windows)]
        public void TryCreate_SetsProxyCredentials()
        {
            var uri = new Uri("https://api.nuget.org/v3/index.json");
            var authUsername = "username";
            var authPassword = "password";
            var proxyUsername = "proxyUsername";
            var proxyPassword = "proxyPassword";

            var expectation = new TestExpectation(
                serviceIndexJson: null,
                sourceUri: null,
                operationClaims: new[] { OperationClaim.Authentication },
                connectionOptions: ConnectionOptions.CreateDefault(),
                pluginVersion: ProtocolConstants.CurrentVersion,
                uri: uri,
                authenticationUsername: authUsername,
                authenticationPassword: authPassword,
                success: true,
                proxyUsername: proxyUsername,
                proxyPassword: proxyPassword
                );

            using (var test = new PluginManagerMock(
                pluginFilePath: "a",
                pluginFileState: PluginFileState.Valid,
                expectations: expectation))
            {
                var discoveryResult = new PluginDiscoveryResult(new PluginFile("a", new Lazy<PluginFileState>(() => PluginFileState.Valid)));
                var provider = new SecurePluginCredentialProvider(test.PluginManager, discoveryResult, true, NullLogger.Instance);
                var proxy = new System.Net.WebProxy()
                {
                    Credentials = new NetworkCredential(proxyUsername, proxyPassword)
                };
                var credType = CredentialRequestType.Unauthorized;
                var message = "nothing";
                var isRetry = false;
                var isInteractive = false;
                var token = CancellationToken.None;
                var credentialResponse = provider.GetAsync(uri, proxy, credType, message, isRetry, isInteractive, token).Result;

                Assert.True(credentialResponse.Status == CredentialStatus.Success);
                Assert.NotNull(credentialResponse.Credentials);
                Assert.Equal(authUsername, credentialResponse.Credentials.GetCredential(uri, null).UserName);
                Assert.Equal(authPassword, credentialResponse.Credentials.GetCredential(uri, null).Password);
            }
        }

        [PlatformFact(Platform.Windows)]
        public void TryCreate_DoesNotCreateNonCredentialsPluginTwice_CachesCapabilities()
        {
            var uri = new Uri("https://api.nuget.org/v3/index.json");
            var authUsername = "username";
            var authPassword = "password";

            var expectation = new TestExpectation(
                serviceIndexJson: null,
                sourceUri: null,
                operationClaims: new[] { OperationClaim.DownloadPackage },
                connectionOptions: ConnectionOptions.CreateDefault(),
                pluginVersion: ProtocolConstants.CurrentVersion,
                uri: uri,
                authenticationUsername: authUsername,
                authenticationPassword: authPassword,
                success: false
                );

            using (var test = new PluginManagerMock(
                pluginFilePath: "a",
                pluginFileState: PluginFileState.Valid,
                expectations: expectation))
            {
                var discoveryResult = new PluginDiscoveryResult(new PluginFile("a", new Lazy<PluginFileState>(() => PluginFileState.Valid)));
                var provider = new SecurePluginCredentialProvider(test.PluginManager, discoveryResult, true, NullLogger.Instance);

                IWebProxy proxy = null;
                var credType = CredentialRequestType.Unauthorized;
                var message = "nothing";
                var isRetry = false;
                var isInteractive = false;
                var token = CancellationToken.None;
                var credentialResponse = provider.GetAsync(uri, proxy, credType, message, isRetry, isInteractive, token).Result;

                Assert.True(credentialResponse.Status == CredentialStatus.ProviderNotApplicable);
                Assert.Null(credentialResponse.Credentials);
            }

            var expectations2 = new TestExpectation(
                serviceIndexJson: null,
                sourceUri: null,
                operationClaims: new[] { OperationClaim.DownloadPackage },
                connectionOptions: ConnectionOptions.CreateDefault(),
                pluginVersion: ProtocolConstants.CurrentVersion,
                uri: uri,
                authenticationUsername: authUsername,
                authenticationPassword: authPassword,
                success: false,
                pluginLaunched: false
                );

            using (var test = new PluginManagerMock(
                pluginFilePath: "a",
                pluginFileState: PluginFileState.Valid,
                expectations: expectations2))
            {
                var discoveryResult = new PluginDiscoveryResult(new PluginFile("a", new Lazy<PluginFileState>(() => PluginFileState.Valid)));
                var provider = new SecurePluginCredentialProvider(test.PluginManager, discoveryResult, true, NullLogger.Instance);

                IWebProxy proxy = null;
                var credType = CredentialRequestType.Unauthorized;
                var message = "nothing";
                var isRetry = false;
                var isInteractive = false;
                var token = CancellationToken.None;
                var credentialResponse = provider.GetAsync(uri, proxy, credType, message, isRetry, isInteractive, token).Result;

                Assert.True(credentialResponse.Status == CredentialStatus.ProviderNotApplicable);
                Assert.Null(credentialResponse.Credentials);
            }
        }

        [PlatformFact(Platform.Windows)]
        public void GetCredentialsAsync_SendsCorrectCanShowDialogValue()
        {
            var uri = new Uri("https://api.nuget.org/v3/index.json");
            var authUsername = "username";
            var authPassword = "password";
            var canShowDialog = false;
            var expectation = new TestExpectation(
                serviceIndexJson: null,
                sourceUri: null,
                operationClaims: new[] { OperationClaim.Authentication },
                connectionOptions: ConnectionOptions.CreateDefault(),
                pluginVersion: ProtocolConstants.CurrentVersion,
                uri: uri,
                authenticationUsername: authUsername,
                authenticationPassword: authPassword,
                success: true,
                proxyUsername: null,
                proxyPassword: null,
                pluginLaunched: true,
                canShowDialog: canShowDialog
                );

            using (var test = new PluginManagerMock(
                pluginFilePath: "a",
                pluginFileState: PluginFileState.Valid,
                expectations: expectation))
            {
                var discoveryResult = new PluginDiscoveryResult(new PluginFile("a", new Lazy<PluginFileState>(() => PluginFileState.Valid)));
                var provider = new SecurePluginCredentialProvider(test.PluginManager, discoveryResult, canShowDialog, NullLogger.Instance);

                IWebProxy proxy = null;
                var credType = CredentialRequestType.Unauthorized;
                var message = "nothing";
                var isRetry = false;
                var isInteractive = false;
                var token = CancellationToken.None;
                var credentialResponse = provider.GetAsync(uri, proxy, credType, message, isRetry, isInteractive, token).Result;

                Assert.True(credentialResponse.Status == CredentialStatus.Success);
                Assert.NotNull(credentialResponse.Credentials);
                Assert.Equal(authUsername, credentialResponse.Credentials.GetCredential(uri, null).UserName);
                Assert.Equal(authPassword, credentialResponse.Credentials.GetCredential(uri, null).Password);
            }
        }

        private static PluginDiscoveryResult CreatePluginDiscoveryResult(PluginFileState pluginState = PluginFileState.Valid)
        {
            return new PluginDiscoveryResult(new PluginFile(@"C:\random\path\plugin.exe", new Lazy<PluginFileState>(() => pluginState)));
        }

        private static PluginManager CreateDefaultPluginManager()
        {
            return new PluginManager(
                reader: Mock.Of<IEnvironmentVariableReader>(),
                pluginDiscoverer: new Lazy<IPluginDiscoverer>(),
                pluginFactoryCreator: (TimeSpan idleTimeout) => Mock.Of<IPluginFactory>());
        }
    }
}