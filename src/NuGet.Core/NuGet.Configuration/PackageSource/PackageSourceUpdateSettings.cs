// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Configuration
{
    public sealed class PackageSourceUpdateSettings
    {
        public readonly static PackageSourceUpdateSettings Default = new PackageSourceUpdateSettings(true, true);

        public bool UpdateCredentials { get; private set; }
        public bool UpdateEnabled { get; private set; }

        public PackageSourceUpdateSettings(bool updateCredentials, bool updateEnabled)
        {
            UpdateCredentials = updateCredentials;
            UpdateEnabled = updateEnabled;
        }
    }
}
