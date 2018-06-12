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
        private static async Task<IEnumerable<ICredentialProvider>> GetCredentialProvidersAsync(ILogger logger)
        {
            var providers = new List<ICredentialProvider>();

            var securePluginProviders = await (new SecureCredentialProviderBuilder(PluginManager.Instance, logger)).BuildAll();
            providers.AddRange(securePluginProviders);

            if (securePluginProviders.Any())
            {
                if (PreviewFeatureSettings.DefaultCredentialsAfterCredentialProviders)
                {
                    providers.Add(new DefaultCredentialsCredentialProvider());
                }
            }
            return providers;
        }

    }
}
