// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement
{
    // TODO: make this internal

    /// <summary>
    /// Source aware PackageDependencyInfo for multi repo scenarios.
    /// </summary>
    public class SourceDependencyInfo : PackageDependencyInfo
    {
        public SourceRepository Source { get; private set; }

        public SourceDependencyInfo(PackageDependencyInfo info, SourceRepository source)
            : base(info, info.Dependencies)
        {
            Source = source;
        }
    }
}
