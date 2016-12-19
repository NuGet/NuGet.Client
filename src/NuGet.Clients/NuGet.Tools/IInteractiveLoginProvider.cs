// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Services.Client.AccountManagement;

namespace NuGetVSExtension
{
    public interface IInteractiveLoginProvider
    {
        Task<ICredentials> PromptUserForAccount(
            string tenentId,
            VSAccountProvider provider,
            bool nonInteractive,
            CancellationToken cancellationToken);

        Task<ICredentials> GetTokenFromAccount(AccountAndTenant account,
            VSAccountProvider provider, bool nonInteractive, CancellationToken cancellationToken);

        TenantInformation FindTenantInAccount(Account account, string tenantId,
            VSAccountProvider provider);

        Task<string> LookupTenant(Uri uri, IWebProxy proxy,
            CancellationToken cancellationToken);

        Task<bool> AccountHasAccess(Uri uri, IWebProxy proxy, ICredentials credentials,
            CancellationToken cancellationToken);
    }

    public class AccountAndTenant
    {
        public AccountAndTenant(Account account, TenantInformation tenant)
        {
            UserAccount = account;
            TenantToUse = tenant;
        }
        public Account UserAccount { get; }
        public TenantInformation TenantToUse { get; }
    }
}
