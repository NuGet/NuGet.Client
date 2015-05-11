// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
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
    /// <summary>
    /// Represents the tracing details for a specific invocation.
    /// </summary>
    public class TraceContext
    {
        private string _invocationId;
        private ITraceSink _sink;

        /// <summary>
        /// Constructs a new <see cref="TraceContext"/> using the specified invocation ID and target <see cref="ITraceSink"/>
        /// </summary>
        /// <param name="invocationId">The ID of the invocation represented in this <see cref="TraceContext"/></param>
        /// <param name="sink">The <see cref="ITraceSink"/> to write trace events to</param>
        public TraceContext(string invocationId, ITraceSink sink)
        {
            Guard.NotNull(invocationId, "invocationId"); // String.Empty is OK and indicates no invocationId is available.
            Guard.NotNull(sink, "sink");

            _invocationId = invocationId;
            _sink = sink;
        }

        /// <summary>
        /// Traces the <see cref="Enter"/> event and returns an <see cref="IDisposable"/> object that will trace the <see cref="Exit"/> event when <see cref="IDisposable.Dispose"/> is called on it.
        /// </summary>
        /// <param name="methodName">The name of the method that invoked this. May be null. Automatically provided by compatible compilers</param>
        /// <param name="filePath">The path to the file containing the code invoking this. May be null. Automatically provided by compatible compilers</param>
        /// <param name="line">The line number in the file that contains the code invoking this. May be 0. Automatically provided by compatible compilers</param>
        /// <returns>An <see cref="IDisposable"/> object that will trace the <see cref="Exit"/> event when <see cref="IDisposable.Dispose"/> is called on it.</returns>
        [SuppressMessage("Microsoft.Design", "CA1026:DefaultParametersShouldNotBeUsed", Justification = "Default parameters are required by the Caller Member Info attributes")]
        public IDisposable EnterExit([CallerMemberName] string methodName = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int line = 0)
        {
            Enter(methodName, filePath, line);
            return new EnterExitTracer(this, methodName);
        }

        /// <summary>
        /// Called when a NuGet Client method is invoked.
        /// </summary>
        /// <param name="methodName">The name of the method that invoked this. May be null. Automatically provided by compatible compilers</param>
        /// <param name="filePath">The path to the file containing the code invoking this. May be null. Automatically provided by compatible compilers</param>
        /// <param name="line">The line number in the file that contains the code invoking this. May be 0. Automatically provided by compatible compilers</param>
        [SuppressMessage("Microsoft.Design", "CA1026:DefaultParametersShouldNotBeUsed", Justification = "Default parameters are required by the Caller Member Info attributes")]
        public void Enter([CallerMemberName] string methodName = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int line = 0)
        {
            _sink.Enter(_invocationId, methodName, filePath, line);
        }

        /// <summary>
        /// Called when an HTTP Request is sent by a NuGet Client method
        /// </summary>
        /// <param name="request">The <see cref="System.Net.Http.HttpRequestMessage"/> being sent</param>
        /// <param name="methodName">The name of the method that invoked this. May be null. Automatically provided by compatible compilers</param>
        /// <param name="filePath">The path to the file containing the code invoking this. May be null. Automatically provided by compatible compilers</param>
        /// <param name="line">The line number in the file that contains the code invoking this. May be 0. Automatically provided by compatible compilers</param>
        [SuppressMessage("Microsoft.Design", "CA1026:DefaultParametersShouldNotBeUsed", Justification = "Default parameters are required by the Caller Member Info attributes")]
        public void SendRequest(HttpRequestMessage request, [CallerMemberName] string methodName = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int line = 0)
        {
            Guard.NotNull(request, "request");

            _sink.SendRequest(_invocationId, request, methodName, filePath, line);
        }

        /// <summary>
        /// Called when an HTTP Response is received by a NuGet Client method.
        /// </summary>
        /// <param name="response">The <see cref="System.Net.Http.HttpResponseMessage"/> being received</param>
        /// <param name="methodName">The name of the method that invoked this. May be null. Automatically provided by compatible compilers</param>
        /// <param name="filePath">The path to the file containing the code invoking this. May be null. Automatically provided by compatible compilers</param>
        /// <param name="line">The line number in the file that contains the code invoking this. May be 0. Automatically provided by compatible compilers</param>
        [SuppressMessage("Microsoft.Design", "CA1026:DefaultParametersShouldNotBeUsed", Justification = "Default parameters are required by the Caller Member Info attributes")]
        public void ReceiveResponse(HttpResponseMessage response, [CallerMemberName] string methodName = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int line = 0)
        {
            Guard.NotNull(response, "response");

            _sink.ReceiveResponse(_invocationId, response, methodName, filePath, line);
        }

        /// <summary>
        /// Called when an Exception is raised by a NuGet Client method.
        /// </summary>
        /// <param name="exception">The <see cref="System.Exception"/> being raised</param>
        /// <param name="methodName">The name of the method that invoked this. May be null. Automatically provided by compatible compilers</param>
        /// <param name="filePath">The path to the file containing the code invoking this. May be null. Automatically provided by compatible compilers</param>
        /// <param name="line">The line number in the file that contains the code invoking this. May be 0. Automatically provided by compatible compilers</param>
        [SuppressMessage("Microsoft.Design", "CA1026:DefaultParametersShouldNotBeUsed", Justification = "Default parameters are required by the Caller Member Info attributes")]
        public void Error(Exception exception, [CallerMemberName] string methodName = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int line = 0)
        {
            Guard.NotNull(exception, "exception");
            
            _sink.Error(_invocationId, exception, methodName, filePath, line);
        }

        /// <summary>
        /// Called when a NuGet Client method is exited.
        /// </summary>
        /// <param name="methodName">The name of the method that invoked this. May be null. Automatically provided by compatible compilers</param>
        /// <param name="filePath">The path to the file containing the code invoking this. May be null. Automatically provided by compatible compilers</param>
        /// <param name="line">The line number in the file that contains the code invoking this. May be 0. Automatically provided by compatible compilers</param>
        [SuppressMessage("Microsoft.Design", "CA1026:DefaultParametersShouldNotBeUsed", Justification = "Default parameters are required by the Caller Member Info attributes")]
        public void Exit([CallerMemberName] string methodName = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int line = 0)
        {
            _sink.Exit(_invocationId, methodName, filePath, line);
        }

        /// <summary>
        /// Called when a recoverable issue is encountered parsing JSON data.
        /// </summary>
        /// <param name="token">The JSON token containing the issue.</param>
        /// <param name="warning">The issue encountered.</param>
        /// <param name="methodName">The name of the method that invoked this. May be null. Automatically provided by compatible compilers</param>
        /// <param name="filePath">The path to the file containing the code invoking this. May be null. Automatically provided by compatible compilers</param>
        /// <param name="line">The line number in the file that contains the code invoking this. May be 0. Automatically provided by compatible compilers</param>
        [SuppressMessage("Microsoft.Design", "CA1026:DefaultParametersShouldNotBeUsed", Justification = "Default parameters are required by the Caller Member Info attributes")]
        public void JsonParseWarning(JToken token, string warning, [CallerMemberName] string methodName = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int line = 0)
        {
            Guard.NotNull(token, "token");
            Guard.NotNullOrEmpty(warning, "warning");

            _sink.JsonParseWarning(_invocationId, token, warning, methodName, filePath, line);
        }

        /// <summary>
        /// Called when a NuGet Client invocation begins.
        /// </summary>
        /// <remarks>
        /// An invocation is a single client-instigated operation. Multiple calls to <see cref="Enter"/> and <see cref="Exit"/> may be contained in a single invocation.
        /// </remarks>
        /// <param name="methodName">The name of the method that invoked this. May be null. Automatically provided by compatible compilers</param>
        /// <param name="filePath">The path to the file containing the code invoking this. May be null. Automatically provided by compatible compilers</param>
        /// <param name="line">The line number in the file that contains the code invoking this. May be 0. Automatically provided by compatible compilers</param>
        [SuppressMessage("Microsoft.Design", "CA1026:DefaultParametersShouldNotBeUsed", Justification = "Default parameters are required by the Caller Member Info attributes")]
        public void Start([CallerMemberName] string methodName = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int line = 0)
        {
            _sink.Start(_invocationId, methodName, filePath, line);
        }

        /// <summary>
        /// Called when a NuGet Client invocation ends.
        /// </summary>
        /// <remarks>
        /// An invocation is a single client-instigated operation. Multiple calls to <see cref="Enter"/> and <see cref="Exit"/> may be contained in a single invocation.
        /// </remarks>
        /// <param name="methodName">The name of the method that invoked this. May be null. Automatically provided by compatible compilers</param>
        /// <param name="filePath">The path to the file containing the code invoking this. May be null. Automatically provided by compatible compilers</param>
        /// <param name="line">The line number in the file that contains the code invoking this. May be 0. Automatically provided by compatible compilers</param>
        [SuppressMessage("Microsoft.Design", "CA1026:DefaultParametersShouldNotBeUsed", Justification = "Default parameters are required by the Caller Member Info attributes")]
        public void End([CallerMemberName] string methodName = null, [CallerFilePath] string filePath = null, [CallerLineNumber] int line = 0)
        {
            _sink.End(_invocationId, methodName, filePath, line);
        }

        private class EnterExitTracer : IDisposable
        {
            private TraceContext _tracer;
            private string _methodName;

            public EnterExitTracer(TraceContext tracer, string methodName) { 
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
