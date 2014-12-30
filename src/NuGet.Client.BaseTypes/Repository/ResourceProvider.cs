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
        public abstract Task<Resource> Create(PackageSource source);      
    }
}
