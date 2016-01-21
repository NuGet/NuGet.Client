using System;

namespace NuGet.Packaging
{
    [Flags]
    public enum PackageSaveMode
    {
        None = 0,
        Nuspec = 1,
        Nupkg = 2,
        Files = 4,
        Default = Nuspec | Nupkg | Files,
    }
}
