// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Protocol.Core.v2
{
    /// <summary>
    /// Allows an IPackageRepository repository to be passed in to a SourceRepository
    /// </summary>
    public class V2PackageSource : Configuration.PackageSource
    {
        private Func<IPackageRepository> _createRepo;

        public V2PackageSource(string source, Func<IPackageRepository> createRepo)
            : base(source)
        {
            _createRepo = createRepo;
        }

        public IPackageRepository CreatePackageRepository()
        {
            return _createRepo();
        }
    }
}
