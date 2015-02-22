using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NuGet.Frameworks;
using NuGet.LibraryModel;

namespace NuGet.DependencyResolver
{
    public interface IRemoteDependencyProvider
    {
        bool IsHttp { get; }

        Task<RemoteMatch> FindLibrary(LibraryRange libraryRange, NuGetFramework targetFramework);
        Task<IEnumerable<LibraryDependency>> GetDependencies(RemoteMatch match, NuGetFramework targetFramework);
        Task CopyToAsync(RemoteMatch match, Stream stream);
    }

}