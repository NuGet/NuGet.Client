using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Client.Diagnostics;

namespace NuGet.Client
{
    /// <summary>
    /// Trace sink that captures events to a list
    /// </summary>
    public class CapturingTraceSink : ITraceSink
    {
        private List<TraceSinkEvent> _events = new List<TraceSinkEvent>();

        public IReadOnlyList<TraceSinkEvent> Events { get { return _events.AsReadOnly(); } }

        public void Enter(string invocationId, string methodName, string filePath, int line)
        {
            Capture(new
            {
                invocationId,
                methodName,
                filePath,
                line
            });
        }

        public void SendRequest(string invocationId, HttpRequestMessage request, string methodName, string filePath, int line)
        {
            Capture(new
            {
                invocationId,
                request,
                methodName,
                filePath,
                line
            });
        }

        public void ReceiveResponse(string invocationId, HttpResponseMessage response, string methodName, string filePath, int line)
        {
            Capture(new
            {
                invocationId,
                response,
                methodName,
                filePath,
                line
            });
        }

        public void Error(string invocationId, Exception exception, string methodName, string filePath, int line)
        {
            Capture(new
            {
                invocationId,
                exception,
                methodName,
                filePath,
                line
            });
        }

        public void Exit(string invocationId, string methodName, string filePath, int line)
        {
            Capture(new
            {
                invocationId,
                methodName,
                filePath,
                line
            });
        }

        public void JsonParseWarning(string invocationId, JToken token, string warning, string methodName, string filePath, int line)
        {
            Capture(new
            {
                invocationId,
                token,
                warning,
                methodName,
                filePath,
                line
            });
        }

        public void Start(string invocationId, string methodName, string filePath, int line)
        {
            Capture(new
            {
                invocationId,
                methodName,
                filePath,
                line
            });
        }

        public void End(string invocationId, string methodName, string filePath, int line)
        {
            Capture(new
            {
                invocationId,
                methodName,
                filePath,
                line
            });
        }

        private void Capture(object payload, [CallerMemberName] string methodName = null)
        {
            _events.Add(new TraceSinkEvent(methodName, Utils.ObjectToDictionary(payload)));
        }
    }

    public class TraceSinkEvent
    {
        public string Name { get; private set; }
        public IReadOnlyDictionary<string, object> Payload { get; private set; }

        public TraceSinkEvent(string name, IEnumerable<KeyValuePair<string, object>> payload)
        {
            Name = name;
            Payload = new ReadOnlyDictionary<string, object>(payload.ToDictionary(pair => pair.Key, pair => pair.Value));
        }
    }
}
