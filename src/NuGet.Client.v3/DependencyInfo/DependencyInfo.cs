using NuGet.Versioning;
using System;
using System.Globalization;
using System.Xml;

namespace NuGet.Client.DependencyInfo
{
    internal class DependencyInfo
    {
        public string Id { get; set; }
        public VersionRange Range { get; set; }
        public Uri RegistrationUri { get; set; }
        public RegistrationInfo RegistrationInfo { get; set; }

        public override string ToString()
        {
            return String.Format(CultureInfo.InvariantCulture, "{0} {1}", Id, Range);
        }
    }
}
