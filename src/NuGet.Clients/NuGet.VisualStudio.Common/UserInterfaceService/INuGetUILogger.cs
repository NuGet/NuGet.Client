// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.PackageManagement.VisualStudio
{
    public interface INuGetUILogger
    {
        void Log(ProjectManagement.MessageLevel level, string message, params object[] args);

        void ReportError(string message);

        void ReportError(ILogMessage message);

        void Start();

        void End();
    }
}
