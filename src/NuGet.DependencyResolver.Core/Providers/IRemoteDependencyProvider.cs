using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Frameworks;
using NuGet.LibraryModel;

namespace NuGet.DependencyResolver
{
    public interface IRemoteDependencyProvider
    {
        bool IsHttp { get; }

        Task<LibraryIdentity> FindLibraryAsync(LibraryRange libraryRange, NuGetFramework targetFramework, CancellationToken cancellationToken);

        Task<IEnumerable<LibraryDependency>> GetDependenciesAsync(LibraryIdentity match, NuGetFramework targetFramework, CancellationToken cancellationToken);

        Task CopyToAsync(LibraryIdentity match, Stream stream, CancellationToken cancellationToken);
    }
}