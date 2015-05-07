// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.ProjectManagement;

namespace NuGet.PackageManagement.UI
{
    public interface INuGetUILogger
    {
        void Log(MessageLevel level, string message, params object[] args);

        void ReportError(string message);

        void Start();

        void End();
    }
}
