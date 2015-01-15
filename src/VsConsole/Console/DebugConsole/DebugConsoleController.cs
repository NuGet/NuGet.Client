using NuGet;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace NuGetConsole
{
    [Export(typeof(IDebugConsoleController))]
    public class DebugConsoleController : IDebugConsoleController
    {
        public DebugConsoleController()
        {

        }

        public void Log(DateTime timestamp, string message, TraceEventType level, string source)
        {
            if (OnMessage != null)
            {
                DebugConsoleMessageEventArgs args = new DebugConsoleMessageEventArgs(timestamp, message, level, source);

                OnMessage(this, args);
            }
        }

        public event EventHandler<DebugConsoleMessageEventArgs> OnMessage;
    }
}
