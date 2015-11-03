// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.ProjectManagement;

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
        /// Event raised after user actions are executed.
        /// </summary>
        event EventHandler<ActionsExecutedEventArgs> ActionsExecuted;

        /// <summary>
        /// Gets the current open solution directory, can only be called from the main UI thread.
        /// </summary>
        string SolutionDirectory { get; }

        /// <summary>
        /// Gets the name of the default <see cref="NuGetProject" />. Default NuGetProject is the selected NuGetProject
        /// in the IDE.
        /// </summary>
        string DefaultNuGetProjectName { get; set; }

        /// <summary>
        /// Gets the default <see cref="NuGetProject" />. Default NuGetProject is the selected NuGetProject in the IDE.
        /// </summary>
        NuGetProject DefaultNuGetProject { get; }

        /// <summary>
        /// Returns true if the solution is open
        /// </summary>
        bool IsSolutionOpen { get; }

        /// <summary>
        /// Returns true if the solution is available to manage nuget packages.
        /// That is, if the solution is open and a solution file is available.
        /// For solutions with only BuildIntegratedProject(s), and a globalPackagesFolder which is
        /// not a relative path, it will return true, even if the solution file is not available.
        /// </summary>
        bool IsSolutionAvailable { get; }

        INuGetProjectContext NuGetProjectContext { get; set; }

        IEnumerable<NuGetProject> GetNuGetProjects();

        /// <summary>
        /// Get the safe name of the specified <see cref="NuGetProject" /> which guarantees not to conflict with other
        /// projects.
        /// </summary>
        /// <returns>
        /// Returns the simple name if there are no conflicts. Otherwise returns the unique name.
        /// </returns>
        string GetNuGetProjectSafeName(NuGetProject nuGetProject);

        /// <summary>
        /// Gets the <see cref="NuGetProject" /> corresponding to the safe name passed in
        /// </summary>
        /// <param name="nuGetProjectSafeName">
        /// nuGetProjectSafeName is the nuGetProject's unique name if one is
        /// available or its name.
        /// </param>
        /// <returns>
        /// Returns the <see cref="NuGetProject" /> in this solution manager corresponding to the safe name
        /// passed in.
        /// </returns>
        NuGetProject GetNuGetProject(string nuGetProjectSafeName);

        /// <summary>
        /// Fires ActionsExecuted event.
        /// </summary>
        /// <param name="actions"></param>
        void OnActionsExecuted(IEnumerable<ResolvedAction> actions);
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