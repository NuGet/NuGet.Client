using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

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
            DumpMessage("DEBUG", data);
        }

        public void LogError(string data)
        {
            Errors++;
            Messages.Enqueue(data);
            DumpMessage("ERROR", data);
        }

        public void LogInformation(string data)
        {
            Messages.Enqueue(data);
            DumpMessage("INFO ", data);
        }

        public void LogVerbose(string data)
        {
            Messages.Enqueue(data);
            DumpMessage("TRACE", data);
        }

        public void LogWarning(string data)
        {
            Warnings++;
            Messages.Enqueue(data);
            DumpMessage("WARN ", data);
        }
        
        private void DumpMessage(string level, string data)
        {
            // NOTE(anurse): Uncomment this to help when debugging tests
            //Console.WriteLine($"{level}: {data}");
        }
    }
}
