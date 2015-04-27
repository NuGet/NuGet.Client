using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

namespace NuGet.Protocol.Core.v3.DependencyInfo
{
    internal class PackageInfo
    {
        public RegistrationInfo Registration { get; set; }
        public NuGetVersion Version { get; set; }
        public Uri PackageContent { get; set; }
        public IList<DependencyInfo> Dependencies { get; private set; }

        public PackageInfo()
        {
            Dependencies = new List<DependencyInfo>();
        }

        public override string ToString()
        {
            return String.Format(CultureInfo.InvariantCulture, "{0} {1}", Registration.Id, Version.ToNormalizedString());
        }
    }
}
