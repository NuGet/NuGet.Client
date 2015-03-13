using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.ProjectManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackageManagement
{
    public interface ISolutionManager
    {
        event EventHandler SolutionOpening;
        event EventHandler SolutionOpened;
        event EventHandler SolutionClosing;
        event EventHandler SolutionClosed;
        event EventHandler<NuGetProjectEventArgs> NuGetProjectAdded;
        event EventHandler<NuGetProjectEventArgs> NuGetProjectRemoved;
        event EventHandler<NuGetProjectEventArgs> NuGetProjectRenamed;

        string SolutionDirectory { get; }
        string DefaultNuGetProjectName { get; set; }
        NuGetProject DefaultNuGetProject { get; }
        bool IsSolutionOpen { get; }
        INuGetProjectContext NuGetProjectContext { get; set; }

        IEnumerable<NuGetProject> GetNuGetProjects();

        /// <summary>
        /// Get the safe name of the specified project which guarantees not to conflict with other projects.
        /// </summary>
        /// <remarks>
        /// It tries to return simple name if possible. Otherwise it returns the unique name.
        /// </remarks>
        string GetNuGetProjectSafeName(NuGetProject nuGetProject);

        /// <summary>
        /// Gets the NuGetProject corresponding to the safe name passed in
        /// </summary>
        /// <param name="nuGetProjectSafeName"></param>
        /// <returns></returns>
        NuGetProject GetNuGetProject(string nuGetProjectSafeName);
    }

    public class NuGetProjectEventArgs : EventArgs
    {
        public NuGetProjectEventArgs(NuGetProject nuGetProject)
        {
            NuGetProject = nuGetProject;
        }

        public NuGetProject NuGetProject { get; private set; }
    }
}
