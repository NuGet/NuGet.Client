using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Client.VisualStudio
{
    public abstract class PSAutoCompleteResource : INuGetResource
    {
       public abstract Task<IEnumerable<string>> IdStartsWith(string packageIdPrefix, bool includePrerelease, CancellationToken token);

       public abstract Task<IEnumerable<NuGetVersion>> VersionStartsWith(string packageId, string versionPrefix, bool includePrerelease, CancellationToken token);
    }
}

