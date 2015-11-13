// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.Client.AccountManagement;
using Microsoft.VisualStudio.Services.DelegatedAuthorization.Client;
using Microsoft.VisualStudio.Shell;
using NuGet.PackageManagement.VisualStudio;

namespace NuGetVSExtension
{
    class InteractiveLoginProvider: IInteractiveLoginProvider
    {
        private const string VsoEndpointResource = "499b84ac-1321-427f-aa17-267ca6975798";
        private const string VssResourceTenant = "X-VSS-ResourceTenant";
        private const string DefaultMsaTenantId = "f8cdef31-a31e-4b4a-93e4-5f571e91255a";
        private const string MsaOnlyTenantId = "00000000-0000-0000-0000-000000000000";
        private const string SessionTokenScope = "vso.packaging_write";

        private readonly DTE _dte;

        public InteractiveLoginProvider()
        {
            _dte = ServiceLocator.GetInstance<DTE>();
        }

        // Logic shows UI and interacts with all mocked methods.  Mocking this as well.
        public async Task<ICredentials> PromptUserForAccount(
            string tenentId,
            VSAccountProvider provider,
            bool nonInteractive,
            CancellationToken cancellationToken)
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
        public async Task<ICredentials> GetTokenFromAccount(
            AccountAndTenant account,
            VSAccountProvider provider,
            bool nonInteractive,
            CancellationToken cancellationToken)
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

                try
                {
                    result = await provider.AcquireAdalTokenAsync(
                        resource: VsoEndpointResource,
                        tenantId: tenantId,
                        identitifer: new UserIdentifier(uniqueId, UserIdentifierType.UniqueId),
                        parentWindowHandle: parent,
                        accountKeyForReAuthentication: account.UserAccount,
                        prompt: shouldPrompt,
                        cancellationToken: cancellationToken);
                }
                catch (AdalSilentTokenAcquisitionException)
                {
                    result = null;
                }
            });

            if (result == null)
            {
                return null;
            }

            var aadcred = new VssAadCredential(new VssAadToken(result));

            // create the session token
            var connection = new VssConnection(AccountManager.VsoEndpoint, aadcred);
            var delegatedClient = connection.GetClient<DelegatedAuthorizationHttpClient>();

            // Create a scoped session token to the endpoint
            var sessionToken = await delegatedClient.CreateSessionToken(
                cancellationToken: cancellationToken,
                scope: SessionTokenScope);

            var cred = new NetworkCredential
            {
                UserName = account.UserAccount.DisplayInfo.UserName,
                Password = sessionToken.Token
            };

            return cred;
        }

        // Internal so we can mock.  Need to call this a lot and 
        public TenantInformation FindTenantInAccount(Account account, string tenantId,
            VSAccountProvider provider)
        {
            var tenantsInScope = provider.GetTenantsInScope(account);
            return tenantsInScope.FirstOrDefault(tenant => tenant.TenantId == tenantId);
        }

        // Logic to query web.  This will be mocked out in unit tests
        public async Task<string> LookupTenant(Uri uri, IWebProxy proxy,
            CancellationToken cancellationToken)
        {
            if (!IsValidScheme(uri))
            {
                // We are not talking to a https endpoint so it cannot be a VSO endpoint
                return null;
            }

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
        public async Task<bool> AccountHasAccess(Uri uri, IWebProxy proxy, ICredentials credentials,
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
                    if (response != null && ((int)response.StatusCode) < 300)
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

        public static bool IsValidScheme(Uri uri)
        {
            bool ret;
            try
            {
                ret = uri.Scheme.ToLower() == "https";
            }
            catch (InvalidOperationException)
            {
                // if getting the uri scheme causes an invalid operation exception
                // then we know we are not pointing to a https endpoint so this cannot
                // be a VSO endpoint
                ret = false;
            }

            return ret;
        }
    }
}
