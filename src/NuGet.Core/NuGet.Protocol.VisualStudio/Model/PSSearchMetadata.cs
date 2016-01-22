// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.Protocol.VisualStudio
{
    /// <summary>
    /// Model for search results shown by PowerShell console search.
    /// </summary>
    public class PSSearchMetadata
    {
        public PSSearchMetadata(PackageIdentity identity, Lazy<Task<IEnumerable<NuGetVersion>>> versions, string summary)
        {
            Identity = identity;
            Versions = versions;
            Summary = summary;
        }

        public PackageIdentity Identity { get; }

        public NuGetVersion Version { get; }

        public Lazy<Task<IEnumerable<NuGetVersion>>> Versions { get; }

        public string Summary { get; private set; }
    }
}
