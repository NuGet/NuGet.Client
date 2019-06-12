// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Frameworks;

namespace NuGet.CommandLine.XPlat
{
    /// <summary>
    /// A class to simplify holding all of the information
    /// of a targetFramework and its associated packages when using list
    /// </summary>
    internal class TargetFrameworkInfo
    {
        internal NuGetFramework TargetFramework { get; }
        internal IEnumerable<PackageReferenceInfo> TopLevelPackages { get; set; }
        internal IEnumerable<PackageReferenceInfo> TransitivePackages { get; set; }

        /// <summary>
        /// A constructor that takes a framework name, and
        /// initializes the top-level and transitive package
        /// lists
        /// </summary>
        /// <param name="targetFramework">target framework</param>
        internal TargetFrameworkInfo(NuGetFramework targetFramework) : this(targetFramework, new List<PackageReferenceInfo>(), new List<PackageReferenceInfo>())
        {

        }

        /// <summary>
        /// A constructor that takes a targetFramework, a list
        /// of top-level pacakges, and a list of transitive
        /// packages
        /// </summary>
        /// <param name="targetFramework">targetFramework that we have packages for</param>
        /// <param name="topLevelPackages">Top-level packages. Shouldn't be null</param>
        /// <param name="transitivePackages">Transitive packages. Shouldn't be null</param>
        internal TargetFrameworkInfo(NuGetFramework targetFramework,
            IEnumerable<PackageReferenceInfo> topLevelPackages,
            IEnumerable<PackageReferenceInfo> transitivePackages)
        {
            TargetFramework = targetFramework ?? throw new ArgumentNullException(nameof(targetFramework));
            TopLevelPackages = topLevelPackages ?? throw new ArgumentNullException(nameof(topLevelPackages));
            TransitivePackages = transitivePackages ?? throw new ArgumentNullException(nameof(transitivePackages));
        }
    }
}
