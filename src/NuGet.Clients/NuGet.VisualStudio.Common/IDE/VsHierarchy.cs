// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace NuGet.VisualStudio
{
    /// <summary> VS hierarchy helper. </summary>
    public sealed class VsHierarchy
    {
        private static readonly string[] UnsupportedProjectCapabilities = new string[]
        {
            "SharedAssetsProject", // This is true for shared projects in universal apps
        };

        private readonly IVsHierarchy _hierarchy;

        private VsHierarchy(IVsHierarchy hierarchy)
        {
            _hierarchy = hierarchy;
        }

        /// <summary> Create a new instance of <see cref="VisualStudio.VsHierarchy"/> from a DTE project object. </summary>
        /// <param name="project"> A DTE project. </param>
        /// <returns> Instance of <see cref="VisualStudio.VsHierarchy"/> wrapping <paramref name="project"/>. </returns>
        public static VsHierarchy FromDteProject(EnvDTE.Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Assumes.Present(project);
            return new VsHierarchy(ToVsHierarchy(project));
        }

        /// <summary> Create a new instance of <see cref="VisualStudio.VsHierarchy"/> from a <see cref="IVsHierarchy"/> object. </summary>
        /// <param name="project"> A <see cref="IVsHierarchy"/> project. </param>
        /// <returns> Instance of <see cref="VisualStudio.VsHierarchy"/> wrapping <paramref name="project"/>. </returns>
        public static VsHierarchy FromVsHierarchy(IVsHierarchy project)
        {
            Assumes.Present(project);
            return new VsHierarchy(project);
        }

        /// <summary> Underlying <see cref="IVsHierarchy"/> object. </summary>
        public IVsHierarchy Ptr => _hierarchy;

        /// <summary> Try getting a project ID. </summary>
        /// <param name="projectId"> Found Project ID. </param>
        /// <returns> True if project ID is found. </returns>
        public bool TryGetProjectId(out Guid projectId)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var result = _hierarchy.GetGuidProperty(
                VSConstants.VSITEMID_ROOT,
                (int)__VSHPROPID.VSHPROPID_ProjectIDGuid,
                out projectId);

            return result == VSConstants.S_OK;
        }

        /// <summary> Find whether project type is supported. </summary>
        /// <param name="projectTypeGuid"> Project type ID. </param>
        /// <returns> True if project type is supported. </returns>
        public bool IsSupported(string projectTypeGuid)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (IsProjectCapabilityCompliant())
            {
                return true;
            }

            return !string.IsNullOrEmpty(projectTypeGuid) && SupportedProjectTypes.IsSupported(projectTypeGuid) && !HasUnsupportedProjectCapability();
        }

        /// <summary> Finds whether project capability matching. </summary>
        /// <returns> True if project matches required capabilities. </returns>
        public bool IsProjectCapabilityCompliant()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            return _hierarchy.IsCapabilityMatch("AssemblyReferences + DeclaredSourceItems + UserSourceItems");
        }

        /// <summary> Finds whether project has unsupported project capability. </summary>
        /// <returns> True if project has unsupported project capability. </returns>
        public bool HasUnsupportedProjectCapability()
        {
            return UnsupportedProjectCapabilities.Any(c => _hierarchy.IsCapabilityMatch(c));
        }

        /// <summary> Get project type GUIDs. </summary>
        /// <param name="defaultType"> Default project type. </param>
        /// <returns> Array of project type GUIDs. </returns>
        public string[] GetProjectTypeGuids(string defaultType = "")
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var aggregatableProject = _hierarchy as IVsAggregatableProject;
            if (aggregatableProject != null)
            {
                string projectTypeGuids;
                var hr = aggregatableProject.GetAggregateProjectTypeGuids(out projectTypeGuids);
                ErrorHandler.ThrowOnFailure(hr);

                return projectTypeGuids.Split(';');
            }

            if (!string.IsNullOrEmpty(defaultType))
            {
                return new[] { defaultType };
            }

            return Array.Empty<string>();
        }

        /// <summary> Check for CPS capability in IVsHierarchy. </summary>
        /// <returns> True if project is CPS-based. </returns>
        /// <remarks> All CPS projects will have CPS capability except VisualC projects. So checking for VisualC explicitly with a OR flag. </remarks>
        public bool IsCPSCapabilityComplaint()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            return _hierarchy.IsCapabilityMatch("CPS | VisualC");
        }

        /// <summary> Gets <see cref="EnvDTE.Project"/> instance for this hierarchy. </summary>
        /// <returns> A <see cref="EnvDTE.Project"/> instance. </returns>
        public EnvDTE.Project GetDteProject()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Set it to null to avoid unassigned local variable warning
            EnvDTE.Project project = null;
            object projectObject;

            if (_hierarchy.GetProperty(VSConstants.VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_ExtObject, out projectObject) >= 0)
            {
                project = (EnvDTE.Project)projectObject;
            }

            return project;
        }

        private static IVsHierarchy ToVsHierarchy(EnvDTE.Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            IVsHierarchy hierarchy;

            // Get the vs solution
            var solution = ServiceLocator.GetInstance<IVsSolution>();
            var hr = solution.GetProjectOfUniqueName(EnvDTEProjectInfoUtility.GetUniqueName(project), out hierarchy);

            if (hr != VSConstants.S_OK)
            {
                Marshal.ThrowExceptionForHR(hr);
            }

            return hierarchy;
        }
    }
}
