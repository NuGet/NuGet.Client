using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Client.Diagnostics;

namespace NuGet.Client.Diagnostics
{
    public class SystemTraceSink : ITraceSink
    {
        private static readonly TraceSource DefaultTraceSource = new TraceSource("Outercurve-NuGet-Client");

        private TraceSource _traceSource;

        public SystemTraceSink() : this(DefaultTraceSource) { }
        public SystemTraceSink(TraceSource traceSource)
        {
            _traceSource = traceSource;
        }

        public void Enter(string invocationId, string methodName, string filePath, int line)
        {
            _traceSource.TraceEvent(TraceEventType.Start, 0, Strings.SystemTraceSink_Enter, invocationId, methodName, filePath, line);
        }

        public void SendRequest(string invocationId, HttpRequestMessage request, string methodName, string filePath, int line)
        {
            _traceSource.TraceEvent(TraceEventType.Verbose, 1, Strings.SystemTraceSink_SendRequest, invocationId, methodName, filePath, line, request.Method.ToString().ToUpperInvariant(), request.RequestUri);
        }

        public void ReceiveResponse(string invocationId, System.Net.Http.HttpResponseMessage response, string methodName, string filePath, int line)
        {
            _traceSource.TraceEvent(TraceEventType.Verbose, 2, Strings.SystemTraceSink_ReceiveResponse, invocationId, methodName, filePath, line, (int)response.StatusCode, response.RequestMessage.RequestUri);
        }

        public void Error(string invocationId, Exception exception, string methodName, string filePath, int line)
        {
            _traceSource.TraceEvent(TraceEventType.Error, 3, Strings.SystemTraceSink_Error, invocationId, methodName, filePath, line, exception.ToString());
        }

        public void Exit(string invocationId, string methodName, string filePath, int line)
        {
            _traceSource.TraceEvent(TraceEventType.Stop, 4, Strings.SystemTraceSink_Exit, invocationId, methodName, filePath, line);
        }

        public void JsonParseWarning(string invocationId, JToken token, string warning, string methodName, string filePath, int line)
        {
            _traceSource.TraceEvent(TraceEventType.Warning, 5, Strings.SystemTraceSink_JsonParseWarning, invocationId, methodName, filePath, line, GetFileInfo(token), warning);
        }

        internal static string GetFileInfo(JToken token)
        {
            string message = token.Path;
            IJsonLineInfo info = token as IJsonLineInfo;
            if (info != null && info.HasLineInfo())
            {
                return message + String.Format(CultureInfo.CurrentCulture, Strings.SystemTraceSink_LineInfo, info.LineNumber);
            }
            return message;
        }
    }
}
