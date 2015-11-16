using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine
{
    internal static class NuGetResourceProviderExtensions
    {
        public static IEnumerable<Lazy<INuGetResourceProvider>> GetCommandline(this Repository.ProviderFactory factory, 
            Logging.ILogger logger)
        {
            var resourceProviders = new List<Lazy<INuGetResourceProvider>>();

            resourceProviders.AddRange(Protocol.Core.v2.FactoryExtensionsV2.GetCoreV2(factory)
                .Select(p => new Lazy<INuGetResourceProvider>(() => new ConsoleNuGetResourceProvider(p, logger))));
            resourceProviders.AddRange(Protocol.Core.v3.FactoryExtensionsV2.GetCoreV3(factory)
                .Select(p => new Lazy<INuGetResourceProvider>(() => new ConsoleNuGetResourceProvider(p, logger))));

            return resourceProviders;
        }
    }
}