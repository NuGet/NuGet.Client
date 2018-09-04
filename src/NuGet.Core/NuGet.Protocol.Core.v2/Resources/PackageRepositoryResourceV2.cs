// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Protocol.Core.v2
{
    /// <summary>
    /// Holds an IPackageRepository
    /// </summary>
    public class PackageRepositoryResourceV2 : V2Resource
    {
        public PackageRepositoryResourceV2(IPackageRepository repository)
            : base(repository)
        {
        }
    }
}
