// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.VisualStudio
{
    internal class VsPathContext : IVsPathContext
    {
        public VsPathContext(string userPackageFolder, IEnumerable<string> fallbackPackageFolders)
        {
            if (userPackageFolder == null)
            {
                throw new ArgumentNullException(nameof(userPackageFolder));
            }

            if (fallbackPackageFolders == null)
            {
                throw new ArgumentNullException(nameof(fallbackPackageFolders));
            }

            UserPackageFolder = userPackageFolder;
            FallbackPackageFolders = fallbackPackageFolders.ToList();
        }

        public string UserPackageFolder { get; }
        public IEnumerable FallbackPackageFolders { get; }
    }
}
