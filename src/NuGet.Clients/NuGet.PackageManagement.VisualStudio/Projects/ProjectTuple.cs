// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Frameworks;
using NuGet.PackageManagement.VisualStudio.Utility;

namespace NuGet.PackageManagement.VisualStudio
{
    public class ProjectTuple : IEquatable<ProjectTuple>
    {
        public NuGetFramework TargetFramework { get; set; }
        public Dictionary<string, ProjectInstalledPackage> Packages { get; set; }

        public bool Equals(ProjectTuple other)
        {
            if (other == null)
            {
                return false;
            }

            return TargetFramework.Equals(other.TargetFramework) && Packages.Equals(other.Packages);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ProjectTuple);
        }

        public override int GetHashCode()
        {
            return TargetFramework.GetHashCode() + 37 * Packages.GetHashCode();
        }
    }
}
