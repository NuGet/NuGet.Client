// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.VisualStudio.Shell;

namespace NuGet.VisualStudio
{
    /// <summary>
    /// Represents project names in the solution manager.
    /// </summary>
    public class ProjectNames : IEquatable<ProjectNames>
    {
        public string FullName { get; private set; }
        public string UniqueName { get; private set; }
        public string ShortName { get; private set; }
        public string CustomUniqueName { get; private set; }

        public ProjectNames(
            string fullName,
            string uniqueName,
            string shortName,
            string customUniqueName)
        {
            if (string.IsNullOrEmpty(fullName))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(fullName));
            }

            if (string.IsNullOrEmpty(uniqueName))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(uniqueName));
            }

            if (string.IsNullOrEmpty(shortName))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(shortName));
            }

            if (string.IsNullOrEmpty(customUniqueName))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(customUniqueName));
            }

            FullName = fullName;
            UniqueName = uniqueName;
            ShortName = shortName;
            CustomUniqueName = customUniqueName;
        }

        /// <summary>
        /// Factory method initializing instance of <see cref="ProjectNames"/> with values retrieved from a DTE project.
        /// </summary>
        /// <param name="dteProject">DTE project to get project names for.</param>
        /// <returns>New instance of <see cref="ProjectNames"/>.</returns>
        public static async Task<ProjectNames> FromDTEProjectAsync(EnvDTE.Project dteProject)
        {
            Assumes.Present(dteProject);
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            return new ProjectNames(
                fullName: dteProject.FullName,
                uniqueName: EnvDTEProjectInfoUtility.GetUniqueName(dteProject),
                shortName: EnvDTEProjectInfoUtility.GetName(dteProject),
                customUniqueName: await EnvDTEProjectInfoUtility.GetCustomUniqueNameAsync(dteProject));
        }

        public static ProjectNames FromFullProjectPath(string name)
        {
            return new ProjectNames(
                fullName: name,
                uniqueName: name,
                shortName: Path.GetFileNameWithoutExtension(name),
                customUniqueName: name);
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
