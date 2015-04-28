using EnvDTE;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;

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
            else if (!String.IsNullOrEmpty(project.Kind))
            {
                return new[] { project.Kind };
            }
            else
            {
                return new string[0];
            }
        }
    }
}
