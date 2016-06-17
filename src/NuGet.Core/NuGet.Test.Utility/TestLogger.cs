using System;
using System.Collections.Concurrent;

namespace NuGet.Test.Utility
{
    public class TestLogger : Common.ILogger
    {
        /// <summary>
        /// Logged messages
        /// </summary>
        public ConcurrentQueue<string> Messages { get; } = new ConcurrentQueue<string>();
        public ConcurrentQueue<string> DebugMessages { get; } = new ConcurrentQueue<string>();
        public ConcurrentQueue<string> VerboseMessages { get; } = new ConcurrentQueue<string>();
        public ConcurrentQueue<string> MinimalMessages { get; } = new ConcurrentQueue<string>();
        public ConcurrentQueue<string> ErrorMessages { get; } = new ConcurrentQueue<string>();

        public int Errors { get; set; }

        public int Warnings { get; set; }

        public void LogDebug(string data)
        {
            Messages.Enqueue(data);
            DebugMessages.Enqueue(data);
            DumpMessage("DEBUG", data);
        }

        public void LogError(string data)
        {
            Errors++;
            Messages.Enqueue(data);
            ErrorMessages.Enqueue(data);
            DumpMessage("ERROR", data);
        }

        public void LogInformation(string data)
        {
            Messages.Enqueue(data);
            DumpMessage("INFO ", data);
        }

        public void LogMinimal(string data)
        {
            Messages.Enqueue(data);
            MinimalMessages.Enqueue(data);
            DumpMessage("LOG  ", data);
        }

        public void LogVerbose(string data)
        {
            Messages.Enqueue(data);
            VerboseMessages.Enqueue(data);
            DumpMessage("TRACE", data);
        }

        public void LogWarning(string data)
        {
            Warnings++;
            Messages.Enqueue(data);
            DumpMessage("WARN ", data);
        }

        public void LogInformationSummary(string data)
        {
            Messages.Enqueue(data);
            DumpMessage("ISMRY", data);
        }

        public void LogErrorSummary(string data)
        {
            Messages.Enqueue(data);
            DumpMessage("ESMRY", data);
        }

        private void DumpMessage(string level, string data)
        {
            // NOTE(anurse): Uncomment this to help when debugging tests
            //Console.WriteLine($"{level}: {data}");
        }

        public void Clear()
        {
            string msg;
            while (Messages.TryDequeue(out msg))
            {
                // do nothing
            }
        }

        public string ShowErrors()
        {
            return string.Join(Environment.NewLine, ErrorMessages);
        }

        public string ShowMessages()
        {
            return string.Join(Environment.NewLine, Messages);
        }
    }
}
