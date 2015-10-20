// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Packaging.Core;

namespace NuGet.PackageManagement
{
    /// <summary>
    /// If 'Restored' is false, it means that the package was already restored
    /// If 'Restored' is true, the package was restored and successfully
    /// </summary>
    public class PackageRestoredEventArgs : EventArgs
    {
        public PackageIdentity Package { get; private set; }
        public bool Restored { get; private set; }

        public PackageRestoredEventArgs(PackageIdentity packageIdentity, bool restored)
        {
            if (packageIdentity == null)
            {
                throw new ArgumentNullException(nameof(packageIdentity));
            }

            Package = packageIdentity;
            Restored = restored;
        }
    }
}