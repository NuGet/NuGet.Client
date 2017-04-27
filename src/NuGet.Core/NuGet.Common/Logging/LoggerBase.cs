﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Common
{
    public abstract class LoggerBase : ILogger
    {
<<<<<<< HEAD
        public LogLevel VerbosityLevel { get; private set; } = LogLevel.Debug;

        public LoggerBase()
        {
        }

        public LoggerBase(LogLevel verbosityLevel)
        {
            VerbosityLevel = verbosityLevel;
        }

        public abstract void Log(ILogMessage message);

        public abstract Task LogAsync(ILogMessage message);

        public virtual void Log(LogLevel level, string data)
        {
            if (DisplayMessage(level))
            {
                Log(new RestoreLogMessage(level, NuGetLogCode.Undefined, data, string.Empty, string.Empty));
            }
        }

        public virtual Task LogAsync(LogLevel level, string data)
        {
            if (DisplayMessage(level))
            {
                return LogAsync(new RestoreLogMessage(level, NuGetLogCode.Undefined, data, string.Empty, string.Empty));
            }

            return Task.FromResult(true);
        }
=======
        public abstract void Log(ILogMessage message);

        public abstract void Log(LogLevel level, string data);

        public abstract Task LogAsync(ILogMessage message);

        public abstract Task LogAsync(LogLevel level, string data);
>>>>>>> dev-restore-errors

        public void LogDebug(string data)
        {
            Log(LogLevel.Debug, data);
        }

        public void LogError(string data)
        {
            Log(LogLevel.Error, data);
        }

        public void LogErrorSummary(string data)
        {
            //TODO remove summaries
            Log(LogLevel.Error, data);
        }

        public void LogInformation(string data)
        {
            Log(LogLevel.Information, data);
        }

        public void LogInformationSummary(string data)
        {
            Log(LogLevel.Information, data);
        }

        public void LogMinimal(string data)
        {
            Log(LogLevel.Minimal, data);
        }

        public void LogVerbose(string data)
        {
            Log(LogLevel.Verbose, data);
        }

        public void LogWarning(string data)
        {
            Log(LogLevel.Warning, data);
        }

        /// <summary>
        /// True if the message meets the verbosity level.
        /// </summary>
        protected virtual bool DisplayMessage(LogLevel messageLevel)
        {
            return (messageLevel >= VerbosityLevel);
        }

    }
}
