// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace NuGet.VisualStudio
{
    [Export(typeof(IVsNuGetPathContext))]
    public class VsNuGetPathContext : IVsNuGetPathContext
    {
        public VsNuGetPathContext(string userPackageFolder, IEnumerable<string> fallbackPackageFolders)
        {
            UserPackageFolder = userPackageFolder;
            FallbackPackageFolders = fallbackPackageFolders.ToList();
        }

        public string UserPackageFolder { get; }
        public IReadOnlyList<string> FallbackPackageFolders { get; }
    }
}
