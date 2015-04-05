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

        /// <summary>
        /// Gets the current open solution directory, can only be called from the main UI thread.
        /// </summary>
        string SolutionDirectory { get; }

        /// <summary>
        /// Gets the name of the default <see cref="NuGetProject"/>. Default NuGetProject is the selected NuGetProject in the IDE.
        /// </summary>
        string DefaultNuGetProjectName { get; set; }

        /// <summary>
        /// Gets the default <see cref="NuGetProject"/>. Default NuGetProject is the selected NuGetProject in the IDE.
        /// </summary>
        NuGetProject DefaultNuGetProject { get; }
        /// <summary>
        /// Gets the current open solution directory, can only be called from the main UI thread.
        /// </summary>
        bool IsSolutionOpen { get; }
        INuGetProjectContext NuGetProjectContext { get; set; }

        IEnumerable<NuGetProject> GetNuGetProjects();

        /// <summary>
        /// Get the safe name of the specified <see cref="NuGetProject"/> which guarantees not to conflict with other projects.
        /// </summary>
        /// <returns>
        /// Returns the simple name if there are no conflicts. Otherwise returns the unique name.
        /// </returns>
        string GetNuGetProjectSafeName(NuGetProject nuGetProject);

        /// <summary>
        /// Gets the <see cref="NuGetProject"/> corresponding to the safe name passed in
        /// </summary>
        /// <param name="nuGetProjectSafeName">nuGetProjectSafeName is the nuGetProject's unique name if one is available or its name.</param>
        /// <returns>Returns the <see cref="NuGetProject"/> in this solution manager corresponding to the safe name passed in.</returns>
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
