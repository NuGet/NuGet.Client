// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine.XPlat
{
    /// <summary>
    /// Defines methods for rendering the search results of NuGet packages.
    /// </summary>
    internal interface IPackageSearchResultRenderer
    {
        /// <summary>
        /// Starts the rendering operation.
        /// </summary>
        void Start();

        /// <summary>
        /// Adds a list of packages from a source to be rendered.
        /// </summary>
        Task Add(PackageSource source, Task<IEnumerable<IPackageSearchMetadata>> completedSearchTask);

        /// <summary>
        /// Finishes the rendering operation.
        /// </summary>
        void Finish();
    }
}
