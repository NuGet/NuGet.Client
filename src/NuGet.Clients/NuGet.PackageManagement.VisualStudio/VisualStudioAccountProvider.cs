// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Services.Client.AccountManagement;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using NuGet.Configuration;
using NuGet.Credentials;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// This provider connects to Visual Studio Online endpoints by pulling the token
    /// from the VS keychain.
    /// </summary>
    public class VisualStudioAccountProvider : ICredentialProvider
    {
        private readonly AsyncLazy<IAccountManager> _accountManager;
        private readonly IInteractiveLoginProvider _loginProvider;

        public VisualStudioAccountProvider()
            //  Loadup the account manager and the account provider so that we can query the keychain.
            : this(new AsyncLazy<IAccountManager>(() =>
                    ServiceLocator.GetGlobalServiceAsync<SVsAccountManager, IAccountManager>(), NuGetUIThreadHelper.JoinableTaskFactory),
                new InteractiveLoginProvider())
        {
        }

        /// <summary>
        /// Provided for unit tests.  Expectation is that the default constructor will be called.
        /// </summary>
        /// <param name="accountManager">Account manager, most likely a mock</param>
        /// <param name="interactiveLogin">Interactive login provider most likely a mock</param>
        public VisualStudioAccountProvider(AsyncLazy<IAccountManager> accountManager, IInteractiveLoginProvider interactiveLogin)
        {
            _accountManager = accountManager;
            _loginProvider = interactiveLogin;
            Id = $"{typeof (VisualStudioAccountProvider).Name}_{Guid.NewGuid()}";
        }

        /// <summary>
        /// Unique identifier of this credential provider
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Determines if the endpoint is a Visual Studio Online endpoint.  If so, uses the keychain to get a
        /// session token for the endpoint and returns that as a ICredentials object
        /// </summary>
        /// <param name="uri">URI for the feed endponint to use</param>
        /// <param name="proxy">Web proxy to use when comunicating on the network.  Null if there is no proxy
        /// authentication configured</param>
        /// <param name="type">
        /// The type of credential request that is being made. Note that this implementation of
        /// <see cref="ICredentialProvider"/> does not support providing proxy credenitials and treats
        /// all other types the same.
        /// </param>
        /// <param name="message">A message provided by NuGet to show to the user when prompting.</param>
        /// <param name="isRetry">Flag to indicate if this is the first time the URI has been looked up.
        /// If this is true we check to see if the account has access to the feed.
        /// First time we assume that is true to minimize network trafic.</param>
        /// <param name="nonInteractive">Flag to indicate if UI can be shown.  If true, we will fail in cases
        /// where we need to show UI instead of prompting</param>
        /// <param name="cancellationToken">Cancelation token used to comunicate cancelation to the async tasks</param>
        /// <returns>If a credentials can be obtained a credentails object with a session token for the URI,
        /// if not NULL</returns>
        public async Task<CredentialResponse> GetAsync(
            Uri uri,
            IWebProxy proxy,
            CredentialRequestType type,
            string message,
            bool isRetry,
            bool nonInteractive, CancellationToken cancellationToken)
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            if (!InteractiveLoginProvider.IsValidScheme(uri))
            {
                // We are not talking to a https endpoint so it cannot be a VSO endpoint
                return new CredentialResponse(CredentialStatus.ProviderNotApplicable);
            }

            if (type == CredentialRequestType.Proxy)
            {
                //  We don't handle getting proxy credentials so don't try to do anything on a isProxyRequest.
                return new CredentialResponse(CredentialStatus.ProviderNotApplicable);
            }

            var posibleAccounts = new List<AccountAndTenant>();

            //  Check to see if this is a VSO endpoint before we do anything else
            var uriTenantId = await _loginProvider.LookupTenant(uri, proxy, cancellationToken);
            if (string.IsNullOrWhiteSpace(uriTenantId))
            {
                //  we don't have a tenant ID so this cannot be a VSO endpoint
                return new CredentialResponse(CredentialStatus.ProviderNotApplicable);
            }

            if (_accountManager == null)
            {
                // we know this is a VSO endpoint but we are unable to get the credentials so we should
                // throw so that the other providers will not be called
                throw new InvalidOperationException(Strings.AccountProvider_FailedToLoadAccountManager);
            }

            var provider = (VSAccountProvider) await (await _accountManager.GetValueAsync())
                .GetAccountProviderAsync(VSAccountProvider.AccountProviderIdentifier);

            if (provider == null)
            {
                // we know this is a VSO endpoint but we are unable to get the credentials so we should
                // throw so that the other providers will not be called
                throw new InvalidOperationException(Strings.AccountProvider_FailedToLoadVSOAccountProvider);
            }

            //  Ask keychain for all accounts
            var accounts = (await _accountManager.GetValueAsync()).Store.GetAllAccounts();

            //  Look through the accounts to see what ones have the VSO tenant in them (collected from
            //  the LookupTenant() call at the top of the method).
            foreach (var account in accounts)
            {
                var tenant = _loginProvider.FindTenantInAccount(account, uriTenantId, provider);
                if (tenant != null)
                {
                    posibleAccounts.Add(new AccountAndTenant(account, tenant));
                }
            }

            ICredentials credentials;
            if (posibleAccounts.Count == 1)
            {
                //  If we only have one posible account use it
                credentials = await _loginProvider.GetTokenFromAccount(
                        posibleAccounts[0],
                        provider,
                        nonInteractive,
                        cancellationToken);

                if (credentials == null)
                {
                    throw new InvalidOperationException(Strings.AccountProvider_NoValidCrededentialsFound);
                }

                if (isRetry)
                {
                    var hasAccess = await _loginProvider.AccountHasAccess(
                        uri,
                        proxy,
                        credentials,
                        cancellationToken);

                    if (!hasAccess)
                    {
                        // The account didn't have access and we are on a retry so the token didn't expire.
                        // we either need to prompt the user for different creds or fail in this case
                        credentials = await _loginProvider.PromptUserForAccount(
                            uriTenantId,
                            provider,
                            nonInteractive,
                            cancellationToken);
                    }
                }
            }
            else if (posibleAccounts.Count > 1)
            {
                var accountsWithAccess = new List<ICredentials>();
                foreach (var account in posibleAccounts)
                {
                    ICredentials cred = null;

                    cred = await _loginProvider.GetTokenFromAccount(
                        account,
                        provider,
                        nonInteractive: true,
                        cancellationToken: cancellationToken);

                    if (cred == null)
                    {
                        continue;
                    }

                    var hasAccess = await _loginProvider.AccountHasAccess(uri, proxy, cred, cancellationToken);
                    if (hasAccess)
                    {
                        accountsWithAccess.Add(cred);
                    }
                }
                if (accountsWithAccess.Count == 1)
                {
                    credentials = accountsWithAccess[0];
                }
                else
                {
                    // we couldn't finde a unique account with access to the endpoint so we are going to have
                    // to ask the user...
                    credentials = await _loginProvider.PromptUserForAccount(
                        uriTenantId, 
                        provider, 
                        nonInteractive, 
                        cancellationToken);
                }

            }
            else // count == 0 so we should prompt the user
            {
                credentials = await _loginProvider.PromptUserForAccount(
                    uriTenantId, 
                    provider, 
                    nonInteractive, 
                    cancellationToken);
            }

            if (credentials == null)
            {
                // No credentials found but we know that this is a VSO endpoint so we want to throw an
                // exception to prevent the other providers from being called
                throw new InvalidOperationException(Strings.AccountProvider_NoValidCrededentialsFound);
            }

            var response = new CredentialResponse(credentials);

            return response;
        }
    }
}