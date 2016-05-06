using System.Collections.Concurrent;
using NuGet.CommandLine.XPlat;
using NuGet.Common;
using NuGet.Test.Utility;

namespace NuGet.XPlat.FuncTest
{
    public class TestCommandOutputLogger : CommandOutputLogger
    {
        public TestLogger Logger { get; set; } = new TestLogger();

        public TestCommandOutputLogger()
            : base(LogLevel.Debug)
        {
        }

        protected override void LogInternal(LogLevel logLevel, string message)
        {
            switch (logLevel)
            {
                case LogLevel.Debug:
                    Logger.LogDebug(message);
                    break;
                case LogLevel.Error:
                    Logger.LogError(message);
                    break;
                case LogLevel.Information:
                    Logger.LogInformation(message);
                    break;
                case LogLevel.Minimal:
                    Logger.LogMinimal(message);
                    break;
                case LogLevel.Verbose:
                    Logger.LogVerbose(message);
                    break;
                case LogLevel.Warning:
                    Logger.LogWarning(message);
                    break;
                default:
                    Logger.LogDebug(message);
                    break;
            }
        }

        public ConcurrentQueue<string> Messages
        {
            get
            {
                return Logger.Messages;
            }
        }

        public int Errors
        {
            get
            {
                return Logger.Errors;
            }
        }

        public int Warnings
        {
            get
            {
                return Logger.Warnings;
            }
        }
    }
}
