using System;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Common;
using NuGet.Protocol.Core.Types;

namespace NuGet.Commands
{
    /// <summary>
    /// Shared code to run the "push" command from the command line projects
    /// </summary>
    public static class PushRunner
    {
        public static async Task Run(
            ISettings settings,
            IPackageSourceProvider sourceProvider,
            string packagePath,
            string source,
            string apiKey,
            int timeoutSeconds,
            bool disableBuffering,
            bool noSymbols,
            ILogger logger)
        {
            source = CommandRunnerUtility.ResolveSource(sourceProvider, source);

            if (timeoutSeconds == 0)
            {
                timeoutSeconds = 5 * 60;
            }

            PackageUpdateResource packageUpdateResource = await CommandRunnerUtility.GetPackageUpdateResource(sourceProvider, source);

            // only push to SymbolSource when the actual package is being pushed to the official NuGet.org
            string symbolsSource = string.Empty;

            Uri sourceUri = packageUpdateResource.SourceUri;
            if (!noSymbols && !sourceUri.IsFile && sourceUri.IsAbsoluteUri)
            {
                if (sourceUri.Host.Equals(NuGetConstants.NuGetHostName, StringComparison.OrdinalIgnoreCase) // e.g. nuget.org
                    || sourceUri.Host.EndsWith("." + NuGetConstants.NuGetHostName, StringComparison.OrdinalIgnoreCase)) // *.nuget.org, e.g. www.nuget.org
                {
                    symbolsSource = NuGetConstants.DefaultSymbolServerUrl;
                }
            }

            await packageUpdateResource.Push(
                packagePath,
                symbolsSource,
                timeoutSeconds,
                disableBuffering,
                endpoint => CommandRunnerUtility.GetApiKey(settings, endpoint, apiKey),
                logger);
        }
    }
}
