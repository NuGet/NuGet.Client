// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Frameworks;

namespace NuGet.CommandLine.XPlat
{
    /// <summary>
    /// A class to simplify holding all of the information
    /// of a framework packages when using list
    /// </summary>
    internal class TargetFrameworkInfo
    {
        public NuGetFramework TargetFramework { get; }
        public IEnumerable<PackageReferenceInfo> TopLevelPackages { get; set; }
        public IEnumerable<PackageReferenceInfo> TransitivePackages { get; set; }

        /// <summary>
        /// A constructor that takes a framework name, and
        /// initializes the top-level and transitive package
        /// lists
        /// </summary>
        /// <param name="targetFramework">target framework</param>
        public TargetFrameworkInfo(NuGetFramework targetFramework) : this(targetFramework, new List<PackageReferenceInfo>(), new List<PackageReferenceInfo>())
        {

        }

        /// <summary>
        /// A constructor that takes a framework name, a list
        /// of top-level pacakges, and a list of transitive
        /// packages
        /// </summary>
        /// <param name="targetFramework">Framework name that we have pacakges for</param>
        /// <param name="topLevelPackages">Top-level packages. Shouldn't be null</param>
        /// <param name="transitivePackages">Transitive packages. Shouldn't be null</param>
        public TargetFrameworkInfo(NuGetFramework targetFramework,
            IEnumerable<PackageReferenceInfo> topLevelPackages,
            IEnumerable<PackageReferenceInfo> transitivePackages)
        {
            TargetFramework = targetFramework ?? throw new ArgumentNullException(nameof(targetFramework));
            TopLevelPackages = topLevelPackages ?? throw new ArgumentNullException(nameof(topLevelPackages));
            TransitivePackages = transitivePackages ?? throw new ArgumentNullException(nameof(transitivePackages));
        }
    }
}
