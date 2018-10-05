// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.Credentials
{
    public class DefaultCredentialServiceUtility
    {
        public static void SetupDefaultCredentialService(ILogger logger, bool nonInteractive)
        {
            if (HttpHandlerResourceV3.CredentialService == null)
            {
                var providers = new AsyncLazy<IEnumerable<ICredentialProvider>>(async () => await GetCredentialProvidersAsync(logger));
                HttpHandlerResourceV3.CredentialService = new Lazy<ICredentialService>(
                    () => new CredentialService(
                        providers: providers,
                        nonInteractive: nonInteractive,
                        handlesDefaultCredentials: PreviewFeatureSettings.DefaultCredentialsAfterCredentialProviders));
            }
        }

        // Add only the secure plugin. This will be done when there's nothing set
        // By default the plugins cannot prompt. Currently this is only used to setup from MSBuild/dotnet.exe code paths
        private static async Task<IEnumerable<ICredentialProvider>> GetCredentialProvidersAsync(ILogger logger)
        {
            var providers = new List<ICredentialProvider>();

            var securePluginProviders = await new SecurePluginCredentialProviderBuilder(pluginManager: PluginManager.Instance, canShowDialog: false, logger: logger).BuildAllAsync();
            providers.AddRange(securePluginProviders);

            if (providers.Any())
            {
                if (PreviewFeatureSettings.DefaultCredentialsAfterCredentialProviders)
                {
                    providers.Add(new DefaultNetworkCredentialsCredentialProvider());
                }
            }
            return providers;
        }

    }
}
