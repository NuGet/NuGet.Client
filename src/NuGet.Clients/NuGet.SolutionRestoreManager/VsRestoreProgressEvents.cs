// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;

namespace NuGet.SolutionRestoreManager
{
    [Export(typeof(IVsNuGetProjectUpdateEvents))]
    [Export(typeof(IVsNuGetProgressReporter))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class VsRestoreProgressEvents : IVsNuGetProjectUpdateEvents, IVsNuGetProgressReporter
    {
        public event SolutionRestoreEventHandler SolutionRestoreStarted;
        public event SolutionRestoreEventHandler SolutionRestoreFinished;
        public event ProjectUpdateEventHandler ProjectUpdateStarted;
        public event ProjectUpdateEventHandler ProjectUpdateFinished;

        public void EndProjectUpdate(string projectName, IReadOnlyList<string> updatedFiles)
        {
            if (projectName == null)
            {
                throw new ArgumentNullException(nameof(projectName));
            }

            if (updatedFiles == null)
            {
                throw new ArgumentNullException(nameof(updatedFiles));
            }

            foreach (var handler in ProjectUpdateFinished?.GetInvocationList())
            {
                try
                {
                    handler.DynamicInvoke(projectName, updatedFiles);
                }
                catch { }
            }
        }

        public void StartProjectUpdate(string projectName, IReadOnlyList<string> updatedFiles)
        {
            if (projectName == null)
            {
                throw new ArgumentNullException(nameof(projectName));
            }

            if (updatedFiles == null)
            {
                throw new ArgumentNullException(nameof(updatedFiles));
            }

            foreach (var handler in ProjectUpdateStarted?.GetInvocationList())
            {
                try
                {
                    handler.DynamicInvoke(projectName, updatedFiles);
                }
                catch { }
            }
        }

        public void StartSolutionRestore(IReadOnlyList<string> projects)
        {
            if (projects == null || projects.Count == 0)
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(projects)));
            }

            foreach (var handler in SolutionRestoreStarted?.GetInvocationList())
            {
                try
                {
                    handler.DynamicInvoke(projects);
                }
                catch { }
            }
        }

        public void EndSolutionRestore(IReadOnlyList<string> projects)
        {
            if (projects == null || projects.Count == 0)
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(projects)));
            }

            foreach (var handler in SolutionRestoreFinished?.GetInvocationList())
            {
                try
                {
                    handler.DynamicInvoke(projects);
                }
                catch { }
            }
        }
    }
}
