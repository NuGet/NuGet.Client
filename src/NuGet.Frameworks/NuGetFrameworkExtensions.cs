using System;
using NuGet.Frameworks;

namespace NuGet.Frameworks
{
    public static class NuGetFrameworkExtensions
    {
        public static bool IsDesktop(this NuGetFramework framework)
        {
            return framework.DotNetFrameworkName.StartsWith(".NETFramework", StringComparison.OrdinalIgnoreCase);
        }
    }
}