using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NuGet.Versioning;

namespace NuGet.PackageManagement.UI
{
    internal class PackageDependencyMetadata
    {
        public PackageDependencyMetadata()
        {

        }

        public PackageDependencyMetadata(NuGet.Packaging.Core.PackageDependency serverData)
        {
            Id = serverData.Id;
            Range = serverData.VersionRange;
        }

        public string Id
        {
            get;
            private set;
        }

        public VersionRange Range
        {
            get;
            private set;
        }

        public PackageDependencyMetadata(string id, VersionRange range)
        {
            Id = id;
            Range = range;
        }

        public override string ToString()
        {
            if (Range == null)
            {
                return Id;
            }
            else
            {
                return String.Format(
                    CultureInfo.InvariantCulture,
                    "{0} {1}",
                    Id, Range.PrettyPrint());
            }
        }
    }
}
