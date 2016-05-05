// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NuGet.Common;
using NuGet.Versioning;

namespace NuGet.Packaging.Core
{
    /// <summary>
    /// Basic package reader that provides the identity, min client version, and file access.
    /// </summary>
    /// <remarks>Higher level concepts used for normal development nupkgs should go at a higher level</remarks>
    public interface IPackageCoreReader : IDisposable
    {
        /// <summary>
        /// Identity of the package
        /// </summary>
        /// <returns></returns>
        PackageIdentity GetIdentity();

        /// <summary>
        /// Minimum client version needed to consume the package.
        /// </summary>
        NuGetVersion GetMinClientVersion();

        /// <summary>
        /// Gets zero or more package types from the .nuspec.
        /// </summary>
        IReadOnlyList<PackageType> GetPackageTypes();

        /// <summary>
        /// Returns a file stream from the package.
        /// </summary>
        Stream GetStream(string path);

        /// <summary>
        /// All files in the nupkg
        /// </summary>
        IEnumerable<string> GetFiles();

        /// <summary>
        /// Files in a folder
        /// </summary>
        /// <param name="folder">Folder path</param>
        /// <returns>A collection of files under specified folder</returns>
        IEnumerable<string> GetFiles(string folder);

        /// <summary>
        /// Nuspec stream
        /// </summary>
        Stream GetNuspec();

        IEnumerable<string> CopyFiles(
            string destination,
            IEnumerable<string> packageFiles,
            ExtractPackageFileDelegate extractFile,
            ILogger logger,
            CancellationToken token);
    }
}
