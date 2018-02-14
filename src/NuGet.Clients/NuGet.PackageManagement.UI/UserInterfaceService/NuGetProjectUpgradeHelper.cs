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
using NuGet.PackageManagement;
using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectManagement;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.UI
{
    public static class NuGetProjectUpgradeHelper
    {
        private static readonly HashSet<string> UpgradeableProjectTypes =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                VsProjectTypes.CsharpProjectTypeGuid,
                VsProjectTypes.VbProjectTypeGuid
            };

        public static async Task<bool> IsNuGetProjectUpgradeableAsync(NuGetProject nuGetProject, Project envDTEProject = null)
        {
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

            // Project is supported language, and not an unsupported type
            return UpgradeableProjectTypes.Contains(envDTEProject.Kind) &&
                   VsHierarchyUtility.GetProjectTypeGuids(envDTEProject)
                       .All(projectTypeGuid => !SupportedProjectTypes.IsUnsupported(projectTypeGuid));
        }

        public static bool IsPackagesConfigSelected(IVsMonitorSelection vsMonitorSelection)
        {
            var selectedFileName = GetSelectedFileName(vsMonitorSelection);
            return !string.IsNullOrEmpty(selectedFileName) && Path.GetFileName(selectedFileName).ToLower() == "packages.config";
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