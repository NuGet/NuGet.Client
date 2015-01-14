
namespace NuGetConsole
{
    public interface IConsoleStatus
    {

        /// <summary>
        /// Returns whether the console is busy executing a command.
        /// </summary>
        bool IsBusy { get; }
    }
}