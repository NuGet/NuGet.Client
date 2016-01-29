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

        /// <summary>
        /// Default package save mode for v2 (packages.config)-style restore.
        /// This includes <see cref="Files"/> and <see cref="Nupkg"/>.
        /// </summary>
        Defaultv2 = Nupkg | Files,

        /// <summary>
        /// Default package save mode for v3 (project.json)-style restore.
        /// This includes <see cref="Files"/>, <see cref="Nuspec"/>, and <see cref="Nupkg"/>.
        /// </summary>
        Defaultv3 = Nuspec | Nupkg | Files,
    }
}
