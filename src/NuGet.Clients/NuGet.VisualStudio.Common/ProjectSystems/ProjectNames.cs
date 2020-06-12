// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace NuGet.VisualStudio
{
    /// <summary>
    /// Represents project names in the solution manager.
    /// </summary>
    public class ProjectNames : IEquatable<ProjectNames>
    {
        /// <summary>The full path and filename of the project.</summary>
        public string FullName { get; }

        /// <summary>The relative path and filename from the solution to the project.</summary>
        /// <remarks>If the solution is in <c>c:\sln\solution.sln</c> and the project is in <c>c:\sln\project\project.csproj</c>, this value will be <c>project\project.csproj</c>.</remarks>
        public string UniqueName { get; }

        /// <summary>The project's name</summary>
        /// <remarks>Generally this is the project's filename with the extension removed.</remarks>
        public string ShortName { get; }

        /// <summary>The "human readable" unique name.</summary>
        /// <remarks>Generally, it's the unique name with the project's extension removed.</remarks>
        public string CustomUniqueName { get; }

        /// <summary>The project GUID</summary>
        public string ProjectId { get; }

        public ProjectNames(
            string fullName,
            string uniqueName,
            string shortName,
            string customUniqueName,
            string projectId)
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

            if (string.IsNullOrEmpty(projectId))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(projectId));
            }

            FullName = fullName;
            UniqueName = uniqueName;
            ShortName = shortName;
            CustomUniqueName = customUniqueName;
            ProjectId = projectId;
        }

        /// <summary>
        /// Factory method initializing instance of <see cref="ProjectNames"/> with values retrieved from a DTE project.
        /// </summary>
        /// <param name="dteProject">DTE project to get project names for.</param>
        /// <returns>New instance of <see cref="ProjectNames"/>.</returns>
        public static async Task<ProjectNames> FromDTEProjectAsync(EnvDTE.Project dteProject, IVsSolution5 vsSolution5)
        {
            Assumes.Present(dteProject);
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var fullname = dteProject.FullName;
            var uniqueName = EnvDTEProjectInfoUtility.GetUniqueName(dteProject);
            var shortName = EnvDTEProjectInfoUtility.GetName(dteProject);
            var customUniqueName = await EnvDTEProjectInfoUtility.GetCustomUniqueNameAsync(dteProject);
            var projectId = GetProjectGuid(fullname, vsSolution5);

            return new ProjectNames(
                fullName: fullname,
                uniqueName: uniqueName,
                shortName: shortName,
                customUniqueName: customUniqueName,
                projectId: projectId);
        }

        private static string GetProjectGuid(string fullname, IVsSolution5 vsSolution5)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var guid = vsSolution5.GetGuidOfProjectFile(fullname);
            return guid.ToString();
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
