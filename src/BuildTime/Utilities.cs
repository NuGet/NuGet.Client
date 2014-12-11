using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.BuildTime
{
    public static class Utilities
    {
        /// <summary>
        /// Takes a NuGet framework folder name and returns the .NET FrameworkName
        /// </summary>
        public static FrameworkName GetFrameworkName(string nugetFramework)
        {
            // TODO: Call a non-portable version of Frameworks to do this work instead.

            FrameworkName framework = null;

            NuGetFramework nf = NuGetFramework.Parse(nugetFramework);

            if (nf.IsSpecificFramework)
            {
                framework = new FrameworkName(nf.ToString());
            }

            return framework;
        }
    }
}
