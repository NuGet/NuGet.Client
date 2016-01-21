﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using NuGet.Logging;
using NuGet.Packaging;
using NuGet.Packaging.Core;

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
                try
                {
                    using (var reader = new PackageArchiveReader(nupkgStream, leaveStreamOpen: true))
                    using (var nuspecStream = reader.GetNuspec())
                    {
                        return new NuspecReader(nuspecStream);
                    }
                }
                catch (Exception exception) when (exception is PackagingException 
                                                    || exception is InvalidDataException)
                {
                    var fileStream = nupkgStream as FileStream;
                    if (fileStream != null)
                    {
                        report.LogWarning($"The ZIP archive {fileStream.Name} is corrupt");
                    }
                    throw;
                }
            }
        }
    }
}
