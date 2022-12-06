// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Versioning;

namespace NuGet.PackageManagement
{
    /// <summary>
    /// An additional dependency constraint on a package id specified by a project.
    /// </summary>
    public class ProjectVersionConstraint
    {
        /// <summary>
        /// Parent project
        /// </summary>
        public string ProjectName { get; set; }

        /// <summary>
        /// Range
        /// </summary>
        public VersionRange VersionRange { get; set; } = VersionRange.All;

        /// <summary>
        /// packags.config allowedVersions value.
        /// </summary>
        public bool IsPackagesConfig { get; set; }

        /// <summary>
        /// PackageReference AutoReferenced value.
        /// </summary>
        public bool IsAutoReferenced { get; set; }

        public override string ToString()
        {
            return $"{ProjectName} {VersionRange.ToNormalizedString()}";
        }
    }
}
