// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.Core.v3.RemoteRepositories
{
    internal static class PackageUtilities
    {
        /// <summary>
        /// Create a <see cref="NuspecReader"/> from a nupkg stream.
        /// </summary>
        internal static async Task<NuspecReader> OpenNuspecFromNupkgAsync(
            string id,
            Task<Stream> openNupkgStreamAsync,
            ILogger report)
        {
            using (var nupkgStream = await openNupkgStreamAsync)
            {
                if (nupkgStream == null)
                {
                    throw new FatalProtocolException(string.Format(CultureInfo.CurrentCulture, Strings.Log_FailedToGetNupkgStream, id));
                }

                try
                {
                    using (var reader = new PackageArchiveReader(nupkgStream, leaveStreamOpen: true))
                    using (var nuspecStream = reader.GetNuspec())
                    {
                        if (nupkgStream == null)
                        {
                            throw new FatalProtocolException(string.Format(CultureInfo.CurrentCulture, Strings.Log_FailedToGetNuspecStream, id));
                        }

                        return new NuspecReader(nuspecStream);
                    }
                }
                catch (Exception exception) when (exception is PackagingException 
                                                    || exception is InvalidDataException)
                {
                    var fileStream = nupkgStream as FileStream;
                    if (fileStream != null)
                    {
                        report.LogWarning(string.Format(CultureInfo.CurrentCulture, Strings.Log_FileIsCorrupt, fileStream.Name));
                    }
                    throw;
                }
            }
        }
    }
}
