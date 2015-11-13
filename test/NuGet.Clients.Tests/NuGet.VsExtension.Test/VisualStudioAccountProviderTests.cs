// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.VisualStudio.Services.Client.AccountManagement;
using Moq;
using NuGetVSExtension;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Credentials;
using Xunit;

namespace NuGet.VsExtension.Test
{
    public class VisualStudioAccountProviderTests
    {
        private const string TestTenantId = "1234";
        private readonly VisualStudioAccountProvider _provider;

        private readonly Mock<IAccountManager> _mockAccountManager;
        private readonly Mock<ICredentials> _mockUserEnteredCredentials;
        private readonly Mock<IInteractiveLoginProvider> _mockLoginProvider;


        public VisualStudioAccountProviderTests()
        {
            _mockUserEnteredCredentials = new Mock<ICredentials>();

            var mockAccountProvider = new Mock<VSAccountProvider>("instance");

            _mockAccountManager = new Mock<IAccountManager>();
            _mockAccountManager.Setup(x => x.GetAccountProviderAsync(It.IsAny<Guid>()))
                .Returns(Task.FromResult<IAccountProvider>(mockAccountProvider.Object));
            _mockAccountManager.Setup(x => x.Store.GetAllAccounts()).Returns(new List<Account>().AsReadOnly());

            _mockLoginProvider = new Mock<IInteractiveLoginProvider>();
            _mockLoginProvider
                .Setup(x => x.AccountHasAccess(It.IsAny<Uri>(), It.IsAny<IWebProxy>(),
                It.IsAny<ICredentials>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));
            _mockLoginProvider
                .Setup(x => x.LookupTenant(It.IsAny<Uri>(), It.IsAny<IWebProxy>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(TestTenantId));
            _mockLoginProvider
                .Setup(x => x.PromptUserForAccount(It.IsAny<string>(), It.IsAny<VSAccountProvider>(),
                false, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(_mockUserEnteredCredentials.Object));
            _mockLoginProvider
                .Setup(x => x.GetTokenFromAccount(It.IsAny<AccountAndTenant>(), It.IsAny<VSAccountProvider>(),
                It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(_mockUserEnteredCredentials.Object));
            _mockLoginProvider.Setup(
                x => x.FindTenantInAccount(It.IsAny<Account>(), It.IsAny<string>(), It.IsAny<VSAccountProvider>()))
                .Returns(new TenantInformation("uid", TestTenantId, "name", true, true));

            _provider = new VisualStudioAccountProvider(_mockAccountManager.Object, _mockLoginProvider.Object);
        }

        private Account GetTestAccount()
        {
            return new Account(new AccountInitializationData()
            {
                UniqueId = "1",
                ParentProviderId = Guid.NewGuid(),
                DisplayInfo = new AccountDisplayInfo("accountName", "providerName", "userName", new byte[0],
                    new byte[0]),
                Authenticator = "authenticator",
                SupportedAccountProviders = new List<Guid>().AsReadOnly(),
                Properties = new Dictionary<string, string>()
            });
        }

        [Fact]
        public async Task Get_WhenIsProxyRequest_ThenReturnsNull()
        {
            // Arange
            var uri = new Uri("https://uri1");
            var webProxy = null as IWebProxy;
            var isProxyRequest = true;
            var isRetry = false;
            var nonInteractive = false;

            // Act
            var cred = await _provider.Get(uri, webProxy, isProxyRequest, isRetry, nonInteractive,
                CancellationToken.None);

            // Assert
            Assert.Equal(CredentialStatus.ProviderNotApplicable, cred.Status);
            Assert.Null(cred.Credentials);
        }

        [Fact]
        public async Task Get_WhenNullUri_ThenThrowsArgumentException()
        {
            // Arange
            var uri = null as Uri;
            var webProxy = null as IWebProxy;
            var isProxyRequest = false;
            var isRetry = false;
            var nonInteractive = false;

            // Act - Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                async () => await _provider.Get(uri, webProxy, isProxyRequest, isRetry, nonInteractive,
                    CancellationToken.None));
        }

        [Fact]
        public async Task Get_WhenEmptyKeychain_ThenPromptForCredentials()
        {
            // Arange
            var uri = new Uri("https://uri1");
            var webProxy = null as IWebProxy;
            var isProxyRequest = false;
            var isRetry = false;
            var nonInteractive = false;

            // Act
            var cred = await _provider.Get(uri, webProxy, isProxyRequest, isRetry, nonInteractive,
                CancellationToken.None);

            // Assert
            _mockLoginProvider.Verify( x => x.PromptUserForAccount(
                It.IsAny<string>(), It.IsAny<VSAccountProvider>(), false,It.IsAny<CancellationToken>()),
                Times.Once);
            Assert.Equal(cred.Credentials, _mockUserEnteredCredentials.Object); //get returned the user credentails
        }

        [Fact]
        public async Task Get_WhenUriNotVSO_ThenReturnsNull()
        {
            // Arange
            _mockLoginProvider
                .Setup(x => x.LookupTenant(It.IsAny<Uri>(), It.IsAny<IWebProxy>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(""));

            var uri = new Uri("https://uri1");
            var webProxy = null as IWebProxy;
            var isProxyRequest = false;
            var isRetry = false;
            var nonInteractive = false;

            // Act
            var cred = await _provider.Get(uri, webProxy, isProxyRequest, isRetry, nonInteractive,
                CancellationToken.None);

            // Assert
            Assert.Null(cred.Credentials);
        }

        [Fact]
        public async Task Get_WhenUriNotHTTPS_ThenReturnsNotApplicable()
        {
            // Arange
            _mockLoginProvider
                .Setup(x => x.LookupTenant(It.IsAny<Uri>(), It.IsAny<IWebProxy>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(""));

            var uri = new Uri("http://uri1");
            var webProxy = null as IWebProxy;
            var isProxyRequest = false;
            var isRetry = false;
            var nonInteractive = false;

            // Act
            var cred = await _provider.Get(uri, webProxy, isProxyRequest, isRetry, nonInteractive,
                CancellationToken.None);

            // Assert
            Assert.Null(cred.Credentials);
            Assert.Equal(CredentialStatus.ProviderNotApplicable, cred.Status);
        }

        [Fact]
        public async Task Get_WhenEmptyKeychainAndNonInteractive_ThenThrowsException()
        {
            // Arange
            var uri = new Uri("https://uri1");
            var webProxy = null as IWebProxy;
            var isProxyRequest = false;
            var isRetry = false;
            var nonInteractive = true;

            // Act
            var exception =
                await Assert.ThrowsAsync<InvalidOperationException>(
                        async () => await _provider.Get(
                            uri, 
                            webProxy, 
                            isProxyRequest, 
                            isRetry, 
                            nonInteractive,
                            CancellationToken.None));

            // Assert
            Assert.Contains("No valid credentials", exception.Message);
        }

        [Fact]
        public async Task Get_WhenOneAccountInKeychain_ThenGetTokenFromAccount()
        {
            // Arange
            var account = GetTestAccount();
            var accounts = new List<Account> { account };
            _mockAccountManager.Setup(x => x.Store.GetAllAccounts()).Returns(accounts.AsReadOnly());

            var uri = new Uri("https://uri1");
            var webProxy = null as IWebProxy;
            var isProxyRequest = false;
            var isRetry = false;
            var nonInteractive = false;

            // Act
            var cred = await _provider.Get(uri, webProxy, isProxyRequest, isRetry, nonInteractive,
                CancellationToken.None);

            // Assert
            Assert.NotNull(cred);

            _mockLoginProvider.Verify(
                x =>
                    x.GetTokenFromAccount(It.Is<AccountAndTenant>(p => p.UserAccount == account),
                    It.IsAny<VSAccountProvider>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
                Times.Once());
        }

        [Fact]
        public async Task Get_WhenMultipleAccountsInKeychainButOneInTenant_ThenReturnsThatToken()
        {
            // Arange
            var account1 = GetTestAccount();
            var account2 = GetTestAccount();
            var accounts = new List<Account> { account1, account2 };
            _mockAccountManager.Setup(x => x.Store.GetAllAccounts()).Returns(accounts.AsReadOnly());
            _mockLoginProvider.Setup(
                x => x.FindTenantInAccount(account1, It.IsAny<string>(), It.IsAny<VSAccountProvider>()))
                .Returns((TenantInformation)null);
            _mockLoginProvider.Setup(
                x => x.FindTenantInAccount(It.IsAny<Account>(), It.IsAny<string>(), It.IsAny<VSAccountProvider>()))
                .Returns(new TenantInformation("uid", TestTenantId, "name", true, true));

            var uri = new Uri("https://uri1");
            var webProxy = null as IWebProxy;
            var isProxyRequest = false;
            var isRetry = false;
            var nonInteractive = false;

            // Act
            var cred = await _provider.Get(uri, webProxy, isProxyRequest, isRetry, nonInteractive,
                CancellationToken.None);

            // Assert
            Assert.NotNull(cred);

            _mockLoginProvider.Verify(
                x => x.GetTokenFromAccount(
                    It.Is<AccountAndTenant>(p => p.UserAccount == account2),
                    It.IsAny<VSAccountProvider>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()),
                Times.Once());

        }

        [Fact]
        public async Task Get_WhenMultipleAccountsInKeychainNoneInTenant_ThenPromptsUserForAccount()
        {
            // Arange
            var account1 = GetTestAccount();
            var account2 = GetTestAccount();
            var accounts = new List<Account> { account1, account2 };
            _mockAccountManager.Setup(x => x.Store.GetAllAccounts()).Returns(accounts.AsReadOnly());
            _mockLoginProvider.Setup(
                x => x.FindTenantInAccount(account1, It.IsAny<string>(), It.IsAny<VSAccountProvider>()))
                .Returns((TenantInformation)null);
            _mockLoginProvider.Setup(
                x => x.FindTenantInAccount(account2, It.IsAny<string>(), It.IsAny<VSAccountProvider>()))
                .Returns((TenantInformation)null);

            var uri = new Uri("https://uri1");
            var webProxy = null as IWebProxy;
            var isProxyRequest = false;
            var isRetry = false;
            var nonInteractive = false;

            // Act
            var cred = await _provider.Get(uri, webProxy, isProxyRequest, isRetry, nonInteractive,
                CancellationToken.None);

            // Assert
            Assert.NotNull(cred);
            _mockLoginProvider.Verify(
                x => x.GetTokenFromAccount(
                    It.Is<AccountAndTenant>(p => p.UserAccount == account2),
                    It.IsAny<VSAccountProvider>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
            _mockLoginProvider.Verify(
                x => x.PromptUserForAccount(It.IsAny<string>(), It.IsAny<VSAccountProvider>(), false,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            Assert.Equal(cred.Credentials, _mockUserEnteredCredentials.Object); //get returned the user credentails

        }

        [Fact]
        public async Task Get_WhenMultipleAccountsInKeychainAndTenant_ThenPrompsUserForAccount()
        {
            // Arange
            var account1 = GetTestAccount();
            var account2 = GetTestAccount();
            var accounts = new List<Account> { account1, account2 };
            _mockAccountManager.Setup(x => x.Store.GetAllAccounts()).Returns(accounts.AsReadOnly());
            _mockLoginProvider.Setup(
                x => x.FindTenantInAccount(account1, It.IsAny<string>(), It.IsAny<VSAccountProvider>()))
                .Returns(new TenantInformation("uid", TestTenantId, "name", true, true));
            _mockLoginProvider.Setup(
                x => x.FindTenantInAccount(account2, It.IsAny<string>(), It.IsAny<VSAccountProvider>()))
                .Returns(new TenantInformation("uid", TestTenantId, "name", true, true));
            _mockLoginProvider
                .Setup(x => x.GetTokenFromAccount(It.IsAny<AccountAndTenant>(), It.IsAny<VSAccountProvider>(),
                    It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(_mockUserEnteredCredentials.Object));
            _mockLoginProvider
                .Setup(x => x.AccountHasAccess(It.IsAny<Uri>(), It.IsAny<IWebProxy>(), It.IsAny<ICredentials>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));

            var uri = new Uri("https://uri1");
            var webProxy = null as IWebProxy;
            var isProxyRequest = false;
            var isRetry = false;
            var nonInteractive = false;

            // Act
            var cred = await _provider.Get(uri, webProxy, isProxyRequest, isRetry, nonInteractive,
                CancellationToken.None);

            // Assert
            Assert.NotNull(cred);
            _mockLoginProvider.Verify(
                    x => x.PromptUserForAccount(It.IsAny<string>(), It.IsAny<VSAccountProvider>(), false,
                        It.IsAny<CancellationToken>()),
                    Times.Once);
            Assert.Equal(cred.Credentials, _mockUserEnteredCredentials.Object); //get returned the user credentails

        }

        [Fact]
        public async Task Get_WhenOneAccountInKeychainWithoutAccessOnToTenantRetry_ThenPromptsUser()
        {
            // Arange
            var account = GetTestAccount();
            var accounts = new List<Account> { account };
            _mockAccountManager.Setup(x => x.Store.GetAllAccounts()).Returns(accounts.AsReadOnly());
            _mockLoginProvider
                .Setup(x => x.AccountHasAccess(It.IsAny<Uri>(), It.IsAny<IWebProxy>(), It.IsAny<ICredentials>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(false));

            var uri = new Uri("https://uri1");
            var webProxy = null as IWebProxy;
            var isProxyRequest = false;
            var isRetry = true;
            var nonInteractive = false;

            // Act
            var cred = await _provider.Get(uri, webProxy, isProxyRequest, isRetry, nonInteractive,
                CancellationToken.None);

            // Assert
            Assert.NotNull(cred);
            _mockLoginProvider.Verify( //we prompted the user for an account
                x => x.PromptUserForAccount(It.IsAny<string>(), It.IsAny<VSAccountProvider>(), false,
                    It.IsAny<CancellationToken>()),
                Times.Once);
            Assert.Equal(cred.Credentials, _mockUserEnteredCredentials.Object); //get returned the user credentails
        }
    }
}
