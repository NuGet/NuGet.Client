using NuGet.Packaging.Core;
using System;
using System.Threading.Tasks;

namespace NuGet.ProjectManagement
{
    public abstract class ExecutionContext
    {
        public ExecutionContext()
        {
        }
        // HACK: TODO: OpenFile is likely never called from ProjectManagement
        // Should only be in PackageManagement
        public abstract Task OpenFile(string fullPath);
        public PackageIdentity DirectInstall { get; protected set; }
    }
}
