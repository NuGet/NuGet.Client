// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.PackageManagement.VisualStudio
{
    public interface INuGetUILogger
    {
        void Start();
        Task StartAsync();

        void Log(ProjectManagement.MessageLevel level, string message, params object[] args);
        Task LogAsync(ProjectManagement.MessageLevel level, string message, params object[] args);

        void Log(ILogMessage message);
        Task LogAsync(ILogMessage message);

        void ReportError(string message);
        Task ReportErrorAsync(string message);

        void ReportError(ILogMessage message);
        Task ReportErrorAsync(ILogMessage message);

        void End();
        Task EndAsync();
    }
}
