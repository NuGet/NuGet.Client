// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;
using Strings = NuGet.Protocol.Core.v3.Strings;

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
                    return await client.ProcessStreamAsync(
                        uri: uri,
                        ignoreNotFounds: true,
                        processAsync: async packageStream =>
                        {
                            if (packageStream == null)
                            {
                                return new DownloadResourceResult(DownloadResourceResultStatus.NotFound);
                            }

                            return await GlobalPackagesFolderUtility.AddPackageAsync(
                                identity,
                                packageStream,
                                settings,
                                logger,
                                token);
                        },
                        log: logger,
                        token: token);
                }
                catch (OperationCanceledException)
                {
                    return new DownloadResourceResult(DownloadResourceResultStatus.Cancelled);
                }
                catch (Exception ex) when ((
                        (ex is IOException && ex.InnerException is SocketException)
                        || ex is TimeoutException)
                    && i < 2)
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
