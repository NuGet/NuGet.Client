// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Common;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Commands
{
    public class AggregatePackageSearchResultsEnumerableAsync : IEnumerableAsync<IPackageSearchMetadata>
    {
        private readonly IList<IEnumerableAsync<IPackageSearchMetadata>> _asyncEnumerables;
        private readonly IComparer<IPackageSearchMetadata> _idOrderingComparer;
        private readonly IComparer<NuGetVersion> _versionOrderingComparer;
        private readonly IEqualityComparer<IPackageSearchMetadata> _idEqualityComparer;

        public AggregatePackageSearchResultsEnumerableAsync(List<IEnumerableAsync<IPackageSearchMetadata>> allPackages, IComparer<IPackageSearchMetadata> idOrderingComparer, IComparer<NuGetVersion> versionOrderingComparer, IEqualityComparer<IPackageSearchMetadata> idEqualityComparer)
        {
            this._asyncEnumerables = allPackages;
            this._idOrderingComparer = idOrderingComparer;
            this._versionOrderingComparer = versionOrderingComparer;
            this._idEqualityComparer = idEqualityComparer;
        }

        public IEnumeratorAsync<IPackageSearchMetadata> GetEnumeratorAsync()
        {
            return new AggregatePackageSearchResultsEnumeratorAsync(_asyncEnumerables, _idOrderingComparer, _versionOrderingComparer, _idEqualityComparer);
        }
    }
}