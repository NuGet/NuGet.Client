// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.VisualStudio.Common;

namespace NuGet.VisualStudio
{
    public static class EnvDteProjectExtensions
    {
        public static IVsHierarchy ToVsHierarchy(this EnvDTE.Project project)
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

        public static string[] GetProjectTypeGuids(this EnvDTE.Project project)
        {
            Verify.ArgumentIsNotNull(project, nameof(project));

            ThreadHelper.ThrowIfNotOnUIThread();

            // Get the vs hierarchy as an IVsAggregatableProject to get the project type guids
            var hierarchy = ToVsHierarchy(project);
            var projectTypeGuids = VsHierarchyUtility.GetProjectTypeGuids(hierarchy, project.Kind);

            return projectTypeGuids;
        }
    }
}
