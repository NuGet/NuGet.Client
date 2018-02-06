using System.ComponentModel.Composition;
using Apex.NuGetClient.PackageManageUI;

namespace Apex.NuGetClient.TestServices
{
    [Export]
    public class PackageManageUIService : NuGetClientTestService
    {
        public PackageManageUITestExtension Current
        {
            get
            {
                return this.CreateRemotableInstance<PackageManageUITestExtension>(this);
            }
        }
    }
}
