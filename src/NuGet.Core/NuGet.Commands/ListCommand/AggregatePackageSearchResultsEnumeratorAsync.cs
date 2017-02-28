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
        private IComparer<IPackageSearchMetadata> _idOrderingComparer;
        private IComparer<NuGetVersion> _versionOrderingComparer;

        private readonly List<IEnumeratorAsync<IPackageSearchMetadata>> _asyncEnumerators = new List<IEnumeratorAsync<IPackageSearchMetadata>>();
        private readonly HashSet<IPackageSearchMetadata> _seen;
        private IPackageSearchMetadata CurrentPackage;

        private bool firstPass = true;

        public AggregatePackageSearchResultsEnumeratorAsync(IList<IEnumerableAsync<IPackageSearchMetadata>> _asyncEnumerables, IComparer<IPackageSearchMetadata> _idOrderingComparer, IComparer<NuGetVersion> _versionOrderingComparer, IEqualityComparer<IPackageSearchMetadata> _idEqualityComparer)
        {
            this._idOrderingComparer = _idOrderingComparer;
            this._versionOrderingComparer = _versionOrderingComparer;
            _seen = _idEqualityComparer == null ? new HashSet<IPackageSearchMetadata>() : new HashSet<IPackageSearchMetadata>(_idEqualityComparer);
            foreach(var enumerable in _asyncEnumerables)
            {
                _asyncEnumerators.Add(enumerable.GetEnumeratorAsync());
            }

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
                foreach (IEnumeratorAsync<IPackageSearchMetadata> enumerator in _asyncEnumerators)
                {   //TODO NK - Can a package be without a version?
                    if (candidate == null || (_idOrderingComparer.Compare(candidate, enumerator.Current) > 0 && _versionOrderingComparer.Compare(candidate.Identity.Version,enumerator.Current.Identity.Version) > 0 ))
                    {
                        candidate = enumerator.Current;
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
            var versions = await GetVersions(candidate, packages, new VersionInfoOrderingComparer(_versionOrderingComparer));
            CurrentPackage = PackageSearchMetadataBuilder.FromMetadata(candidate).WithVersions(AsyncLazy.New(versions)).Build();
        }

        private class VersionInfoOrderingComparer : IComparer<VersionInfo>
        {
            private IComparer<NuGetVersion> _versionOrderingComparer;

            public VersionInfoOrderingComparer(IComparer<NuGetVersion> _versionOrderingComparer)
            {
                this._versionOrderingComparer = _versionOrderingComparer;
            }

            public int Compare(VersionInfo x, VersionInfo y)
            {
                return _versionOrderingComparer.Compare(x.Version, y.Version);
            }
        }

        private static async Task<IEnumerable<VersionInfo>> GetVersions(IPackageSearchMetadata candidate, List<IPackageSearchMetadata> packages, IComparer<VersionInfo> _versionOrderingComparer)
        {
            var uniqueVersions = new SortedSet<VersionInfo>(_versionOrderingComparer);

            foreach (var package in packages)
            {
                foreach (var packageVersion in await package.GetVersionsAsync())
                {
                    if (uniqueVersions.Add(packageVersion) && !package.Equals(candidate))
                    {
                        packageVersion.PackageSearchMetadata = candidate;
                    }
                }

            }

            return (IEnumerable<VersionInfo>)uniqueVersions;
        }
    }
}