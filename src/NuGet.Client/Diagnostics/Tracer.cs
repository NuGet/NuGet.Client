using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using Newtonsoft.Json.Linq;

namespace NuGet.Client.Diagnostics
{
    internal class Tracer
    {
        private string _invocationId;
        private ITraceSink _sink;

        public Tracer(string invocationId, ITraceSink sink)
        {
            Guard.NotNull(invocationId, "invocationId"); // String.Empty is OK and indicates no invocationId is available.
            Guard.NotNull(sink, "sink");

            _invocationId = invocationId;
            _sink = sink;
        }

        public IDisposable EnterExit([CallerMemberName] string methodName = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int line = 0)
        {
            Enter(methodName, filePath, line);
            return new EnterExitTracer(this, methodName);
        }

        public void Enter([CallerMemberName] string methodName = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int line = 0)
        {
            _sink.Enter(_invocationId, methodName, filePath, line);
        }

        public void SendRequest(HttpRequestMessage request, [CallerMemberName] string methodName = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int line = 0)
        {
            Guard.NotNull(request, "request");

            _sink.SendRequest(_invocationId, request, methodName, filePath, line);
        }

        public void ReceiveResponse(HttpResponseMessage response, [CallerMemberName] string methodName = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int line = 0)
        {
            Guard.NotNull(response, "response");

            _sink.ReceiveResponse(_invocationId, response, methodName, filePath, line);
        }

        public void Error(Exception exception, [CallerMemberName] string methodName = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int line = 0)
        {
            Guard.NotNull(exception, "exception");
            
            _sink.Error(_invocationId, exception, methodName, filePath, line);
        }

        public void Exit([CallerMemberName] string methodName = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int line = 0)
        {
            _sink.Exit(_invocationId, methodName, filePath, line);
        }

        public void JsonParseWarning(JToken token, string warning, [CallerMemberName] string methodName = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int line = 0)
        {
            Guard.NotNull(token, "token");
            Guard.NotNullOrEmpty(warning, "warning");

            _sink.JsonParseWarning(_invocationId, token, warning, methodName, filePath, line);
        }

        private class EnterExitTracer : IDisposable
        {
            private Tracer _tracer;
            private string _methodName;

            public EnterExitTracer(Tracer tracer, string methodName) { 
                _tracer = tracer;
                _methodName = methodName;
            }

            public void Dispose()
            {
                _tracer.Exit(_methodName);
            }
        }
    }
}
