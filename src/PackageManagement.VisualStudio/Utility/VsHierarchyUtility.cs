// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.InteropServices;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace NuGet.PackageManagement.VisualStudio
{
    public static class VsHierarchyUtility
    {
        public static IVsHierarchy ToVsHierarchy(Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            IVsHierarchy hierarchy;

            // Get the vs solution
            IVsSolution solution = ServiceLocator.GetInstance<IVsSolution>();
            int hr = solution.GetProjectOfUniqueName(EnvDTEProjectUtility.GetUniqueName(project), out hierarchy);

            if (hr != NuGetVSConstants.S_OK)
            {
                Marshal.ThrowExceptionForHR(hr);
            }

            return hierarchy;
        }

        public static string[] GetProjectTypeGuids(Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Get the vs hierarchy as an IVsAggregatableProject to get the project type guids
            var hierarchy = ToVsHierarchy(project);
            var aggregatableProject = hierarchy as IVsAggregatableProject;
            if (aggregatableProject != null)
            {
                string projectTypeGuids;
                int hr = aggregatableProject.GetAggregateProjectTypeGuids(out projectTypeGuids);

                if (hr != NuGetVSConstants.S_OK)
                {
                    Marshal.ThrowExceptionForHR(hr);
                }

                return projectTypeGuids.Split(';');
            }
            if (!String.IsNullOrEmpty(project.Kind))
            {
                return new[] { project.Kind };
            }
            return new string[0];
        }
    }
}
