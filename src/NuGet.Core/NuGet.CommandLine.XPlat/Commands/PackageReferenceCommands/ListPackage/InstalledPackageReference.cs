// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Versioning;

namespace NuGet.CommandLine.XPlat
{
    internal enum UpdateLevel { NoUpdate, Major, Minor, Patch };

    /// <summary>
    /// A class to simplify holding all of the information
    /// about a package reference when using list
    /// </summary>
    internal class InstalledPackageReference
    {
        internal string Name { get; }
        internal string OriginalRequestedVersion { get; set; }
        internal NuGetVersion ResolvedVersion { get; set; }
        internal NuGetVersion LatestVersion { get; set; }
        internal bool AutoReference { get; set; }
        internal UpdateLevel UpdateLevel { get; set; }

        /// <summary>
        /// A constructor that takes a name of a package
        /// </summary>
        /// <param name="name">The name of the package</param>
        public InstalledPackageReference(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
    }
}
