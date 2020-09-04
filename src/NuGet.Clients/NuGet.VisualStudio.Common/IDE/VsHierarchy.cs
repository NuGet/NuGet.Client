// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
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
        public static async Task<VsHierarchy> FromDteProjectAsync(EnvDTE.Project project)
        {
            Assumes.Present(project);
            return new VsHierarchy(await ToVsHierarchyAsync(project));
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
        /// <returns> Found project ID, or Guid.Empty if not found. </returns>
        public async Task<Guid> GetProjectIdAsync()
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var result = _hierarchy.GetGuidProperty(
                VSConstants.VSITEMID_ROOT,
                (int)__VSHPROPID.VSHPROPID_ProjectIDGuid,
                out var projectId);

            if (result != VSConstants.S_OK)
            {
                projectId = Guid.Empty;
            }

            return projectId;
        }

        /// <summary> Find whether project type is supported. </summary>
        /// <param name="projectTypeGuid"> Project type ID. </param>
        /// <returns> True if project type is supported. </returns>
        public async Task<bool> IsSupportedAsync(string projectTypeGuid)
        {
            if (await IsProjectCapabilityCompliantAsync())
            {
                return true;
            }

            return !string.IsNullOrEmpty(projectTypeGuid)
                && SupportedProjectTypes.IsSupported(projectTypeGuid)
                && !await HasUnsupportedProjectCapabilityAsync();
        }

        /// <summary> Finds whether project capability matching. </summary>
        /// <returns> True if project matches required capabilities. </returns>
        public async Task<bool> IsProjectCapabilityCompliantAsync()
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            return _hierarchy.IsCapabilityMatch("AssemblyReferences + DeclaredSourceItems + UserSourceItems");
        }

        /// <summary> Finds whether project has unsupported project capability. </summary>
        /// <returns> True if project has unsupported project capability. </returns>
        public async Task<bool> HasUnsupportedProjectCapabilityAsync()
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            return UnsupportedProjectCapabilities.Any(c => _hierarchy.IsCapabilityMatch(c));
        }

        /// <summary> Get project type GUIDs. </summary>
        /// <param name="defaultType"> Default project type. </param>
        /// <returns> Array of project type GUIDs. </returns>
        public async Task<string[]> GetProjectTypeGuidsAsync(string defaultType = "")
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
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
        public async Task<bool> IsCPSCapabilityComplaintAsync()
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            return _hierarchy.IsCapabilityMatch("CPS | VisualC");
        }

        /// <summary> Gets <see cref="EnvDTE.Project"/> instance for this hierarchy. </summary>
        /// <returns> A <see cref="EnvDTE.Project"/> instance. </returns>
        public async Task<EnvDTE.Project> GetDteProjectAsync()
        {
            // Set it to null to avoid unassigned local variable warning
            EnvDTE.Project project = null;
            object projectObject;

            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (_hierarchy.GetProperty(VSConstants.VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_ExtObject, out projectObject) >= 0)
            {
                project = (EnvDTE.Project)projectObject;
            }

            return project;
        }

        private static async Task<IVsHierarchy> ToVsHierarchyAsync(EnvDTE.Project project)
        {
            var solution = await ServiceLocator.GetInstanceAsync<IVsSolution>();

            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var hr = solution.GetProjectOfUniqueName(EnvDTEProjectInfoUtility.GetUniqueName(project), out var hierarchy);

            if (hr != VSConstants.S_OK)
            {
                Marshal.ThrowExceptionForHR(hr);
            }

            return hierarchy;
        }
    }
}
