using NuGet.PackagingCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackageManagement.UI
{
    public class UserAction
    {
        public UserAction(PackageActionType action, PackageIdentity package)
        {
            Action = action;
            PackageIdentity = package;
        }

        public PackageActionType Action { get; private set; }

        public PackageIdentity PackageIdentity { get; private set; }
    }
}
