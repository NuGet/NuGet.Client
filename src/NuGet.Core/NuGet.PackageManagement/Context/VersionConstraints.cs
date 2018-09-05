using System;

namespace NuGet.PackageManagement
{
    [Flags]
    public enum VersionConstraints
    {
        None = 0,
        ExactMajor = 1,
        ExactMinor = 2,
        ExactPatch = 4,
        ExactRelease = 8
    }
}
