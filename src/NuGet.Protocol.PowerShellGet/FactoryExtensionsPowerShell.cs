// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;

namespace NuGet.Protocol.PowerShellGet
{
    public static class FactoryExtensionsPowerShell
    {
        public static SourceRepository GetPowerShell(this Repository.RepositoryFactory factory, string source)
        {
            return Repository.CreateSource(Repository.Provider.GetPowerShell(), source);
        }

        public static SourceRepository GetPowerShell(this Repository.RepositoryFactory factory, PackageSource source)
        {
            return Repository.CreateSource(Repository.Provider.GetPowerShell(), source);
        }

        /// <summary>
        /// Core V3 + PowerShell
        /// </summary>
        public static IEnumerable<Lazy<INuGetResourceProvider>> GetPowerShell(this Repository.ProviderFactory factory)
        {
            yield return new Lazy<INuGetResourceProvider>(() => new PowerShellSearchResourceProvider());

            foreach (var provider in Repository.Provider.GetCoreV3())
            {
                yield return provider;
            }

            yield break;
        }
    }
}
