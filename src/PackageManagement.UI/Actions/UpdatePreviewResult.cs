using NuGet.Packaging.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackageManagement.UI
{
    public class UpdatePreviewResult
    {
        public PackageIdentity Old { get; private set; }
        public PackageIdentity New { get; private set; }

        public UpdatePreviewResult(PackageIdentity oldPackage, PackageIdentity newPackage)
        {
            Old = oldPackage;
            New = newPackage;
        }

        public override string ToString()
        {
            return Old.ToString() + " -> " + New.ToString();
        }
    }
}
