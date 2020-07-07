// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement.VisualStudio
{
    public sealed class PackageSearchMetadataCache : IDisposable
    {
        // Cached Package Metadata
        public IReadOnlyList<IPackageSearchMetadata> Packages { get; set; }

        // Remember the IncludePrerelease setting corresponding to the Cached Metadata
        public bool IncludePrerelease { get; set; }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (Packages != null)
                {
                    Packages = null;
                }
            }
        }
    }
}
