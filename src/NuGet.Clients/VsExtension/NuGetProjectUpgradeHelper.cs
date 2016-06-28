// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.PackageManagement;
using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectManagement;

namespace NuGetVSExtension
{
    internal static class NuGetProjectUpgradeHelper
    {
        private static readonly HashSet<string> UpgradeableProjectTypes =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                NuGetVSConstants.CsharpProjectTypeGuid,
                NuGetVSConstants.VbProjectTypeGuid
            };

        private static readonly HashSet<string> UnsupportedUpgradeProjectTypes =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                NuGetVSConstants.WindowsStoreProjectTypeGuid,
                NuGetVSConstants.WindowsPhoneSilverlightProjectTypeGuid,
                NuGetVSConstants.WindowsPhone81ProjectTypeGuid,
                NuGetVSConstants.SilverlightProjectTypeGuid,
                NuGetVSConstants.LightSwitchProjectTypeGuid,
                NuGetVSConstants.LightSwitchCsharpProjectTypeGuid,
                NuGetVSConstants.LightSwitchLsxtProjectTypeGuid
            };

        internal static bool IsNuGetProjectUpgradeable(NuGetProject nuGetProject, Project envDTEProject = null)
        {
            if (nuGetProject == null && envDTEProject == null)
            {
                return false;
            }

            if (nuGetProject == null)
            {
                var solutionManager = ServiceLocator.GetInstance<ISolutionManager>();
                nuGetProject = solutionManager.GetNuGetProject(EnvDTEProjectUtility.GetCustomUniqueName(envDTEProject));
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
                    msBuildNuGetProject.MSBuildNuGetProjectSystem as VSMSBuildNuGetProjectSystem;
                if (vsmsBuildNuGetProjectSystem == null)
                {
                    return false;
                }
                envDTEProject = vsmsBuildNuGetProjectSystem.EnvDTEProject;
            }

            if (!EnvDTEProjectUtility.IsSupported(envDTEProject))
            {
                return false;
            }

            // Project is supported language, and not an unsupported type
            return UpgradeableProjectTypes.Contains(envDTEProject.Kind) &&
                   VsHierarchyUtility.GetProjectTypeGuids(envDTEProject)
                       .All(projectTypeGuid => !UnsupportedUpgradeProjectTypes.Contains(projectTypeGuid));
        }

        internal static bool IsPackagesConfigSelected(IVsMonitorSelection vsMonitorSelection)
        {
            var selectedFileName = GetSelectedFileName(vsMonitorSelection);
            return !string.IsNullOrEmpty(selectedFileName) && Path.GetFileName(selectedFileName).ToLower() == "packages.config";
        }

        internal static string GetSelectedFileName(IVsMonitorSelection vsMonitorSelection)
        {
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