using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.VisualStudio
{
    public abstract class PSAutoCompleteResource : INuGetResource
    {
       public abstract Task<IEnumerable<string>> IdStartsWith(string packageIdPrefix, bool includePrerelease, CancellationToken token);

       public abstract Task<IEnumerable<NuGetVersion>> VersionStartsWith(string packageId, string versionPrefix, bool includePrerelease, CancellationToken token);
    }
}

