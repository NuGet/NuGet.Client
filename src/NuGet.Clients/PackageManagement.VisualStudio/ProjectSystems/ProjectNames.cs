// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.VisualStudio.Shell;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Represents a project name in the solution manager.
    /// </summary>
    public class ProjectNames : IEquatable<ProjectNames>
    {
        public string FullName { get; private set; }
        public string UniqueName { get; private set; }
        public string ShortName { get; private set; }
        public string CustomUniqueName { get; private set; }

        public static ProjectNames FromDTEProject(EnvDTE.Project dteProject)
        {
            if (dteProject == null)
            {
                throw new ArgumentNullException(nameof(dteProject));
            }

            Debug.Assert(ThreadHelper.CheckAccess());

            return new ProjectNames
            {
                FullName = dteProject.FullName,
                UniqueName = EnvDTEProjectUtility.GetUniqueName(dteProject),
                ShortName = EnvDTEProjectUtility.GetName(dteProject),
                CustomUniqueName = EnvDTEProjectUtility.GetCustomUniqueName(dteProject)
            };
        }

        /// <summary>
        /// Two projects are equal if they share the same FullNames.
        /// </summary>
        public bool Equals(ProjectNames other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return StringComparer.OrdinalIgnoreCase.Equals(FullName, other.FullName);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ProjectNames);
        }

        public override int GetHashCode()
        {
            return FullName != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(FullName) : 0;
        }

        public static bool operator ==(ProjectNames left, ProjectNames right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(ProjectNames left, ProjectNames right)
        {
            return !Equals(left, right);
        }

        public override string ToString()
        {
            return UniqueName;
        }
    }
}
