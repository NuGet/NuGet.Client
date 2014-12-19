using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGet.Client
{

    public abstract class ResourceProvider
    {
        protected IDictionary<string, object> packageSourceCache;
        public abstract bool TryCreateResource(PackageSource source, out Resource resource);
        public virtual Resource Create(PackageSource source)
        {
            Resource resource = null;
            if (TryCreateResource(source, out resource))
                return resource;
            else
                return null; //*TODOs: Throw ResourceNotCreated exception ?
        }
    }
}
