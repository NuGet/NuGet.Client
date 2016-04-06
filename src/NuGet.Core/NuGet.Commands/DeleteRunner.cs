using System;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Common;
using NuGet.Protocol.Core.Types;

namespace NuGet.Commands
{
    /// <summary>
    /// Shared code to run the "delete" command from the command line projects
    /// </summary>
    public static class DeleteRunner
    {
        public static async Task Run(
            ISettings settings,
            IPackageSourceProvider sourceProvider,
            string packageId,
            string packageVersion,
            string source,
            string apiKey,
            bool nonInteractive,
            Func<string, bool> confirmFunc,
            ILogger logger)
        {
            source = CommandRunnerUtility.ResolveSource(sourceProvider, source);

            PackageUpdateResource packageUpdateResource = await CommandRunnerUtility.GetPackageUpdateResource(sourceProvider, source);

            await packageUpdateResource.Delete(
                packageId,
                packageVersion,
                (endpoint) => CommandRunnerUtility.GetApiKey(settings, endpoint, apiKey),
                (desc) => nonInteractive ? true : confirmFunc(desc),
                logger);
        }
    }
}
