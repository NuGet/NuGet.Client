using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

namespace NuGet.Client.DependencyInfo
{
    internal class RegistrationInfo
    {
        public string Id { get; set; }
        public bool IncludePrerelease { get; set; }
        public IList<PackageInfo> Packages { get; private set; }

        public RegistrationInfo()
        {
            Packages = new List<PackageInfo>();
        }

        public void Add(PackageInfo packageInfo)
        {
            packageInfo.Registration = this;
            Packages.Add(packageInfo);
        }

        public override string ToString()
        {
            return String.Format(CultureInfo.InvariantCulture, "{0} Packages: {1}", Id, Packages.Count);
        }
    }
}
