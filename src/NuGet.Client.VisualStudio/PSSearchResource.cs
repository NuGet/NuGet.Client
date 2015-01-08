using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Client.VisualStudio
{
    public abstract class PSSearchResource : INuGetResource
    {

        public abstract Task<IEnumerable<PSSearchMetadata>> Search(string search, SearchFilter filters, int skip, int take, CancellationToken token);

    }
}
