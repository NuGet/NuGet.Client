using System;
using System.Threading;
using System.Threading.Tasks;
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
            SetPrivateDataOnHost(true);

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

        protected async override Task<string[]> GetExpansionsAsyncCore(string line, string lastWord, CancellationToken token)
        {
            var expansions = await GetExpansionsAsyncCore(line, lastWord, isSync: true, token: token);
            return expansions;
        }

        protected async override Task<SimpleExpansion> GetPathExpansionsAsyncCore(string line, CancellationToken token)
        {
            var simpleExpansion = await GetPathExpansionsAsyncCore(line, isSync: true, token: token);
            return simpleExpansion;
        }
    }
}