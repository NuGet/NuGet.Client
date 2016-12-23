using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol
{
    public class LocalPackageListResource : ListResource
    {
        private readonly PackageSearchResource _localPackageSearchResource;

        public LocalPackageListResource(PackageSearchResource localPackageSearchResource)
        {
            _localPackageSearchResource = localPackageSearchResource;
        }

        public override Task<IEnumerableAsync<IPackageSearchMetadata>> ListAsync(string searchTerm, bool prerelease, bool allVersions, bool includeDelisted, ILogger logger,
            CancellationToken token)
        {
            SearchFilter filter;

            if (allVersions)
            {
                filter = new SearchFilter(includePrerelease: true);
                filter.OrderBy = SearchOrderBy.Id;
                // whether prerelease is included should not matter as allVersions precedes it
                filter.IncludeDelisted = includeDelisted;
            }
            else if (prerelease)
            {
                filter = new SearchFilter(includePrerelease: true, filter: SearchFilterType.IsAbsoluteLatestVersion);
                filter.OrderBy = SearchOrderBy.Id;
                filter.IncludeDelisted = includeDelisted;
            }
            else
            {
                filter = new SearchFilter(includePrerelease: false, filter: SearchFilterType.IsLatestVersion);
                filter.OrderBy = SearchOrderBy.Id;
            }
            IEnumerableAsync<IPackageSearchMetadata> enumerable = new EnumerableAsync<IPackageSearchMetadata>(_localPackageSearchResource, searchTerm, filter,
                logger, token);
            return Task.FromResult(enumerable);

        }

        class EnumerableAsync<T> : IEnumerableAsync<T>
        {
            private readonly SearchFilter _filter;
            private readonly ILogger _logger;
            private readonly string _searchTerm;
            private readonly CancellationToken _token;
            private readonly PackageSearchResource _packageSearchResource;


            public EnumerableAsync(PackageSearchResource feedParser, string searchTerm, SearchFilter filter, ILogger logger, CancellationToken token)
            {
                _packageSearchResource = feedParser;
                _searchTerm = searchTerm;
                _filter = filter;
                _logger = logger;
                _token = token;
            }

            public IEnumeratorAsync<T> GetEnumeratorAsync()
            {
                return (IEnumeratorAsync<T>)new EnumeratorAsync(_packageSearchResource, _searchTerm, _filter, _logger, _token);
            }
        }

        internal class EnumeratorAsync : IEnumeratorAsync<IPackageSearchMetadata>
        {
            private readonly SearchFilter _filter;
            private readonly ILogger _logger;
            private readonly string _searchTerm;
            private readonly CancellationToken _token;
            private readonly PackageSearchResource _packageSearchResource;


            private IEnumerator<IPackageSearchMetadata> _currentEnumerator;

            public EnumeratorAsync(PackageSearchResource feedParser, string searchTerm, SearchFilter filter, ILogger logger, CancellationToken token)
            {
                _packageSearchResource = feedParser;
                _searchTerm = searchTerm;
                _filter = filter;
                _logger = logger;
                _token = token;
            }

            public IPackageSearchMetadata Current
            {
                get
                {
                    return _currentEnumerator?.Current;
                }
            }

            public async Task<bool> MoveNextAsync()
            {
                if (_currentEnumerator == null) // TODO NK - paginate this/rearchitect the enumerableAsync
                {
                    var results = await _packageSearchResource.SearchAsync(_searchTerm, _filter, 0, Int32.MaxValue, _logger, _token);
                    _currentEnumerator = results.OrderBy(p => p.Identity).GetEnumerator();
                }

                if (!_currentEnumerator.MoveNext())
                {
                    _currentEnumerator = null; //TODO NK - This is wrong
                    return false;
                }
                return true;
            }
        }
    }
}