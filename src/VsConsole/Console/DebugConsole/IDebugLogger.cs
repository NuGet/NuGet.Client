using NuGet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace NuGetConsole.Implementation
{
    public interface IDebugLogger
    {
        void Log(string message, ConsoleColor color);

        void SetConsole(DebugConsoleToolWindow console);
    }
}
