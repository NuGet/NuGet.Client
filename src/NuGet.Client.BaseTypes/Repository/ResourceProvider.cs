using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client
{

    public abstract class ResourceProvider
    {
        protected IDictionary<string, object> packageSourceCache = new Dictionary<string,object>();
        public abstract Task<bool> TryCreateResource(PackageSource source, out Resource resource);
        public async virtual Task<Resource> Create(PackageSource source)
        {
            Resource resource = null;
            if (await TryCreateResource(source, out resource))
                return resource;
            else
                return null; //*TODOs: Throw ResourceNotCreated exception ?
        }
    }
}
