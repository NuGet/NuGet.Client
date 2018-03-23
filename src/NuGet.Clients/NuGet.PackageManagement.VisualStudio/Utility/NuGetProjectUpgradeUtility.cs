// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.ProjectManagement;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.VisualStudio
{
    public class NuGetProjectUpgradeUtility
    {
        private static readonly HashSet<string> UpgradeableProjectTypes =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                VsProjectTypes.CsharpProjectTypeGuid,
                VsProjectTypes.VbProjectTypeGuid,
                VsProjectTypes.FsharpProjectTypeGuid
            };

        private static readonly HashSet<string> UnupgradeableProjectTypes =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                VsProjectTypes.CppProjectTypeGuid,
                VsProjectTypes.WebApplicationProjectTypeGuid,
                VsProjectTypes.WebSiteProjectTypeGuid
            };

        public static async Task<bool> IsNuGetProjectUpgradeableAsync(NuGetProject nuGetProject, Project envDTEProject = null)
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (nuGetProject == null && envDTEProject == null)
            {
                return false;
            }

            if (nuGetProject == null)
            {
                var solutionManager = ServiceLocator.GetInstance<IVsSolutionManager>();

                var projectSafeName = await EnvDTEProjectInfoUtility.GetCustomUniqueNameAsync(envDTEProject);
                nuGetProject = await solutionManager.GetNuGetProjectAsync(projectSafeName);

                if (nuGetProject == null)
                {
                    return false;
                }
            }

            if (!nuGetProject.ProjectServices.Capabilities.SupportsPackageReferences)
            {
                return false;
            }

            var msBuildNuGetProject = nuGetProject as MSBuildNuGetProject;
            if (msBuildNuGetProject == null || !msBuildNuGetProject.PackagesConfigNuGetProject.PackagesConfigExists())
            {
                return false;
            }

            if (envDTEProject == null)
            {
                var vsmsBuildNuGetProjectSystem =
                    msBuildNuGetProject.ProjectSystem as VsMSBuildProjectSystem;
                if (vsmsBuildNuGetProjectSystem == null)
                {
                    return false;
                }
                envDTEProject = vsmsBuildNuGetProjectSystem.VsProjectAdapter.Project;
            }

            if (!EnvDTEProjectUtility.IsSupported(envDTEProject))
            {
                return false;
            }
            var projectGuids = VsHierarchyUtility.GetProjectTypeGuids(envDTEProject);

            if (projectGuids.Any(t => UnupgradeableProjectTypes.Contains(t)))
            {
                return false;
            }

            // Project is supported language, and not an unsupported type
            return UpgradeableProjectTypes.Contains(envDTEProject.Kind) &&
                   projectGuids.All(projectTypeGuid => !SupportedProjectTypes.IsUnsupported(projectTypeGuid));
        }

        public static bool IsPackagesConfigSelected(IVsMonitorSelection vsMonitorSelection)
        {
            var selectedFileName = GetSelectedFileName(vsMonitorSelection);
            return !string.IsNullOrEmpty(selectedFileName) && Path.GetFileName(selectedFileName).Equals("packages.config", StringComparison.OrdinalIgnoreCase);
        }

        public static string GetSelectedFileName(IVsMonitorSelection vsMonitorSelection)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (vsMonitorSelection == null)
            {
                return string.Empty;
            }

            var hHierarchy = IntPtr.Zero;
            var hContainer = IntPtr.Zero;
            try
            {
                IVsMultiItemSelect multiItemSelect;
                uint itemId;
                if (vsMonitorSelection.GetCurrentSelection(out hHierarchy, out itemId, out multiItemSelect, out hContainer) != 0)
                {
                    return string.Empty;
                }
                if (itemId >= VSConstants.VSITEMID_SELECTION)
                {
                    return string.Empty;
                }
                var hierarchy = Marshal.GetUniqueObjectForIUnknown(hHierarchy) as IVsHierarchy;
                if (hierarchy == null)
                {
                    return string.Empty;
                }
                string fileName;
                hierarchy.GetCanonicalName(itemId, out fileName);
                return fileName;
            }
            finally
            {
                if (hHierarchy != IntPtr.Zero)
                {
                    Marshal.Release(hHierarchy);
                }
                if (hContainer != IntPtr.Zero)
                {
                    Marshal.Release(hContainer);
                }
            }
        }
    }
}
