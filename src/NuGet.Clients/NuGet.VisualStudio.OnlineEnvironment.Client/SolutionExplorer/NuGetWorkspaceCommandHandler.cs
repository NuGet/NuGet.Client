// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio;
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
        private readonly RestoreCommandHandler _restoreCommandHandler;

        public NuGetWorkspaceCommandHandler(JoinableTaskContext taskContext, IAsyncServiceProvider asyncServiceProvider)
        {
            if (taskContext == null)
            {
                throw new ArgumentNullException(nameof(taskContext));
            }

            _restoreCommandHandler = new RestoreCommandHandler(taskContext.Factory, asyncServiceProvider);
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
            if (pguidCmdGroup == CommandGroup.NuGetOnlineEnvironmentsClientProjectCommandSetGuid)
            {
                var nCmdIDInt = (int)nCmdID;

                if (IsSolutionOnlySelection(selection))
                {
                    switch (nCmdIDInt)
                    {
                        case PkgCmdIDList.CmdidRestorePackages:
                            _restoreCommandHandler.RunSolutionRestore();
                            return VSConstants.S_OK;
                    }
                }
            }
            return (int) Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_NOTSUPPORTED;
        }

        public bool QueryStatus(List<WorkspaceVisualNodeBase> selection, Guid pguidCmdGroup, uint nCmdID, ref uint cmdf, ref string customTitle)
        {
            bool handled = false;

            if (pguidCmdGroup == CommandGroup.NuGetOnlineEnvironmentsClientProjectCommandSetGuid)
            {
                var nCmdIDInt = (int)nCmdID;

                if (IsSolutionOnlySelection(selection))
                {
                    switch (nCmdIDInt)
                    {
                        case PkgCmdIDList.CmdidRestorePackages:
                            var isRestoreActionInProgress = _restoreCommandHandler.IsRestoreActionInProgress();
                            cmdf = (uint)((isRestoreActionInProgress ? 0 : Microsoft.VisualStudio.OLE.Interop.OLECMDF.OLECMDF_ENABLED)
                                | Microsoft.VisualStudio.OLE.Interop.OLECMDF.OLECMDF_SUPPORTED);
                            handled = true;
                            break;
                    }
                }
            }

            return handled;
        }

        private static bool IsSolutionOnlySelection(List<WorkspaceVisualNodeBase> selection)
        {
            return selection != null &&
                selection.Count.Equals(1) &&
                selection.First().NodeMoniker.Equals(string.Empty);
        }

    }
}
