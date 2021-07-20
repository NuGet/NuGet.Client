// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine.XPlat
{
    internal class ListPackageRepositoryAdapter : IListPackageRepositoryAdapter
    {
        public IEnumerable<Lazy<INuGetResourceProvider>> GetCoreV3Provider() => Repository.Provider.GetCoreV3();
        public SourceRepository CreateSource(IEnumerable<Lazy<INuGetResourceProvider>> resourceProviders, PackageSource source, FeedType type) =>
            Repository.CreateSource(resourceProviders, source, type);
    }
}
