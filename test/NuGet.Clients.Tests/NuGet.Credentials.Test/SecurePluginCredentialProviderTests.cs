// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
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
                () => new SecurePluginCredentialProvider(null, NullLogger.Instance));

            Assert.Equal("pluginDiscoveryResult", exception.ParamName);
        }

        [Fact]
        public void Create_ThrowsForNullLogger()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new SecurePluginCredentialProvider(CreatePluginDiscoveryResult(), null));

            Assert.Equal("logger", exception.ParamName);
        }

        [Fact]
        public void Create_ThrowsForInvalidPlugin()
        {
            var exception = Assert.Throws<ArgumentException>(
                () => new SecurePluginCredentialProvider(CreatePluginDiscoveryResult(PluginFileState.InvalidFilePath), NullLogger.Instance));
        }

        [Fact]
        public void Type_IsICredentialProvider()
        {
            var provider = new SecurePluginCredentialProvider(CreatePluginDiscoveryResult(), NullLogger.Instance);
            Assert.True(provider is ICredentialProvider);
        }

        [Fact]
        public void Provider_IdContainsPath()
        {
            var pluginResult = CreatePluginDiscoveryResult();
            var provider = new SecurePluginCredentialProvider(pluginResult, NullLogger.Instance);
            Assert.Contains(pluginResult.PluginFile.Path, provider.Id);
        }


        private static PluginDiscoveryResult CreatePluginDiscoveryResult(PluginFileState pluginState = PluginFileState.Valid)
        {
            return new PluginDiscoveryResult(new PluginFile(@"C:\random\path\plugin.exe", pluginState));
        }

        [PlatformFact(Platform.Windows)]
        public void TryCreate_ReturnsValidCredentials()
        {
            var uri = new Uri("https://api.nuget.org/v3/index.json");
            var authUsername = "username";
            var authPassword = "password";
            var expectation = new TestExpectation(
                null,
                null,
                new[] { OperationClaim.Authentication },
                ConnectionOptions.CreateDefault(),
                Protocol.Plugins.ProtocolConstants.CurrentVersion,
                uri,
                authUsername,
                authPassword,
                true,
                true
                );

            using (var test = new PluginManagerMock(
                pluginFilePath: "a",
                pluginFileState: PluginFileState.Valid,
                expectations: expectation))
            {
                var discoveryResult = new PluginDiscoveryResult(new PluginFile("a", PluginFileState.Valid));
                var provider = new SecurePluginCredentialProvider(discoveryResult, NullLogger.Instance);

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
                null,
                null,
                new[] { OperationClaim.DownloadPackage },
                ConnectionOptions.CreateDefault(),
                Protocol.Plugins.ProtocolConstants.CurrentVersion,
                uri,
                authUsername,
                authPassword,
                false,
                false
                );

            using (var test = new PluginManagerMock(
                pluginFilePath: "a",
                pluginFileState: PluginFileState.Valid,
                expectations: expectation))
            {
                var discoveryResult = new PluginDiscoveryResult(new PluginFile("a", PluginFileState.Valid));
                var provider = new SecurePluginCredentialProvider(discoveryResult, NullLogger.Instance);

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
        public void TryCreate_DoesntDisposeOfMultiOperationPlugin()
        {
            var uri = new Uri("https://api.nuget.org/v3/index.json");
            var authUsername = "username";
            var authPassword = "password";
            var expectation = new TestExpectation(
                null,
                null,
                new[] { OperationClaim.Authentication, OperationClaim.DownloadPackage },
                ConnectionOptions.CreateDefault(),
                Protocol.Plugins.ProtocolConstants.CurrentVersion,
                uri,
                authUsername,
                authPassword,
                true,
                false
                );

            using (var test = new PluginManagerMock(
                pluginFilePath: "a",
                pluginFileState: PluginFileState.Valid,
                expectations: expectation))
            {
                var discoveryResult = new PluginDiscoveryResult(new PluginFile("a", PluginFileState.Valid));
                var provider = new SecurePluginCredentialProvider(discoveryResult, NullLogger.Instance);

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
    }
}