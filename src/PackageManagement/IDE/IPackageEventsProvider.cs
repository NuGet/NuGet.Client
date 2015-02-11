using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackageManagement
{
    /// <summary>
    /// Internal version of the public IVsPackageInstallerEvents
    /// </summary>
    public interface IPackageEventsProvider
    {
        PackageEvents GetPackageEvents();
    }
}
