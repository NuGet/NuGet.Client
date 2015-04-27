using Microsoft.Framework.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NuGet.CommandLine
{
    public class CommandOutputLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string name)
        {
            return new CommandOutputLogger(this);
        }

        public LogLevel LogLevel { get; set; } = LogLevel.Information;
    }
}
