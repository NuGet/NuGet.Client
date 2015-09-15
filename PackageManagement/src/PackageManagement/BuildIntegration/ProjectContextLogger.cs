// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.ProjectManagement;

namespace NuGet.PackageManagement
{
    public class ProjectContextLogger : NuGet.Logging.ILogger
    {
        private readonly INuGetProjectContext _projectContext;

        public ProjectContextLogger(INuGetProjectContext projectContext)
        {
            _projectContext = projectContext;
        }

        public void LogDebug(string data)
        {
            _projectContext.Log(ProjectManagement.MessageLevel.Debug, data);
        }

        public void LogVerbose(string data)
        {
            // Treat Verbose as Debug
            _projectContext.Log(ProjectManagement.MessageLevel.Debug, data);
        }

        public void LogError(string data)
        {
            _projectContext.Log(ProjectManagement.MessageLevel.Error, data);
        }

        public void LogInformation(string data)
        {
            _projectContext.Log(ProjectManagement.MessageLevel.Info, data);
        }

        public void LogWarning(string data)
        {
            _projectContext.Log(ProjectManagement.MessageLevel.Warning, data);
        }
    }
}
