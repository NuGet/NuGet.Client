using NuGet.Versioning;

namespace NuGet.CommandLine.XPlat.Utility
{
    /// <summary>
    /// A class to simplify holding all of the information
    /// about a package reference when using list
    /// </summary>
    class InstalledPackageReference
    {
        public string Name { get; }
        public VersionRange RequestedVersion { get; set; }
        public string PrintableRequestedVersion { get; set; }
        public NuGetVersion ResolvedVersion { get; set; }
        public NuGetVersion SuggestedVersion { get; set; }
        public bool AutoReference { get; set; }

        /// <summary>
        /// A constructor that takes a name of a package
        /// </summary>
        /// <param name="name">The name of the package</param>
        public InstalledPackageReference(string name)
        {
            Name = name;
        }
    }
}
