using NuGet.Logging;
using NuGet.ProjectManagement;
using System;

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

        public void LogError(string data)
        {
            _projectContext.Log(MessageLevel.Error, data);
        }

        public void LogInformation(string data)
        {
            _projectContext.Log(MessageLevel.Info, data);
        }

        public void LogVerbose(string data)
        {
            _projectContext.Log(MessageLevel.Info, data);
        }

        public void LogWarning(string data)
        {
            _projectContext.Log(MessageLevel.Warning, data);
        }
    }
}
