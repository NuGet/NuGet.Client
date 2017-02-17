// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Commands
{
    public class AggregatePackageSearchResultsEnumeratorAsync : IEnumeratorAsync<IPackageSearchMetadata>
    {
        private IList<IEnumerableAsync<IPackageSearchMetadata>> _asyncEnumerables;
        private IComparer<IPackageSearchMetadata> _idOrderingComparer;
        private IComparer<IPackageSearchMetadata> _versionOrderingComparer;

        private readonly List<IEnumeratorAsync<IPackageSearchMetadata>> _asyncEnumerators = new List<IEnumeratorAsync<IPackageSearchMetadata>>();
        private readonly HashSet<IPackageSearchMetadata> _seen;
        private IPackageSearchMetadata CurrentPackage;

        private bool firstPass = true;

        public AggregatePackageSearchResultsEnumeratorAsync(IList<IEnumerableAsync<IPackageSearchMetadata>> _asyncEnumerables, IComparer<IPackageSearchMetadata> _idOrderingComparer, IComparer<IPackageSearchMetadata> _versionOrderingComparer, IEqualityComparer<IPackageSearchMetadata> _idEqualityComparer)
        {
            this._asyncEnumerables = _asyncEnumerables;
            this._idOrderingComparer = _idOrderingComparer;
            this._versionOrderingComparer = _versionOrderingComparer;
            _seen = _idEqualityComparer == null ? new HashSet<IPackageSearchMetadata>() : new HashSet<IPackageSearchMetadata>(_idEqualityComparer);

        }

        IPackageSearchMetadata IEnumeratorAsync<IPackageSearchMetadata>.Current
        {
            get
            {
                return CurrentPackage;
            }
        }

        async Task<bool> IEnumeratorAsync<IPackageSearchMetadata>.MoveNextAsync()
        {
            while (_asyncEnumerators.Count > 0)
            {
                IPackageSearchMetadata candidate = null;
                List<IEnumeratorAsync<IPackageSearchMetadata>> completedEnums = null;

                if (firstPass)
                {
                    foreach (IEnumeratorAsync<IPackageSearchMetadata> enumerator in _asyncEnumerators)
                    {
                        if (!await enumerator.MoveNextAsync())
                        {
                            if (completedEnums == null)
                            {
                                completedEnums = new List<IEnumeratorAsync<IPackageSearchMetadata>>();
                            }
                            completedEnums.Add(enumerator);

                        }
                    }
                    firstPass = false;
                }
                else
                {
                    foreach (IEnumeratorAsync<IPackageSearchMetadata> enumerator in _asyncEnumerators)
                    {
                        if (candidate == null || (_idOrderingComparer.Compare(candidate, enumerator.Current) > 0 && _versionOrderingComparer.Compare(candidate,enumerator.Current) > 0 ))
                        {
                            candidate = enumerator.Current;
                        }
                    }
                }

                var packages = new List<IPackageSearchMetadata>();
                foreach (IEnumeratorAsync<IPackageSearchMetadata> enumerator in _asyncEnumerators)
                {
                    while (_idOrderingComparer.Compare(candidate, enumerator.Current) == 0)
                    {
                        packages.Add(enumerator.Current);
                        if (!await enumerator.MoveNextAsync())
                        {
                            if (completedEnums == null)
                            {
                                completedEnums = new List<IEnumeratorAsync<IPackageSearchMetadata>>();
                            }
                            completedEnums.Add(enumerator);
                            break;
                        }
                    }

                }

                if (completedEnums != null)
                {
                    _asyncEnumerators.RemoveAll(obj => completedEnums.Contains(obj));
                }

                if (candidate != null)
                {
                    if (_seen.Add(candidate))
                    {
                        await UpdateCurrentPackage(candidate, packages);
                        return true;
                    }
                }
            }
            return false;
        }

        private async Task UpdateCurrentPackage(IPackageSearchMetadata candidate, List<IPackageSearchMetadata> packages)
        {
            var versions = await GetVersions(candidate, packages);
            CurrentPackage = PackageSearchMetadataBuilder.FromMetadata(candidate).WithVersions(AsyncLazy.New(versions)).Build();


        }
        private static async Task<IEnumerable<VersionInfo>> GetVersions(IPackageSearchMetadata candidate, List<IPackageSearchMetadata> packages)
        {
            var uniqueVersions = new SortedSet<NuGetVersion>();

            foreach (var package in packages)
            {
                foreach (var packageVersion in await package.GetVersionsAsync())
                {
                    if (uniqueVersions.Add(packageVersion.Version))
                    {
                        packageVersion.PackageSearchMetadata = candidate;
                    }
                }

            }

            return (IEnumerable<VersionInfo>)uniqueVersions;
        }
    }
}