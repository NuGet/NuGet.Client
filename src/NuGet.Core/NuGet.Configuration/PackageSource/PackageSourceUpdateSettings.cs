// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Configuration
{
    public class PackageSourceUpdateSettings
    {
        public bool UpdateCredentials { get; set; }
        public bool UpdateEnabled { get; set; }

        public PackageSourceUpdateSettings(bool updateCredentials = true, bool updateEnabled = true)
        {
            UpdateCredentials = updateCredentials;
            UpdateEnabled = updateEnabled;
        }
    }
}
