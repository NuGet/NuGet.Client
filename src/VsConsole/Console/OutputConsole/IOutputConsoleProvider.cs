using NuGetConsole;

namespace NuGetConsole
{
    public interface IOutputConsoleProvider
    {
        IConsole CreateOutputConsole(bool requirePowerShellHost);
    }
}
