// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Common
{
    /// <summary>
    /// Aggregates from a list of already ordered enumerables
    /// </summary>

    public class AggregateEnumerableAsync<T> : IEnumerableAsync<T>
    {

        private readonly IList<IEnumerableAsync<T>> _asyncEnumerables;
        private readonly IComparer<T> _comparer;
        private readonly IEqualityComparer<T> _equalityComparer;

        public AggregateEnumerableAsync(IList<IEnumerableAsync<T>> asyncEnumerables, IComparer<T> comparer, IEqualityComparer<T> equalityComparer)
        {
            _asyncEnumerables = asyncEnumerables;
            _comparer = comparer;
            _equalityComparer = equalityComparer;
        }

        public IEnumeratorAsync<T> GetEnumeratorAsync()
        {
            return new AggregateEnumeratorAsync<T>(_asyncEnumerables, _comparer, _equalityComparer);
        }
    }

    public class AggregateEnumeratorAsync<T> : IEnumeratorAsync<T>
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
}