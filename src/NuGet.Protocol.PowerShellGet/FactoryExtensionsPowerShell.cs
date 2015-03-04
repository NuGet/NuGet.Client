using NuGet.Protocol.Core.Types;
using System;
using System.Collections;
using System.Collections.Generic;
using NuGet.Protocol.Core.v3;

namespace NuGet.Protocol.PowerShellGet
{
    public static class FactoryExtensionsPowerShell
    {
        public static SourceRepository GetPowerShell(this Repository.RepositoryFactory factory, string source)
        {
            return Repository.CreateSource(Repository.Provider.GetPowerShell(), source);
        }

        public static SourceRepository GetPowerShell(this Repository.RepositoryFactory factory, Configuration.PackageSource source)
        {
            return Repository.CreateSource(Repository.Provider.GetPowerShell(), source);
        }

        /// <summary>
        /// Core V3 + PowerShell
        /// </summary>
        public static IEnumerable<Lazy<INuGetResourceProvider>> GetPowerShell(this Repository.ProviderFactory factory)
        {
            yield return new Lazy<INuGetResourceProvider>(() => new PowerShellSearchResourceProvider());

            foreach (Lazy<INuGetResourceProvider> provider in Repository.Provider.GetCoreV3())
            {
                yield return provider;
            }

            yield break;
        }
    }
}