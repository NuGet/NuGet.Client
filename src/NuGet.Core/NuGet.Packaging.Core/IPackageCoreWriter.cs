// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;

namespace NuGet.Packaging.Core
{
    /// <summary>
    /// Allows package modifications.
    /// </summary>
    public interface IPackageCoreWriter : IPackageCoreReader
    {
        /// <summary>
        /// Remove if the path exists.
        /// </summary>
        /// <param name="path">Relative file path in package.</param>
        /// <returns>True if the file existed before the remove.</returns>
        bool RemoveAsync(string path);

        /// <summary>
        /// Adds or replaces a file in the package.
        /// </summary>
        /// <param name="path">Relative file path in package.</param>
        /// <param name="stream">New file contents.</param>
        void AddAsync(string path, Stream stream);
    }
}
