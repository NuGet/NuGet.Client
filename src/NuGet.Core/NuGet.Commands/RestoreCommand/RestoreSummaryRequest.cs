using System.Collections.Generic;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;

namespace NuGet.Commands
{
    /// <summary>
    /// Wrapper for RestoreRequest
    /// </summary>
    public class RestoreSummaryRequest
    {
        public RestoreRequest Request { get; }

        public ISettings Settings { get; }

        public IReadOnlyList<SourceRepository> Sources { get; }

        public string InputPath { get; }

        public CollectorLogger CollectorLogger {get; }

        public RestoreSummaryRequest(
            RestoreRequest request,
            string inputPath,
            ISettings settings,
            IReadOnlyList<SourceRepository> sources)
        {
            Request = request;
            Settings = settings;
            Sources = sources;
            InputPath = inputPath;

            CollectorLogger = new CollectorLogger(request.Log);
            request.Log = CollectorLogger;
        }
    }
}
