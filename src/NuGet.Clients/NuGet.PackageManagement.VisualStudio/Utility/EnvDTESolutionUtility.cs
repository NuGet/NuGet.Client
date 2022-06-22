// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.VisualStudio
{
    public static class EnvDTESolutionUtility
    {
        public static async Task<IEnumerable<IVsHierarchy>> GetAllProjectsAsync(IVsSolution vsSolution)
        {
            Assumes.NotNull(vsSolution);
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (!IsSolutionOpenFromVSSolution(vsSolution))
            {
                return Enumerable.Empty<IVsHierarchy>();
            }

            var compatibleProjectHierarchies = new List<IVsHierarchy>();

            var hr = vsSolution.GetProjectEnum((uint)__VSENUMPROJFLAGS.EPF_ALLPROJECTS, Guid.Empty, out IEnumHierarchies ppenum);
            // EPF_LOADEDINSOLUTION instead maybe?
            ErrorHandler.ThrowOnFailure(hr);

            IVsHierarchy[] hierarchies = new IVsHierarchy[1];
            while ((ppenum.Next((uint)hierarchies.Length, hierarchies, out uint fetched) == VSConstants.S_OK) && (fetched == (uint)hierarchies.Length))
            {
                var hierarchy = hierarchies[0];
                if (VsHierarchyUtility.IsNuGetSupported(hierarchy))
                {
                    compatibleProjectHierarchies.Add(hierarchy);
                }
            }

            return compatibleProjectHierarchies;
        }

        private static object GetVSSolutionProperty(IVsSolution vsSolution, int propId)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            object value;
            var hr = vsSolution.GetProperty(propId, out value);

            ErrorHandler.ThrowOnFailure(hr);

            return value;
        }

        private static bool IsSolutionOpenFromVSSolution(IVsSolution vsSolution)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return (bool)GetVSSolutionProperty(vsSolution, (int)__VSPROPID.VSPROPID_IsSolutionOpen);
        }

        public static async Task<IEnumerable<EnvDTE.Project>> GetAllEnvDTEProjectsAsync(EnvDTE.DTE dte)
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var envDTESolution = dte.Solution;
            if (envDTESolution == null
                || !envDTESolution.IsOpen)
            {
                return Enumerable.Empty<EnvDTE.Project>();
            }

            var envDTEProjects = new Stack<EnvDTE.Project>();
            foreach (EnvDTE.Project envDTEProject in envDTESolution.Projects)
            {
                if (!EnvDTEProjectUtility.IsExplicitlyUnsupported(envDTEProject))
                {
                    envDTEProjects.Push(envDTEProject);
                }
            }

            var resultantEnvDTEProjects = new List<EnvDTE.Project>();
            while (envDTEProjects.Any())
            {
                var envDTEProject = envDTEProjects.Pop();

                if (await EnvDTEProjectUtility.IsSupportedAsync(envDTEProject))
                {
                    resultantEnvDTEProjects.Add(envDTEProject);
                }
                else if (EnvDTEProjectUtility.IsExplicitlyUnsupported(envDTEProject))
                {
                    // do not drill down further if this project is explicitly unsupported, e.g. LightSwitch projects
                    continue;
                }

                EnvDTE.ProjectItems envDTEProjectItems = null;
                try
                {
                    // bug 1138: Oracle Database Project doesn't implement the ProjectItems property
                    envDTEProjectItems = envDTEProject.ProjectItems;
                }
                catch (NotImplementedException)
                {
                    continue;
                }

                // ProjectItems property can be null if the project is unloaded
                if (envDTEProjectItems != null)
                {
                    foreach (EnvDTE.ProjectItem envDTEProjectItem in envDTEProjectItems)
                    {
                        try
                        {
                            if (envDTEProjectItem.SubProject != null)
                            {
                                envDTEProjects.Push(envDTEProjectItem.SubProject);
                            }
                        }
                        catch (NotImplementedException)
                        {
                            // Some project system don't implement the SubProject property,
                            // just ignore those
                        }
                    }
                }
            }

            return resultantEnvDTEProjects;
        }
    }
}
