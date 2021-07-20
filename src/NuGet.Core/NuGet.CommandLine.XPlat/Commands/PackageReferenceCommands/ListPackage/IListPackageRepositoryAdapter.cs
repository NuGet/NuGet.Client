// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine.XPlat
{
    internal interface IListPackageRepositoryAdapter
    {
        IEnumerable<Lazy<INuGetResourceProvider>> GetCoreV3Provider();
        SourceRepository CreateSource(IEnumerable<Lazy<INuGetResourceProvider>> resourceProviders, PackageSource source, FeedType type);
    }
}
