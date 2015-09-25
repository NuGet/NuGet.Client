// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.VisualStudio.Shell;
using EnvDTEProject = EnvDTE.Project;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Represents a project name in the solution manager.
    /// </summary>
    internal class EnvDTEProjectName : IEquatable<EnvDTEProjectName>
    {
        public EnvDTEProjectName(EnvDTEProject envDTEProject)
        {
            Debug.Assert(ThreadHelper.CheckAccess());

            FullName = envDTEProject.FullName;
            UniqueName = EnvDTEProjectUtility.GetUniqueName(envDTEProject);
            ShortName = EnvDTEProjectUtility.GetName(envDTEProject);
            CustomUniqueName = EnvDTEProjectUtility.GetCustomUniqueName(envDTEProject);
        }

        public string FullName { get; private set; }
        public string UniqueName { get; private set; }
        public string ShortName { get; private set; }
        public string CustomUniqueName { get; private set; }

        /// <summary>
        /// Two projects are equal if they share the same FullNames.
        /// </summary>
        public bool Equals(EnvDTEProjectName other)
        {
            return other.FullName.Equals(other.FullName, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            return FullName.GetHashCode();
        }

        public override string ToString()
        {
            return UniqueName;
        }
    }
}
