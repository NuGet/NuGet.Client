// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary> UI logger abstraction. </summary>
    public interface INuGetUILogger
    {
        /// <summary> Log a message. </summary>
        /// <param name="message"> Log message. </param>
        void Log(ILogMessage message);

        /// <summary> Report an error or warning. </summary>
        /// <param name="message"> Error or warning log message. </param>
        void ReportError(ILogMessage message);

        /// <summary> Start the logging. </summary>
        void Start();

        /// <summary> End the logging. </summary>
        void End();
    }
}
