using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Tests.Foundation
{
    internal static partial class VisualStudioVersionInfo
    {
        public const string MajorVersion = "15";
        public const string MinorVersion = "0";
        public const string ProductVersion = MajorVersion + "." + MinorVersion;
        public const string VSAssemblyVersion = MajorVersion + "." + MinorVersion + ".0.0";
    }

}
