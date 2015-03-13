using System;

namespace NuGet.PackageManagement
{
    public class PackageAlreadyInstalledException : Exception
    {
        public PackageAlreadyInstalledException(string message) : base(message)
        {

        }
    }

    public class NuGetVersionNotSatisfiedException : Exception
    {
        public NuGetVersionNotSatisfiedException(string message)
            : base(message)
        {
        }
    }
}
