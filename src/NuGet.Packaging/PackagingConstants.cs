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
        public const string ContentFolder = "content";
        public const string AnyFramework = "any";
        public const string AgnosticFramework = "agnostic";

        public const string TargetFrameworkPropertyKey = "targetframework";
    }
}
