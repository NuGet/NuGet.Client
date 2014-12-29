using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NuGet.Packaging
{
    public static class PackagingConstants
    {
        public const string NupkgExtension = ".nupkg";
        public const string NuspecExtension = ".nuspec";
        public const string ContentFolder = "content";
        public const string AnyFramework = "any";
        public const string AgnosticFramework = "agnostic";

        public const string TargetFrameworkPropertyKey = "targetframework";

        // should this logic be taken out of packaging?
        public const RegexOptions RegexFlags = RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture;
        public static readonly Regex FrameworkRegex = new Regex(@"^(?<Framework>[A-Za-z]+)(?<Version>([0-9]+)(\.([0-9]+))*)?(?<Profile>-([A-Za-z]+[0-9]*)+(\+[A-Za-z]+[0-9]*)*)?$", RegexFlags);


        public static class Schema
        {
            public static class TreeItemTypes
            {
                public const string Content = "content";
                public const string Reference = "reference";
                public const string Intellisense = "intellisense";
                public const string FrameworkReference = "frameworkreference";
                public const string Tool = "tool";
                public const string Build = "build";
            }

            public static class TreePropertyTypes
            {
                public const string KeyValueProperty = "http://schema.nuget.org/nupkg#KeyValueProp";
            }
        }
    }
}
