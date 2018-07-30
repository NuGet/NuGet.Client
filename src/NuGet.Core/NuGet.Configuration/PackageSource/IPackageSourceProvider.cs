// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Configuration
{
    public interface IPackageSourceProvider
    {
        IEnumerable<PackageSource> LoadPackageSources();

        PackageSource GetPackageSourceWithName(string name);
        PackageSource GetPackageSourceWithSource(string source);

        event EventHandler PackageSourcesChanged;

        void RemovePackageSource(string name);
        void EnablePackageSource(string name);
        void DisablePackageSource(string name);
        void UpdatePackageSource(PackageSource source, bool updateCredentials, bool updateEnabled);
        void AddPackageSource(PackageSource source);
        void SavePackageSources(IEnumerable<PackageSource> sources);
        string ActivePackageSourceName { get; }
        string DefaultPushSource { get; }

        void SaveActivePackageSource(PackageSource source);
    }
}
