using NuGet.PackagingCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackageManagement.UI
{
    public abstract class PackageAction
    {
        public PackageActionType ActionType { get; private set; }
        public PackageIdentity Package { get; private set; }

        protected PackageAction(
            PackageActionType actionType,
            PackageIdentity package)
        {
            ActionType = actionType;
            Package = package;
        }
    }
}
