// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using EnvDTE;
using Microsoft.VisualStudio.Services.Client.AccountManagement;
using Moq;
using NuGetVSExtension;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NuGet.VsExtension.Test
{
    public class VisualStudioAccountProviderTests
    {
        private const string TestTenantId = "1234";

        private readonly Mock<IAccountManager> _mockAccountManager;
        private readonly Mock<VSAccountProvider> _mockAccountProvider;
        private readonly Mock<VisualStudioAccountProvider> _mockExtensionProvider;
        private readonly Mock<ICredentials> _mockUserEnteredCredentials;


        public VisualStudioAccountProviderTests()
        {
            var mockDte = new Mock<DTE>();
            _mockUserEnteredCredentials = new Mock<ICredentials>();

            _mockAccountProvider = new Mock<VSAccountProvider>("instance");

            _mockAccountManager = new Mock<IAccountManager>();
            _mockAccountManager.Setup(x => x.GetAccountProviderAsync(It.IsAny<Guid>()))
                .Returns(Task.FromResult<IAccountProvider>(_mockAccountProvider.Object));
            _mockAccountManager.Setup(x => x.Store.GetAllAccounts()).Returns(new List<Account>().AsReadOnly());

            // We want to mock a few things here, the provider will hit the network and the UI thread.
            // Neither of these Should be posible so we need to mock it out.
            _mockExtensionProvider =
                new Mock<VisualStudioAccountProvider>(_mockAccountManager.Object, mockDte.Object);
            _mockExtensionProvider
                .Setup(x => x.AccountHasAccess(It.IsAny<Uri>(), It.IsAny<IWebProxy>(),
                    It.IsAny<ICredentials>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));
            _mockExtensionProvider
                .Setup(x => x.LookupTenant(It.IsAny<Uri>(), It.IsAny<IWebProxy>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(TestTenantId));
            // Mock out the Prompt user for account only if the interactive is true.  we want to let the
            // non -interactive test call base since it should evaluate before we do any UI.
            _mockExtensionProvider
                .Setup(x => x.PromptUserForAccount(It.IsAny<string>(), It.IsAny<VSAccountProvider>(),
                    false, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(_mockUserEnteredCredentials.Object));
            _mockExtensionProvider
                .Setup(x => x.GetTokenFromAccount(It.IsAny<AccountAndTenant>(), It.IsAny<VSAccountProvider>(),
                    It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(_mockUserEnteredCredentials.Object));
            _mockExtensionProvider.Setup(
                x => x.FindTenantInAccount(It.IsAny<Account>(), It.IsAny<string>(), It.IsAny<VSAccountProvider>()))
                .Returns(new TenantInformation("uid", TestTenantId, "name", true, true));
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
            var provider = _mockExtensionProvider.Object;
            var uri = new Uri("http://uri1");
            var webProxy = null as IWebProxy;
            var isProxyRequest = true;
            var isRetry = false;
            var nonInteractive = false;

            // Act
            var cred = await provider.Get(uri, webProxy, isProxyRequest, isRetry, nonInteractive,
                CancellationToken.None);

            // Assert
            Assert.Null(cred);
        }

        [Fact]
        public async Task Get_WhenNullUri_ThenThrowsArgumentException()
        {
            // Arange
            var provider = _mockExtensionProvider.Object;
            var uri = null as Uri;
            var webProxy = null as IWebProxy;
            var isProxyRequest = false;
            var isRetry = false;
            var nonInteractive = false;

            // Act - Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                async () => await provider.Get(uri, webProxy, isProxyRequest, isRetry, nonInteractive,
                    CancellationToken.None));
        }

        [Fact]
        public async Task Get_WhenEmptyKeychain_ThenPromptForCredentials()
        {
            // Arange
            var provider = _mockExtensionProvider.Object;
            var uri = new Uri("http://uri1");
            var webProxy = null as IWebProxy;
            var isProxyRequest = false;
            var isRetry = false;
            var nonInteractive = false;

            // Act
            var cred = await provider.Get(uri, webProxy, isProxyRequest, isRetry, nonInteractive,
                CancellationToken.None);

            // Assert
            _mockExtensionProvider.Verify( x => x.PromptUserForAccount(
                It.IsAny<string>(), It.IsAny<VSAccountProvider>(), false,It.IsAny<CancellationToken>()),
                Times.Once);
            Assert.Equal(cred, _mockUserEnteredCredentials.Object); //get returned the user credentails
        }

        [Fact]
        public async Task Get_WhenUriNotVSO_ThenReturnsNull()
        {
            // Arange
            _mockExtensionProvider
                .Setup(x => x.LookupTenant(It.IsAny<Uri>(), It.IsAny<IWebProxy>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(""));

            var provider = _mockExtensionProvider.Object;
            var uri = new Uri("http://uri1");
            var webProxy = null as IWebProxy;
            var isProxyRequest = false;
            var isRetry = false;
            var nonInteractive = false;

            // Act
            var cred = await provider.Get(uri, webProxy, isProxyRequest, isRetry, nonInteractive,
                CancellationToken.None);

            // Assert
            Assert.Null(cred);
        }

        [Fact]
        public async Task Get_WhenEmptyKeychainAndNonInteractive_ThenThrowsException()
        {
            // Arange
            var provider = _mockExtensionProvider.Object;
            var uri = new Uri("http://uri1");
            var webProxy = null as IWebProxy;
            var isProxyRequest = false;
            var isRetry = false;
            var nonInteractive = true;

            // Act
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await provider.Get(uri, webProxy, isProxyRequest, isRetry, nonInteractive, CancellationToken.None));

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

            var provider = _mockExtensionProvider.Object;
            var uri = new Uri("http://uri1");
            var webProxy = null as IWebProxy;
            var isProxyRequest = false;
            var isRetry = false;
            var nonInteractive = false;

            // Act
            var cred = await provider.Get(uri, webProxy, isProxyRequest, isRetry, nonInteractive,
                CancellationToken.None);

            // Assert
            Assert.NotNull(cred);

            _mockExtensionProvider.Verify(
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
            _mockExtensionProvider.Setup(
                x => x.FindTenantInAccount(account1, It.IsAny<string>(), It.IsAny<VSAccountProvider>()))
                .Returns((TenantInformation)null);
            _mockExtensionProvider.Setup(
                x => x.FindTenantInAccount(It.IsAny<Account>(), It.IsAny<string>(), It.IsAny<VSAccountProvider>()))
                .Returns(new TenantInformation("uid", TestTenantId, "name", true, true));

            var provider = _mockExtensionProvider.Object;
            var uri = new Uri("http://uri1");
            var webProxy = null as IWebProxy;
            var isProxyRequest = false;
            var isRetry = false;
            var nonInteractive = false;

            // Act
            var cred = await provider.Get(uri, webProxy, isProxyRequest, isRetry, nonInteractive,
                CancellationToken.None);

            // Assert
            Assert.NotNull(cred);

            _mockExtensionProvider.Verify(
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
            _mockExtensionProvider.Setup(
                x => x.FindTenantInAccount(account1, It.IsAny<string>(), It.IsAny<VSAccountProvider>()))
                .Returns((TenantInformation)null);
            _mockExtensionProvider.Setup(
                x => x.FindTenantInAccount(account2, It.IsAny<string>(), It.IsAny<VSAccountProvider>()))
                .Returns((TenantInformation)null);

            var provider = _mockExtensionProvider.Object;
            var uri = new Uri("http://uri1");
            var webProxy = null as IWebProxy;
            var isProxyRequest = false;
            var isRetry = false;
            var nonInteractive = false;

            // Act
            var cred = await provider.Get(uri, webProxy, isProxyRequest, isRetry, nonInteractive,
                CancellationToken.None);

            // Assert
            Assert.NotNull(cred);
            _mockExtensionProvider.Verify(
                x => x.GetTokenFromAccount(
                    It.Is<AccountAndTenant>(p => p.UserAccount == account2),
                    It.IsAny<VSAccountProvider>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
            _mockExtensionProvider.Verify(
                x => x.PromptUserForAccount(It.IsAny<string>(), It.IsAny<VSAccountProvider>(), false,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            Assert.Equal(cred, _mockUserEnteredCredentials.Object); //get returned the user credentails

        }

        [Fact]
        public async Task Get_WhenMultipleAccountsInKeychainAndTenant_ThenPrompsUserForAccount()
        {
            // Arange
            var account1 = GetTestAccount();
            var account2 = GetTestAccount();
            var accounts = new List<Account> { account1, account2 };
            _mockAccountManager.Setup(x => x.Store.GetAllAccounts()).Returns(accounts.AsReadOnly());
            _mockExtensionProvider.Setup(
                x => x.FindTenantInAccount(account1, It.IsAny<string>(), It.IsAny<VSAccountProvider>()))
                .Returns(new TenantInformation("uid", TestTenantId, "name", true, true));
            _mockExtensionProvider.Setup(
                x => x.FindTenantInAccount(account2, It.IsAny<string>(), It.IsAny<VSAccountProvider>()))
                .Returns(new TenantInformation("uid", TestTenantId, "name", true, true));
            _mockExtensionProvider
                .Setup(x => x.GetTokenFromAccount(It.IsAny<AccountAndTenant>(), It.IsAny<VSAccountProvider>(),
                    It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(_mockUserEnteredCredentials.Object));
            _mockExtensionProvider
                .Setup(x => x.AccountHasAccess(It.IsAny<Uri>(), It.IsAny<IWebProxy>(), It.IsAny<ICredentials>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));

            var provider = _mockExtensionProvider.Object;
            var uri = new Uri("http://uri1");
            var webProxy = null as IWebProxy;
            var isProxyRequest = false;
            var isRetry = false;
            var nonInteractive = false;

            // Act
            var cred = await provider.Get(uri, webProxy, isProxyRequest, isRetry, nonInteractive,
                CancellationToken.None);

            // Assert
            Assert.NotNull(cred);
            _mockExtensionProvider.Verify(
                    x => x.PromptUserForAccount(It.IsAny<string>(), It.IsAny<VSAccountProvider>(), false,
                        It.IsAny<CancellationToken>()),
                    Times.Once);
            Assert.Equal(cred, _mockUserEnteredCredentials.Object); //get returned the user credentails

        }

        [Fact]
        public async Task Get_WhenOneAccountInKeychainWithoutAccessOnToTenantRetry_ThenPromptsUser()
        {
            // Arange
            var account = GetTestAccount();
            var accounts = new List<Account> { account };
            _mockAccountManager.Setup(x => x.Store.GetAllAccounts()).Returns(accounts.AsReadOnly());
            _mockExtensionProvider
                .Setup(x => x.AccountHasAccess(It.IsAny<Uri>(), It.IsAny<IWebProxy>(), It.IsAny<ICredentials>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(false));

            var provider = _mockExtensionProvider.Object;
            var uri = new Uri("http://uri1");
            var webProxy = null as IWebProxy;
            var isProxyRequest = false;
            var isRetry = true;
            var nonInteractive = false;

            // Act
            var cred = await provider.Get(uri, webProxy, isProxyRequest, isRetry, nonInteractive,
                CancellationToken.None);

            // Assert
            Assert.NotNull(cred);
            _mockExtensionProvider.Verify( //we prompted the user for an account
                x => x.PromptUserForAccount(It.IsAny<string>(), It.IsAny<VSAccountProvider>(), false,
                    It.IsAny<CancellationToken>()),
                Times.Once);
            Assert.Equal(cred, _mockUserEnteredCredentials.Object); //get returned the user credentails
        }
    }
}
