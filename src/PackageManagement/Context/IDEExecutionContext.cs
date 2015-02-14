using NuGet.PackagingCore;
using NuGet.ProjectManagement;
using System;
using System.Threading.Tasks;

namespace NuGet.PackageManagement.Context
{
    public class IDEExecutionContext : ExecutionContext
    {
        public ICommonOperations CommonOperations { get; private set; }
        public IDEExecutionContext(PackageIdentity directInstall, ICommonOperations commonOperations) : base(directInstall)
        {
            if(commonOperations == null)
            {
                throw new ArgumentNullException("commonOperations");
            }
            CommonOperations = commonOperations;
        }
        public override async Task OpenFile(string fullPath)
        {
            await CommonOperations.OpenFile(fullPath);
        }
    }
}
