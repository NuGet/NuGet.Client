// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio;

#nullable enable

namespace NuGet.SolutionRestoreManager
{
    /// <summary>
    /// Provides methods to interact with the Solution Explorer window frame.
    /// </summary>
    internal static class WindowFrame
    {
        /// <summary>
        /// Retrieves the frame for the Solution Explorer tool window asynchronously.
        /// </summary>
        /// <param name="serviceProvider">The service provider to retrieve services from.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the IVsWindowFrame of the Solution Explorer, or null if not found.</returns>
        internal async static Task<IVsWindowFrame?> GetSolutionExplorerFrameAsync(IAsyncServiceProvider serviceProvider)
        {
            if (serviceProvider == null) throw new ArgumentNullException(nameof(serviceProvider));
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            IVsUIShell? uiShell = await serviceProvider.GetServiceAsync<SVsUIShell, IVsUIShell>(throwOnFailure: false);
            if (uiShell == null)
            {
                // Consider logging the error or throwing an exception based on your requirements.
                return null;
            }
            if (ErrorHandler.Succeeded(uiShell.FindToolWindow((uint)__VSFINDTOOLWIN.FTW_fFindFirst, VSConstants.StandardToolWindows.SolutionExplorer, out IVsWindowFrame frame)))
            {
                return frame;
            }
            return null;
        }
    }
}
