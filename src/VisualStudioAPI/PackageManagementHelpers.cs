using EnvDTE;
using NuGet.PackageManagement;
using NuGet.ProjectManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.VisualStudio
{
    internal static class PackageManagementHelpers
    {
        /// <summary>
        /// Finds the NuGetProject from a DTE project
        /// </summary>
        public static NuGetProject GetProject(ISolutionManager solution, Project project)
        {
            return solution.GetNuGetProjects()
                    .Where(p => StringComparer.Ordinal.Equals(solution.GetNuGetProjectSafeName(p), project.UniqueName))
                    .SingleOrDefault();
        }
    }
}
