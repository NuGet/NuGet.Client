using NuGet.PackagingCore;
using System;
using System.Threading.Tasks;

namespace NuGet.ProjectManagement
{
    public abstract class ExecutionContext
    {
        public ExecutionContext(PackageIdentity directInstall)
        {
            if(directInstall == null)
            {
                throw new ArgumentNullException("directInstall");
            }
            DirectInstall = directInstall;
        }
        // HACK: TODO: OpenFile is likely never called from ProjectManagement
        // Should only be in PackageManagement
        public abstract Task OpenFile(string fullPath);
        public PackageIdentity DirectInstall { get; protected set; }
    }
}
