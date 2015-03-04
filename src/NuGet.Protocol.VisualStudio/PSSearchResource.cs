using NuGet.Protocol.Core.Types;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.VisualStudio
{
    public abstract class PSSearchResource : INuGetResource
    {

        public abstract Task<IEnumerable<PSSearchMetadata>> Search(string search, SearchFilter filters, int skip, int take, CancellationToken token);

    }
}
