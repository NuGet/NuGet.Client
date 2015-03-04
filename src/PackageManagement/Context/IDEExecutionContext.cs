using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using System;
using System.Threading.Tasks;

namespace NuGet.PackageManagement
{
    public class IDEExecutionContext : ExecutionContext
    {
        public ICommonOperations CommonOperations { get; private set; }
        public IDEExecutionContext(ICommonOperations commonOperations)
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

        public PackageIdentity IDEDirectInstall
        {
            get
            {
                return DirectInstall;
            }
            set
            {
                DirectInstall = value;
            }
        }
    }
}
