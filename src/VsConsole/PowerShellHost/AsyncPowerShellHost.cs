using NuGet.PackageManagement.VisualStudio;
using System;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace NuGetConsole.Host.PowerShell.Implementation
{
    internal class AsyncPowerShellHost : PowerShellHost, IAsyncHost
    {
        public event EventHandler ExecuteEnd;

        public AsyncPowerShellHost(string name, IRunspaceManager runspaceManager)
            : base(name, runspaceManager)
        {
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        protected override bool ExecuteHost(string fullCommand, string command, params object[] inputs)
        {
            SetSyncModeOnHost(false);
            try
            {
                Pipeline pipeline = Runspace.InvokeAsync(fullCommand, inputs, true, (sender, e) =>
                {
                    switch (e.PipelineStateInfo.State)
                    {
                        case PipelineState.Completed:
                        case PipelineState.Failed:
                        case PipelineState.Stopped:
                            if (e.PipelineStateInfo.Reason != null)
                            {
                                ReportError(e.PipelineStateInfo.Reason);
                            }

                            OnExecuteCommandEnd();
                            ExecuteEnd.Raise(this, EventArgs.Empty);
                            break;
                    }
                });

                ExecutingPipeline = pipeline;
                return true;
            }
            catch (RuntimeException e)
            {
                ReportError(e.ErrorRecord);
                ExceptionHelper.WriteToActivityLog(e);
            }
            catch (Exception e)
            {
                ReportError(e);
                ExceptionHelper.WriteToActivityLog(e);
            }

            return false; // Error occurred, command not executing
        }
    }
}