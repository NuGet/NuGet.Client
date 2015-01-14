
namespace NuGetConsole.Implementation.Console
{
    internal interface IPrivateConsoleStatus : IConsoleStatus
    {
        void SetBusyState(bool isBusy);
    }
}
