
namespace NuGetConsole.Host.PowerShell.Implementation
{
    public static class PowerShellHostService
    {

        private static readonly IRunspaceManager _runspaceManager = new RunspaceManager();

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Microsoft.Reliability",
            "CA2000:Dispose objects before losing scope",
            Justification = "Can't dispose an object if we want to return it.")]
        public static IHost CreateHost(string name, bool isAsync)
        {
            IHost host;
            if (isAsync)
            {
                host = new AsyncPowerShellHost(name, _runspaceManager);
            }
            else
            {
                host = new SyncPowerShellHost(name, _runspaceManager);
            }

            return host;
        }
    }
}