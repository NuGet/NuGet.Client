using System;
using NuGet.PackageManagement.VisualStudio;

namespace NuGetConsole.Host.PowerShell.Implementation
{
    internal class SyncPowerShellHost : PowerShellHost
    {
        public SyncPowerShellHost(string name, IRunspaceManager runspaceManager)
            : base(name, runspaceManager)
        {
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        protected override bool ExecuteHost(string fullCommand, string command, params object[] inputs)
        {
            SetSyncModeOnHost(true);
            base.SetPackageManagementContextOnHost();
            base.SetActivePackageSourceOnHost();

            try
            {
                Runspace.Invoke(fullCommand, inputs, true);
                OnExecuteCommandEnd();
            }
            catch (Exception e)
            {
                ExceptionHelper.WriteToActivityLog(e);
                throw;
            }

            return true;
        }
    }
}