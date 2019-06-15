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
        internal bool Transitive { get; set; }
        internal UpdateLevel UpdateLevel { get; set; }
        internal string PrefixString
        {
            get
            {
                if (InAllTargetFrameworks)
                {
                    return AutoReference ? "  A" : (Transitive ? "  T" : "  D");
                }
                else
                {
                    return AutoReference ? "  a" : (Transitive ? "  t" : "  d");
                }
            }
        }

        internal bool InAllTargetFrameworks { get; set; }

        public override bool Equals(object obj)
        {
            PackageReferenceInfo pr2 = obj as PackageReferenceInfo;
            if (pr2 != null)
            {
                return (Id.Equals(pr2.Id)
                    && ResolvedVersion == pr2.ResolvedVersion
                    && OriginalRequestedVersion == pr2.OriginalRequestedVersion
                    && PrefixString.ToLower() == pr2.PrefixString.ToLower()
                    );
            }

            return false;
        }
        public override int GetHashCode()
        {
            unchecked // Overflow is fine, just wrap
            {
                int hash = 17;
                hash = hash * 23 + Id.GetHashCode();
                hash = ResolvedVersion == null ? hash : hash * 23 + ResolvedVersion.GetHashCode();
                hash = OriginalRequestedVersion == null ? hash : hash * 23 + OriginalRequestedVersion.GetHashCode();
                hash = PrefixString == null ? hash : hash * 23 + PrefixString.GetHashCode();
                return hash;
            }
        }

        /// <summary>
        /// A constructor that takes a id of a package
        /// </summary>
        /// <param name="id">The id of the package</param>
        public PackageReferenceInfo(string id)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
        }
    }
}
