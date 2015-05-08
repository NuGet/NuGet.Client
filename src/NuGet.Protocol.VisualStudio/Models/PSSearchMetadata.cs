// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.Protocol.VisualStudio
{
    /// <summary>
    /// Model for search results shown by PowerShell console search.
    /// *TODOS: Should we extract out ID,version and summary to a base search model ?
    /// </summary>
    public sealed class PSSearchMetadata
    {
        public PSSearchMetadata(PackageIdentity identity, IEnumerable<NuGetVersion> versions, string summary)
        {
            Identity = identity;
            Versions = versions;
            Summary = summary;
        }

        public PackageIdentity Identity { get; private set; }
        public NuGetVersion Version { get; private set; }
        public IEnumerable<NuGetVersion> Versions { get; private set; }
        public string Summary { get; private set; }
    }
}
