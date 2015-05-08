// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.Protocol.Core.Types
{
    /// <summary>
    /// A basic search result needed for the command line
    /// </summary>
    public class SimpleSearchMetadata
    {
        /// <summary>
        /// Package id and version
        /// </summary>
        public PackageIdentity Identity { get; private set; }

        /// <summary>
        /// Package description
        /// </summary>
        public string Description { get; private set; }

        /// <summary>
        /// All versions of the package
        /// </summary>
        public IEnumerable<NuGetVersion> AllVersions { get; private set; }

        public SimpleSearchMetadata(PackageIdentity identity, string description, IEnumerable<NuGetVersion> allVersions)
        {
            Identity = identity;
            Description = description;
            AllVersions = allVersions;
        }
    }
}
