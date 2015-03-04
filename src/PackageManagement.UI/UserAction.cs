using NuGet.Packaging.Core;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackageManagement.UI
{
    public class UserAction
    {
        public UserAction(NuGetProjectActionType action, string packageId, NuGetVersion packageVersion)
        {
            Action = action;
            PackageId = packageId;
            if(packageVersion != null)
            {
                PackageIdentity = new PackageIdentity(packageId, packageVersion);
            }
        }

        public NuGetProjectActionType Action { get; private set; }

        public string PackageId { get; private set; }
        /// <summary>
        /// This can be null. This means that the only package id was provided in the user action
        /// </summary>
        public PackageIdentity PackageIdentity { get; private set; }
    }
}
