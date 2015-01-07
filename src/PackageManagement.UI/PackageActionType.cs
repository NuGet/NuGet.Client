using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackageManagement.UI
{
    public enum PackageActionType
    {
        // installs a package into a project/solution
        Install,

        // uninstalls a package from a project/solution
        Uninstall,

        // downloads the package if needed and adds it to the packages folder
        AddToPackagesFolder,

        // deletes the package from the packages folder
        DeleteFromPackagesFolder
    }
}
