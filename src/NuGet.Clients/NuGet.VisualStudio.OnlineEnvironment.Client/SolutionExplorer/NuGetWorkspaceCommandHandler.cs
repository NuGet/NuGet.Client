// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Workspace.VSIntegration.UI;

namespace NuGet.VisualStudio.OnlineEnvironment.Client
{
    /// <summary>
    /// Extends the Solution Explorer in online environment scenarios.
    /// </summary>
    internal class NuGetWorkspaceCommandHandler : IWorkspaceCommandHandler
    {
        private readonly JoinableTaskContext _taskContext;
        private readonly IAsyncServiceProvider _serviceProvider;

        private static bool IsNuGetFunctionalityAvailable = false;

        public NuGetWorkspaceCommandHandler(JoinableTaskContext taskContext, IAsyncServiceProvider asyncServiceProvider)
        {
            _taskContext = taskContext ?? throw new ArgumentNullException(nameof(taskContext));
            _serviceProvider = asyncServiceProvider ?? throw new ArgumentNullException(nameof(asyncServiceProvider));
        }

        /// <summary>
        /// The command handlers priority. If there are multiple handlers for a given node
        /// then they are called in order of decreasing priority.
        /// </summary>
        public int Priority => 2000;

        /// <summary>
        /// Whether or not this handler should be ignored when multiple nodes are selected.
        /// </summary>
        public bool IgnoreOnMultiselect => true;

        public int Exec(List<WorkspaceVisualNodeBase> selection, Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            if (IsNuGetFunctionalityAvailable && pguidCmdGroup == CommandGroup.NuGetOnlineEnvironmentsClientProjectCommandSetGuid)
            {
                var nCmdIDInt = (int)nCmdID;

                if (IsSolutionOnlySelection(selection))
                {
                    switch (nCmdIDInt)
                    {
                        case PkgCmdIDList.CmdidRestorePackages:
                            ExecuteSolutionRestore(selection.SingleOrDefault());

                            return 0;
                    }
                }
            }
            return 1;
        }

        public bool QueryStatus(List<WorkspaceVisualNodeBase> selection, Guid pguidCmdGroup, uint nCmdID, ref uint cmdf, ref string customTitle)
        {
            bool handled = false;

            if (IsNuGetFunctionalityAvailable && pguidCmdGroup == CommandGroup.NuGetOnlineEnvironmentsClientProjectCommandSetGuid)
            {
                var nCmdIDInt = (int)nCmdID;

                if (IsSolutionOnlySelection(selection))
                {
                    switch (nCmdIDInt)
                    {
                        case PkgCmdIDList.CmdidRestorePackages:
                            cmdf = (uint)(Microsoft.VisualStudio.OLE.Interop.OLECMDF.OLECMDF_ENABLED | Microsoft.VisualStudio.OLE.Interop.OLECMDF.OLECMDF_SUPPORTED);
                            handled = true;
                            break;
                    }
                }
            }

            return handled;
        }

        private void ExecuteSolutionRestore(WorkspaceVisualNodeBase node)
        {
            // TODO: https://github.com/NuGet/Home/issues/9308
        }

        private static bool IsSolutionOnlySelection(List<WorkspaceVisualNodeBase> selection)
        {
            return selection != null &&
                selection.Count.Equals(1) &&
                selection.First().NodeMoniker.Equals(string.Empty);
        }

    }
}
