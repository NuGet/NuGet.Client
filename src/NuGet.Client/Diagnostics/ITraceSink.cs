using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace NuGet.Client.Diagnostics
{
    /// <summary>
    /// Provides an interface to an object that can receive tracing events from the NuGet Client Platform.
    /// </summary>
    public interface ITraceSink
    {
        /// <summary>
        /// Called when a NuGet Client method is invoked.
        /// </summary>
        /// <param name="invocationId">The root invocation Id assigned to the request</param>
        /// <param name="methodName">The name of the method that invoked this. May be null. Automatically provided by compatible compilers</param>
        /// <param name="filePath">The path to the file containing the code invoking this. May be null. Automatically provided by compatible compilers</param>
        /// <param name="line">The line number in the file that contains the code invoking this. May be 0. Automatically provided by compatible compilers</param>
        void Enter(string invocationId, string methodName, string filePath, int line);

        /// <summary>
        /// Called when an HTTP Request is sent by a NuGet Client method
        /// </summary>
        /// <param name="invocationId">The root invocation Id assigned to the request</param>
        /// <param name="request">The <see cref="System.Net.Http.HttpRequestMessage"/> being sent</param>
        /// <param name="methodName">The name of the method that invoked this. May be null. Automatically provided by compatible compilers</param>
        /// <param name="filePath">The path to the file containing the code invoking this. May be null. Automatically provided by compatible compilers</param>
        /// <param name="line">The line number in the file that contains the code invoking this. May be 0. Automatically provided by compatible compilers</param>
        void SendRequest(string invocationId, HttpRequestMessage request, string methodName, string filePath, int line);

        /// <summary>
        /// Called when an HTTP Response is received by a NuGet Client method.
        /// </summary>
        /// <param name="invocationId">The root invocation Id assigned to the request</param>
        /// <param name="response">The <see cref="System.Net.Http.HttpResponseMessage"/> being received</param>
        /// <param name="methodName">The name of the method that invoked this. May be null. Automatically provided by compatible compilers</param>
        /// <param name="filePath">The path to the file containing the code invoking this. May be null. Automatically provided by compatible compilers</param>
        /// <param name="line">The line number in the file that contains the code invoking this. May be 0. Automatically provided by compatible compilers</param>
        void ReceiveResponse(string invocationId, HttpResponseMessage response, string methodName, string filePath, int line);

        /// <summary>
        /// Called when an Exception is raised by a NuGet Client method.
        /// </summary>
        /// <param name="invocationId">The root invocation Id assigned to the request</param>
        /// <param name="exception">The <see cref="System.Exception"/> being raised</param>
        /// <param name="methodName">The name of the method that invoked this. May be null. Automatically provided by compatible compilers</param>
        /// <param name="filePath">The path to the file containing the code invoking this. May be null. Automatically provided by compatible compilers</param>
        /// <param name="line">The line number in the file that contains the code invoking this. May be 0. Automatically provided by compatible compilers</param>
        [SuppressMessage("Microsoft.Naming", "CA1716:IdentifiersShouldNotMatchKeywords", MessageId = "Error", Justification="Error is the most appropriate name for this and conflict is unlikely")]
        void Error(string invocationId, Exception exception, string methodName, string filePath, int line);

        /// <summary>
        /// Called when a NuGet Client method is exited.
        /// </summary>
        /// <param name="invocationId">The root invocation Id assigned to the request</param>
        /// <param name="methodName">The name of the method that invoked this. May be null. Automatically provided by compatible compilers</param>
        /// <param name="filePath">The path to the file containing the code invoking this. May be null. Automatically provided by compatible compilers</param>
        /// <param name="line">The line number in the file that contains the code invoking this. May be 0. Automatically provided by compatible compilers</param>
        [SuppressMessage("Microsoft.Naming", "CA1716:IdentifiersShouldNotMatchKeywords", MessageId = "Exit", Justification = "Exit is the most appropriate name for this and conflict is unlikely")]
        void Exit(string invocationId, string methodName, string filePath, int line);

        /// <summary>
        /// Called when a recoverable issue is encountered parsing JSON data.
        /// </summary>
        /// <param name="invocationId">The root invocation Id assigned to the request</param>
        /// <param name="token">The JSON token containing the issue.</param>
        /// <param name="warning">The issue encountered.</param>
        /// <param name="methodName">The name of the method that invoked this. May be null. Automatically provided by compatible compilers</param>
        /// <param name="filePath">The path to the file containing the code invoking this. May be null. Automatically provided by compatible compilers</param>
        /// <param name="line">The line number in the file that contains the code invoking this. May be 0. Automatically provided by compatible compilers</param>
        void JsonParseWarning(string invocationId, JToken token, string warning, string methodName, string filePath, int line);
    }

    /// <summary>
    /// Provides access to common trace sink implementations
    /// </summary>
    public static class TraceSinks
    {
        /// <summary>
        /// Gets a reference to the singleton "null" trace sink
        /// </summary>
        /// <remarks>The "null" trace sink does nothing when trace events are reported.</remarks>
        [SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification="The NullTraceSink object is immutable")]
        public static readonly ITraceSink Null = NullTraceSink.Instance;

        private class NullTraceSink : ITraceSink
        {
            public static readonly NullTraceSink Instance = new NullTraceSink();
            private NullTraceSink() { }

            public void Enter(string invocationId, string methodName = null, string file = null, int line = 0)
            {
            }

            public void SendRequest(string invocationId, HttpRequestMessage request, string methodName = null, string file = null, int line = 0)
            {
            }

            public void ReceiveResponse(string invocationId, HttpResponseMessage response, string methodName = null, string file = null, int line = 0)
            {
            }

            public void Error(string invocationId, Exception exception, string methodName = null, string file = null, int line = 0)
            {
            }

            public void Exit(string invocationId, string methodName = null, string file = null, int line = 0)
            {
            }

            public void JsonParseWarning(string invocationId, JToken token, string warning, string methodName, string filePath, int line)
            {
            }
        }
    }
}
