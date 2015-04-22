using Microsoft.Framework.Logging;
using Microsoft.Framework.Runtime.Common.CommandLine;
using System;

namespace NuGet.CommandLine
{
    /// <summary>
    /// Logger to print formatted command output.
    /// </summary>
    public class CommandOutputLogger : ILogger
    {
        private readonly CommandOutputLoggerProvider _provider;

        public CommandOutputLogger(CommandOutputLoggerProvider commandOutputProvider)
        {
            _provider = commandOutputProvider;
        }

        public IDisposable BeginScopeImpl(object state)
        {
            throw new NotImplementedException();
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            if (logLevel < _provider.LogLevel)
            {
                return false;
            }

            return true;
        }

        public void Log(LogLevel logLevel, int eventId, object state, Exception exception, Func<object, Exception, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                AnsiConsole.Output.WriteLine(string.Format("{0}: {1}", Caption(logLevel), formatter(state, exception)));
            }
        }

        private string Caption(LogLevel logLevel)
        {
            switch (logLevel)
            {
                case LogLevel.Debug: return "\x1b[35mdebug\x1b[39m";
                case LogLevel.Verbose: return "\x1b[35mtrace\x1b[39m";
                case LogLevel.Information: return "\x1b[32minfo \x1b[39m";
                case LogLevel.Warning: return "\x1b[33mwarn \x1b[39m";
                case LogLevel.Error: return "\x1b[31merror\x1b[39m";
                case LogLevel.Critical: return "\x1b[31mfatal\x1b[39m";
            }

            throw new Exception("Unknown LogLevel");
        }
    }
}
