using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NuGet.Frameworks
{
    public static class FrameworkConstants
    {
        public const string NetFrameworkIdentifier = ".NETFramework";
        public const string NetCoreFrameworkIdentifier = ".NETCore";
        public const string PortableFrameworkIdentifier = ".NETPortable";
        public const string LessThanOrEqualTo = "\u2264";
        public const string GreaterThanOrEqualTo = "\u2265";

        public const RegexOptions RegexFlags = RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture;
        public static readonly Regex FrameworkRegex = new Regex(@"^(?<Framework>[A-Za-z]+)(?<Version>([0-9]+)(\.([0-9]+))*)?(?<Profile>-([A-Za-z]+[0-9]*)+(\+[A-Za-z]+[0-9]*)*)?$", RegexFlags);
    }
}
