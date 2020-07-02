// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Configuration
{
    public class PackageSourceTransaction
    {
        public PackageSource PackageSource { get; set; }
        public bool UpdateCredentials { get; set; }
        public bool UpdateEnabled { get; set; }

        public PackageSourceTransaction(PackageSource packageSource, bool updateCredentials = true, bool updateEnabled = true)
        {
            PackageSource = packageSource;
            UpdateCredentials = updateCredentials;
            UpdateEnabled = updateEnabled;
        }
    }
}
