// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Commands.ListCommand;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.Commands
{
    /// <summary>
    /// Command Runner used to run the business logic for nuget list command
    /// </summary>
    public class ListCommandRunner : IListCommandRunner
    {
        /// <summary>
        /// Executes the logic for nuget list command.
        /// </summary>
        /// <returns></returns>
        public async Task ExecuteCommand(ListArgs listArgs)

        {
            //Create SourceFeed for each packageSource
            IList<ListResource> sourceFeeds = new List<ListResource>();
            foreach (KeyValuePair<PackageSource, string> packageSource in listArgs.ListEndpoints)
            {
                var sourceRepository = Repository.Factory.GetCoreV3(packageSource.Value);
                var feed = await sourceRepository.GetResourceAsync<ListResource>(listArgs.CancellationToken);

                if (feed != null)
                {
                    sourceFeeds.Add(feed);
                }
            }


            IList<IEnumerableAsync<IPackageSearchMetadata>> allPackages = new List<IEnumerableAsync<IPackageSearchMetadata>>();
            foreach (var feed in sourceFeeds)
            {
                
               var packagesFromSource = await feed.ListAsync(listArgs.Arguments[0], listArgs.Prerelease, listArgs.AllVersions,
                    listArgs.IncludeDelisted, listArgs.Logger , listArgs.CancellationToken);
                allPackages.Add(packagesFromSource);
            }
            //TODO NK - Find an IComparer

            CompareIPackageSearchMetadata comparer = new CompareIPackageSearchMetadata();
            await PrintPackages(listArgs, new AggregateEnumerableAsync<IPackageSearchMetadata>(allPackages, comparer, comparer).GetEnumeratorAsync());
        }

        private class CompareIPackageSearchMetadata : IComparer<IPackageSearchMetadata>, IEqualityComparer<IPackageSearchMetadata>
        {
            private PackageIdentityComparer comparer;
            public CompareIPackageSearchMetadata()
            {
                comparer = PackageIdentityComparer.Default;
            }
            public int Compare(IPackageSearchMetadata x, IPackageSearchMetadata y)
            {
                if (x == null && y == null)
                {
                    return 0;
                }
                else if (x == null && y != null)
                {
                    return 1;
                }
                else if (x != null && y == null)
                {
                    return -1;
                }
                return comparer.Compare(x.Identity, y.Identity);
            }

            public bool Equals(IPackageSearchMetadata x, IPackageSearchMetadata y)
            {
                if (x == null && y == null)
                {
                    return true;
                }
                if (x == null || y == null)
                {
                    return false;
                }
                return comparer.Equals(x.Identity, y.Identity);
            }

            public int GetHashCode(IPackageSearchMetadata obj)
            {
                // TODO NK - does this make sense to be fixed? 
                return comparer.GetHashCode(obj.Identity);
            }
        }

        class AggregateEnumerableAsync<T> : IEnumerableAsync<T>
        {

            private readonly IList<IEnumerableAsync<T>> _asyncEnumerables;
            private readonly IComparer<T> _comparer;
            private readonly IEqualityComparer<T> _equalityComparer; 

            public AggregateEnumerableAsync(IList<IEnumerableAsync<T>> asyncEnumerables, IComparer<T> comparer, IEqualityComparer<T> equalityComparer )
            {
                _asyncEnumerables = asyncEnumerables;
                _comparer = comparer;
                _equalityComparer = equalityComparer;
            } 
             
            public IEnumeratorAsync<T> GetEnumeratorAsync()
            {
                return new AggregateEnumeratorAsync<T>(_asyncEnumerables,_comparer,_equalityComparer);
            }
        }

        class AggregateEnumeratorAsync<T> : IEnumeratorAsync<T>
        {

            private readonly HashSet<T> _seen;
            private readonly IComparer<T> _comparer;
            private readonly List<IEnumeratorAsync<T>> _asyncEnumerators = new List<IEnumeratorAsync<T>>();
            private IEnumeratorAsync<T> _currentEnumeratorAsync;
            private IEnumeratorAsync<T> _lastAwaitedEnumeratorAsync;


            public AggregateEnumeratorAsync(IList<IEnumerableAsync<T>> asyncEnumerables, IComparer<T> comparer, IEqualityComparer<T> equalityComparer)
            {
                for (int i = 0; i < asyncEnumerables.Count; i++)
                {
                    var enumerator = asyncEnumerables[i].GetEnumeratorAsync();
                    _asyncEnumerators.Add(enumerator);
                }
                _comparer = comparer;
                _seen = new HashSet<T>(equalityComparer);
            }

            public T Current
            {
                get
                {
                    if (_currentEnumeratorAsync == null)
                    {
                        return default(T);
                    }
                    return _currentEnumeratorAsync.Current;
                }
            }

            public async Task<bool> MoveNextAsync()
            {
                while (_asyncEnumerators.Count > 0)
                {
                    T currentValue = default(T);
                    foreach (IEnumeratorAsync<T> enumerator in _asyncEnumerators)
                    {
                        if (enumerator.Current == null || enumerator == _lastAwaitedEnumeratorAsync)
                        {
                            await enumerator.MoveNextAsync();
                        }

                        if (_comparer.Compare(enumerator.Current, currentValue) < 0)
                        {
                            currentValue = enumerator.Current;
                            _currentEnumeratorAsync = enumerator;
                        }
                    }
                    _lastAwaitedEnumeratorAsync = _currentEnumeratorAsync;
                    //Remove all the feeds with a null current
                    _asyncEnumerators.RemoveAll(enumerator => enumerator.Current == null);
                    if (currentValue != null)
                    {
                        if (_seen.Add(currentValue))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
        }

        private async Task PrintPackages(ListArgs listArgs, IEnumeratorAsync<IPackageSearchMetadata> asyncEnumerator)
        {
            bool hasPackages = false;
            if (asyncEnumerator != null)
            {
                if (listArgs.IsDetailed)
                {
                    /***********************************************
                     * Package-Name
                     *  1.0.0.2010
                     *  This is the package Description
                     * 
                     * Package-Name-Two
                     *  2.0.0.2010
                     *  This is the second package Description
                     ***********************************************/
                    while (await asyncEnumerator.MoveNextAsync())
                    {
                        var p = asyncEnumerator.Current;
                        listArgs.PrintJustified(0, p.Identity.Id);
                        listArgs.PrintJustified(1, p.Identity.Version.ToString());
                        listArgs.PrintJustified(1, p.Description);
                        if (!string.IsNullOrEmpty(p.LicenseUrl?.OriginalString))
                        {
                            listArgs.PrintJustified(1,
                                string.Format(
                                    CultureInfo.InvariantCulture,
                                    listArgs.ListCommandLicenseUrl,
                                    p.LicenseUrl.OriginalString));
                        }
                        Console.WriteLine();
                        hasPackages = true;
                    }
                }
                else
                {
                    /***********************************************
                     * Package-Name 1.0.0.2010
                     * Package-Name-Two 2.0.0.2010
                     ***********************************************/
//                    IEnumeratorAsync<IPackageSearchMetadata> asyncEnumerator = packages.GetAsyncEnumerator();

                    while (await asyncEnumerator.MoveNextAsync())
                    {
                        var p = asyncEnumerator.Current;
                        listArgs.PrintJustified(0, p.GetFullName());
                        hasPackages = true;
                    }
                }
            }
            if (!hasPackages)
            {
                Console.WriteLine(listArgs.ListCommandNoPackages);
            }
        }
    }
}