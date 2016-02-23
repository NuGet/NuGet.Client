using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Logging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;

namespace NuGet.Protocol
{
    public static class GetDownloadResultUtility
    {
        public static async Task<DownloadResourceResult> GetDownloadResultAsync(
           HttpSource client,
           PackageIdentity identity,
           Uri uri,
           ISettings settings,
           ILogger logger,
           CancellationToken token)
        {
            // Uri is not null, so the package exists in the source
            // Now, check if it is in the global packages folder, before, getting the package stream

            // TODO: This code should respect no_cache settings and not write or read packages from the global packages folder
            var packageFromGlobalPackages = GlobalPackagesFolderUtility.GetPackage(identity, settings);

            if (packageFromGlobalPackages != null)
            {
                return packageFromGlobalPackages;
            }

            for (int i = 0; i < 3; i++)
            {
                try
                {
                    using (var packageStream = await client.GetStreamAsync(uri, logger, token))
                    {
                        var downloadResult = await GlobalPackagesFolderUtility.AddPackageAsync(identity,
                            packageStream,
                            settings,
                            logger,
                            token);

                        return downloadResult;
                    }
                }
                catch (IOException ex) when (ex.InnerException is SocketException && i < 2)
                {
                    string message = string.Format(CultureInfo.CurrentCulture, Strings.Log_ErrorDownloading, identity, uri)
                        + Environment.NewLine
                        + ExceptionUtilities.DisplayMessage(ex);
                    logger.LogWarning(message);
                }
                catch (Exception ex)
                {
                    string message = string.Format(CultureInfo.CurrentCulture, Strings.Log_ErrorDownloading, identity, uri);
                    logger.LogError(message + Environment.NewLine + ExceptionUtilities.DisplayMessage(ex));

                    throw new FatalProtocolException(message, ex);
                }
            }

            throw new InvalidOperationException("Reached an unexpected point in the code");
        }
    }
}
