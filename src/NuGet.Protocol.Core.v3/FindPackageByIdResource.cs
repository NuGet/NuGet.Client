using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Versioning;

namespace NuGet.Protocol.Core.Types
{
    public abstract class FindPackageByIdResource : INuGetResource
    {
        public abstract Task<IEnumerable<NuGetVersion>> GetAllVersionsAsync(string id, CancellationToken token);

        public abstract Task<NuspecReader> GetNuspecReaderAsync(string id, NuGetVersion version, CancellationToken token);

        public abstract Task<Stream> GetNupkgStreamAsync(string id, NuGetVersion version, CancellationToken token);
    }
}
