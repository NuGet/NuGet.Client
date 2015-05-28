using System.Collections.Generic;

namespace NuGet.Commands.Test
{
    public class TestLogger : NuGet.Logging.ILogger
    {
        /// <summary>
        /// Logged messages
        /// </summary>
        public Queue<string> Messages { get; } = new Queue<string>();

        public int Errors { get; set; }

        public int Warnings { get; set; }

        public void LogDebug(string data)
        {
            Messages.Enqueue(data);
        }

        public void LogError(string data)
        {
            Errors++;
            Messages.Enqueue(data);
        }

        public void LogInformation(string data)
        {
            Messages.Enqueue(data);
        }

        public void LogVerbose(string data)
        {
            Messages.Enqueue(data);
        }

        public void LogWarning(string data)
        {
            Warnings++;
            Messages.Enqueue(data);
        }
    }
}
