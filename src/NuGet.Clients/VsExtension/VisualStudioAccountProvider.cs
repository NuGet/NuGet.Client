// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using EnvDTE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.Client.AccountManagement;
using Microsoft.VisualStudio.Services.DelegatedAuthorization.Client;
using NuGet.PackageManagement.VisualStudio;
using VsUserAccount = Microsoft.VisualStudio.Services.Client.AccountManagement.Account;
using System.Threading;

namespace NuGetVSExtension
{
    /// <summary>
    /// This provider connects to Visual Studio Online endpoints by pulling the token
    /// from the VS keychain.
    /// </summary>
    public class VisualStudioAccountProvider: NuGet.Credentials.ICredentialProvider
    {
        private const string VsoEndpointResource = "499b84ac-1321-427f-aa17-267ca6975798";
        private const string VssResourceTenant = "X-VSS-ResourceTenant";
        private const string DefaultMsaTenantId = "f8cdef31-a31e-4b4a-93e4-5f571e91255a";
        private const string MsaOnlyTenantId = "00000000-0000-0000-0000-000000000000";
        private const string SessionTokenScope = "vso.packaging_write";

        private readonly IAccountManager _accountManager;
        private readonly DTE _dte;

        public VisualStudioAccountProvider()
        {
            //  Loadup the account manager and the account provider so that we can query the keychain.
            var serviceProvider = ServiceProvider.GlobalProvider;
            _accountManager = serviceProvider.GetService(typeof(SVsAccountManager)) as IAccountManager;
            _dte = ServiceLocator.GetInstance<DTE>();
        }

        internal VisualStudioAccountProvider(IAccountManager accountManager, DTE dte)
        {
            _accountManager = accountManager;
            _dte = dte;
        }

        /// <summary>
        /// Determins if the endpoint is a Visual Studio Online endpoint.  If so, uses the keychain to get a
        /// session token for the endpoint and returns that as a ICredentials object
        /// </summary>
        /// <param name="uri">URI for the feed endponint to use</param>
        /// <param name="proxy">Web proxy to use when comunicating on the network.  Null if there is no proxy
        /// authentication configured</param>
        /// <param name="isProxyRequest">Flag to indicate that this request is to get proxy authentication
        /// credetials.  Note, if this is set to true method will return null</param>
        /// <param name="isRetry">Flag to indicate if this is the first time the URI has been looked up.
        /// If this is true we check to see if the account has access to the feed.
        /// First time we assume that is true to minimize network trafic.</param>
        /// <param name="nonInteractive">Flag to indicate if UI can be shown.  If true, we will fail in cases
        /// where we need to show UI instead of prompting</param>
        /// <param name="cancellationToken">Cancelation token used to comunicate cancelation to the async tasks</param>
        /// <returns>If a credentials can be obtained a credentails object with a session token for the URI,
        /// if not NULL</returns>
        public async Task<ICredentials> Get(Uri uri, IWebProxy proxy, bool isProxyRequest, bool isRetry,
            bool nonInteractive, CancellationToken cancellationToken)
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            if (isProxyRequest)
            {
                //  We don't handle getting proxy credentials so don't try to do anything on a isProxyRequest.
                return null;
            }

            ICredentials ret;
            var posibleAccounts = new List<AccountAndTenant>();

            //  Check to see if this is a VSO endpoint before we do anything else
            var uriTenantId = await LookupTenant(uri, proxy, cancellationToken);
            if (string.IsNullOrWhiteSpace(uriTenantId))
            {
                //  we don't have a tenant ID so this cannot be a VSO endpoint
                return null;
            }

            if (_accountManager == null)
            {
                // we know this is a VSO endpoint but we are unable to get the credentials so we should
                // throw so that the other providers will not be called
                throw new InvalidOperationException(Resources.AccountProvider_FailedToLoadAccountManager);
            }

            var provider = (VSAccountProvider) await _accountManager
                .GetAccountProviderAsync(VSAccountProvider.AccountProviderIdentifier);

            if (provider == null)
            {
                // we know this is a VSO endpoint but we are unable to get the credentials so we should
                // throw so that the other providers will not be called
                throw new InvalidOperationException(Resources.AccountProvider_FailedToLoadVSOAccountProvider);
            }

            //  Ask keychain for all accounts
            var accounts = _accountManager.Store.GetAllAccounts();

            //  Look through the accounts to see what ones have the VSO tenant in them (collected from
            //  the LookupTenant() call at the top of the method).
            foreach (var account in accounts)
            {
                var tenant = FindTenantInAccount(account, uriTenantId, provider);
                if (tenant != null)
                {
                    posibleAccounts.Add(new AccountAndTenant(account, tenant));
                }
            }

            if (posibleAccounts.Count == 1)
            {
                //  If we only have one posible account use it
                ret = await GetTokenFromAccount(posibleAccounts[0], provider, nonInteractive, cancellationToken);
                if (isRetry)
                {
                    var hasAccess = await AccountHasAccess(uri, proxy, ret, cancellationToken);
                    if (!hasAccess)
                    {
                        // The account didn't have access and we are on a retry so the token didn't expire.
                        // we either need to prompt the user for different creds or fail in this case
                        ret = await PromptUserForAccount(uriTenantId, provider, nonInteractive, cancellationToken);
                    }
                }
            }
            else if (posibleAccounts.Count > 1)
            {
                var accountsWithAccess = new List<AccountWithCreds>();
                foreach (var account in posibleAccounts)
                {
                    var cred = await GetTokenFromAccount(
                        account,
                        provider,
                        nonInteractive: true,
                        cancellationToken:cancellationToken);

                    var hasAccess = await AccountHasAccess(uri, proxy, cred, cancellationToken);
                    if (hasAccess)
                    {
                        accountsWithAccess.Add(new AccountWithCreds(account, cred));
                    }
                }

                if (accountsWithAccess.Count == 1)
                {
                    ret = accountsWithAccess[0].Creds;
                }
                else
                {
                    // we couldn't finde a unique account with access to the endpoint so we are going to have
                    // to ask the user...
                    ret = await PromptUserForAccount(uriTenantId, provider, nonInteractive, cancellationToken);
                }

            }
            else // count == 0 so we should prompt the user
            {
                ret = await PromptUserForAccount(uriTenantId, provider, nonInteractive, cancellationToken);
            }

            if (ret == null)
            {
                // No credentials found but we know that this is a VSO endpoint so we want to throw an
                // exception to prevent the other providers from being called
                throw new InvalidOperationException(Resources.AccountProvider_NoValidCrededentialsFound);
            }

            return ret;
        }

        // Logic shows UI and interacts with all mocked methods.  Mocking this as well.
        internal virtual async Task<ICredentials> PromptUserForAccount(
        	string tenentId, VSAccountProvider provider, bool nonInteractive, CancellationToken cancellationToken)
        {
            ICredentials ret = null;
            if (nonInteractive)
            {
                //  If we are not supposed to interact with the user then we can't prompt for account so we
                // need to fail.
                throw new InvalidOperationException(Resources.AccountProvider_TriedToShowUIOnNonInteractive);
            }
            Account account = null;

            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var parent = IntPtr.Zero;
                if (_dte != null)
                {
                    parent = new IntPtr(_dte.MainWindow.HWnd);
                }

                account = await provider.CreateAccountWithUIAsync(parent, cancellationToken);
            });

            var tenant = FindTenantInAccount(account, tenentId, provider);
            if (tenant != null)
            {
                ret = await GetTokenFromAccount(
                    new AccountAndTenant(account, tenant),
                    provider,
                    false,
                    cancellationToken);
            }

            return ret;
        }

        // Logic goes between UI and web.  made internal to be mocked for unit tests
        internal virtual async Task<ICredentials> GetTokenFromAccount(AccountAndTenant account,
            VSAccountProvider provider, bool nonInteractive, CancellationToken cancellationToken)
        {
            // get the ADAL creds for the user account
            var uniqueId = account.TenantToUse.UniqueIds.First();
            var tenantId = account.TenantToUse.TenantId;

            // we are passed the flag as non-interactive.  we realy want to know if we should prompt so
            // need to reverse the flag
            var shouldPrompt = !nonInteractive;
            AuthenticationResult result = null;
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var parent = IntPtr.Zero;
                if (_dte != null)
                {
                    parent = new IntPtr(_dte.MainWindow.HWnd);
                }

                result = await provider.AcquireAdalTokenAsync(
                    resource: VsoEndpointResource,
                    tenantId: tenantId,
                    identitifer: new UserIdentifier(uniqueId, UserIdentifierType.UniqueId),
                    parentWindowHandle: parent,
                    accountKeyForReAuthentication: account.UserAccount,
                    prompt: shouldPrompt,
                    cancellationToken: cancellationToken);
            });

            var aadcred = new VssAadCredential(new VssAadToken(result));

            // create the session token
            var connection = new VssConnection(AccountManager.VsoEndpoint, aadcred);
            var delegatedClient = connection.GetClient<DelegatedAuthorizationHttpClient>();

            // Create a scoped session token to the endpoint
            var sessionToken = await delegatedClient.CreateSessionToken(cancellationToken: cancellationToken, scope: SessionTokenScope);

            var cred = new NetworkCredential
            {
                UserName = account.UserAccount.DisplayInfo.UserName,
                Password = sessionToken.Token
            };

            return cred;
        }

        // Internal so we can mock.  Need to call this a lot and 
        internal virtual TenantInformation FindTenantInAccount(VsUserAccount account, string tenantId,
            VSAccountProvider provider)
        {
            var tenantsInScope = provider.GetTenantsInScope(account);
            return tenantsInScope.FirstOrDefault(tenant => tenant.TenantId == tenantId);
        }

        // Logic to query web.  This will be mocked out in unit tests
        internal virtual async Task<string> LookupTenant(Uri uri, IWebProxy proxy,
            CancellationToken cancellationToken)
        {
            string tenantId;
            //  we assume the call will be access denied (or the provider shouldn't have been called)
            //  so calling the URI shouldn't be too expensive.
            var req = WebRequest.Create(uri);
            if (proxy != null)
            {
                req.Proxy = proxy;
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using (var response = await req.GetResponseAsync())
                {
                    tenantId = response.Headers[VssResourceTenant];
                }
            }
            catch (WebException ex)
            {
                //  unauthorized is thrown as an exception from GetResponse so we have to pull
                //  the headers out of the exception as well.
                tenantId = ex.Response.Headers[VssResourceTenant];
            }

            if (string.Equals(tenantId, MsaOnlyTenantId))
            {
                //  For MSA only endpoints the X-VSS-ResourceTenant header is set to a 0 GUID.
                //  The keychain has no actual accounts with this as a tenant but all MSA accounts
                //  should have the Default MSA Tenant ID in them so setting the ID to this.
                //  Doing this lets us identify all MSA accounts in the keychain.
                tenantId = DefaultMsaTenantId;
            }

            return tenantId;
        }

        // Logic to query web.  This will be mocked out in unit test
        internal virtual async Task<bool> AccountHasAccess(Uri uri, IWebProxy proxy, ICredentials credentials,
            CancellationToken cancellationToken)
        {
            var ret = false;
            var req = WebRequest.Create(uri);
            if (proxy != null)
            {
                req.Proxy = proxy;
            }

            req.Credentials = credentials;

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using (var response = await req.GetResponseAsync() as HttpWebResponse)
                {
                    if (response != null && ((int) response.StatusCode) < 300)
                    {
                        //we were able to get a response without error
                        ret = true;
                    }
                }
            }
            catch (WebException)
            {
                // Hit an error while doing the request requesting unauthorized so we don't have access.
                ret = false;
            }

            return ret;
        }
    }

    class AccountAndTenant
    {
        public AccountAndTenant(VsUserAccount account, TenantInformation tenant)
        {
            UserAccount = account;
            TenantToUse = tenant;
        }
        public VsUserAccount UserAccount { get; }
        public TenantInformation TenantToUse { get; }
    }

    class AccountWithCreds
    {
        public AccountWithCreds(AccountAndTenant account, ICredentials cred)
        {
            Account = account;
            Creds = cred;
        }

        public AccountAndTenant Account { get; }
        public ICredentials Creds { get; }
    }
}