// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using NuGet.Common;
using Xunit.Abstractions;

namespace NuGet.Test.Utility
{
    public class TestLogger : LegacyLoggerAdapter, ILogger
    {
        private readonly ITestOutputHelper _output;

        public TestLogger()
        {
        }

        public TestLogger(ITestOutputHelper output)
        {
            _output = output;
        }

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

        public override void LogDebug(string data)
        {
            Messages.Enqueue(data);
            DebugMessages.Enqueue(data);
            DumpMessage("DEBUG", data);
        }

        public override void LogError(string data)
        {
            Errors++;
            Messages.Enqueue(data);
            ErrorMessages.Enqueue(data);
            DumpMessage("ERROR", data);
        }

        public override void LogInformation(string data)
        {
            Messages.Enqueue(data);
            DumpMessage("INFO ", data);
        }

        public override void LogMinimal(string data)
        {
            Messages.Enqueue(data);
            MinimalMessages.Enqueue(data);
            DumpMessage("LOG  ", data);
        }

        public override void LogVerbose(string data)
        {
            Messages.Enqueue(data);
            VerboseMessages.Enqueue(data);
            DumpMessage("TRACE", data);
        }

        public override void LogWarning(string data)
        {
            Warnings++;
            Messages.Enqueue(data);
            DumpMessage("WARN ", data);
        }

        public override void LogInformationSummary(string data)
        {
            Messages.Enqueue(data);
            DumpMessage("ISMRY", data);
        }

        public override void LogErrorSummary(string data)
        {
            Messages.Enqueue(data);
            DumpMessage("ESMRY", data);
        }

        private void DumpMessage(string level, string data)
        {
            // NOTE(anurse): Uncomment this to help when debugging tests
            //Console.WriteLine($"{level}: {data}");
            _output?.WriteLine($"{level}: {data}");
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
