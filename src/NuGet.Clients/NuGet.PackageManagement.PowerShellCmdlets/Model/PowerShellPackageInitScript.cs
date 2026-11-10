// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    public sealed class PowerShellPackageInitScript
    {
        internal PowerShellPackageInitScript(PackageIdentity identity, string installPath)
        {
            Identity = identity;
            InstallPath = installPath;
            FullName = Path.GetFullPath(Path.Combine(installPath, "tools", "init.ps1"));
        }

        public string Id => Identity.Id;
        public NuGetVersion Version => Identity.Version;
        public string FullName { get; }

        internal PackageIdentity Identity { get; }
        internal string InstallPath { get; }
    }
}
