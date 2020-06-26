// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Common;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary> UI logger. </summary>
    public interface INuGetUILogger : IDisposable
    {
        /// <summary> Log a message at given level. </summary>
        /// <param name="level"> Message level. </param>
        /// <param name="message"> Message to log. </param>
        /// <param name="args"> Message format arguments. </param>
        void Log(MessageLevel level, string message, params object[] args);

        /// <summary> Log a message. </summary>
        /// <param name="message"> Message to log. </param>
        void Log(ILogMessage message);

        /// <summary> Report an error. </summary>
        /// <param name="message"> Error message. </param>
        void ReportError(string message);

        /// <summary> Report an error. </summary>
        /// <param name="message"> Error message. </param>
        void ReportError(ILogMessage message);
    }
}
