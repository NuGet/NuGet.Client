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
    internal class PackageReferenceInfo
    {
        internal string Id { get; }
        internal string OriginalRequestedVersion { get; set; }
        internal NuGetVersion ResolvedVersion { get; set; }
        internal NuGetVersion LatestVersion { get; set; }
        internal bool AutoReference { get; set; }
        internal bool LikelyTransitive { get; set; }
        internal UpdateLevel UpdateLevel { get; set; }

        /// <summary>
        /// A constructor that takes a name of a package
        /// </summary>
        /// <param name="name">The name of the package</param>
        public PackageReferenceInfo(string name)
        {
            Id = name ?? throw new ArgumentNullException(nameof(name));
        }
    }
}
