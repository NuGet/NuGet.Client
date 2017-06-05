// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Configuration;

namespace NuGet.Test.PackageDownloadPlugin
{
    internal sealed class PluginPackageSource
    {
        internal bool ExposeNupkgFilesToNuGet { get; }
        internal PackageSource PackageSource { get; }

        internal PluginPackageSource(PackageSource packageSource, bool exposeNupkgFilesToNuGet)
        {
            Assert.IsNotNull(packageSource, nameof(packageSource));

            PackageSource = packageSource;
            ExposeNupkgFilesToNuGet = exposeNupkgFilesToNuGet;
        }
    }
}