using System;
using System.Diagnostics;

namespace NuGet.LibraryModel
{
    [Flags]
    public enum LibraryIncludeFlags : ushort
    {
        None = 0,
        Runtime = 1 << 0,
        Compile = 1 << 1,
        Build = 1 << 2,
        Native = 1 << 3,
        ContentFiles = 1 << 4,
        Analyzers = 1 << 5,
        All = 0xFFFF
    }
}
