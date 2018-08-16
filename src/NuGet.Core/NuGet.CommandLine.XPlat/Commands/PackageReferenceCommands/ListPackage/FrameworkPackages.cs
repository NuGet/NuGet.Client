using System;
using System.Collections.Generic;

namespace NuGet.CommandLine.XPlat.Utility
{
    /// <summary>
    /// A class to simplify holding all of the information
    /// of a framework packages when using list
    /// </summary>
    class FrameworkPackages
    {
        public string Framework { get; }
        public IEnumerable<InstalledPackageReference> TopLevelPackages { get; set; }
        public IEnumerable<InstalledPackageReference> TransitivePacakges { get; set; }

        /// <summary>
        /// A constructor that takes a framework name, and
        /// initializes the top-level and transitive package
        /// lists
        /// </summary>
        /// <param name="framework">Framework name that we have pacakges for</param>
        public FrameworkPackages(string framework)
        {
            Framework = framework;
            TopLevelPackages = new List<InstalledPackageReference>();
            TransitivePacakges = new List<InstalledPackageReference>();
        }

        /// <summary>
        /// A constructor that takes a framework name, a list
        /// of top-level pacakges, and a list of transitive
        /// packages
        /// </summary>
        /// <param name="framework">Framework name that we have pacakges for</param>
        /// <param name="topLevelPackages">Top-level packages. Shouldn't be null</param>
        /// <param name="transitivePackages">Transitive packages. Shouldn't be null</param>
        public FrameworkPackages(string framework,
            IEnumerable<InstalledPackageReference> topLevelPackages,
            IEnumerable<InstalledPackageReference> transitivePackages)
        {
            Framework = framework;
            TopLevelPackages = topLevelPackages ?? throw new ArgumentNullException("topLevelPackages");
            TransitivePacakges = transitivePackages ?? throw new ArgumentNullException("transitivePackages");
        }
    }
}
