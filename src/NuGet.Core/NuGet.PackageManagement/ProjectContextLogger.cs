// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement
{
    public class ProjectContextLogger : ILogger
    {
        private readonly INuGetProjectContext _projectContext;

        public ProjectContextLogger(INuGetProjectContext projectContext)
        {
            _projectContext = projectContext;
        }

        public void LogDebug(string data)
        {
            _projectContext.Log(MessageLevel.Debug, data);
        }

        public void LogVerbose(string data)
        {
            // Treat Verbose as Debug
            LogDebug(data);
        }

        public void LogError(string data)
        {
            _projectContext.Log(MessageLevel.Error, data);
        }

        public void LogInformation(string data)
        {
            _projectContext.Log(MessageLevel.Info, data);
        }

        public void LogMinimal(string data)
        {
            // Treat Minimal as Information
            LogInformation(data);
        }

        public void LogWarning(string data)
        {
            _projectContext.Log(MessageLevel.Warning, data);
        }

        public void LogInformationSummary(string data)
        {
            // Treat Summary as Debug
            LogDebug(data);
        }

        public void LogErrorSummary(string data)
        {
            // Treat Summary as Debug
            LogDebug(data);
        }
    }
}
