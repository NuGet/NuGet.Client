using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet
{
    public class DebugConsoleMessageEventArgs : EventArgs
    {
        public string Message { get; private set; }
        public TraceEventType Level { get; private set; }
        public string Source { get; private set; }
        public DateTime Timestamp { get; private set; }

        public DebugConsoleMessageEventArgs(DateTime timestamp, string message, TraceEventType level, string source)
        {
            Timestamp = timestamp;
            Message = message;
            Level = level;
            Source = source;
        }
    }
}
